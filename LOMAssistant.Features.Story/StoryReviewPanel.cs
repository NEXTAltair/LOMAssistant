using System;
using System.Collections.Generic;
using System.Linq;
using LOMAssistant.Config;
using UnityEngine;

namespace LOMAssistant.Features.Story;

/// <summary>
/// 剧情回顾面板（默认 F3）：
/// 路线图——主线按剧本里写明的「下一个主线任务」连成线，岔开的路自动分到不同的车道，按游戏日历排开；支线挂在下面。
/// 剧情图——一个任务里的剧情段画成流程图：每段一个方块，选项、掷骰、条件连到下一段。
/// 收录节点、结局速查是列表。
/// </summary>
public partial class StoryReviewPanel : MonoBehaviour
{
	private const int WindowId = 584135;
	private static readonly string[] Tabs = { "路线图", "剧情图", "收录节点", "结局速查" };

	private bool _visible;
	private int _tab;
	private Rect _windowRect;
	private float _scale = -1f;
	private string _message = "";
	private float _messageUntil;
	private string _tooltip = "";

	// 配色：宣纸底、墨色字、朱砂点睛，和游戏的水墨画风搭
	private static readonly Color Paper = new Color(0.95f, 0.92f, 0.85f, 1f);
	private static readonly Color PaperDeep = new Color(0.9f, 0.86f, 0.77f, 1f);
	private static readonly Color PaperLight = new Color(0.98f, 0.96f, 0.91f, 1f);
	private static readonly Color Ink = new Color(0.17f, 0.14f, 0.11f, 1f);
	private static readonly Color InkMuted = new Color(0.45f, 0.39f, 0.33f, 1f);
	private static readonly Color InkLine = new Color(0.3f, 0.25f, 0.2f, 0.7f);
	private static readonly Color Edge = new Color(0.55f, 0.45f, 0.33f, 1f);
	private static readonly Color Cinnabar = new Color(0.7f, 0.17f, 0.13f, 1f);
	private static readonly Color Jade = new Color(0.22f, 0.45f, 0.36f, 1f);

	private GUIStyle _window, _label, _rich, _small, _muted, _heading, _tabOff, _tabOn, _button, _primary, _field, _boxTitle, _boxSmall, _tag, _center, _card, _seal;

	public static void Ensure(GameObject host)
	{
		if (host.GetComponent<StoryReviewPanel>() == null)
		{
			host.AddComponent<StoryReviewPanel>();
		}
	}

	private void Update()
	{
		StoryAtlas.Tick();
		if (GUIUtility.keyboardControl == 0 && ConfigManager.StoryReviewKey.Value != KeyCode.None && Input.GetKeyDown(ConfigManager.StoryReviewKey.Value))
		{
			_visible = !_visible;
			_confirming = null;
			if (_visible)
			{
				_reading.Clear();
				_texts.Clear();
				StoryAtlas.ForgetConditions();
			}
		}
	}

	private float S(float v) => v * _scale;

	private void BuildStyles(float scale)
	{
		_scale = scale;
		_textures.Clear();
		_framed.Clear();
		GUIStyle Plain(int size, Color color, bool wrap = true)
		{
			var s = new GUIStyle(GUI.skin.label) { fontSize = Mathf.Max(10, Mathf.RoundToInt(size * scale)), wordWrap = wrap, richText = true };
			s.normal.textColor = color;
			s.padding = new RectOffset(0, 0, 0, 0);
			return s;
		}
		// 描边按钮：底色加一圈细边，悬停时底色加深
		GUIStyle Outlined(Color fill, Color hover, Color edge, Color text, int size = 18)
		{
			var s = Plain(size, text, false);
			s.alignment = TextAnchor.MiddleCenter;
			s.normal.background = Framed(fill, edge);
			s.hover.background = s.active.background = Framed(hover, edge);
			s.border = new RectOffset(2, 2, 2, 2);
			s.hover.textColor = s.active.textColor = text;
			s.padding = new RectOffset((int)S(10), (int)S(10), (int)S(4), (int)S(4));
			return s;
		}
		_window = new GUIStyle(GUI.skin.window);
		_window.normal.background = _window.onNormal.background = Framed(Paper, Ink);
		_window.border = new RectOffset(2, 2, 2, 2);
		_window.padding = new RectOffset((int)S(16), (int)S(16), (int)S(12), (int)S(12));
		_label = Plain(19, Ink);
		_rich = Plain(18, Ink);
		_small = Plain(16, Ink);
		_muted = Plain(16, InkMuted);
		_heading = Plain(22, Ink);
		_heading.fontStyle = FontStyle.Bold;
		_center = Plain(16, InkMuted, false);
		_center.alignment = TextAnchor.MiddleCenter;
		_tabOff = Plain(20, InkMuted, false);
		_tabOff.alignment = TextAnchor.MiddleCenter;
		_tabOff.hover.textColor = Ink;
		_tabOn = new GUIStyle(_tabOff) { fontStyle = FontStyle.Bold };
		_tabOn.normal.textColor = _tabOn.hover.textColor = Ink;
		_button = Outlined(new Color(0, 0, 0, 0), PaperDeep, Edge, Ink, 17);
		_primary = Outlined(Cinnabar, new Color(0.8f, 0.24f, 0.18f, 1f), Cinnabar, PaperLight, 17);
		_field = new GUIStyle(GUI.skin.textField) { fontSize = Mathf.RoundToInt(18 * scale) };
		_field.normal.background = _field.hover.background = Framed(PaperLight, Edge);
		// 点中时边框变朱砂红，看得出正在输入
		_field.focused.background = Framed(new Color(1f, 0.99f, 0.96f, 1f), Cinnabar);
		_field.border = new RectOffset(2, 2, 2, 2);
		_field.normal.textColor = _field.focused.textColor = _field.hover.textColor = Ink;
		_field.padding = new RectOffset((int)S(8), (int)S(8), (int)S(4), (int)S(4));
		_boxTitle = Plain(16, Ink, false);
		_boxTitle.clipping = TextClipping.Clip;
		_boxSmall = Plain(13, InkMuted, false);
		_boxSmall.clipping = TextClipping.Clip;
		_tag = Plain(13, Ink, false);
		_tag.alignment = TextAnchor.MiddleCenter;
		_tag.normal.background = Texture(new Color(PaperLight.r, PaperLight.g, PaperLight.b, 0.92f));
		_tag.padding = new RectOffset((int)S(5), (int)S(5), (int)S(1), (int)S(1));
		_card = new GUIStyle(GUI.skin.box);
		_card.normal.background = Framed(PaperLight, new Color(Edge.r, Edge.g, Edge.b, 0.5f));
		_card.border = new RectOffset(2, 2, 2, 2);
		_card.padding = new RectOffset((int)S(10), (int)S(10), (int)S(8), (int)S(8));
		_card.margin = new RectOffset(0, 0, (int)S(4), (int)S(4));
		_seal = Plain(18, PaperLight, false);
		_seal.alignment = TextAnchor.MiddleCenter;
		_seal.fontStyle = FontStyle.Bold;
		_windowRect = new Rect(Screen.width * 0.04f, Screen.height * 0.05f, Screen.width * 0.92f, Screen.height * 0.9f);
	}

	private void OnGUI()
	{
		if (!_visible || UiHider.Active)
		{
			return;
		}
		float scale = Mathf.Min(Screen.width / 1920f, Screen.height / 1080f);
		if (scale <= 0f)
		{
			return;
		}
		if (!Mathf.Approximately(scale, _scale))
		{
			BuildStyles(scale);
		}
		_windowRect = GUI.Window(WindowId, _windowRect, DrawWindow, "", _window);
		InputGuard.Report("StoryReview", _windowRect);
	}

	private void Notice(string text)
	{
		_message = text;
		_messageUntil = Time.realtimeSinceStartup + 6f;
	}

	private void DrawWindow(int id)
	{
		_tooltip = "";
		float w = _windowRect.width, pad = S(18);
		// 确认框开着时，点击、滚轮、按键先给确认框，剩下的吞掉，下面的界面不响应
		EventType type = Event.current.type;
		bool input = type != EventType.Layout && type != EventType.Repaint;
		if (_confirming != null && input)
		{
			DrawConfirm();
			if (Event.current.type != EventType.Used && Event.current.type != EventType.Ignore)
			{
				Event.current.Use();
			}
		}
		// 题头：朱砂印「回」+ 标题
		var seal = new Rect(pad, S(12), S(34), S(34));
		Round(seal, Cinnabar, S(4));
		GUI.Label(seal, "回", _seal);
		GUI.Label(new Rect(seal.xMax + S(10), S(10), S(160), S(38)), "剧情回顾", _heading);
		float x = seal.xMax + S(150);
		for (int i = 0; i < Tabs.Length; i++)
		{
			var tab = new Rect(x, S(10), S(110), S(36));
			if (GUI.Button(tab, Tabs[i], i == _tab ? _tabOn : _tabOff))
			{
				_tab = i;
			}
			if (i == _tab)
			{
				Fill(new Rect(tab.x + S(22), tab.yMax, tab.width - S(44), S(3)), Cinnabar);
			}
			x += S(114);
		}
		var close = new Rect(w - pad - S(36), S(12), S(36), S(36));
		if (GUI.Button(close, "✕", _tabOff))
		{
			_visible = false;
		}
		if (Time.realtimeSinceStartup < _messageUntil)
		{
			GUI.Label(new Rect(x + S(16), S(14), close.x - x - S(30), S(30)), _message, _label);
		}
		// 题头下一道墨线
		Fill(new Rect(pad, S(56), w - pad * 2, S(2)), InkLine);
		var body = new Rect(pad, S(68), w - pad * 2, _windowRect.height - S(68) - pad);
		switch (_tab)
		{
			case 0: DrawRoute(body); break;
			case 1: DrawFlow(body); break;
			case 2: DrawNodes(body); break;
			default: DrawEndings(body); break;
		}
		if (_confirming != null && !input)
		{
			DrawConfirm();
		}
		else if (_tooltip.Length > 0)
		{
			DrawTooltip();
		}
		GUI.DragWindow(new Rect(0, 0, w, S(56)));
	}

	private void Hover(Rect r, string text)
	{
		if (!string.IsNullOrEmpty(text) && r.Contains(Event.current.mousePosition))
		{
			_tooltip = text;
		}
	}

	private void DrawTooltip()
	{
		var content = new GUIContent(_tooltip);
		float width = Mathf.Min(S(460), _small.CalcSize(content).x + S(20));
		float height = _small.CalcHeight(content, width - S(20)) + S(14);
		Vector2 p = Event.current.mousePosition;
		var r = new Rect(Mathf.Min(p.x + S(18), _windowRect.width - width - S(8)), Mathf.Min(p.y + S(20), _windowRect.height - height - S(8)), width, height);
		Fill(r, PaperLight);
		Frame(r, Edge, 1);
		GUI.Label(new Rect(r.x + S(10), r.y + S(7), r.width - S(20), r.height - S(14)), content, _small);
	}

	private StoryNode _confirming;

	/// <summary>重玩按钮：点了先弹确认框，确认了才读档。</summary>
	private void ReplayButton(StoryNode node, string text, GUIStyle style, params GUILayoutOption[] options)
	{
		if (GUILayout.Button(text, style, options))
		{
			_confirming = node;
		}
	}

	/// <summary>读档确认框：盖在面板中间，底下压一层淡墨。</summary>
	private void DrawConfirm()
	{
		StoryNode node = _confirming;
		if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
		{
			_confirming = null;
			Event.current.Use();
			return;
		}
		if (Event.current.type == EventType.Repaint)
		{
			Fill(new Rect(0, 0, _windowRect.width, _windowRect.height), new Color(0.1f, 0.08f, 0.05f, 0.35f));
		}
		float width = S(460), height = S(210);
		var box = new Rect((_windowRect.width - width) / 2, (_windowRect.height - height) / 2, width, height);
		if (Event.current.type == EventType.Repaint)
		{
			Fill(box, PaperLight);
			Frame(box, Cinnabar, 2);
		}
		GUI.Label(new Rect(box.x + S(24), box.y + S(18), width - S(48), S(36)), "确认读档", _heading);
		string where = StoryScripts.SegmentName(node.Script) + "　" + $"第{node.Year}年{node.Month}月";
		GUI.Label(new Rect(box.x + S(24), box.y + S(60), width - S(48), S(70)), "读取「" + where + "」？\n当前没有存档的进度会丢失。", _label);
		float bw = S(150), bh = S(40), by = box.yMax - bh - S(20);
		if (GUI.Button(new Rect(box.xMax - S(24) - bw * 2 - S(14), by, bw, bh), "取消", _button))
		{
			_confirming = null;
		}
		if (GUI.Button(new Rect(box.xMax - S(24) - bw, by, bw, bh), "读档", _primary))
		{
			_confirming = null;
			Replay(node);
			GUIUtility.ExitGUI();
		}
	}

	/// <summary>从节点重玩：成功就关掉面板。</summary>
	private void Replay(StoryNode node)
	{
		string error = StoryNodes.Replay(node);
		if (error.Length > 0)
		{
			Notice(error);
			return;
		}
		_visible = false;
	}

	/// <summary>搜索框；返回去掉首尾空格的内容。</summary>
	private string SearchBox(Rect r, ref string value)
	{
		string name = "storyreview.search." + (int)r.x + "." + (int)r.y;
		GUI.SetNextControlName(name);
		Color cursor = GUI.skin.settings.cursorColor;
		GUI.skin.settings.cursorColor = Ink;
		value = GUI.TextField(new Rect(r.x, r.y, r.width - S(44), r.height), value, _field);
		GUI.skin.settings.cursorColor = cursor;
		bool focused = GUI.GetNameOfFocusedControl() == name;
		if (string.IsNullOrEmpty(value) && !focused)
		{
			GUI.Label(new Rect(r.x + S(10), r.y, S(80), r.height), "搜索", _center);
		}
		else if (GUI.Button(new Rect(r.xMax - S(40), r.y, S(40), r.height), "×", _button))
		{
			value = "";
			GUI.FocusControl(null);
		}
		return value.Trim();
	}

	private static bool Matches(string query, params string[] texts)
	{
		return query.Length == 0 || texts.Any(t => t != null && RichText.Strip(t).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0);
	}

	// ---------------- 绘图小工具 ----------------

	private readonly Dictionary<Color, Texture2D> _textures = new Dictionary<Color, Texture2D>();

	private Texture2D Texture(Color color)
	{
		if (!_textures.TryGetValue(color, out Texture2D texture) || texture == null)
		{
			texture = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
			texture.SetPixel(0, 0, color);
			texture.Apply();
			_textures[color] = texture;
		}
		return texture;
	}

	private readonly Dictionary<(Color, Color), Texture2D> _framed = new Dictionary<(Color, Color), Texture2D>();

	/// <summary>带一圈细边的底图（配合 GUIStyle.border = 2 九宫格拉伸）。</summary>
	private Texture2D Framed(Color fill, Color edge)
	{
		if (!_framed.TryGetValue((fill, edge), out Texture2D texture) || texture == null)
		{
			texture = new Texture2D(5, 5) { hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Point };
			for (int x = 0; x < 5; x++)
			{
				for (int y = 0; y < 5; y++)
				{
					texture.SetPixel(x, y, x == 0 || y == 0 || x == 4 || y == 4 ? edge : fill);
				}
			}
			texture.Apply();
			_framed[(fill, edge)] = texture;
		}
		return texture;
	}

	private void Fill(Rect r, Color color) => GUI.DrawTexture(r, Texture(color));

	private void Frame(Rect r, Color color, float w)
	{
		Fill(new Rect(r.x, r.y, r.width, w), color);
		Fill(new Rect(r.x, r.yMax - w, r.width, w), color);
		Fill(new Rect(r.x, r.y, w, r.height), color);
		Fill(new Rect(r.xMax - w, r.y, w, r.height), color);
	}

	private void Round(Rect r, Color color, float radius, float border = 0)
	{
		GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, color, border, radius);
	}

	/// <summary>横平竖直的线；dashed 为虚线。</summary>
	private void Segment(Vector2 a, Vector2 b, Color color, float w, bool dashed)
	{
		var r = new Rect(Mathf.Min(a.x, b.x) - w / 2, Mathf.Min(a.y, b.y) - w / 2, Mathf.Abs(b.x - a.x) + w, Mathf.Abs(b.y - a.y) + w);
		if (!dashed)
		{
			Fill(r, color);
			return;
		}
		bool across = r.width >= r.height;
		float length = across ? r.width : r.height, dash = S(7), gap = S(5);
		for (float t = 0; t < length; t += dash + gap)
		{
			Fill(across ? new Rect(r.x + t, r.y, Mathf.Min(dash, length - t), r.height) : new Rect(r.x, r.y + t, r.width, Mathf.Min(dash, length - t)), color);
		}
	}

	/// <summary>从 a 到 b 的折线：先横、再竖、再横。</summary>
	private void Elbow(Vector2 a, Vector2 b, Color color, float w, bool dashed, float bendX)
	{
		if (Mathf.Abs(a.y - b.y) < 0.5f)
		{
			Segment(a, b, color, w, dashed);
			return;
		}
		Segment(a, new Vector2(bendX, a.y), color, w, dashed);
		Segment(new Vector2(bendX, a.y), new Vector2(bendX, b.y), color, w, dashed);
		Segment(new Vector2(bendX, b.y), b, color, w, dashed);
	}

	/// <summary>读过多少：一条细进度条，墨色填到已读的比例，读完变青；没有对白不画。</summary>
	private void ReadBar(Rect r, int? read)
	{
		if (read == null)
		{
			return;
		}
		Fill(r, new Color(Edge.r, Edge.g, Edge.b, 0.25f));
		Fill(new Rect(r.x, r.y, r.width * read.Value / 100f, r.height), read.Value >= 100 ? Jade : Ink);
	}

	/// <summary>条件文字里的颜色是给深色底配的，换到宣纸底上调深一些。</summary>
	private static string OnPaper(string text)
	{
		return (text ?? "").Replace("#A3B86C", "#2F6B3A").Replace("#CD594A", "#A3302A").Replace("#BCA136", "#7A5A10");
	}

	private static string Short(string text, int length)
	{
		text = RichText.Strip(text ?? "").Replace("\n", " ");
		return text.Length <= length ? text : text.Substring(0, length) + "…";
	}

	// ---------------- 按住拖动滚动 ----------------

	// 游戏的滚动列表只认滚轮和滚动条；这里让按住拖动也能滚。轻点照常是点按钮，拖出一小段才算拖动
	private string _dragId;
	private Vector2 _dragFrom;
	private bool _dragging;

	private void DragScroll(string id, Rect area, ref Vector2 scroll)
	{
		Event e = Event.current;
		// 最右边一条是滚动条，留给它自己拖
		if (e.type == EventType.MouseDown && e.button == 0 && new Rect(area.x, area.y, area.width - S(24), area.height).Contains(e.mousePosition))
		{
			_dragId = id;
			_dragFrom = e.mousePosition;
			_dragging = false;
		}
		else if (e.type == EventType.MouseDrag && _dragId == id)
		{
			if (!_dragging && (e.mousePosition - _dragFrom).magnitude > S(8))
			{
				_dragging = true;
				GUIUtility.hotControl = 0;
			}
			if (_dragging)
			{
				scroll.y = Mathf.Max(0, scroll.y - e.delta.y);
				e.Use();
			}
		}
		else if (e.rawType == EventType.MouseUp && _dragId == id)
		{
			_dragId = null;
			if (_dragging)
			{
				_dragging = false;
				e.Use();
			}
		}
	}

	// ---------------- 文字缓存 ----------------

	// 条件文字要查表、读当前数值，每帧重算会卡；算一次存着，打开面板或数据变了再重算
	private readonly Dictionary<object, string> _texts = new Dictionary<object, string>();
	private int _textsAtlas = -1, _textsNodes = -1;

	private string Cached(object key, Func<string> make)
	{
		if (_textsAtlas != StoryAtlas.Version || _textsNodes != StoryNodes.Version)
		{
			_texts.Clear();
			_textsAtlas = StoryAtlas.Version;
			_textsNodes = StoryNodes.Version;
		}
		if (!_texts.TryGetValue(key, out string text))
		{
			_texts[key] = text = make();
		}
		return text;
	}

	// ---------------- 已读标记 ----------------

	private readonly Dictionary<string, string> _reading = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

	/// <summary>一个去处读过没有：看接下去那个剧本的对白（全部周目）。</summary>
	private string ReadMark(StoryExit exit)
	{
		if (exit.Target.Length == 0)
		{
			return "";
		}
		if (_reading.TryGetValue(exit.Target, out string mark))
		{
			return mark;
		}
		StoryScript next = StoryScripts.Get(exit.Target);
		int? read = next != null ? StoryScripts.ReadPercent(next.Lines) : null;
		mark = read == null || read == 100 ? "" : read == 0 ? "〔未读〕" : "〔读过 " + read + "%〕";
		_reading[exit.Target] = mark;
		return mark;
	}

	/// <summary>路线图、剧情图、结局都要先把全部剧本读一遍。</summary>
	private bool EnsureAtlas(Rect r)
	{
		if (StoryAtlas.Ready)
		{
			return true;
		}
		if (!StoryAtlas.Building && StoryAtlas.Status.Length == 0)
		{
			StoryAtlas.Start();
		}
		GUI.Label(new Rect(r.x, r.y + S(20), r.width, S(30)), StoryAtlas.Status, _label);
		if (!StoryAtlas.Building && GUI.Button(new Rect(r.x, r.y + S(60), S(140), S(36)), "重新生成", _button))
		{
			StoryAtlas.Start();
		}
		return false;
	}

	// ---------------- 收录节点 ----------------

	private Vector2 _nodesScroll;
	private string _nodesSearch = "";
	private bool _nodesAll;

	private void OnApplicationQuit() => StoryNodes.Flush();

	private void DrawNodes(Rect r)
	{
		string query = SearchBox(new Rect(r.x, r.y, Mathf.Min(S(520), r.width), S(36)), ref _nodesSearch);
		if (StoryNodes.HasCurrentSave)
		{
			var all = new Rect(r.x + Mathf.Min(S(520), r.width) + S(16), r.y, S(200), S(36));
			if (GUI.Button(all, _nodesAll ? "全部存档的节点" : "当前存档的节点", _button))
			{
				_nodesAll = !_nodesAll;
			}
		}
		var area = new Rect(r.x, r.y + S(46), r.width, r.height - S(46));
		DragScroll("nodes", area, ref _nodesScroll);
		GUILayout.BeginArea(area);
		_nodesScroll = GUILayout.BeginScrollView(_nodesScroll);
		var visible = new HashSet<StoryNode>(StoryNodes.Visible);
		var nodes = (_nodesAll ? StoryNodes.All : visible).OrderByDescending(n => n.Year).ThenByDescending(n => n.Month).ThenByDescending(n => n.Stage).ThenByDescending(n => n.Saved).ToList();
		if (nodes.Count == 0)
		{
			GUILayout.Label("还没有收录", _muted);
		}
		string month = "";
		foreach (StoryNode node in nodes)
		{
			StoryScript script = StoryScripts.Get(node.Script);
			string options = script == null ? "" : string.Join("　", script.Menus.SelectMany(menu => menu).Select(step =>
				"「" + StoryScripts.OptionText(step.Key) + "」" + string.Concat(script.Exits.Where(e => e.Path.Count > 0 && e.Path[0].Key == step.Key).Select(ReadMark).Distinct().ToArray())).ToArray());
			if (!Matches(query, node.Script, node.Mission, options))
			{
				continue;
			}
			string head = $"第{node.Year}年{node.Month}月";
			if (head != month)
			{
				month = head;
				GUILayout.Space(S(8));
				GUILayout.Label(head, _heading);
			}
			GUILayout.BeginHorizontal(_card);
			GUILayout.BeginVertical();
			GUILayout.Label($"{StoryAtlas.StageText(node.Stage)}　{(node.Mission.Length > 0 ? node.Mission : node.Script)}" + (_nodesAll && !visible.Contains(node) ? "　<color=#73644F>（其他存档）</color>" : ""), _label);
			GUILayout.Label(options.Length > 0 ? options : node.Script, _muted);
			GUILayout.EndVertical();
			ReplayButton(node, "从这里重玩", _primary, GUILayout.Width(S(170)), GUILayout.Height(S(40)));
			if (GUILayout.Button("删除", _button, GUILayout.Width(S(70)), GUILayout.Height(S(40))))
			{
				StoryNodes.Delete(node);
				GUIUtility.ExitGUI();
			}
			GUILayout.EndHorizontal();
		}
		GUILayout.EndScrollView();
		GUILayout.EndArea();
	}

	// ---------------- 结局速查 ----------------

	/// <summary>一个结局的「怎么走到」，每处一行，最多四处。</summary>
	private static string EndingWays(StoryEnding ending)
	{
		if (ending.Ways.Count == 0)
		{
			return "<color=#73644F>在剧本和决斗数据里都没找到触发的地方（可能由团战、小游戏等直接触发）</color>";
		}
		var lines = new List<string>();
		foreach (StoryEndingWay way in ending.Ways.Take(4))
		{
			string head = way.Mission != null ? $"在{(way.Mission.Main ? "主线" : "支线")}「{way.Mission.Title}」（{way.Mission.When}）" : "在剧本 " + way.Script;
			var parts = way.Path.Select(StoryScripts.Describe).ToList();
			if (way.LostDuel)
			{
				parts.Add("决斗输了");
			}
			string steps = parts.Count > 0 ? "：" + string.Join(" → ", parts.ToArray()) : "：走到这一段就会触发";
			lines.Add(OnPaper(head + steps));
		}
		if (ending.Ways.Count > 4)
		{
			lines.Add($"<color=#73644F>还有 {ending.Ways.Count - 4} 处</color>");
		}
		return string.Join("\n", lines.ToArray());
	}

	private Vector2 _endingsScroll;
	private string _endingsSearch = "";

	private void DrawEndings(Rect r)
	{
		if (!EnsureAtlas(r))
		{
			return;
		}
		string query = SearchBox(new Rect(r.x, r.y, Mathf.Min(S(520), r.width), S(36)), ref _endingsSearch);
		var groups = new[] { (Death: false, Title: "结局（汗青书）"), (Death: true, Title: "死亡（生死簿）") };
		float x = r.x + Mathf.Min(S(520), r.width) + S(16);
		foreach (var g in groups)
		{
			var list = StoryAtlas.Endings.Where(e => e.Death == g.Death).ToList();
			GUI.Label(new Rect(x, r.y + S(6), S(260), S(30)), $"{g.Title} {list.Count(e => e.Achieved)}/{list.Count}", _label);
			x += S(270);
		}
		var endingsArea = new Rect(r.x, r.y + S(46), r.width, r.height - S(46));
		DragScroll("endings", endingsArea, ref _endingsScroll);
		GUILayout.BeginArea(endingsArea);
		_endingsScroll = GUILayout.BeginScrollView(_endingsScroll);
		foreach (var g in groups)
		{
			GUILayout.Space(S(8));
			GUILayout.Label(g.Title, _heading);
			foreach (StoryEnding ending in StoryAtlas.Endings.Where(e => e.Death == g.Death)
				.OrderBy(e => e.Ways.Select(w => w.Mission?.From ?? int.MaxValue).DefaultIfEmpty(int.MaxValue).Min()).ThenBy(e => e.Id, StringComparer.Ordinal))
			{
				string where = string.Join("、", ending.Ways.Select(w => w.Mission).Where(m => m != null).Select(m => (m.Main ? "主线" : m.SeriesTitle) + "「" + m.Title + "」").Distinct().ToArray());
				if (!Matches(query, ending.Title, ending.Desc, ending.Id, where))
				{
					continue;
				}
				GUILayout.BeginVertical(_card);
				GUILayout.BeginHorizontal();
				GUILayout.Label(ending.Achieved ? "<color=#2F6B3A><b>✓ 已达成</b></color>" : "<color=#A3302A>未达成</color>", _small, GUILayout.Width(S(80)));
				GUILayout.Label("<b>" + ending.Title + "</b>", _label);
				GUILayout.EndHorizontal();
				GUILayout.Label(Cached(ending, () => EndingWays(ending)), _rich);
				GUILayout.EndVertical();
			}
		}
		GUILayout.EndScrollView();
		GUILayout.EndArea();
	}
}
