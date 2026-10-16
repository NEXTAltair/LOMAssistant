using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace LOMAssistant.Features.Extras;

/// <summary>
/// 存档备注输入框（IMGUI，屏幕居中），从 F8 面板打开。
/// </summary>
public class SaveNoteEditor : MonoBehaviour
{
	private const int WindowId = 0x4E4F54; // "NOT"
	private const int MaxLength = 30;
	private const string FieldName = "LOMAssistant_SaveNote";

	private static SaveNoteEditor _instance;

	private string _slot;
	private string _text = "";
	private Action _onSaved;
	private bool _focusPending;
	private IMECompositionMode _prevIme;
	// The game's uGUI EventSystem is paused while the box is open so Enter/clicks don't also hit the save slots behind it.
	private EventSystem _pausedEventSystem;

	private float _scale = -1f;
	private GUIStyle _label;
	private GUIStyle _button;
	private GUIStyle _field;
	private GUIStyle _window;

	public static bool IsOpen => _instance != null && _instance._slot != null;

	public static void Open(string slot, Action onSaved)
	{
		if (_instance == null)
		{
			_instance = LOMAssistantPlugin.Instance.gameObject.AddComponent<SaveNoteEditor>();
		}
		if (_instance._slot == null)
		{
			_instance._prevIme = Input.imeCompositionMode;
		}
		_instance._slot = slot;
		_instance._text = SaveNotePatch.GetNote(slot);
		_instance._onSaved = onSaved;
		_instance._focusPending = true;
		Input.imeCompositionMode = IMECompositionMode.On;
		if (_instance._pausedEventSystem == null && EventSystem.current != null && EventSystem.current.enabled)
		{
			_instance._pausedEventSystem = EventSystem.current;
			_instance._pausedEventSystem.enabled = false;
		}
	}

	private void Close(bool save)
	{
		if (save)
		{
			SaveNotePatch.SetNote(_slot, _text);
			try
			{
				_onSaved?.Invoke();
			}
			catch (Exception e)
			{
				FileLogger.LogError("存档备注：刷新栏位失败: " + e.Message);
			}
		}
		_slot = null;
		_onSaved = null;
		Input.imeCompositionMode = _prevIme;
		if (_pausedEventSystem != null)
		{
			StartCoroutine(ResumeEventSystem(_pausedEventSystem));
			_pausedEventSystem = null;
		}
	}

	// Re-enable the game UI only after the click/Enter that closed the box is released,
	// otherwise the same click lands on the save slot behind it (e.g. reopening "overwrite?").
	private System.Collections.IEnumerator ResumeEventSystem(EventSystem eventSystem)
	{
		yield return null;
		while (Input.GetMouseButton(0) || Input.GetMouseButton(1) || Input.GetKey(KeyCode.Return) || Input.GetKey(KeyCode.KeypadEnter))
		{
			yield return null;
		}
		yield return null;
		if (eventSystem == null)
		{
			yield break;
		}
		if (IsOpen)
		{
			// Reopened meanwhile: keep it paused and let the next Close restore it.
			_pausedEventSystem = eventSystem;
			yield break;
		}
		eventSystem.enabled = true;
	}

	private void OnGUI()
	{
		if (_slot == null)
		{
			return;
		}
		float scale = Mathf.Min(Screen.width / 1920f, Screen.height / 1080f);
		if (!Mathf.Approximately(scale, _scale))
		{
			BuildStyles(scale);
		}
		float w = 560f * _scale;
		float h = 190f * _scale;
		Rect rect = new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f, w, h);
		GUI.ModalWindow(WindowId, rect, DrawWindow, $"存档 {_slot} 备注", _window);
	}

	private void DrawWindow(int id)
	{
		Event e = Event.current;
		if (e.type == EventType.KeyDown && (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter))
		{
			e.Use();
			Close(save: true);
			return;
		}

		GUILayout.Space(10f * _scale);
		GUI.SetNextControlName(FieldName);
		_text = GUILayout.TextField(_text, MaxLength, _field);
		if (_focusPending && e.type == EventType.Repaint)
		{
			GUI.FocusControl(FieldName);
			_focusPending = false;
		}
		GUILayout.Space(10f * _scale);
		GUILayout.BeginHorizontal();
		if (GUILayout.Button("保存", _button))
		{
			Close(save: true);
		}
		if (GUILayout.Button("不写", _button))
		{
			Close(save: false);
		}
		GUILayout.EndHorizontal();
	}

	private void BuildStyles(float scale)
	{
		_scale = scale;
		int fontSize = Mathf.Max(12, (int)(24f * scale));
		_label = new GUIStyle(GUI.skin.label) { fontSize = fontSize };
		_button = new GUIStyle(GUI.skin.button) { fontSize = fontSize, fixedHeight = 44f * scale };
		_field = new GUIStyle(GUI.skin.textField) { fontSize = fontSize, fixedHeight = 44f * scale };
		_window = new GUIStyle(GUI.skin.window) { fontSize = fontSize };
		_window.padding.top = (int)(34f * scale);
		_window.padding.left = _window.padding.right = (int)(16f * scale);
	}
}
