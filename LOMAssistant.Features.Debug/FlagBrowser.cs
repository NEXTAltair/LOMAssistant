using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Mortal.Core;
using UnityEngine;
using UnityEngine.UI;

namespace LOMAssistant.Features.Debug;

/// <summary>
/// F9 面板「旗幟」列的搜索和翻页。
/// 那两列一共 51 行（TestFlagStatPanel），是场景里摆好的，每行绑一面 FlagData；游戏里旗标有一千多面，绝大多数看不到。
/// 这里不新增行，而是把这 51 行当成窗口：搜索到的旗标分页填进去（改绑每行的 _stat 再刷新），行上的 +/- 改的仍是真实旗标。
/// 搜索框为空时恢复原版那 51 行。
/// </summary>
public class FlagBrowser : MonoBehaviour
{
	private const int WindowId = 0x464C4147; // "FLAG"
	// 行标题只有约 7 个英文字宽，超出就换行叠到下一行；截成 6 个字加「…」正好一行
	private const int MaxTitleChars = 7;

	private static FlagBrowser _instance;

	private GameObject _testPanel;
	private TestFlagStatPanel[] _rows;
	private FlagData[] _original;
	private string _query = "";
	private string _applied;
	private List<FlagData> _results = new List<FlagData>();
	private int _page;
	private bool _hotKeyPaused;
	private IMECompositionMode _prevIme;

	private readonly Dictionary<FlagData, string> _notes = new Dictionary<FlagData, string>();

	private float _scale = -1f;
	private GUIStyle _label;
	private GUIStyle _button;
	private GUIStyle _field;
	private GUIStyle _box;
	private GUIStyle _title;
	private GUIStyle _center;
	private Texture2D _buttonTex;
	private Texture2D _buttonHoverTex;
	private Texture2D _fieldTex;
	private Font _font;
	private Texture2D _background;

	/// <summary>F9 面板打开后调用，记下面板和原版的 51 行。</summary>
	public static void Attach(GameObject testPanel)
	{
		if (_instance == null)
		{
			_instance = LOMAssistantPlugin.Instance.gameObject.AddComponent<FlagBrowser>();
		}
		_instance.Bind(testPanel);
		_instance.StartCoroutine(_instance.SimplifyTexts(testPanel));
	}

	private void Bind(GameObject testPanel)
	{
		if (_testPanel == testPanel && _rows != null)
		{
			return;
		}
		_testPanel = testPanel;
		_rows = testPanel.GetComponentsInChildren<TestFlagStatPanel>(includeInactive: true);
		_plainColors.Clear();
		_tipHome.Clear();
		_original = _rows.Select(r => Traverse.Create(r).Field("_stat").GetValue<FlagData>()).ToArray();
		_query = "";
		_applied = null;
		_results.Clear();
		_page = 0;
		_font = _rows.Select(r => r.GetComponentInChildren<Text>(true)).FirstOrDefault(x => x != null && x.font != null)?.font;
		_scale = -1f;
		// 旗标说明的小框不接鼠标：原版靠鼠标进出文字来开关它，小框弹出后会盖住旁边几行，
		// 鼠标移到下一行时还停在上一行的小框上，看到的说明就和要加减的那行对不上
		_tips = _rows.Select(r => Traverse.Create(r).Field("_tip").GetValue<GameObject>()).ToArray();
		foreach (GameObject tip in _tips.Where(t => t != null))
		{
			foreach (Graphic graphic in tip.GetComponentsInChildren<Graphic>(includeInactive: true))
			{
				graphic.raycastTarget = false;
			}
		}
		FileLogger.Log($"旗标搜索：F9 旗标列共 {_rows.Length} 行");
	}

	private static readonly Color ButtonFill = new Color(0.20f, 0.18f, 0.15f, 1f);
	private static readonly Color ButtonHover = new Color(0.32f, 0.28f, 0.22f, 1f);
	private static readonly Color TextLight = new Color(0.93f, 0.89f, 0.80f, 1f);

	// F9 面板是游戏开发用的：文字都是繁体，按钮是白底。每次打开时（等各行 Awake/Start 填好文字后）
	// 把文字转成简体，按钮改成深底浅字，和黑色面板一致。
	private System.Collections.IEnumerator SimplifyTexts(GameObject panel)
	{
		yield return null;
		yield return null;
		if (panel == null)
		{
			yield break;
		}
		int changed = 0;
		foreach (Text text in panel.GetComponentsInChildren<Text>(includeInactive: true))
		{
			string s = text.text;
			if (string.IsNullOrEmpty(s))
			{
				continue;
			}
			string simplified = TextConverter.ToSimplified(s);
			if (simplified != s)
			{
				text.text = simplified;
				changed++;
			}
		}
		if (changed > 0)
		{
			FileLogger.Log($"F9 面板：{changed} 处文字转为简体");
		}
		foreach (Button button in panel.GetComponentsInChildren<Button>(includeInactive: true))
		{
			if (button.targetGraphic is Image image)
			{
				image.color = ButtonFill;
			}
			ColorBlock colors = button.colors;
			colors.normalColor = Color.white;
			colors.highlightedColor = new Color(1.5f, 1.5f, 1.5f, 1f);
			colors.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
			colors.selectedColor = Color.white;
			button.colors = colors;
			foreach (Text label in button.GetComponentsInChildren<Text>(includeInactive: true))
			{
				label.color = TextLight;
			}
		}
	}

	// F9 面板在屏幕上的位置（IMGUI 坐标，原点在左上）
	private Rect PanelScreenRect()
	{
		var rt = _testPanel.transform as RectTransform;
		if (rt == null)
		{
			return new Rect(0f, 0f, Screen.width, Screen.height);
		}
		Canvas canvas = _testPanel.GetComponentInParent<Canvas>();
		Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
		var corners = new Vector3[4];
		rt.GetWorldCorners(corners);
		Vector2 min = RectTransformUtility.WorldToScreenPoint(cam, corners[0]);
		Vector2 max = RectTransformUtility.WorldToScreenPoint(cam, corners[2]);
		return new Rect(min.x, Screen.height - max.y, max.x - min.x, max.y - min.y);
	}

	private GameObject[] _tips = new GameObject[0];

	/// <summary>
	/// 旗标说明按鼠标所在的整行来显示：鼠标在哪一行（名字、数值、加减按钮都算），就只显示那一行的说明。
	/// 放在 LateUpdate，盖过原版鼠标进出事件的开关。
	/// </summary>
	private void LateUpdate()
	{
		if (!PanelVisible || _tips.Length != _rows.Length)
		{
			return;
		}
		Canvas canvas = _testPanel.GetComponentInParent<Canvas>();
		Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
		Vector2 mouse = Input.mousePosition;
		// 行本身的范围比看到的宽（左列的行会伸到右列底下），所以按每行看得见的名字、数值和加减按钮来算范围
		// 两列的范围仍可能重叠，几行都盖住鼠标时取左边缘离鼠标最近的那行（就是鼠标所在那一列）
		int hovered = -1;
		float bestLeft = float.MinValue;
		for (int i = 0; i < _rows.Length; i++)
		{
			if (!_rows[i].gameObject.activeInHierarchy)
			{
				continue;
			}
			Rect? bounds = null;
			foreach (Graphic graphic in _rows[i].GetComponentsInChildren<Graphic>())
			{
				if (_tips[i] != null && graphic.transform.IsChildOf(_tips[i].transform))
				{
					continue;
				}
				Rect r = ScreenRect(graphic.rectTransform, cam);
				bounds = bounds == null ? r : Rect.MinMaxRect(Mathf.Min(bounds.Value.xMin, r.xMin), Mathf.Min(bounds.Value.yMin, r.yMin), Mathf.Max(bounds.Value.xMax, r.xMax), Mathf.Max(bounds.Value.yMax, r.yMax));
			}
			if (bounds != null && bounds.Value.Contains(mouse) && bounds.Value.xMin > bestLeft)
			{
				hovered = i;
				bestLeft = bounds.Value.xMin;
			}
		}
		for (int i = 0; i < _tips.Length; i++)
		{
			bool on = i == hovered;
			if (_tips[i] != null && _tips[i].activeSelf != on)
			{
				_tips[i].SetActive(on);
			}
			Highlight(i, on);
		}
		if (hovered >= 0 && _tips[hovered] != null)
		{
			KeepOnScreen(_tips[hovered].transform as RectTransform, cam);
		}
	}

	// 说明框原本的位置；说明很长时框会伸出屏幕，挪回屏幕里
	private readonly Dictionary<RectTransform, Vector3> _tipHome = new Dictionary<RectTransform, Vector3>();

	private void KeepOnScreen(RectTransform tip, Camera cam)
	{
		if (tip == null)
		{
			return;
		}
		if (!_tipHome.TryGetValue(tip, out Vector3 home))
		{
			_tipHome[tip] = home = tip.localPosition;
		}
		tip.localPosition = home;
		Canvas.ForceUpdateCanvases();
		Rect r = ScreenRect(tip, cam);
		float dx = r.xMin < 0 ? -r.xMin : r.xMax > Screen.width ? Screen.width - r.xMax : 0f;
		float dy = r.yMax > Screen.height ? Screen.height - r.yMax : r.yMin < 0 ? -r.yMin : 0f;
		if (dx == 0f && dy == 0f || !(tip.parent is RectTransform parent))
		{
			return;
		}
		// 屏幕上的位移换算成父节点里的位移
		if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, r.center, cam, out Vector2 from)
			&& RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, r.center + new Vector2(dx, dy), cam, out Vector2 to))
		{
			tip.localPosition = home + (Vector3)(to - from);
		}
	}

	private static Rect ScreenRect(RectTransform rt, Camera cam)
	{
		var corners = new Vector3[4];
		rt.GetWorldCorners(corners);
		Vector2 a = RectTransformUtility.WorldToScreenPoint(cam, corners[0]);
		Vector2 b = RectTransformUtility.WorldToScreenPoint(cam, corners[2]);
		return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
	}

	// 鼠标所在的那行，名字和数值改成金色，说明框指的是哪行一眼就能看出来
	private static readonly Color HoverColor = new Color(0.95f, 0.78f, 0.42f, 1f);
	private readonly Dictionary<Text, Color> _plainColors = new Dictionary<Text, Color>();

	private void Highlight(int row, bool on)
	{
		var tr = Traverse.Create(_rows[row]);
		foreach (Text text in new[] { tr.Field("_nameText").GetValue<Text>(), tr.Field("_valueText").GetValue<Text>() })
		{
			if (text == null)
			{
				continue;
			}
			if (!_plainColors.TryGetValue(text, out Color plain))
			{
				_plainColors[text] = plain = text.color;
			}
			Color want = on ? HoverColor : plain;
			if (text.color != want)
			{
				text.color = want;
			}
		}
	}

	private bool PanelVisible => _testPanel != null && _testPanel.activeInHierarchy && _rows != null && _rows.Length > 0;

	private int PageCount => Math.Max(1, (_results.Count + _rows.Length - 1) / _rows.Length);

	private void OnGUI()
	{
		// 隐藏界面看 CG 时本 Mod 的窗口也不画
		if (UiHider.Active)
		{
			return;
		}
		if (!PanelVisible)
		{
			ResumeHotKeys();
			return;
		}
		float scale = Mathf.Min(Screen.width / 1920f, Screen.height / 1080f);
		if (!Mathf.Approximately(scale, _scale))
		{
			BuildStyles(scale);
		}
		// 放在面板右下角的空白处，竖着排；放右上会挡住第二列旗标的悬停提示
		Rect panel = PanelScreenRect();
		float w = 300f * _scale;
		float h = (_applied != null ? 320f : 210f) * _scale;
		float pad = 24f * _scale;
		Rect rect = new Rect(panel.xMax - w - pad, panel.yMax - h - pad, w, h);
		InputGuard.Report("F9Search", rect);
		GUILayout.BeginArea(rect, _box);
		GUILayout.Label("旗标搜索", _title);
		GUI.SetNextControlName("LOMAssistant_FlagSearch");
		_query = GUILayout.TextField(_query, 40, _field);
		bool focused = GUI.GetNameOfFocusedControl() == "LOMAssistant_FlagSearch";
		Event e = Event.current;
		bool enter = focused && e.type == EventType.KeyDown && (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
			&& string.IsNullOrEmpty(Input.compositionString);
		GUILayout.Space(6f * _scale);
		GUILayout.BeginHorizontal();
		if (GUILayout.Button("搜索", _button) || enter)
		{
			Search(_query);
		}
		if (GUILayout.Button("全部", _button))
		{
			_query = "*";
			Search(_query);
		}
		if (GUILayout.Button("原版", _button))
		{
			_query = "";
			Search(_query);
		}
		GUILayout.EndHorizontal();
		if (_applied != null)
		{
			GUILayout.Space(10f * _scale);
			GUILayout.BeginHorizontal();
			if (GUILayout.Button("<<", _button, GUILayout.Width(60f * _scale)))
			{
				ShowPage(_page - 1);
			}
			GUILayout.Label($"{_page + 1} / {PageCount}", _center);
			if (GUILayout.Button(">>", _button, GUILayout.Width(60f * _scale)))
			{
				ShowPage(_page + 1);
			}
			GUILayout.EndHorizontal();
			GUILayout.Label($"共 {_results.Count} 面", _center);
		}
		GUILayout.EndArea();

		// 打字时暂停游戏自己的快捷键，免得输入的字母触发地图操作
		if (e.type == EventType.Repaint)
		{
			if (focused)
			{
				PauseHotKeys();
			}
			else
			{
				ResumeHotKeys();
			}
		}
	}

	private void Search(string query)
	{
		query = (query ?? "").Trim();
		if (query.Length == 0)
		{
			RestoreOriginal();
			return;
		}
		string[] terms = query == "*" ? new string[0] : query.Split(new[] { ' ', ',', '，', '、' }, StringSplitOptions.RemoveEmptyEntries);
		_results = AllFlags()
			.Where(f => terms.Length == 0 || terms.Any(t => f.name.IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0 || Note(f).IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0))
			.OrderBy(f => f.name, StringComparer.Ordinal)
			.ToList();
		_applied = query;
		FileLogger.Log($"旗标搜索：「{query}」找到 {_results.Count} 面");
		ShowPage(0);
	}

	private static IEnumerable<FlagData> AllFlags()
	{
		MissionManagerData missions = MissionManagerData.Instance;
		if (missions == null || missions.FlagsCollection == null)
		{
			return Enumerable.Empty<FlagData>();
		}
		return missions.FlagsCollection.Where(c => c != null && c.List != null).SelectMany(c => c.List).Where(f => f != null).Distinct();
	}

	private void ShowPage(int page)
	{
		if (_applied == null)
		{
			return;
		}
		_page = Mathf.Clamp(page, 0, PageCount - 1);
		for (int i = 0; i < _rows.Length; i++)
		{
			int index = _page * _rows.Length + i;
			FlagData flag = index < _results.Count ? _results[index] : null;
			_rows[i].gameObject.SetActive(flag != null);
			if (flag != null)
			{
				Rebind(_rows[i], flag);
			}
		}
	}

	private void RestoreOriginal()
	{
		_applied = null;
		_results.Clear();
		_page = 0;
		for (int i = 0; i < _rows.Length; i++)
		{
			_rows[i].gameObject.SetActive(true);
			if (_original[i] != null)
			{
				Rebind(_rows[i], _original[i]);
			}
		}
	}

	private void Rebind(TestFlagStatPanel row, FlagData flag)
	{
		var tr = Traverse.Create(row);
		tr.Field("_stat").SetValue(flag);
		Text name = tr.Field("_nameText").GetValue<Text>();
		if (name != null)
		{
			name.text = flag.name.Length > MaxTitleChars ? flag.name.Substring(0, MaxTitleChars - 1) + "…" : flag.name;
			name.horizontalOverflow = HorizontalWrapMode.Overflow; // 宽一点的字也不换行
		}
		Text tip = tr.Field("_textTip").GetValue<Text>();
		if (tip != null)
		{
			string note = Note(flag);
			tip.text = note.Length > 0 ? flag.name + "\n" + note : flag.name;
		}
		row.UpdateValue();
	}

	// 开发者备注转简体，缓存起来，搜索时按它匹配
	private string Note(FlagData flag)
	{
		if (!_notes.TryGetValue(flag, out string note))
		{
			try
			{
				note = TextConverter.ToSimplified(flag.DevNote ?? "");
			}
			catch
			{
				note = flag.DevNote ?? "";
			}
			_notes[flag] = note;
		}
		return note;
	}

	private void PauseHotKeys()
	{
		if (_hotKeyPaused)
		{
			return;
		}
		_hotKeyPaused = true;
		_prevIme = Input.imeCompositionMode;
		Input.imeCompositionMode = IMECompositionMode.On;
		Mortal.Free.FreeManager free = Mortal.Free.FreeManager.Instance;
		if (free != null)
		{
			free.SetHotKey(active: false);
		}
	}

	private void ResumeHotKeys()
	{
		if (!_hotKeyPaused)
		{
			return;
		}
		_hotKeyPaused = false;
		Input.imeCompositionMode = _prevIme;
		Mortal.Free.FreeManager free = Mortal.Free.FreeManager.Instance;
		if (free != null)
		{
			free.SetHotKey(active: true);
		}
	}

	// 搜索栏用游戏自己的字体，配色和 F9 面板一致：黑底、深色按钮、米白字
	private void BuildStyles(float scale)
	{
		_scale = scale;
		int fontSize = Mathf.Max(12, (int)(22f * scale));
		if (_background == null)
		{
			_background = Solid(new Color(0f, 0f, 0f, 0.9f));
			_buttonTex = Solid(ButtonFill);
			_buttonHoverTex = Solid(ButtonHover);
			_fieldTex = Solid(new Color(0.12f, 0.11f, 0.09f, 1f));
		}
		_box = new GUIStyle();
		_box.normal.background = _background;
		_box.padding = new RectOffset((int)(14f * scale), (int)(14f * scale), (int)(10f * scale), (int)(14f * scale));
		_title = new GUIStyle(GUI.skin.label) { fontSize = Mathf.Max(14, (int)(32f * scale)), alignment = TextAnchor.MiddleCenter };
		_title.normal.textColor = Color.white;
		_label = new GUIStyle(GUI.skin.label) { fontSize = fontSize, alignment = TextAnchor.MiddleLeft, fixedHeight = 40f * scale };
		_label.normal.textColor = TextLight;
		_center = new GUIStyle(_label) { alignment = TextAnchor.MiddleCenter };
		_button = new GUIStyle(GUI.skin.button) { fontSize = fontSize, fixedHeight = 40f * scale, alignment = TextAnchor.MiddleCenter };
		_button.normal.background = _buttonTex;
		_button.hover.background = _buttonHoverTex;
		_button.active.background = _buttonHoverTex;
		_button.focused.background = _buttonTex;
		_button.normal.textColor = _button.hover.textColor = _button.active.textColor = _button.focused.textColor = TextLight;
		_button.border = new RectOffset(0, 0, 0, 0);
		_field = new GUIStyle(GUI.skin.textField) { fontSize = fontSize, fixedHeight = 42f * scale, alignment = TextAnchor.MiddleLeft };
		_field.normal.background = _field.focused.background = _field.hover.background = _fieldTex;
		_field.normal.textColor = _field.focused.textColor = _field.hover.textColor = Color.white;
		_field.border = new RectOffset(0, 0, 0, 0);
		_field.padding = new RectOffset((int)(8f * scale), (int)(8f * scale), 0, 0);
		if (_font != null)
		{
			_title.font = _font;
			_label.font = _font;
			_center.font = _font;
			_button.font = _font;
			_field.font = _font;
		}
	}

	private static Texture2D Solid(Color color)
	{
		var texture = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
		texture.SetPixel(0, 0, color);
		texture.Apply();
		return texture;
	}

}
