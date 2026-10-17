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
	private const int MaxTitleChars = 8;

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

	/// <summary>F9 面板打开后调用，记下面板和原版的 51 行。</summary>
	public static void Attach(GameObject testPanel)
	{
		if (_instance == null)
		{
			_instance = LOMAssistantPlugin.Instance.gameObject.AddComponent<FlagBrowser>();
		}
		_instance.Bind(testPanel);
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
		FileLogger.Log($"旗标搜索：F9 旗标列共 {_rows.Length} 行");
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
		float w = 1180f * _scale;
		float h = 64f * _scale;
		Rect rect = new Rect((Screen.width - w) / 2f, Screen.height - h - 8f * _scale, w, h);
		GUILayout.BeginArea(rect, _box);
		GUILayout.BeginHorizontal();
		GUILayout.Label("旗标搜索", _label, GUILayout.Width(110f * _scale));
		GUI.SetNextControlName("LOMAssistant_FlagSearch");
		_query = GUILayout.TextField(_query, 40, _field, GUILayout.Width(300f * _scale));
		bool focused = GUI.GetNameOfFocusedControl() == "LOMAssistant_FlagSearch";
		Event e = Event.current;
		bool enter = focused && e.type == EventType.KeyDown && (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
			&& string.IsNullOrEmpty(Input.compositionString);
		if (GUILayout.Button("搜索", _button, GUILayout.Width(80f * _scale)) || enter)
		{
			Search(_query);
		}
		if (GUILayout.Button("全部", _button, GUILayout.Width(80f * _scale)))
		{
			_query = "*";
			Search(_query);
		}
		if (GUILayout.Button("原版", _button, GUILayout.Width(80f * _scale)))
		{
			_query = "";
			Search(_query);
		}
		if (_applied != null)
		{
			if (GUILayout.Button("<<", _button, GUILayout.Width(56f * _scale)))
			{
				ShowPage(_page - 1);
			}
			GUILayout.Label($"第 {_page + 1}/{PageCount} 页（共 {_results.Count} 面）", _label, GUILayout.Width(250f * _scale));
			if (GUILayout.Button(">>", _button, GUILayout.Width(56f * _scale)))
			{
				ShowPage(_page + 1);
			}
		}
		else
		{
			GUILayout.Label("按旗标名或备注搜，如 孟仙谣、H040、神兵帖；多个词用空格隔开", _label);
		}
		GUILayout.EndHorizontal();
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

	private void BuildStyles(float scale)
	{
		_scale = scale;
		int fontSize = Mathf.Max(12, (int)(22f * scale));
		_label = new GUIStyle(GUI.skin.label) { fontSize = fontSize, alignment = TextAnchor.MiddleLeft, fixedHeight = 44f * scale };
		_button = new GUIStyle(GUI.skin.button) { fontSize = fontSize, fixedHeight = 44f * scale };
		_field = new GUIStyle(GUI.skin.textField) { fontSize = fontSize, fixedHeight = 44f * scale };
		_box = new GUIStyle(GUI.skin.box);
		_box.padding = new RectOffset((int)(10f * scale), (int)(10f * scale), (int)(10f * scale), (int)(10f * scale));
	}
}
