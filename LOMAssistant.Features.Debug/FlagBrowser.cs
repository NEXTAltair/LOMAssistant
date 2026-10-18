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
		_original = _rows.Select(r => Traverse.Create(r).Field("_stat").GetValue<FlagData>()).ToArray();
		_query = "";
		_applied = null;
		_results.Clear();
		_page = 0;
		_font = _rows.Select(r => r.GetComponentInChildren<Text>(true)).FirstOrDefault(x => x != null && x.font != null)?.font;
		_scale = -1f;
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

	private bool PanelVisible => _testPanel != null && _testPanel.activeInHierarchy && _rows != null && _rows.Length > 0;

	private int PageCount => Math.Max(1, (_results.Count + _rows.Length - 1) / _rows.Length);

	private void OnGUI()
	{
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
