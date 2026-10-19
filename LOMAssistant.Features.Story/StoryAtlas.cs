using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Mortal.Core;
using UnityEngine;

namespace LOMAssistant.Features.Story;

/// <summary>一个任务（主线一节或支线的一步）。</summary>
/// <summary>从别的任务的剧本接进这个任务：在哪个任务、哪个剧本，走过哪些分岔。</summary>
public sealed class StoryEntry
{
	public StoryMission From;
	public string Script = "";
	public List<StoryStep> Path = new List<StoryStep>();
}

public sealed class StoryMission
{
	public string Key = "";
	/// <summary>所属任务组（MissionData），主线为 Main。</summary>
	public string Series = "";
	public string SeriesTitle = "";
	public bool Main;
	/// <summary>游戏数据里没启用、不会在地图上自己触发，只能由别的剧本直接接进来（襄阳行这类）。</summary>
	public bool Inactive;
	public string Desc = "";
	public string Place = "";
	/// <summary>触发时间，按旬计；任意时间、每月一次的为 null。</summary>
	public int? From, To;
	public string When = "";
	public string StartScript = "";
	public ConditionResultItem[] Conditions = new ConditionResultItem[0];
	/// <summary>从开始剧本一路接下去、到回地图或别的任务为止的剧本。</summary>
	public List<string> Scripts = new List<string>();
	public int? Read;
	public bool Visited;
	public int Endings;
	/// <summary>别的任务的剧本里接进来的地方。</summary>
	public List<StoryEntry> Entries = new List<StoryEntry>();
	/// <summary>主线：剧本里明写的下一个主线任务（Mission("Main", …)），按日期往后的才算。</summary>
	public List<string> Next = new List<string>();
	/// <summary>这个任务的剧本里第一条剧情记录，用作标题旁的一句话简介。</summary>
	public string Summary = "";

	/// <summary>显示用的名字：任务说明，没有就用第一条剧情记录；主线再没有就按开始剧本写成「1-1」这样的章节号，最后才用编号。</summary>
	public string Title => Desc.Length > 0 ? Desc : Summary.Length > 0 ? (Summary.Length > 16 ? Summary.Substring(0, 16) + "…" : Summary) : Main ? ChapterOf(StartScript) ?? Key : SeriesTitle;

	private static readonly System.Text.RegularExpressions.Regex Chapter = new System.Text.RegularExpressions.Regex(@"^ch(\d+)_(\d+)");

	private static string ChapterOf(string script)
	{
		var m = Chapter.Match(script ?? "");
		return m.Success ? m.Groups[1].Value + "-" + m.Groups[2].Value : null;
	}
}

/// <summary>会回到标题的剧本（结局、死亡），以及怎么走到。</summary>
/// <summary>一个结局（汗青书）或死亡（生死簿）条目，以及剧本里哪些地方会走到它。</summary>
public sealed class StoryEnding
{
	public string Id = "";
	public bool Death;
	public string Title = "";
	public string Desc = "";
	public LibraryItemData Item;
	public List<StoryEndingWay> Ways = new List<StoryEndingWay>();

	/// <summary>达成过没有：游戏自己的记录（全部周目）。</summary>
	public bool Achieved => Item != null && Item.Count > 0;
}

public sealed class StoryEndingWay
{
	public string Script = "";
	public List<StoryStep> Path = new List<StoryStep>();
	public StoryMission Mission;
	/// <summary>输掉这一场决斗才会触发。</summary>
	public bool LostDuel;
}

/// <summary>
/// 把全部剧本和任务读一遍，整理成时间线和结局速查。第一次打开时开始，分很多帧做完，不卡游戏；
/// 读过、走过的标记按生成那一刻算，面板上可以重新生成。
/// </summary>
public static class StoryAtlas
{
	private const double FrameBudgetMs = 5;

	public static readonly List<StoryMission> Missions = new List<StoryMission>();
	public static readonly List<StoryEnding> Endings = new List<StoryEnding>();
	/// <summary>剧本 → 它属于哪个任务（从任务开始剧本一路接下去的那些）。</summary>
	public static readonly Dictionary<string, StoryMission> Owner = new Dictionary<string, StoryMission>(StringComparer.OrdinalIgnoreCase);
	/// <summary>全部台词（剧本、文字），搜索用；读完剧本以后在后台慢慢整理。</summary>
	private static readonly List<(string Script, string Text)> Dialogue = new List<(string, string)>();
	public static bool DialogueReady { get; private set; }
	private static bool _dialoguePhase;
	public static bool Ready { get; private set; }
	public static bool Building => _steps != null;
	public static string Status { get; private set; } = "";
	public static string CurrentMain { get; private set; } = "";
	/// <summary>每次生成完加一，面板据此重新排版。</summary>
	public static int Version { get; private set; }

	private static readonly Dictionary<StoryMission, string> Conditions = new Dictionary<StoryMission, string>();

	/// <summary>条件文字里带当前值（读数值、随机值都有代价），每次打开面板清一次。</summary>
	public static void ForgetConditions() => Conditions.Clear();

	private static IEnumerator _steps;

	public static void Start()
	{
		if (_steps != null)
		{
			return;
		}
		Ready = false;
		DialogueReady = false;
		_dialoguePhase = false;
		Status = "正在读取剧本";
		_steps = Build();
	}

	/// <summary>每帧调用：在时间预算内往下做几步。</summary>
	public static void Tick()
	{
		if (_steps == null)
		{
			return;
		}
		var clock = Stopwatch.StartNew();
		try
		{
			while (clock.Elapsed.TotalMilliseconds < FrameBudgetMs)
			{
				if (!_steps.MoveNext())
				{
					if (_dialoguePhase)
					{
						_steps = null;
						DialogueReady = true;
						break;
					}
					Ready = true;
					Version++;
					Conditions.Clear();
					Status = "";
					// 剧本读完就能用了；台词索引接着在后台整理
					_dialoguePhase = true;
					_steps = BuildDialogue();
					break;
				}
			}
		}
		catch (Exception ex)
		{
			_steps = null;
			Status = "生成失败：" + ex.Message;
			FileLogger.LogWarning("[剧情回顾] 生成时间线失败: " + ex);
		}
	}

	private static IEnumerator Build()
	{
		var timer = Stopwatch.StartNew();
		Missions.Clear();
		Endings.Clear();
		// 全部剧本：读一遍，记下大概，文字马上放掉
		TextAsset[] assets = Resources.LoadAll<TextAsset>("story/chinesetraditional");
		var names = new List<string>();
		for (int i = 0; i < assets.Length; i++)
		{
			TextAsset asset = assets[i];
			if (asset == null)
			{
				continue;
			}
			names.Add(asset.name);
			StoryScripts.Remember(StoryScripts.Parse(asset.name, asset.text));
			Resources.UnloadAsset(asset);
			if (i % 20 == 0)
			{
				Status = $"正在读取剧本 {i}/{assets.Length}";
				yield return null;
			}
		}
		assets = null;
		yield return null;

		// 任务
		Status = "正在整理任务";
		MissionManagerData data = MissionManagerData.Instance;
		if (data == null)
		{
			Status = "读档进入游戏后再生成";
			yield break;
		}
		CurrentMain = data.Main != null ? data.Main.CurrentKey : "";
		AddSeries(data.Main, true);
		SubMissionsData subs = data.GetFieldValue<SubMissionsData>("_subMissions");
		if (subs != null)
		{
			foreach (MissionData series in subs.List)
			{
				AddSeries(series, false);
			}
		}
		yield return null;

		// 每个任务的剧本：从开始剧本沿着「接到」走，遇到别的任务的开始剧本、回地图就停
		var starts = new HashSet<string>(Missions.Select(m => m.StartScript).Where(s => s.Length > 0), StringComparer.OrdinalIgnoreCase);
		var owner = new Dictionary<string, StoryMission>(StringComparer.OrdinalIgnoreCase);
		// 标题画面还没读档时，这一局走过的剧情记录是空的
		var visited = new HashSet<string>(PlayerStatManagerData.Instance?.StoryKeyList ?? new List<string>(), StringComparer.Ordinal);
		int done = 0;
		foreach (StoryMission mission in Missions)
		{
			if (mission.StartScript.Length == 0)
			{
				continue;
			}
			var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			var todo = new Queue<string>();
			todo.Enqueue(mission.StartScript);
			while (todo.Count > 0 && seen.Count < 400)
			{
				string name = todo.Dequeue();
				if (seen.Contains(name) || (seen.Count > 0 && starts.Contains(name)))
				{
					continue;
				}
				StoryScript script = StoryScripts.Get(name);
				if (script == null)
				{
					continue;
				}
				seen.Add(name);
				foreach (StoryExit exit in script.Exits)
				{
					if (exit.Target.Length > 0)
					{
						todo.Enqueue(exit.Target);
					}
				}
			}
			mission.Scripts = seen.ToList();
			foreach (string name in mission.Scripts)
			{
				StoryScript s = StoryScripts.Get(name);
				if (s == null)
				{
					continue;
				}
				foreach (StoryExit exit in s.Exits)
				{
					if (exit.MainMission.Length > 0 && !mission.Next.Contains(exit.MainMission))
					{
						mission.Next.Add(exit.MainMission);
					}
				}
				if (mission.Summary.Length == 0)
				{
					mission.Summary = s.Records.Select(StoryScripts.Legend).FirstOrDefault(t => t.Length > 0) ?? "";
				}
			}
			var scripts = mission.Scripts.Select(StoryScripts.Get).Where(s => s != null).ToList();
			mission.Read = StoryScripts.ReadPercent(scripts.SelectMany(s => s.Lines));
			mission.Visited = scripts.Any(s => s.Records.Any(visited.Contains));
			foreach (string name in mission.Scripts)
			{
				if (!owner.ContainsKey(name))
				{
					owner[name] = mission;
				}
			}
			if (++done % 10 == 0)
			{
				Status = $"正在整理任务 {done}/{Missions.Count}";
				yield return null;
			}
		}

		// 支线演到一半回地图、后面几段由地图事件接着演的（襄阳行这类），顺着剧本走不到：
		// 同一任务组编号开头、又没归到别的任务的剧本，也算这条支线的（台词搜索、读过多少、剧情图、走完接到的主线都算上）
		foreach (StoryMission mission in Missions.Where(m => !m.Main && m.Series.Length > 0))
		{
			var extra = names.Where(n => n.StartsWith(mission.Series + "_", StringComparison.OrdinalIgnoreCase) && !owner.ContainsKey(n)).ToList();
			if (extra.Count == 0)
			{
				continue;
			}
			foreach (string name in extra)
			{
				owner[name] = mission;
				mission.Scripts.Add(name);
				StoryScript s = StoryScripts.Get(name);
				if (s == null)
				{
					continue;
				}
				foreach (StoryExit exit in s.Exits)
				{
					if (exit.MainMission.Length > 0 && !mission.Next.Contains(exit.MainMission))
					{
						mission.Next.Add(exit.MainMission);
					}
				}
			}
			var all = mission.Scripts.Select(StoryScripts.Get).Where(s => s != null).ToList();
			mission.Read = StoryScripts.ReadPercent(all.SelectMany(s => s.Lines));
			mission.Visited = all.Any(s => s.Records.Any(visited.Contains));
		}

		// 剧本归属，以及别的任务接进来的地方（支线怎么进入）
		Owner.Clear();
		foreach (var pair in owner)
		{
			Owner[pair.Key] = pair.Value;
		}
		foreach (var pair in owner)
		{
			StoryScript s = StoryScripts.Get(pair.Key);
			if (s == null)
			{
				continue;
			}
			foreach (StoryExit exit in s.Exits)
			{
				if (exit.Target.Length == 0 || !owner.TryGetValue(exit.Target, out StoryMission into) || into == pair.Value || into.Entries.Count >= 8
					|| into.Entries.Any(e => e.From == pair.Value && e.Script == pair.Key))
				{
					continue;
				}
				into.Entries.Add(new StoryEntry { From = pair.Value, Script = pair.Key, Path = exit.Path });
			}
		}
		// 没启用、也没有剧本接进来的支线是用不到的，去掉
		var unused = new HashSet<StoryMission>(Missions.Where(m => m.Inactive && m.Entries.Count == 0));
		Missions.RemoveAll(unused.Contains);
		foreach (string name in Owner.Where(p => unused.Contains(p.Value)).Select(p => p.Key).ToList())
		{
			Owner.Remove(name);
		}
		foreach (StoryMission m in Missions)
		{
			m.Entries.RemoveAll(e => unused.Contains(e.From));
		}
		yield return null;

		// 主线之间的连线：只留往后走的、指向真有的主线任务（共用剧本里会混进别处的跳转）
		var mains = Missions.Where(m => m.Main).ToDictionary(m => m.Key, StringComparer.Ordinal);
		foreach (StoryMission m in mains.Values)
		{
			m.Next = m.Next.Where(k => k != m.Key && mains.TryGetValue(k, out StoryMission t) && (m.From == null || t.From == null || t.From >= m.From)).ToList();
		}

		// 结局速查：游戏的汗青书（结局）和生死簿（死亡）条目，加上剧本里哪些地方会触发它们
		Status = "正在整理结局";
		var byId = new Dictionary<string, StoryEnding>(StringComparer.Ordinal);
		LibrarySystem library = LibrarySystem.Instance;
		void AddItems(LibraryItemCollection collection, bool death)
		{
			if (collection == null || collection.List == null)
			{
				return;
			}
			foreach (LibraryItemData item in collection.List)
			{
				if (item == null)
				{
					continue;
				}
				var ending = new StoryEnding { Id = item.name, Death = death, Item = item, Title = LibraryText(item.GetTitleKey(), item.name), Desc = LibraryText(item.GetDescKey(), "") };
				byId[(death ? "d" : "e") + item.name] = ending;
				Endings.Add(ending);
			}
		}
		if (library != null)
		{
			AddItems(library.EndGame, false);
			AddItems(library.Dead, true);
		}
		foreach (string name in names)
		{
			StoryScript script = StoryScripts.Get(name);
			if (script == null)
			{
				continue;
			}
			foreach (var (path, death, id) in script.Endings)
			{
				owner.TryGetValue(name, out StoryMission m);
				if (!byId.TryGetValue((death ? "d" : "e") + id, out StoryEnding ending))
				{
					ending = new StoryEnding { Id = id, Death = death, Title = (death ? "死亡 " : "结局 ") + id };
					byId[(death ? "d" : "e") + id] = ending;
					Endings.Add(ending);
				}
				ending.Ways.Add(new StoryEndingWay { Script = script.Name, Path = path, Mission = m });
				if (m != null && !death)
				{
					m.Endings++;
				}
			}
			foreach (var (path, level) in script.Fights)
			{
				if (!ConditionBook.CombatDeaths.TryGetValue(level, out string id) || !byId.TryGetValue("d" + id, out StoryEnding ending))
				{
					continue;
				}
				owner.TryGetValue(name, out StoryMission m);
				ending.Ways.Add(new StoryEndingWay { Script = script.Name, Path = path, Mission = m, LostDuel = true });
			}
		}
		FileLogger.Log($"[剧情回顾] 时间线生成完毕：剧本 {names.Count}，任务 {Missions.Count}，结局 {Endings.Count}，用时 {timer.ElapsedMilliseconds} 毫秒");
	}

	/// <summary>一句台词的文字（简体、去掉格式），读不到返回空。</summary>
	public static string LineText(string key)
	{
		try
		{
			string text = LocalizationManager.Instance.LocaleResolver.GetStoryText(key);
			return string.IsNullOrWhiteSpace(text) || text == key ? "" : TextConverter.ToSimplified(System.Text.RegularExpressions.Regex.Replace(text, "<[^>]+>", "")).Replace("\n", " ");
		}
		catch
		{
			return "";
		}
	}

	private static IEnumerator BuildDialogue()
	{
		Dialogue.Clear();
		int n = 0;
		foreach (string name in Owner.Keys.ToList())
		{
			StoryScript script = StoryScripts.Get(name);
			if (script == null)
			{
				continue;
			}
			foreach (string key in script.Lines)
			{
				string text = LineText(key);
				if (text.Length > 0)
				{
					Dialogue.Add((name, text));
				}
				if (++n % 200 == 0)
				{
					yield return null;
				}
			}
		}
		FileLogger.Log($"[剧情回顾] 台词索引：{Dialogue.Count} 句");
	}

	/// <summary>按台词搜：返回（剧本、包含这段话的片段），最多 max 条。</summary>
	public static List<(string Script, string Snippet)> SearchDialogue(string query, int max)
	{
		var found = new List<(string, string)>();
		if (!DialogueReady || string.IsNullOrEmpty(query))
		{
			return found;
		}
		foreach (var (script, text) in Dialogue)
		{
			int at = text.IndexOf(query, StringComparison.OrdinalIgnoreCase);
			if (at < 0)
			{
				continue;
			}
			int from = Math.Max(0, at - 10);
			found.Add((script, (from > 0 ? "…" : "") + text.Substring(from, Math.Min(40, text.Length - from))));
			if (found.Count >= max)
			{
				break;
			}
		}
		return found;
	}

	private static void AddSeries(MissionData series, bool main)
	{
		if (series == null)
		{
			return;
		}
		string title = TextConverter.ToSimplified(series.Name ?? "");
		string desc = TextConverter.ToSimplified(series.GetFieldValue<string>("_desc") ?? "");
		if (title.Length == 0 || IsCode(title))
		{
			title = desc.Length > 0 ? FirstLine(desc, 16) : series.name;
		}
		foreach (MissionCheckData check in series.GetFieldValue<MissionCheckData[]>("_missions") ?? new MissionCheckData[0])
		{
			// 没启用的支线先留着，等算完入口再看有没有剧本接进来
			bool active = check != null && check.GetFieldValue<bool>("_active");
			if (check == null || (main && !active))
			{
				continue;
			}
			var mission = new StoryMission
			{
				Key = check.Key,
				Series = series.name,
				SeriesTitle = main ? "主线" : title,
				Main = main,
				Desc = FirstLine(TextConverter.ToSimplified(check.GetFieldValue<string>("_desc") ?? ""), 30),
				Place = check.Position.ToString() == "無" ? "" : TextConverter.ToSimplified(check.Position.ToString()),
				StartScript = check.StartScript ?? "",
				Conditions = check.GetFieldValue<ConditionResultItem[]>("_conditions") ?? new ConditionResultItem[0],
				Inactive = !active
			};
			SetTime(mission, check);
			Missions.Add(mission);
		}
	}

	private static bool IsCode(string text) => text.All(c => c < 128);

	private static void SetTime(StoryMission mission, MissionCheckData check)
	{
		if (!check.GetFieldValue<bool>("_dayCheck"))
		{
			mission.When = "任意时间";
			return;
		}
		switch (check.GetFieldValue<TimeCheckType>("_timeCheckType"))
		{
			case TimeCheckType.特定:
				GameTime at = check.GetFieldValue<GameTime>("_specificTime");
				mission.From = mission.To = Rounds(at);
				mission.When = DateText(at);
				break;
			case TimeCheckType.區間:
				GameTime min = check.GetFieldValue<GameTime>("_minTime"), max = check.GetFieldValue<GameTime>("_maxTime");
				mission.From = Rounds(min);
				mission.To = Rounds(max);
				mission.When = DateText(min) + " ～ " + DateText(max);
				break;
			default:
				mission.When = "每月" + StageText((int)check.GetFieldValue<MonthStageType>("_periodStage"));
				break;
		}
	}

	// GameTime 重载了 ==，和 null 比会抛异常，只能用 ReferenceEquals
	private static int? Rounds(GameTime t) => ReferenceEquals(t, null) || t.Year <= 0 ? (int?)null : (t.Year - 1) * 36 + (t.Month - 1) * 3 + ((int)t.Stage - 1);

	public static string DateText(GameTime t) => ReferenceEquals(t, null) || t.Year <= 0 ? "时间不定" : $"第{t.Year}年{t.Month}月{StageText((int)t.Stage)}";

	public static string DateText(int rounds) => $"第{rounds / 36 + 1}年{rounds % 36 / 3 + 1}月{StageText(rounds % 3 + 1)}";

	public static StoryMission Find(string key) => Missions.FirstOrDefault(m => m.Key == key);

	private static string LibraryText(string key, string fallback)
	{
		try
		{
			string text = LocalizationManager.Instance.LocaleResolver.GetString(key);
			if (!string.IsNullOrWhiteSpace(text) && text != key)
			{
				return TextConverter.ToSimplified(System.Text.RegularExpressions.Regex.Replace(text, "<[^>]+>", "").Trim());
			}
		}
		catch
		{
		}
		return fallback;
	}

	public static string StageText(int stage) => stage == 1 ? "上旬" : stage == 2 ? "中旬" : "下旬";

	private static string FirstLine(string text, int length)
	{
		string first = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim() ?? "";
		return first.Length > length ? first.Substring(0, length) + "…" : first;
	}

	/// <summary>任务的触发条件（多组之间是「或」），带当前值，满足绿、不满足红。</summary>
	public static string ConditionText(StoryMission mission)
	{
		if (Conditions.TryGetValue(mission, out string cached))
		{
			return cached;
		}
		var parts = new List<string>();
		foreach (ConditionResultItem item in mission.Conditions)
		{
			if (item == null)
			{
				continue;
			}
			string text = DataHelper.ExtractConditions(item, out _);
			if (text.Length > 0)
			{
				parts.Add(text);
			}
		}
		return Conditions[mission] = string.Join("\n或 ", parts.ToArray());
	}
}
