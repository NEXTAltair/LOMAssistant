using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using HarmonyLib;
using LOMAssistant.Config;
using LOMAssistant.Features.Story;
using Mortal.Core;
using Mortal.Free;
using Mortal.Story;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LOMAssistant;

/// <summary>
/// 养成地图上的建筑：鼠标停上去，列出这里的事件和触发条件（主线、支线这一步在不在这里、时间到没到、条件满足没有，
/// 以及点进去会抽的随机事件和概率），满足的绿字、不满足的红字，写法参考赵活系统。
/// 有还没看过的事件（能触发的主线、支线这一局没走过，能抽到的随机事件有没读过的对白）的建筑，一直亮着游戏自己的悬停光效。
/// </summary>
public class MapEventHints : MonoBehaviour
{
	private static MapEventHints _instance;
	private static PositionController _hovered;
	private static string _text;
	// 我们点亮光效的建筑：鼠标移开时游戏会把光效关掉，再点亮
	private static readonly HashSet<PositionController> Glowing = new HashSet<PositionController>();
	private static readonly Regex PoolCall = new Regex(@"checkpointmanager\.Position\(\s*""([A-Za-z0-9_]+)""\s*\)");

	private float _nextGlowCheck;
	private float _scale = -1f;
	private int _fontMode = -1;
	private GUIStyle _box;

	public static void Ensure(GameObject host)
	{
		if (_instance == null)
		{
			_instance = host.GetComponent<MapEventHints>() ?? host.AddComponent<MapEventHints>();
			SceneManager.activeSceneChanged += (a, b) =>
			{
				_hovered = null;
				Glowing.Clear();
			};
		}
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(PositionController), nameof(PositionController.OnPositionEnter))]
	public static void Enter_Postfix(PositionController __instance)
	{
		_hovered = __instance;
		_text = null;
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(PositionController), nameof(PositionController.OnPositionExit))]
	public static void Exit_Postfix(PositionController __instance)
	{
		if (_hovered == __instance)
		{
			_hovered = null;
		}
		// 游戏移开鼠标时把光效关了；有没看过的事件就接着亮
		if (Glowing.Contains(__instance))
		{
			Particle(__instance)?.Play();
		}
	}

	private void Update()
	{
		if (Time.unscaledTime < _nextGlowCheck)
		{
			return;
		}
		_nextGlowCheck = Time.unscaledTime + 2f;
		try
		{
			RefreshGlow();
		}
		catch (Exception ex)
		{
			FileLogger.LogWarning("[地图事件] 更新高亮失败: " + ex.Message);
		}
	}

	private static void RefreshGlow()
	{
		bool on = ConfigManager.EnableMapHints.Value;
		foreach (PositionController position in FindObjectsOfType<PositionController>())
		{
			bool unread = on && Active(position) && HasUnread(position);
			ParticleSystem particle = Particle(position);
			if (particle == null)
			{
				continue;
			}
			if (unread && !Glowing.Contains(position))
			{
				Glowing.Add(position);
				particle.Play();
			}
			else if (!unread && Glowing.Remove(position) && position != _hovered)
			{
				particle.Stop();
			}
		}
	}

	private void OnGUI()
	{
		if (_hovered == null || !ConfigManager.EnableMapHints.Value || InputGuard.ModWindowOpen || EventPicker.IsWaiting || !Active(_hovered))
		{
			return;
		}
		float scale = Mathf.Min(Screen.width / 1920f, Screen.height / 1080f);
		if (scale <= 0f)
		{
			return;
		}
		if (_box == null || ModFont.NeedsRebuild(scale, _scale, _fontMode))
		{
			_scale = scale;
			_fontMode = ModFont.Mode;
			_box = new GUIStyle(GUI.skin.box) { fontSize = Mathf.Max(11, (int)(19f * scale)), alignment = TextAnchor.UpperLeft, wordWrap = true, richText = true };
			_box.normal.textColor = new Color(0.92f, 0.9f, 0.85f);
			_box.padding = new RectOffset((int)(14f * scale), (int)(14f * scale), (int)(10f * scale), (int)(10f * scale));
			ModFont.Apply(_box);
		}
		if (_text == null)
		{
			try
			{
				_text = Describe(_hovered);
			}
			catch (Exception ex)
			{
				_text = "";
				FileLogger.LogWarning("[地图事件] 读取事件失败: " + ex.Message);
			}
		}
		if (_text.Length == 0)
		{
			return;
		}
		var content = new GUIContent(_text);
		float width = 600f * scale;
		float height = _box.CalcHeight(content, width);
		Vector2 mouse = Event.current.mousePosition;
		// 放在鼠标右下，超出屏幕就挪到左边、上边
		float x = mouse.x + 24f * scale, y = mouse.y + 16f * scale;
		if (x + width > Screen.width)
		{
			x = mouse.x - width - 16f * scale;
		}
		y = Mathf.Clamp(y, 0f, Mathf.Max(0f, Screen.height - height));
		Color old = GUI.backgroundColor;
		GUI.backgroundColor = new Color(0f, 0f, 0f, 2.5f);
		GUI.Box(new Rect(Mathf.Max(0f, x), y, width, height), content, _box);
		GUI.backgroundColor = old;
	}

	// ---------------- 这里有什么事件 ----------------

	private static PositionType Position(PositionController position) => Traverse.Create(position).Field("_position").GetValue<PositionType>();

	private static ParticleSystem Particle(PositionController position) => Traverse.Create(position).Field("_hoverParticle").GetValue<ParticleSystem>();

	// 游戏自己能不能点（正在演出、这个场所没开放时不显示）
	private static bool Active(PositionController position) => position != null && position.isActiveAndEnabled && Traverse.Create(position).Method("IsActiveStatus").GetValue<bool>();

	/// <summary>主线和每条支线现在这一步，在这个场所的。</summary>
	private static IEnumerable<(MissionCheckData Check, bool Main, string Series)> Missions(PositionType place)
	{
		MissionManagerData data = MissionManagerData.Instance;
		if (data == null)
		{
			yield break;
		}
		var series = new List<(MissionData, bool)> { (data.Main, true) };
		SubMissionsData subs = data.GetFieldValue<SubMissionsData>("_subMissions");
		if (subs != null)
		{
			series.AddRange(subs.List.Select(s => (s, false)));
		}
		foreach (var (mission, main) in series)
		{
			if (mission == null || mission.CurrentKey == "clear")
			{
				continue;
			}
			MissionCheckData check = mission.GetByKey(mission.CurrentKey);
			if (check != null && check.Position == place && !string.IsNullOrEmpty(check.StartScript))
			{
				yield return (check, main, mission.Name ?? mission.name);
			}
		}
	}

	// 现在点进去就会演这一步：主线开始了且在这里，或支线已经满足条件
	private static bool Triggerable(MissionCheckData check, bool main)
	{
		MissionManagerData data = MissionManagerData.Instance;
		return main ? data.MainMissionStart && data.MainMissionPosition == check.Position : data.ActiveSubMissions != null && data.ActiveSubMissions.ContainsKey(check.Key);
	}

	// 场所默认剧本里抽随机事件的池子（checkpointmanager.Position("…")）
	private static List<PositionEventData> Events(PositionController position)
	{
		FreePositionData data = Traverse.Create(position).Field("_positionData").GetValue<FreePositionData>();
		string script = data != null ? data.GetFieldValue<string>("_defaultScript") : null;
		string text = StoryScripts.Text(script);
		if (text == null)
		{
			return new List<PositionEventData>();
		}
		return PoolCall.Matches(text).Cast<Match>().Select(m => m.Groups[1].Value).Distinct()
			.SelectMany(pool => PositionEventCache.Instance.GetPositionEvents(pool)).Distinct().ToList();
	}

	private static bool Unread(string script)
	{
		StoryScript s = StoryScripts.Get(script);
		int? read = s != null ? StoryScripts.ReadPercent(s.Lines) : null;
		return read.HasValue && read.Value < 100;
	}

	private static bool HasUnread(PositionController position)
	{
		foreach (var (check, main, _) in Missions(Position(position)))
		{
			if (Triggerable(check, main))
			{
				StoryMission mission = StoryAtlas.Ready ? StoryAtlas.Find(check.Key) : null;
				if (mission == null ? Unread(check.StartScript) : !mission.Visited)
				{
					return true;
				}
			}
		}
		return Events(position).Any(e => e.GetRate() > 0 && Unread(PositionEventCache.Instance.ResultScript(e)));
	}

	private static string Describe(PositionController position)
	{
		PositionType place = Position(position);
		var missions = Missions(place).ToList();
		List<PositionEventData> events = Events(position);
		var text = new StringBuilder();
		text.Append($"<b>{TextConverter.ToSimplified(place.ToString())}：{missions.Count + events.Count} 个事件</b>");
		// 能触发、或在时间内的写全（状态、时间、条件）；不在时间内的只在最后写一行名字和时间，提示不至于太长
		var later = new List<string>();
		foreach (var (check, main, series) in missions)
		{
			StoryMission mission = StoryAtlas.Ready ? StoryAtlas.Find(check.Key) : null;
			string title = mission?.Title ?? TextConverter.ToSimplified(series ?? check.Key);
			string head = (main ? "主线" : "支线") + "「" + title + "」";
			bool now = Triggerable(check, main);
			bool inPeriod = DataHelper.GetMissionPeriod(check, out string period);
			string unseen = mission != null && !mission.Visited ? "　" + RichText.Golden("没看过") : "";
			if (!now && !inPeriod)
			{
				later.Add(head + "　" + period);
				continue;
			}
			text.Append("\n\n" + RichText.Golden(head) + unseen);
			text.Append("\n" + (now ? RichText.Green("现在点进去就会触发") : RichText.Red("还不能触发")) + "　" + RichText.Green("时间 " + period));
			foreach (ConditionResultItem condition in check.GetFieldValue<ConditionResultItem[]>("_conditions") ?? new ConditionResultItem[0])
			{
				string line = condition != null ? DataHelper.ExtractConditions(condition, out _) : "";
				if (line.Length > 0)
				{
					text.Append("\n" + line);
				}
			}
		}
		if (later.Count > 0)
		{
			text.Append("\n\n" + RichText.Golden("不在时间内"));
			foreach (string line in later)
			{
				text.Append("\n<color=#9A9286>" + line + "</color>");
			}
		}
		if (events.Count > 0)
		{
			int total = events.Sum(e => Math.Max(0, e.GetRate()));
			text.Append("\n\n" + RichText.Golden("随机事件"));
			foreach (PositionEventData data in events)
			{
				int rate = data.GetRate();
				string name = RichText.Strip(DataHelper.GetEventName(data));
				string chance = rate > 0 && total > 0 ? RichText.Green($"{rate * 100f / total:0.#}%") : RichText.Red("条件不满足");
				string unread = rate > 0 && Unread(PositionEventCache.Instance.ResultScript(data)) ? "　" + RichText.Golden("没看过") : "";
				text.Append($"\n{name}　{chance}{unread}");
				ConditionResultItem active = data.GetFieldValue<bool>("_toggleCondition") ? data.GetFieldValue<ConditionResultItem>("_activeCondition") : null;
				if (active != null && rate <= 0)
				{
					string line = DataHelper.ExtractConditions(active, out _);
					if (line.Length > 0)
					{
						text.Append("\n　" + line);
					}
				}
			}
		}
		return text.ToString();
	}
}
