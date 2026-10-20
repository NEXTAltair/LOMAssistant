using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Mortal.Core;
using UnityEngine;

namespace LOMAssistant.Features.Story;

/// <summary>
/// 「数值」页（F9 直接打开）：角色数值、武功与学识、门派、好感、天赋、旗标，一页里分几段，顶上一个搜索框全都能搜。
/// 每行：名字、当前值、加减按钮、输入框（改了数字点「设定」）。改的是游戏自己的数据，和原版测试面板一样会写进存档。
/// </summary>
public partial class StoryReviewPanel
{
	private sealed class StatRow
	{
		public string Id = "", Name = "", Note = "";
		public Func<int> Get;
		public Action<int> Set; // 设成这个值
		public int Step = 5;
		public Func<string> Extra; // 数值后面的小字，如加成、心上人是谁、等级上限
	}

	private string _statsSearch = "";
	private Vector2 _statsScroll;
	private int _flagPage;
	private string _flagQuery;
	private readonly Dictionary<string, string> _statInputs = new Dictionary<string, string>();
	private List<(string Title, List<StatRow> Rows)> _statSections;
	private object _statsFor;

	private const int FlagsPerPage = 60;

	private static readonly GameStatType[] Character =
	{
		GameStatType.體力, GameStatType.內力, GameStatType.輕功, GameStatType.學問, GameStatType.嘴力, GameStatType.心理衛生,
		GameStatType.性情, GameStatType.處世, GameStatType.修養, GameStatType.道德, GameStatType.陰陽內功, GameStatType.命運,
		GameStatType.魅力, GameStatType.銀兩, GameStatType.個人貢獻度, GameStatType.抗毒, GameStatType.抗麻, GameStatType.愛人, GameStatType.變心,
	};

	private static readonly GameStatType[] Martial =
	{
		GameStatType.武功刀劍, GameStatType.武功暗器, GameStatType.武功拳掌, GameStatType.武功腿法, GameStatType.武功奇門,
		GameStatType.武功軟兵器, GameStatType.武功槍棍, GameStatType.武功內功, GameStatType.儒學, GameStatType.道學, GameStatType.佛學,
	};

	private static readonly GameStatType[] Sect =
	{
		GameStatType.門派規模, GameStatType.門派人數, GameStatType.向心力, GameStatType.門派名聲, GameStatType.武學點數,
		GameStatType.鍛造, GameStatType.毒藥, GameStatType.門派資產, GameStatType.門派貢獻,
	};

	/// <summary>F9：打开面板并切到「数值」页；已经在这一页就关掉。</summary>
	public static void ToggleStats()
	{
		// 插件的物体对 FindObjectOfType 不可见，用 Ensure 时记下的那个
		StoryReviewPanel panel = Instance;
		if (panel == null)
		{
			return;
		}
		if (panel._visible && panel._tab == StatsTab)
		{
			panel._visible = false;
			return;
		}
		panel._visible = true;
		panel._tab = StatsTab;
		panel._statSections = null;
		panel._statInputs.Clear();
	}

	private const int StatsTab = 5;

	private void DrawStats(Rect r)
	{
		PlayerStatManagerData player = PlayerStatManagerData.Instance;
		if (player == null || player.Stats == null || SaveSystem.Instance == null || !StoryNodes.HasCurrentSave)
		{
			GUI.Label(new Rect(r.x, r.y, r.width, S(40)), "进游戏读档后才能改数值", _muted);
			return;
		}
		if (_statSections == null || !ReferenceEquals(_statsFor, player))
		{
			_statSections = BuildStatSections(player);
			_statsFor = player;
		}
		string query = SearchBox(new Rect(r.x, r.y, S(420), S(36)), ref _statsSearch);
		if (query != _flagQuery)
		{
			_flagQuery = query;
			_flagPage = 0;
		}
		var area = new Rect(r.x, r.y + S(46), r.width, r.height - S(46));
		DragScroll("stats", area, ref _statsScroll);
		GUILayout.BeginArea(area);
		_statsScroll = GUILayout.BeginScrollView(_statsScroll);
		float rowWidth = S(540);
		int columns = Mathf.Max(1, (int)((area.width - S(30)) / rowWidth));
		foreach (var (title, rows) in _statSections)
		{
			var shown = rows.Where(row => Matches(query, row.Name, row.Id, row.Note)).ToList();
			if (shown.Count == 0)
			{
				continue;
			}
			bool flags = title == "旗标";
			int pages = 1;
			if (flags)
			{
				pages = Mathf.Max(1, (shown.Count + FlagsPerPage - 1) / FlagsPerPage);
				_flagPage = Mathf.Clamp(_flagPage, 0, pages - 1);
				shown = shown.Skip(_flagPage * FlagsPerPage).Take(FlagsPerPage).ToList();
			}
			GUILayout.Space(S(8));
			GUILayout.BeginHorizontal();
			GUILayout.Label(title + (flags ? $"　<size={Mathf.RoundToInt(16 * _scale)}><color=#73644F>共 {rows.Count(row => Matches(query, row.Name, row.Id, row.Note))} 面，按编号或中文说明搜</color></size>" : ""), _heading);
			GUILayout.FlexibleSpace();
			if (flags && pages > 1)
			{
				if (GUILayout.Button("上一页", _button, GUILayout.Width(S(90)))) _flagPage = Mathf.Max(0, _flagPage - 1);
				GUILayout.Label($"{_flagPage + 1} / {pages}", _center, GUILayout.Width(S(80)));
				if (GUILayout.Button("下一页", _button, GUILayout.Width(S(90)))) _flagPage = Mathf.Min(pages - 1, _flagPage + 1);
			}
			GUILayout.EndHorizontal();
			for (int i = 0; i < shown.Count; i += columns)
			{
				GUILayout.BeginHorizontal();
				for (int c = 0; c < columns && i + c < shown.Count; c++)
				{
					DrawStatRow(shown[i + c], rowWidth);
				}
				GUILayout.FlexibleSpace();
				GUILayout.EndHorizontal();
			}
		}
		GUILayout.EndScrollView();
		GUILayout.EndArea();
	}

	private void DrawStatRow(StatRow row, float width)
	{
		GUILayout.BeginHorizontal(_card, GUILayout.Width(width - S(10)));
		// 一行放得下：名字、说明太长就截短，鼠标停上去看全文
		GUIStyle line = OneLine(_label);
		GUIStyle muted = OneLine(_muted);
		float nameWidth = S(150), extraWidth = S(110);
		GUILayout.Label(Fit(row.Name, line, nameWidth), line, GUILayout.Width(nameWidth));
		Rect nameRect = GUILayoutUtility.GetLastRect();
		int value = row.Get();
		GUILayout.Label(value.ToString(), line, GUILayout.Width(S(60)));
		string extra = row.Extra?.Invoke() ?? "";
		GUILayout.Label(Fit(extra, muted, extraWidth), muted, GUILayout.Width(extraWidth));
		Rect extraRect = GUILayoutUtility.GetLastRect();
		string full = row.Name + (row.Note.Length > 0 ? "\n" + row.Note : "");
		if (full != row.Name || Fit(row.Name, line, nameWidth) != row.Name)
		{
			Hover(nameRect, full);
			Hover(extraRect, full);
		}
		if (GUILayout.Button("-" + row.Step, _button, GUILayout.Width(S(52))))
		{
			row.Set(value - row.Step);
			_statInputs.Remove(row.Id);
		}
		if (GUILayout.Button("+" + row.Step, _button, GUILayout.Width(S(52))))
		{
			row.Set(value + row.Step);
			_statInputs.Remove(row.Id);
		}
		if (!_statInputs.TryGetValue(row.Id, out string input))
		{
			input = value.ToString();
		}
		string typed = GUILayout.TextField(input, _field, GUILayout.Width(S(64)));
		if (typed != value.ToString())
		{
			_statInputs[row.Id] = typed;
		}
		else
		{
			_statInputs.Remove(row.Id);
		}
		bool changed = _statInputs.ContainsKey(row.Id) && int.TryParse(typed, out int target);
		GUI.enabled = changed;
		if (GUILayout.Button("设定", changed ? _primary : _button, GUILayout.Width(S(52))) && int.TryParse(typed, out target))
		{
			row.Set(target);
			_statInputs.Remove(row.Id);
			GUIUtility.keyboardControl = 0;
		}
		GUI.enabled = true;
		GUILayout.EndHorizontal();
	}

	private static List<(string, List<StatRow>)> BuildStatSections(PlayerStatManagerData player)
	{
		var sections = new List<(string, List<StatRow>)>
		{
			("角色数值", Character.Select(t => StatRowOf(player, t)).Where(x => x != null).ToList()),
			("武功与学识", Martial.Select(t => StatRowOf(player, t)).Where(x => x != null).ToList()),
			("门派", Sect.Select(t => StatRowOf(player, t)).Where(x => x != null).ToList()),
		};
		// 好感：游戏里所有人，原版测试面板没列的（龙渊、石公远……）也在
		var favor = new List<StatRow>();
		foreach (RelationshipStat stat in player.Relationships?.List ?? new List<RelationshipStat>())
		{
			if (stat == null)
			{
				continue;
			}
			RelationshipStat s = stat;
			favor.Add(new StatRow
			{
				Id = "rel:" + s.name,
				Name = RelationshipName(s),
				Get = () => s.Value,
				Set = v => s.SetValue(v),
			});
		}
		sections.Add(("好感", favor));
		// 天赋：会显示等级的那些
		var talents = new List<StatRow>();
		foreach (PlayerTalentData talent in player.Talents?.List ?? new List<PlayerTalentData>())
		{
			if (talent == null || !talent.DisplayLevel)
			{
				continue;
			}
			PlayerTalentData t = talent;
			int max = Traverse.Create(t).Field("_maxLevel").GetValue<int>();
			talents.Add(new StatRow
			{
				Id = "talent:" + t.Id,
				Name = Localized(t.GetIdKey(), t.name),
				Note = Localized(t.GetDescKey(), ""),
				Get = () => t.Level,
				Set = v => t.AddLevel(Mathf.Clamp(v, 0, max) - t.Level),
				Step = 1,
				Extra = () => "/ " + max,
			});
		}
		sections.Add(("天赋", talents));
		// 旗标：全部，按编号排，说明转简体
		var flags = new List<StatRow>();
		MissionManagerData missions = MissionManagerData.Instance;
		foreach (FlagData flag in missions?.FlagsCollection?.Where(c => c != null && c.List != null).SelectMany(c => c.List).Where(f => f != null).Distinct().OrderBy(f => f.name, StringComparer.Ordinal)
			?? Enumerable.Empty<FlagData>())
		{
			FlagData f = flag;
			string note = TextConverter.ToSimplified(f.DevNote ?? "").Replace("\n", " ").Trim();
			flags.Add(new StatRow
			{
				Id = "flag:" + f.name,
				Name = f.name,
				Note = note,
				Get = () => f.State,
				Set = v => f.State = v,
				Step = 1,
				Extra = () => note,
			});
		}
		sections.Add(("旗标", flags));
		return sections;
	}

	private static StatRow StatRowOf(PlayerStatManagerData player, GameStatType type)
	{
		GameStat stat;
		try
		{
			stat = player.Stats.Get(type);
		}
		catch
		{
			return null;
		}
		if (stat == null)
		{
			return null;
		}
		GameStat s = stat;
		bool money = type == GameStatType.銀兩 || type == GameStatType.門派資產;
		bool small = type == GameStatType.門派規模 || type == GameStatType.愛人 || type == GameStatType.變心;
		return new StatRow
		{
			Id = "stat:" + (int)type,
			Name = Localized(s.GetLocaleKey(), type.ToString()),
			Get = () => s.FinalValue,
			Set = v => s.AddValue(v - s.FinalValue, totalAdd: false),
			Step = money ? 500 : small ? 1 : 5,
			Extra = () =>
			{
				if (type == GameStatType.愛人)
				{
					string who = Localized("PlayerInfo/Lover/" + s.FinalValue, "");
					return who.Length > 0 ? "(" + who + ")" : "";
				}
				return s.AdditionValue != 0 ? "(" + s.AdditionValue.ToString("+0;-0") + ")" : "";
			},
		};
	}

	private static string Localized(string key, string fallback)
	{
		try
		{
			string text = LocalizationManager.Instance.LocaleResolver.GetString(key);
			if (!string.IsNullOrEmpty(text) && text != key)
			{
				return TextConverter.ToSimplified(text);
			}
		}
		catch
		{
		}
		return TextConverter.ToSimplified(fallback ?? "");
	}

	/// <summary>好感的人名：类型名（简体）；类型没起名的（同一人的几份好感）用资源名后半段。</summary>
	private static string RelationshipName(RelationshipStat stat)
	{
		string name = stat.Type.ToString();
		if (name.All(char.IsDigit))
		{
			string[] parts = stat.name.Split('_');
			name = parts.Length > 2 ? string.Join("_", parts.Skip(2).ToArray()) : stat.name;
		}
		return TextConverter.ToSimplified(name);
	}

	private readonly Dictionary<GUIStyle, GUIStyle> _oneLine = new Dictionary<GUIStyle, GUIStyle>();

	private GUIStyle OneLine(GUIStyle style)
	{
		if (!_oneLine.TryGetValue(style, out GUIStyle one))
		{
			_oneLine[style] = one = new GUIStyle(style) { wordWrap = false, clipping = TextClipping.Clip, richText = false };
		}
		return one;
	}

	/// <summary>放不下就截短加「…」。</summary>
	private static string Fit(string text, GUIStyle style, float width)
	{
		if (string.IsNullOrEmpty(text) || style.CalcSize(new GUIContent(text)).x <= width)
		{
			return text ?? "";
		}
		for (int n = text.Length - 1; n > 0; n--)
		{
			string cut = text.Substring(0, n) + "…";
			if (style.CalcSize(new GUIContent(cut)).x <= width)
			{
				return cut;
			}
		}
		return "…";
	}

	private static string ShortNote(string note)
	{
		string first = note.Split(' ')[0];
		return first.Length > 8 ? first.Substring(0, 8) + "…" : first;
	}
}
