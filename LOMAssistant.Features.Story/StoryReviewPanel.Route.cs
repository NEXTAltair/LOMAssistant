using System;
using System.Collections.Generic;
using System.Linq;
using Mortal.Core;
using UnityEngine;

namespace LOMAssistant.Features.Story;

// 路线图：主线任务是站点，按剧本里写明的「下一个主线任务」连线（没写明、按日期接上的画虚线）；
// 一个站点往后岔出几条路时，第一条留在原车道，其余分到上下的车道。横轴是游戏日历，支线按任务组挂在下面。
public partial class StoryReviewPanel
{
	private sealed class RouteLayout
	{
		public readonly Dictionary<string, Vector2> Points = new Dictionary<string, Vector2>(StringComparer.Ordinal);
		public readonly List<(string From, string To, bool ByDate)> Edges = new List<(string, string, bool)>();
		public readonly List<(float X, int Date)> Columns = new List<(float, int)>();
		public readonly List<SideStory> Side = new List<SideStory>();
		public float Width, Height, MainBottom, LabelWidth;
	}

	private sealed class SideStory
	{
		public string Series = "", Title = "";
		public List<StoryMission> Missions = new List<StoryMission>();
		/// <summary>走完会把主线设到这些站（襄阳行这类）。</summary>
		public List<StoryMission> Joins = new List<StoryMission>();
		public Rect Pill;
		public int? Read;
		public bool Visited, Dated;
	}

	private RouteLayout _route;
	private int _routeBuiltFor = -1;
	private float _routeZoom = 1f, _routeBuiltZoom;
	private Vector2 _routeScroll, _routeDetailScroll;
	private string _routeSearch = "", _routeLastQuery = "";
	private StoryMission _routePicked;
	private SideStory _routeSide;

	private float Z(float v) => S(v) * _routeZoom;

	private void DrawRoute(Rect r)
	{
		if (!EnsureAtlas(r))
		{
			return;
		}
		if (_route == null || _routeBuiltFor != StoryAtlas.Version || !Mathf.Approximately(_routeBuiltZoom, _routeZoom * _scale))
		{
			_route = BuildRoute();
			_routeBuiltFor = StoryAtlas.Version;
			_routeBuiltZoom = _routeZoom * _scale;
		}
		// 顶上一行：搜索、缩放、跳到当前主线、重新生成
		string query = SearchBox(new Rect(r.x, r.y, S(360), S(36)), ref _routeSearch);
		// 输入新的搜索词时，右边改成显示搜索结果
		if (query != _routeLastQuery)
		{
			_routeLastQuery = query;
			if (query.Length >= 2)
			{
				_routePicked = null;
				_routeSide = null;
				_routeDetailScroll = Vector2.zero;
			}
		}
		float bx = r.x + S(376);
		if (GUI.Button(new Rect(bx, r.y, S(40), S(36)), "－", _button)) _routeZoom = Mathf.Max(0.6f, _routeZoom - 0.1f);
		GUI.Label(new Rect(bx + S(44), r.y, S(60), S(36)), Mathf.RoundToInt(_routeZoom * 100) + "%", _center);
		if (GUI.Button(new Rect(bx + S(108), r.y, S(40), S(36)), "＋", _button)) _routeZoom = Mathf.Min(1.6f, _routeZoom + 0.1f);
		if (GUI.Button(new Rect(bx + S(160), r.y, S(130), S(36)), "当前主线", _button) && _route.Points.TryGetValue(StoryAtlas.CurrentMain, out Vector2 now))
		{
			_routePicked = StoryAtlas.Find(StoryAtlas.CurrentMain);
			_routeSide = null;
			_routeScroll = new Vector2(Mathf.Max(0, now.x - r.width * 0.3f), Mathf.Max(0, now.y - r.height * 0.4f));
		}
		if (GUI.Button(new Rect(r.xMax - S(130), r.y, S(130), S(36)), "重新生成", _button))
		{
			_reading.Clear();
			StoryAtlas.Start();
			return;
		}
		float detailWidth = Mathf.Min(S(460), r.width * 0.32f);
		var canvas = new Rect(r.x, r.y + S(46), r.width - detailWidth - S(12), r.height - S(46));
		var detail = new Rect(canvas.xMax + S(12), canvas.y, detailWidth, canvas.height);
		Fill(canvas, PaperLight);
		Frame(canvas, Edge, 1);
		DrawRouteCanvas(canvas, query);
		Fill(detail, PaperDeep);
		Frame(detail, Edge, 1);
		DragScroll("routeDetail", detail, ref _routeDetailScroll);
		GUILayout.BeginArea(new Rect(detail.x + S(12), detail.y + S(10), detail.width - S(24), detail.height - S(20)));
		_routeDetailScroll = GUILayout.BeginScrollView(_routeDetailScroll);
		if (query.Length >= 2 && _routeSide == null && _routePicked == null)
		{
			DrawSearchResults(query);
		}
		else if (_routeSide != null)
		{
			DrawSideDetail(_routeSide);
		}
		else if (_routePicked != null)
		{
			DrawMissionDetail(_routePicked);
		}
		GUILayout.EndScrollView();
		GUILayout.EndArea();
	}

	private RouteLayout BuildRoute()
	{
		var layout = new RouteLayout { LabelWidth = Z(132) };
		var mains = StoryAtlas.Missions.Where(m => m.Main && m.StartScript.Length > 0).ToDictionary(m => m.Key, StringComparer.Ordinal);
		// 连线：剧本里写明的
		var edges = new List<(string From, string To, bool ByDate)>();
		foreach (StoryMission m in mains.Values)
		{
			foreach (string next in m.Next)
			{
				if (mains.ContainsKey(next))
				{
					edges.Add((m.Key, next, false));
				}
			}
		}
		// 日期：没有日期的任务跟在它前一站后面
		var date = mains.Values.ToDictionary(m => m.Key, m => m.From, StringComparer.Ordinal);
		for (int pass = 0; pass < 6; pass++)
		{
			foreach (var e in edges)
			{
				if (date[e.To] == null && date[e.From] != null)
				{
					date[e.To] = date[e.From];
				}
			}
		}
		var placed = mains.Values.Where(m => date[m.Key] != null).OrderBy(m => date[m.Key]).ThenBy(m => m.Key, StringComparer.Ordinal).ToList();
		// 没有任何剧本写明会走到的站点（按日期自动开始的），从日期最近的前一站接一条虚线
		var hasIncoming = new HashSet<string>(edges.Select(e => e.To), StringComparer.Ordinal);
		for (int i = 1; i < placed.Count; i++)
		{
			StoryMission m = placed[i];
			if (hasIncoming.Contains(m.Key))
			{
				continue;
			}
			int? before = placed.Take(i).Select(p => date[p.Key]).Where(d => d < date[m.Key]).DefaultIfEmpty(null).Max();
			if (before == null)
			{
				continue;
			}
			foreach (StoryMission p in placed.Take(i).Where(p => date[p.Key] == before).Take(2))
			{
				edges.Add((p.Key, m.Key, true));
			}
		}
		layout.Edges.AddRange(edges);

		// 横轴：每个出现过的日期一列，列距按相隔的旬数，有上下限
		float x = Z(40);
		int? last = null;
		foreach (int d in placed.Select(m => date[m.Key].Value).Distinct().OrderBy(v => v))
		{
			if (last != null)
			{
				x += Mathf.Clamp((d - last.Value) * Z(14), layout.LabelWidth + Z(16), Z(260));
			}
			layout.Columns.Add((x, d));
			last = d;
		}

		// 车道：沿着连线往下排，岔路的第一条留在原车道，其余就近找空车道
		var lane = new Dictionary<string, int>(StringComparer.Ordinal);
		var used = new Dictionary<int, List<float>>();
		var children = edges.GroupBy(e => e.From).ToDictionary(g => g.Key, g => g.Select(e => e.To).OrderBy(k => date[k] ?? int.MaxValue).ThenBy(k => k, StringComparer.Ordinal).ToList(), StringComparer.Ordinal);
		bool Free(int l, float px) => !used.TryGetValue(l, out var xs) || xs.All(v => Mathf.Abs(v - px) >= layout.LabelWidth + Z(8));
		foreach (StoryMission m in placed)
		{
			float px = XOf(layout, date[m.Key].Value);
			var preds = edges.Where(e => e.To == m.Key && lane.ContainsKey(e.From)).Select(e => e.From).ToList();
			int want = 0;
			string heir = preds.FirstOrDefault(p => children.TryGetValue(p, out var c) && c[0] == m.Key);
			if (heir != null)
			{
				want = lane[heir];
			}
			else if (preds.Count > 0)
			{
				want = lane[preds[0]] + 1;
			}
			int chosen = want;
			for (int step = 0; step < 20; step++)
			{
				int candidate = want + (step % 2 == 0 ? step / 2 : -(step / 2 + 1));
				if (Free(candidate, px))
				{
					chosen = candidate;
					break;
				}
			}
			lane[m.Key] = chosen;
			if (!used.TryGetValue(chosen, out var list))
			{
				used[chosen] = list = new List<float>();
			}
			list.Add(px);
		}
		int minLane = lane.Values.DefaultIfEmpty(0).Min(), maxLane = lane.Values.DefaultIfEmpty(0).Max();
		float top = Z(70), laneHeight = Z(96);
		foreach (StoryMission m in placed)
		{
			layout.Points[m.Key] = new Vector2(XOf(layout, date[m.Key].Value), top + (lane[m.Key] - minLane) * laneHeight + Z(20));
		}
		layout.MainBottom = top + (maxLane - minLane + 1) * laneHeight;

		// 支线：每个任务组一颗胶囊，放在最早能触发的日期上，一行放不下就换行
		float y = layout.MainBottom + Z(56);
		var rows = new List<float>();
		var groups = StoryAtlas.Missions.Where(m => !m.Main && m.StartScript.Length > 0).GroupBy(m => m.Series)
			.Select(g => new SideStory
			{
				Series = g.Key,
				Title = SideTitle(g.ToList()),
				Missions = g.ToList(),
				Joins = g.SelectMany(m => m.Next).Distinct().Where(mains.ContainsKey).Select(k => mains[k]).ToList(),
				Dated = g.Any(m => m.From != null),
				Read = StoryScripts.ReadPercent(g.SelectMany(m => m.Scripts).Distinct().Select(StoryScripts.Get).Where(s => s != null).SelectMany(s => s.Lines)),
				Visited = g.Any(m => m.Visited)
			}).OrderBy(s => s.Dated ? s.Missions.Where(m => m.From != null).Min(m => m.From.Value) : int.MaxValue).ToList();
		var pillStyle = new GUIStyle(_boxTitle) { fontSize = Mathf.Max(9, Mathf.RoundToInt(_boxTitle.fontSize * _routeZoom)) };
		// 签条：左边一道墨条，前面留「✓ 」的位置
		foreach (SideStory side in groups)
		{
			float sx = side.Dated ? XOf(layout, side.Missions.Where(m => m.From != null).Min(m => m.From.Value)) - Z(10) : Z(20);
			float w = pillStyle.CalcSize(new GUIContent("✓ " + side.Title + (side.Joins.Count > 0 ? " → 主线" : ""))).x + Z(26);
			int row = rows.FindIndex(end => end <= sx);
			if (row < 0)
			{
				row = rows.Count;
				rows.Add(0);
			}
			rows[row] = sx + w + Z(10);
			side.Pill = new Rect(sx, y + row * Z(30), w, Z(24));
			layout.Side.Add(side);
		}
		layout.Width = Mathf.Max(layout.Columns.Count > 0 ? layout.Columns[layout.Columns.Count - 1].X : 0, rows.DefaultIfEmpty(0).Max()) + Z(200);
		layout.Height = y + rows.Count * Z(30) + Z(30);
		return layout;
	}

	/// <summary>
	/// 支线签条的名字：任务组自己的名字；游戏里没起名（只有 S2321 这样的编号）的，
	/// 用最早那个任务的说明，再没有就用它的第一条剧情记录。
	/// </summary>
	private static string SideTitle(List<StoryMission> missions)
	{
		string title = missions[0].SeriesTitle;
		if (title.Any(c => c >= 128))
		{
			return title;
		}
		var first = missions.OrderBy(m => m.From ?? int.MaxValue).ThenBy(m => m.Key, StringComparer.Ordinal).ToList();
		string named = first.Select(m => m.Desc).FirstOrDefault(d => d.Any(c => c >= 128))
			?? first.Select(m => m.Summary).FirstOrDefault(d => d.Length > 0);
		return named != null ? Short(named, 12) : title;
	}

	/// <summary>日期 → 横坐标：落在两列之间的按旬数插进去。</summary>
	private static float XOf(RouteLayout layout, int date)
	{
		var cols = layout.Columns;
		if (cols.Count == 0)
		{
			return 0;
		}
		if (date <= cols[0].Date)
		{
			return cols[0].X;
		}
		for (int i = 1; i < cols.Count; i++)
		{
			if (date <= cols[i].Date)
			{
				float t = (float)(date - cols[i - 1].Date) / Mathf.Max(1, cols[i].Date - cols[i - 1].Date);
				return Mathf.Lerp(cols[i - 1].X, cols[i].X, t);
			}
		}
		return cols[cols.Count - 1].X;
	}

	private void DrawRouteCanvas(Rect canvas, string query)
	{
		RouteLayout layout = _route;
		Event e = Event.current;
		// Ctrl + 滚轮：以鼠标所在的位置为中心缩放（地图按新比例重排后，鼠标下面还是同一个地方）
		if (e.type == EventType.ScrollWheel && canvas.Contains(e.mousePosition) && (e.control || e.command))
		{
			float before = _routeZoom;
			float after = Mathf.Clamp(Mathf.Round((before + (e.delta.y < 0 ? 0.1f : -0.1f)) * 10f) / 10f, 0.6f, 1.6f);
			if (!Mathf.Approximately(before, after))
			{
				Vector2 mouse = e.mousePosition - canvas.position;
				_routeScroll = Vector2.Max(Vector2.zero, (_routeScroll + mouse) * (after / before) - mouse);
				_routeZoom = after;
			}
			e.Use();
		}
		else if (e.type == EventType.ScrollWheel && canvas.Contains(e.mousePosition) && e.shift)
		{
			_routeScroll.x += e.delta.y * Z(30);
			e.Use();
		}
		_routeScroll = GUI.BeginScrollView(canvas, _routeScroll, new Rect(0, 0, Mathf.Max(layout.Width, canvas.width) + canvas.width * 0.3f, Mathf.Max(layout.Height, canvas.height) + canvas.height * 0.4f));
		int pan = GUIUtility.GetControlID(FocusType.Passive);
		var view = new Rect(_routeScroll.x, _routeScroll.y, canvas.width, canvas.height);
		bool repaint = e.type == EventType.Repaint;
		var tiny = new GUIStyle(_boxSmall) { fontSize = Mathf.Max(8, Mathf.RoundToInt(_boxSmall.fontSize * _routeZoom)), alignment = TextAnchor.UpperCenter, wordWrap = true, clipping = TextClipping.Overflow };
		var name = new GUIStyle(_boxTitle) { fontSize = Mathf.Max(9, Mathf.RoundToInt(_boxTitle.fontSize * _routeZoom)), alignment = TextAnchor.UpperCenter, wordWrap = true, clipping = TextClipping.Overflow };
		if (repaint)
		{
			// 日历：每年一条淡墨底色，每列标上月份
			int year = -1;
			for (int i = 0; i < layout.Columns.Count; i++)
			{
				var (cx, d) = layout.Columns[i];
				if (d / 36 != year)
				{
					year = d / 36;
					float end = layout.Columns.Skip(i).Where(c => c.Date / 36 != year).Select(c => c.X - layout.LabelWidth / 2).DefaultIfEmpty(layout.Width).First();
					Fill(new Rect(cx - layout.LabelWidth / 2, 0, end - cx + layout.LabelWidth / 2, layout.Height), new Color(Ink.r, Ink.g, Ink.b, year % 2 == 0 ? 0.02f : 0.05f));
					Fill(new Rect(cx - layout.LabelWidth / 2, 0, Z(2), layout.Height), new Color(Ink.r, Ink.g, Ink.b, 0.18f));
					GUI.Label(new Rect(cx - layout.LabelWidth / 2 + Z(8), Z(6), Z(140), Z(26)), $"<b>第{year + 1}年</b>", name);
				}
				Fill(new Rect(cx, Z(56), 1, layout.MainBottom - Z(56)), new Color(Ink.r, Ink.g, Ink.b, 0.07f));
				GUI.Label(new Rect(cx - Z(40), Z(34), Z(80), Z(20)), $"{d % 36 / 3 + 1}月{StoryAtlas.StageText(d % 3 + 1).Substring(0, 1)}", tiny);
			}
			Fill(new Rect(0, layout.MainBottom + Z(16), layout.Width, Z(2)), InkLine);
			GUI.Label(new Rect(view.x + Z(12), layout.MainBottom + Z(24), Z(200), Z(26)), "<b>支线</b>", name);
			// 连线：这一局走过的两站之间用青色粗线
			foreach (var (from, to, byDate) in layout.Edges)
			{
				if (!layout.Points.TryGetValue(from, out Vector2 a) || !layout.Points.TryGetValue(to, out Vector2 b))
				{
					continue;
				}
				StoryMission ma = StoryAtlas.Find(from), mb = StoryAtlas.Find(to);
				bool walked = ma != null && mb != null && ma.Visited && mb.Visited;
				Elbow(new Vector2(a.x + Z(10), a.y), new Vector2(b.x - Z(10), b.y), walked ? Jade : InkLine, walked ? Z(3) : Z(2), byDate, b.x - Z(26));
			}
		}
		// 站点：墨圈，读得越多圈里墨越浓；走过的名字前打勾，当前主线盖一枚朱砂印
		foreach (var pair in layout.Points)
		{
			StoryMission m = StoryAtlas.Find(pair.Key);
			Vector2 p = pair.Value;
			var hit = new Rect(p.x - layout.LabelWidth / 2, p.y - Z(14), layout.LabelWidth, Z(66));
			if (m == null || !hit.Overlaps(view))
			{
				continue;
			}
			bool dim = query.Length > 0 && !Matches(query, m.Title, m.Key, m.Place, m.Summary);
			if (repaint)
			{
				float rr = Z(9), alpha = dim ? 0.25f : 1f;
				var dot = new Rect(p.x - rr, p.y - rr, rr * 2, rr * 2);
				Round(dot, new Color(PaperLight.r, PaperLight.g, PaperLight.b, alpha), rr);
				if (m.Read != null)
				{
					Round(dot, new Color(Ink.r, Ink.g, Ink.b, alpha * (0.12f + 0.88f * m.Read.Value / 100f)), rr);
				}
				Round(dot, new Color(Ink.r, Ink.g, Ink.b, alpha), rr, Z(2));
				if (_routePicked == m)
				{
					Round(new Rect(dot.x - Z(4), dot.y - Z(4), dot.width + Z(8), dot.height + Z(8)), Cinnabar, rr + Z(4), Z(2));
				}
				if (StoryAtlas.CurrentMain == m.Key)
				{
					var stamp = new Rect(p.x + Z(9), p.y - Z(30), Z(22), Z(22));
					Round(stamp, Cinnabar, Z(3));
					GUI.Label(stamp, "今", new GUIStyle(_seal) { fontSize = Mathf.Max(8, Mathf.RoundToInt(_seal.fontSize * 0.7f * _routeZoom)) });
				}
				if (m.Endings > 0)
				{
					GUI.Label(new Rect(p.x - Z(34), p.y - Z(22), Z(24), Z(18)), "<color=#A3302A><b>终</b></color>", tiny);
				}
				var label = new Rect(p.x - layout.LabelWidth / 2 - Z(20), p.y + Z(13), layout.LabelWidth + Z(40), Z(24));
				string title = (m.Visited ? "<color=#2F6B3A>✓</color> " : "") + Short(m.Title, 9);
				GUI.Label(label, dim ? "<color=#B5AA98>" + RichText.Strip(title) + "</color>" : title, new GUIStyle(name) { wordWrap = false });
			}
			if (GUI.Button(hit, GUIContent.none, GUIStyle.none))
			{
				_routePicked = m;
				_routeSide = null;
				_routeDetailScroll = Vector2.zero;
			}
		}
		// 支线签条
		foreach (SideStory side in layout.Side)
		{
			if (!side.Pill.Overlaps(view))
			{
				continue;
			}
			bool dim = query.Length > 0 && !Matches(query, side.Title) && !side.Missions.Any(m => Matches(query, m.Title, m.Key, m.Place, m.Summary));
			if (repaint)
			{
				float alpha = dim ? 0.3f : 1f;
				Fill(side.Pill, new Color(PaperLight.r, PaperLight.g, PaperLight.b, alpha));
				Frame(side.Pill, _routeSide == side ? Cinnabar : new Color(Edge.r, Edge.g, Edge.b, alpha), _routeSide == side ? Z(2) : 1);
				Fill(new Rect(side.Pill.x, side.Pill.y, Z(4), side.Pill.height), new Color(Ink.r, Ink.g, Ink.b, alpha));
				if (side.Read != null)
				{
					ReadBar(new Rect(side.Pill.x + Z(6), side.Pill.yMax - Z(4), side.Pill.width - Z(10), Z(2)), side.Read);
				}
				var tagText = new GUIStyle(name) { alignment = TextAnchor.MiddleCenter, wordWrap = false };
				// 走完会接到某一站主线的，后面标「→ 主线」
				string text = (side.Visited ? "<color=#2F6B3A>✓</color> " : "") + side.Title + (side.Joins.Count > 0 ? " <color=#A3302A>→ 主线</color>" : "");
				GUI.Label(new Rect(side.Pill.x + Z(4), side.Pill.y - Z(1), side.Pill.width - Z(4), side.Pill.height), dim ? "<color=#B5AA98>" + side.Title + "</color>" : text, tagText);
			}
			if (GUI.Button(side.Pill, GUIContent.none, GUIStyle.none))
			{
				_routeSide = side;
				_routePicked = null;
				_routeDetailScroll = Vector2.zero;
			}
		}
		// 按住空白处拖动地图
		switch (e.GetTypeForControl(pan))
		{
			case EventType.MouseDown when e.button == 0 && new Rect(view.x, view.y, view.width - S(22), view.height - S(22)).Contains(e.mousePosition):
				GUIUtility.hotControl = pan;
				e.Use();
				break;
			case EventType.MouseDrag when GUIUtility.hotControl == pan:
				_routeScroll -= e.delta;
				e.Use();
				break;
			case EventType.MouseUp when GUIUtility.hotControl == pan:
				GUIUtility.hotControl = 0;
				e.Use();
				break;
		}
		GUI.EndScrollView();
	}

	private void DrawMissionDetail(StoryMission m)
	{
		GUILayout.Label((m.Main ? "主线" : m.SeriesTitle) + "　" + m.Key, _muted);
		GUILayout.Label(m.Title, _heading);
		if (m.Summary.Length > 0)
		{
			GUILayout.Label(m.Summary, _muted);
		}
		GUILayout.Space(S(6));
		GUILayout.Label(m.When + (m.Place.Length > 0 ? "　·　" + m.Place : ""), _label);
		GUILayout.Label((m.Visited ? "<color=#2F6B3A>✓ 这一局走过</color>" : "这一局没走过") + (m.Read != null ? $"　·　对白读过 {m.Read}%" : "") + (m.Endings > 0 ? $"　·　<color=#A3302A>{m.Endings} 个结局</color>" : ""), _label);
		if (StoryAtlas.CurrentMain == m.Key)
		{
			GUILayout.Label("<color=#A3302A><b>当前主线</b></color>", _label);
		}
		if (GUILayout.Button("看剧情图", _primary, GUILayout.Height(S(38))))
		{
			OpenFlow(m);
		}
		string conditions = StoryAtlas.ConditionText(m);
		if (conditions.Length > 0)
		{
			GUILayout.Space(S(10));
			GUILayout.Label("触发条件", _heading);
			GUILayout.Label(OnPaper(conditions), _rich);
		}
		DrawSections(m);
		DrawEntries(m);
		var before = StoryAtlas.Missions.Where(x => x.Main && x.Next.Contains(m.Key)).ToList();
		var after = m.Next.Select(StoryAtlas.Find).Where(x => x != null).ToList();
		if (before.Count + after.Count > 0)
		{
			GUILayout.Space(S(10));
			GUILayout.Label("前后", _heading);
			foreach (StoryMission x in before)
			{
				if (GUILayout.Button("← " + x.Title, _button))
				{
					_routePicked = x;
				}
			}
			foreach (StoryMission x in after)
			{
				if (GUILayout.Button("→ " + x.Title, _button))
				{
					_routePicked = x;
				}
			}
		}
		var kept = m.Scripts.Select(StoryNodes.Latest).Where(n => n != null).ToList();
		if (kept.Count > 0)
		{
			GUILayout.Space(S(10));
			GUILayout.Label("可以从这里重玩", _heading);
			foreach (StoryNode node in kept.Take(8))
			{
				ReplayButton(node, StoryScripts.SegmentName(node.Script) + "　" + $"第{node.Year}年{node.Month}月", _button);
			}
		}
	}

	private void DrawSideDetail(SideStory side)
	{
		GUILayout.Label("支线", _muted);
		GUILayout.Label(side.Title, _heading);
		foreach (StoryMission m in side.Joins)
		{
			if (GUILayout.Button("走完接到主线「" + m.Title + "」", _button))
			{
				_routePicked = m;
				_routeSide = null;
			}
		}
		GUILayout.Label((side.Visited ? "<color=#2F6B3A>✓ 这一局走过</color>" : "这一局没走过") + (side.Read != null ? $"　·　对白读过 {side.Read}%" : ""), _label);
		foreach (StoryMission m in side.Missions.OrderBy(m => m.From ?? int.MaxValue))
		{
			GUILayout.Space(S(8));
			GUILayout.BeginVertical(_card);
			GUILayout.Label(m.Title + (m.Visited ? "　<color=#2F6B3A>✓ 走过</color>" : ""), _label);
			GUILayout.Label(m.When + (m.Place.Length > 0 ? "　·　" + m.Place : "") + (m.Read != null ? $"　·　读过 {m.Read}%" : ""), _muted);
			string conditions = StoryAtlas.ConditionText(m);
			if (conditions.Length > 0)
			{
				GUILayout.Label(OnPaper(conditions), _small);
			}
			if (m.Entries.Count > 0)
			{
				GUILayout.Label(Cached((m, "entries"), () => string.Join("\n", m.Entries.Select(EntryText).ToArray())), _small);
			}
			if (GUILayout.Button("看剧情图", _button))
			{
				OpenFlow(m);
			}
			GUILayout.EndVertical();
		}
	}

	/// <summary>从别的任务接进来的一处：「从「某任务」：选… → 如果…」。</summary>
	private static string EntryText(StoryEntry e)
	{
		string from = (e.From.Main ? "主线" : "支线") + "「" + e.From.Title + "」";
		return OnPaper("从" + from + "接进来" + (e.Path.Count > 0 ? "：" + string.Join(" → ", e.Path.Select(StoryScripts.Describe).ToArray()) : ""));
	}

	private void DrawEntries(StoryMission m)
	{
		if (m.Entries.Count == 0)
		{
			return;
		}
		GUILayout.Space(S(10));
		GUILayout.Label("从别处接进来", _heading);
		GUILayout.Label(Cached((m, "entries"), () => string.Join("\n", m.Entries.Select(EntryText).ToArray())), _rich);
	}

	/// <summary>站内小节：这个任务的剧情段，每条路都会经过的是主干，其余是岔路（写出怎么进去）。</summary>
	private void DrawSections(StoryMission m)
	{
		FlowGraph graph = FlowOf(m);
		var boxes = graph.Boxes.Where(b => !b.Terminal).OrderBy(b => b.Layer).ThenBy(b => b.Row).ToList();
		if (boxes.Count == 0)
		{
			return;
		}
		HashSet<FlowBox> trunk = Trunk(graph);
		GUILayout.Space(S(10));
		GUILayout.Label("小节", _heading);
		foreach (FlowBox box in boxes.Take(40))
		{
			bool main = trunk.Contains(box);
			string entry = main ? "" : Cached((box, "entry"), () => string.Join("；", graph.Links.Where(l => l.To == box && l.Text.Length > 0).Select(l => l.Text.Replace("\n", " / ")).ToArray()));
			string head = (main ? "<b>主干</b>　" : "<color=#73644F>岔路</color>　") + Short(box.Title, 18) + (box.Read != null ? $"　<color=#73644F>读过 {box.Read}%</color>" : "");
			if (GUILayout.Button(head + (entry.Length > 0 ? "\n<size=" + Mathf.RoundToInt(14 * _scale) + "><color=#73644F>" + entry + "</color></size>" : ""),
				new GUIStyle(_button) { alignment = TextAnchor.MiddleLeft, wordWrap = true, richText = true }))
			{
				OpenFlow(m, box.Last != null ? box.Last.Name : null);
			}
		}
		if (boxes.Count > 40)
		{
			GUILayout.Label($"……共 {boxes.Count} 段", _muted);
		}
	}

	// ---------------- 旗标搜索 ----------------

	private const int FlagHitsShown = 5;
	private List<(string Name, string Note)> _allFlags;
	private string _flagSearchedFor;
	private List<(string Name, string Note, List<StoryUse> Uses)> _flagHits = new List<(string, string, List<StoryUse>)>();

	/// <summary>搜索词对得上旗标编号或说明时，列出这面旗标在哪些剧情里改动、判定（和数值页点旗标名字看到的一样），每处能跳到剧情图。</summary>
	private void DrawFlagSearch(string query)
	{
		if (_flagSearchedFor != query)
		{
			_flagSearchedFor = query;
			_allFlags ??= MissionManagerData.Instance?.FlagsCollection?.Where(c => c != null && c.List != null).SelectMany(c => c.List).Where(f => f != null)
				.Select(f => f.name).Distinct().OrderBy(n => n, StringComparer.Ordinal)
				.Select(n => (n, TextConverter.ToSimplified(MissionManagerData.Instance.GetFlag(n)?.DevNote ?? "").Replace("\n", " ").Trim())).ToList();
			_flagHits = new List<(string, string, List<StoryUse>)>();
			foreach (var (name, note) in _allFlags ?? new List<(string, string)>())
			{
				if (note.Contains("暂存") || !Matches(query, name, note))
				{
					continue;
				}
				List<StoryUse> uses = StoryUsage.Flag(name);
				if (uses == null)
				{
					// 反查还没整理好：下一帧再搜
					_flagSearchedFor = null;
					return;
				}
				if (uses.Count > 0)
				{
					_flagHits.Add((name, note, uses));
				}
			}
		}
		if (_flagHits.Count == 0)
		{
			return;
		}
		GUILayout.Label($"旗标里找到 {_flagHits.Count} 面" + (_flagHits.Count > FlagHitsShown ? $"（只列前 {FlagHitsShown} 面，搜得更具体些能缩小范围）" : ""), _heading);
		foreach (var (name, note, uses) in _flagHits.Take(FlagHitsShown))
		{
			GUILayout.Space(S(6));
			GUILayout.Label($"<b>{name}</b>" + (note.Length > 0 ? "　" + Short(note, 40) : ""), _label);
			foreach (StoryUse use in uses.OrderBy(u => !u.Changes).ThenBy(u => u.Mission == null).ThenBy(u => u.Mission?.From ?? int.MaxValue).Take(20))
			{
				DrawUse(use);
			}
			if (uses.Count > 20)
			{
				GUILayout.Label($"还有 {uses.Count - 20} 处，到数值页点这面旗标的名字看全部", _muted);
			}
		}
		GUILayout.Space(S(12));
	}

	// ---------------- 台词搜索 ----------------

	private string _searchedFor;
	private List<(string Script, string Snippet)> _searchHits = new List<(string, string)>();

	private void DrawSearchResults(string query)
	{
		DrawFlagSearch(query);
		if (!StoryAtlas.DialogueReady)
		{
			GUILayout.Label("正在整理台词……", _muted);
			return;
		}
		if (_searchedFor != query)
		{
			_searchedFor = query;
			_searchHits = StoryAtlas.SearchDialogue(query, 60);
		}
		GUILayout.Label(_searchHits.Count == 0 ? "台词里没找到" : $"台词里找到 {_searchHits.Count} 处" + (_searchHits.Count >= 60 ? "（只列前 60 处）" : ""), _heading);
		foreach (var (script, snippet) in _searchHits)
		{
			StoryAtlas.Owner.TryGetValue(script, out StoryMission m);
			string where = m != null ? (m.Main ? "主线" : m.SeriesTitle) + "「" + Short(m.Title, 14) + "」" : script;
			if (GUILayout.Button($"<size={Mathf.RoundToInt(14 * _scale)}><color=#73644F>{where}</color></size>\n{snippet}", new GUIStyle(_button) { alignment = TextAnchor.MiddleLeft, wordWrap = true, richText = true }) && m != null)
			{
				OpenFlow(m, script);
			}
		}
	}
}
