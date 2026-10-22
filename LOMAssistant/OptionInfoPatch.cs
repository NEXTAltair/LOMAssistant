using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using BepInEx;
using Fungus;
using HarmonyLib;
using LOMAssistant.Config;
using LOMAssistant.LuaParse;
using Mortal.Core;
using Mortal.Free;
using Mortal.Story;
using UnityEngine;

namespace LOMAssistant;

internal static class OptionInfoPatch
{
	private static string _originalDialogKey = string.Empty;

	private static PositionController _positionCtrl = null;

	/// <summary>最近载入的剧本（解析缓存的键），剧情回顾「此刻」页用它取这一段的选项、判定和事件。</summary>
	public static string LastScriptKey;

	/// <summary>
	/// 游戏里最近一次弹出的选项菜单是哪几个选项（选项编号）。剧本的 choose 先取选项文字（GetStoryText）再加按钮（AddOption），
	/// 加按钮时传的就是取到的那段文字：取文字时记下「文字 → 编号」，加按钮时按文字查回编号（中间还会取别的文字，不能按顺序对）。
	/// 「此刻」页据此标出现在这一组。
	/// </summary>
	public static readonly List<string> MenuKeys = new List<string>();
	private static readonly Dictionary<string, string> _textKeys = new Dictionary<string, string>();
	private static int _menuFrame = -1;

	/// <summary>为真时取选项文字不经过下面的结果说明（本 Mod 自己取原文用）。</summary>
	public static bool Raw;

	/// <summary>掷骰转盘下面那几行：选项表每项是「选项编号|范围」，开始掷骰时记下这一组。</summary>
	[HarmonyPrefix]
	[HarmonyPatch(typeof(DiceMenuDialog), "ExecuteRoll")]
	public static void DiceMenuDialog_ExecuteRoll_Prefix(string[] __0)
	{
		if (__0 == null)
		{
			return;
		}
		MenuKeys.Clear();
		foreach (string entry in __0)
		{
			if (!string.IsNullOrEmpty(entry))
			{
				MenuKeys.Add(entry.Split('|')[0]);
			}
		}
		_menuFrame = Time.frameCount;
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(CustomMenuDialog), "AddOption")]
	public static void CustomMenuDialog_AddOption_Prefix(string __0)
	{
		if (Time.frameCount != _menuFrame)
		{
			MenuKeys.Clear();
			_menuFrame = Time.frameCount;
		}
		if (__0 != null && _textKeys.TryGetValue(__0, out string key))
		{
			MenuKeys.Add(key);
		}
	}

	/// <summary>记下每段取过的文字是哪个编号（在结果说明接好以后，和加按钮时传的文字一致）。</summary>
	[HarmonyPostfix]
	[HarmonyPriority(Priority.Last)]
	[HarmonyPatch(typeof(LeanLocalizationResolver), "GetStoryText")]
	public static void LeanLocalizationResolver_GetStoryText_Remember(string key, string __result)
	{
		if (Raw || __result == null)
		{
			return;
		}
		if (_textKeys.Count > 500)
		{
			_textKeys.Clear();
		}
		_textKeys[__result] = key;
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(LuaEnvironment), "LoadLuaFunction")]
	public static void LuaEnvironment_LoadLuaFunction_Prefix(string luaString, string friendlyName)
	{
		var timer = System.Diagnostics.Stopwatch.StartNew();
		try
		{
			if (friendlyName == "tmp.LuaScript")
			{
				return;
			}
			// 剧本内容不会变：解析结果按内容缓存，同一个剧本再次载入（地点菜单等会反复载入）不用重新解析
			string key = friendlyName + "#" + (luaString ?? "").Length + "#" + (luaString ?? "").GetHashCode();
			long lap = 0;
			string laps = "";
			void Lap(string name)
			{
				long now = timer.ElapsedMilliseconds;
				laps += name + " " + (now - lap) + "，";
				lap = now;
			}
			// 选项结果、锁住选项：解析结果存在 LuaParser 里，显示选项文字时取用
			LuaParser.Instance.ExtractOptionActionsFromLua(key, luaString);
			LuaParser.Instance.ExtractSwitchCheckFromLua(key, luaString);
			LuaParser.Instance.ExtractPositionWorkFromLua(key, luaString);
			if (!friendlyName.StartsWith("tmp.") && !friendlyName.StartsWith("Flowchart.Say.ExecuteLua"))
			{
				LastScriptKey = key;
			}
			Lap("选项");
			LuaParser.Instance.ExtractLockedOptions(key, luaString);
			Lap("锁住选项");
			if (timer.ElapsedMilliseconds >= 50)
			{
				FileLogger.Log($"[性能] 解析剧本 {friendlyName}：{laps.TrimEnd('，')}（毫秒）");
			}
		}
		catch (Exception ex)
		{
			DicePlugin.LogError("LoadLuaFunction Prefix 解析失败，已跳过以保证对话加载: " + ex);
		}
		LuaLoadTiming.ParseMs += timer.ElapsedMilliseconds;
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(LeanLocalizationResolver), "GetStoryText")]
	public static void LeanLocalizationResolver_GetStoryText_Postfix(string key, ref string __result)
	{
		// 本 Mod 自己取选项原文（剧情回顾里显示）时不加结果说明，也不算游戏弹出的菜单
		if (Raw)
		{
			return;
		}
		try
		{
			// 被锁的选项（「解锁条件：？？？」「银两不足」这类）：写出真正的解锁条件和当前值，以及解锁后的效果
			if (LuaParser.Instance.LockedOptions.TryGetValue(key, out var locked))
			{
				string condition = RichText.Strip(DataHelper.ParseConditionCheckpoint(locked.Condition));
				string effects = locked.Unlocked != null && LuaParser.Instance.OptionResult.TryGetValue(locked.Unlocked, out var unlockedActions)
					? RichText.Strip(TextConverter.ToSimplifiedUnlessJapanese(LuaParser.Description(unlockedActions)))
					: "";
				if (!condition.IsNullOrWhiteSpace())
				{
					__result = __result + TextConverter.ForDisplay("\n<size=16>(" + condition + (effects.IsNullOrWhiteSpace() ? "" : "；解锁后：" + effects) + ")</size>");
				}
				return;
			}
			// 满足某个分支条件就直接接一段剧情：写成「若 条件：触发剧情」
			string jumps = "";
			if (LuaParser.Instance.OptionJumps.TryGetValue(key, out var jumpList))
			{
				foreach (var jump in jumpList)
				{
					string condition = RichText.Strip(DataHelper.SwitchBranchCondition(jump.Switch, jump.Branch));
					if (!condition.IsNullOrWhiteSpace())
					{
						jumps += (jumps.Length > 0 ? "；" : "") + "若 " + condition + "：触发剧情";
					}
				}
			}
			if (LuaParser.Instance.OptionResult.TryGetValue(key, out var value))
			{
				string text = TextConverter.ToSimplifiedUnlessJapanese(LuaParser.Description(value));
				if (jumps.Length > 0)
				{
					text = text.IsNullOrWhiteSpace() ? jumps : text + "；" + jumps;
				}
				if (!text.IsNullOrWhiteSpace())
				{
					string text2 = RichText.Strip(text);
					__result = __result + TextConverter.ForDisplay("\n<size=16>(" + text2 + ")</size>");
				}
			}
		}
		catch (Exception ex)
		{
			DicePlugin.LogError("GetStoryText Postfix 追加选项结果失败，已跳过以保证对话显示: " + ex);
		}
	}

	/// <summary>
	/// 骰子菜单的线路文字框很窄、行距很小：后面接的结果说明一长就折成两行，两行叠在一起看不清。
	/// 这里的文字框改成不折行，结果说明一行往右写完（右边是空的）。
	/// 线路文字本身（DiceOption 的 _normalText、_highText）是 TextMeshPro，不是 UI.Text，也要一起改，
	/// 不然结果说明一超过框宽（723）就折成几行，框高（55.2）放不下（日语的旗标名、变量名比中文长，更容易超过）。
	/// </summary>
	[HarmonyPostfix]
	[HarmonyPatch(typeof(DiceMenuDialog), "OnEnable")]
	public static void DiceMenuDialog_OnEnable_Postfix(DiceMenuDialog __instance)
	{
		foreach (UnityEngine.UI.Button button in __instance.CachedButtons ?? new UnityEngine.UI.Button[0])
		{
			if (button != null)
			{
				DiceNoWrap(button);
			}
		}
	}

	// 线路文字填进去以后，照画面宽度缩小说明。先再设一次不折行、不自动缩小，量出来的才是写成一行的宽度
	[HarmonyPostfix]
	[HarmonyPatch(typeof(DiceOption), "UpdateContent")]
	public static void DiceOption_UpdateContent_Postfix(DiceOption __instance)
	{
		DiceNoWrap(__instance);
		DiceFitToScreen(__instance);
	}

	// 选中时显示的是另一个文字框（_highText，粗体字型，字宽不同），换上来时再量一次
	[HarmonyPostfix]
	[HarmonyPatch(typeof(DiceOption), "Select")]
	public static void DiceOption_Select_Postfix(DiceOption __instance) => DiceFitToScreen(__instance);

	/// <summary>说明往右写到画面右边还写不完时，把说明的字缩小到刚好写到画面右边为止（最小 <see cref="DiceMinDescSize"/>）。</summary>
	private const float DiceMinDescSize = 11f;
	private const float DiceRightMargin = 24f;
	private static readonly Regex DiceDescSize = new Regex(@"\n<size=(\d+(?:\.\d+)?)>");
	private static readonly Vector3[] DiceCorners = new Vector3[4];

	private static void DiceFitToScreen(DiceOption option)
	{
		try
		{
			foreach (TMPro.TMP_Text text in option.GetComponentsInChildren<TMPro.TMP_Text>(includeInactive: false))
			{
				Match size = DiceDescSize.Match(text.text ?? string.Empty);
				if (!size.Success || text.canvas == null)
				{
					continue;
				}
				text.ForceMeshUpdate();
				TMPro.TMP_TextInfo info = text.textInfo;
				float left = float.MaxValue;
				float right = float.MinValue;
				for (int i = 1; i < info.lineCount; i++)
				{
					left = Mathf.Min(left, info.lineInfo[i].lineExtents.min.x);
					right = Mathf.Max(right, info.lineInfo[i].lineExtents.max.x);
				}
				((RectTransform)text.canvas.rootCanvas.transform).GetWorldCorners(DiceCorners);
				float screenRight = text.rectTransform.InverseTransformPoint(DiceCorners[2]).x - DiceRightMargin;
				if (right <= screenRight || right <= left)
				{
					continue;
				}
				float current = float.Parse(size.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
				float fitted = Mathf.Max(DiceMinDescSize, Mathf.Floor(current * (screenRight - left) / (right - left)));
				if (fitted < current)
				{
					text.text = DiceDescSize.Replace(text.text, "\n<size=" + fitted.ToString(System.Globalization.CultureInfo.InvariantCulture) + ">", 1);
				}
			}
		}
		catch (Exception ex)
		{
			FileLogger.LogWarning("骰子菜单结果说明缩小失败: " + ex.Message);
		}
	}

	private static void DiceNoWrap(UnityEngine.Component root)
	{
		try
		{
			foreach (UnityEngine.UI.Text text in root.GetComponentsInChildren<UnityEngine.UI.Text>(includeInactive: true))
			{
				text.horizontalOverflow = HorizontalWrapMode.Overflow;
			}
			// 自动缩小也关掉：不折行时它为了把整行塞进框里，会把标题缩到最小（15），说明是固定字号（<size=16>）却不跟着缩。
			// 标题照最大字号（28），说明往右写完，太长的由 DiceFitToScreen 缩小
			foreach (TMPro.TMP_Text text in root.GetComponentsInChildren<TMPro.TMP_Text>(includeInactive: true))
			{
				text.enableWordWrapping = false;
				text.overflowMode = TMPro.TextOverflowModes.Overflow;
				if (text.enableAutoSizing)
				{
					text.enableAutoSizing = false;
					text.fontSize = text.fontSizeMax;
				}
			}
		}
		catch (Exception ex)
		{
			FileLogger.LogWarning("骰子菜单文字不折行设置失败: " + ex.Message);
		}
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(PositionController), "OnPositionEnter")]
	public static void PositionController_OnPositionEnter_Prefix(PositionController __instance)
	{
		PositionType fieldValue = __instance.GetFieldValue<PositionType>("_position");
		if (_positionCtrl != null)
		{
			HandleDialogChange(_positionCtrl, addSuffix: false);
		}
		if (HasTriggerMission(fieldValue, out var missionKey))
		{
			HandleDialogChange(__instance, addSuffix: true, "可触发：" + missionKey);
		}
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(PositionController), "OnPositionClick")]
	public static void PositionController_OnPositionClick_Prefix()
	{
		if (_positionCtrl != null)
		{
			HandleDialogChange(_positionCtrl, addSuffix: false);
		}
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(ConditionResultData), "Execute")]
	public static void PrefixExecuteConditionResult(ConditionResultData __instance)
	{
		List<ConditionResultItem> fieldValue = __instance.GetFieldValue<List<ConditionResultItem>>("_items");
		LogCheckpoint("触发剧情检查点:", fieldValue);
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(MissionCheckData), "Check")]
	public static void PrefixCheckMission(MissionCheckData __instance)
	{
		ConditionResultItem[] fieldValue = __instance.GetFieldValue<ConditionResultItem[]>("_conditions");
		StringBuilder stringBuilder = new StringBuilder("触发任务检查点:");
		AppendConditions(fieldValue, stringBuilder);
		if (__instance.GetFieldValue<bool>("_dayCheck"))
		{
			DataHelper.GetMissionPeriod(__instance, out var periodText);
			if (!periodText.IsNullOrWhiteSpace())
			{
				stringBuilder.AppendLine("    时限：" + periodText);
			}
		}
		PrintToNarrativeLog(stringBuilder.ToString());
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(SwitchResultData), "Execute")]
	public static void PrefixExecuteSwitchResult(SwitchResultData __instance)
	{
		List<ConditionResultItem> fieldValue = __instance.GetFieldValue<List<ConditionResultItem>>("_items");
		LogCheckpoint("触发剧情分支检查点:", fieldValue);
	}

	// 地点对白后面接的「(可触发：…)」：游戏是繁体时接的是繁体
	private static bool IsTriggerHint(string text) => text.Contains("可触发：") || text.Contains("可觸發：") || text.Contains("발생 가능:") || text.Contains("発生可能：");

	private static void HandleDialogChange(PositionController instance, bool addSuffix, string suffix = "")
	{
		DicePlugin.LogInfo($"HandleDialogChange called, addSuffix: {addSuffix}, suffix: {suffix}");
		try
		{
			PositionDialogItem dialog = instance.GetFieldValue<FreePositionData>("_positionData").GetDialog();
			string text = dialog.GetFieldValue<string>("_dialog");
			if (addSuffix)
			{
				if (IsTriggerHint(text))
				{
					int num = text.LastIndexOf("(");
					if (num > 0)
					{
						text = text.Substring(0, num);
					}
				}
				_originalDialogKey = text;
				_positionCtrl = instance;
				string text2 = LocalizationManager.Instance.LocaleResolver.GetString("Position/Dialog/" + text);
				if (string.IsNullOrEmpty(text2))
				{
					text2 = text;
				}
				dialog.SetPrivateField("_dialog", text2 + "(" + TextConverter.ForDisplay(suffix) + ")");
				return;
			}
			if (!string.IsNullOrEmpty(_originalDialogKey) && IsTriggerHint(_originalDialogKey))
			{
				int num2 = _originalDialogKey.LastIndexOf("(");
				if (num2 > 0)
				{
					_originalDialogKey = _originalDialogKey.Substring(0, num2);
				}
			}
			dialog.SetPrivateField("_dialog", _originalDialogKey);
			_positionCtrl = null;
			_originalDialogKey = string.Empty;
		}
		catch (Exception o)
		{
			DicePlugin.LogError(o);
		}
	}

	/// <summary>
	/// 点这个地点会不会触发任务，和游戏点地点时的判断一样：主线正在等（有地点）时，只有主线的地点能点，点了走主线；
	/// 否则是本旬生效的支线里，地点是这里、有开头剧本的那个。返回任务的名字，主线前面加「主线」。
	/// </summary>
	private static bool HasTriggerMission(PositionType position, out string missionKey)
	{
		missionKey = string.Empty;
		MissionManagerData missions = MissionManagerData.Instance;
		if (missions == null)
		{
			return false;
		}
		if (missions.MainMissionStart && missions.MainMissionPosition != PositionType.無)
		{
			MissionCheckData main = missions.MainCheckData;
			if (position != missions.MainMissionPosition || main == null || string.IsNullOrEmpty(main.StartScript))
			{
				return false;
			}
			missionKey = "主线「" + MissionName(main, missions.MainMissionKey) + "」";
			return true;
		}
		if (missions.ActiveSubMissions == null)
		{
			return false;
		}
		// 刚做完的支线在「本旬生效」的列表里还会留一阵；和「此刻」页一样，只算任务当前这一步（做完的已经是 clear，不在里面）
		Dictionary<MissionCheckData, (string, string)> current = DataHelper.GetMissions();
		foreach (KeyValuePair<string, MissionCheckData> active in missions.ActiveSubMissions)
		{
			MissionCheckData check = active.Value;
			if (check.Position != position || string.IsNullOrEmpty(check.StartScript) || !current.ContainsKey(check))
			{
				continue;
			}
			missionKey = MissionName(check, active.Key);
			return true;
		}
		return false;
	}

	/// <summary>任务的显示名：剧情回顾里整理过的标题，没有就用任务说明的第一行，再没有用编号。</summary>
	private static string MissionName(MissionCheckData check, string fallback)
	{
		LOMAssistant.Features.Story.StoryMission known = LOMAssistant.Features.Story.StoryAtlas.Find(check.Key);
		string desc = TextConverter.ToSimplified(check.GetFieldValue<string>("_desc") ?? "").Split('\n')[0].Trim();
		return known != null && known.Title.Length > 0 ? known.Title : desc.Length > 0 ? desc : fallback;
	}

	private static void LogCheckpoint(string header, IEnumerable<ConditionResultItem> items)
	{
		StringBuilder stringBuilder = new StringBuilder(header);
		AppendConditions(items, stringBuilder);
		PrintToNarrativeLog(stringBuilder.ToString());
	}

	private static void AppendConditions(IEnumerable<ConditionResultItem> items, StringBuilder logBuilder)
	{
		foreach (ConditionResultItem item in items)
		{
			bool isInPeriod;
			string text = DataHelper.ExtractConditions(item, out isInPeriod);
			if (!text.IsNullOrWhiteSpace())
			{
				logBuilder.AppendLine("    " + text);
			}
		}
	}

	private static void PrintToNarrativeLog(string message)
	{
		string text = Regex.Replace(message, "^\\s*$\\n|\\r", string.Empty, RegexOptions.Multiline).Trim();
		NarrativeLog narrativeLog = FungusManager.Instance.NarrativeLog;
		if (narrativeLog != null)
		{
			NarrativeLogEntry entry = new NarrativeLogEntry
			{
				name = "LOMAssistant",
				// 记到游戏的对话记录里，不经过本 Mod 界面的翻译，这里先换成界面语言（繁体、韩语、日语）
				text = "<size=18>" + TextConverter.ForDisplay(text) + "</size>"
			};
			narrativeLog.AddLine(entry);
		}
	}
}
