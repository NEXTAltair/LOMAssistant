using System;
using System.IO;
using System.Text.RegularExpressions;
using LOMAssistant.Config;
using UnityEngine;
using UnityEngine.EventSystems;

namespace LOMAssistant.Features.AnytimeSave;

/// <summary>随时存档面板（默认 F6，样式同 F8 面板）和快捷键（默认 F5 存、F7 读）。</summary>
public partial class AnytimeSave
{
	private const int SlotCount = 8;
	private const int WindowId = 584129;
	private float _scale = -1f;
	private GUIStyle _label, _title, _button, _selected, _window, _toast;
	private Rect _windowRect;
	private string _lastLayout;
	private string detailPath, detailText = "", detailMeta = "";
	private float nextDetailRefresh;

	private void UpdateHotkeys()
	{
		if (TextInputFocused()) return;
		if (InputGuard.HotkeyDown(ConfigManager.AnytimePanelKey.Value)) { if (installed) SetPanel(!panel); else panel = !panel; }
		else if (!installed) return;
		else if (InputGuard.HotkeyDown(ConfigManager.AnytimeSaveKey.Value)) SaveSlot();
		else if (InputGuard.HotkeyDown(ConfigManager.AnytimeLoadKey.Value)) LoadSlot();
		else if (InputGuard.HotkeyDown(ConfigManager.AnytimeRollbackKey.Value)) Rollback();
	}

	private static bool TextInputFocused()
	{
		if (GUIUtility.keyboardControl != 0) return true;
		var selected = EventSystem.current == null ? null : EventSystem.current.currentSelectedGameObject;
		if (selected == null) return false;
		var input = selected.GetComponent<UnityEngine.UI.InputField>();
		return (input != null && input.isFocused) || selected.GetComponent("TMP_InputField") != null;
	}

	private void BuildStyles(float scale)
	{
		_scale = scale;
		int fontSize = Mathf.Max(12, (int)(22f * scale));
		_label = new GUIStyle(GUI.skin.label) { fontSize = fontSize, wordWrap = true };
		_button = new GUIStyle(GUI.skin.button) { fontSize = fontSize };
		_selected = new GUIStyle(_button) { fontStyle = FontStyle.Bold };
		_selected.normal.textColor = _selected.hover.textColor = new Color(1f, 0.82f, 0.4f);
		_window = new GUIStyle(GUI.skin.window) { fontSize = fontSize };
		_window.padding.top = (int)(8f * scale);
		_title = new GUIStyle(_label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
		_toast = new GUIStyle(GUI.skin.box) { fontSize = fontSize, wordWrap = true, alignment = TextAnchor.MiddleLeft };
		_toast.padding = new RectOffset((int)(12 * scale), (int)(12 * scale), (int)(8 * scale), (int)(8 * scale));
		float width = 560f * scale;
		_windowRect = new Rect((Screen.width - width) / 2f, 20f * scale, width, 10f);
	}

	private void OnGUI()
	{
		// 隐藏界面看 CG 时本 Mod 的窗口也不画
		if (UiHider.Active)
		{
			return;
		}
		float scale = Mathf.Min(Screen.width / 1920f, Screen.height / 1080f);
		if (scale <= 0) return;
		if (!Mathf.Approximately(scale, _scale)) BuildStyles(scale);
		if (panel)
		{
			// 内容行数变了才重算高度（每次 OnGUI 都重置会闪）
			string layout = detailMeta + "|" + detailText.Length + "|" + (CanSave(out _) ? 1 : 0) + (Time.realtimeSinceStartup < messageUntil ? message : "") + Faulted;
			if (layout != _lastLayout && Event.current.type == EventType.Layout)
			{
				_lastLayout = layout;
				_windowRect.height = 10f;
			}
			_windowRect = GUILayout.Window(WindowId, _windowRect, DrawWindow, "", _window);
			InputGuard.Report("AnytimeSave", _windowRect);
		}
		else if (Replaying || Time.realtimeSinceStartup < messageUntil)
		{
			var content = new GUIContent(Replaying ? "正在读取随时存档 " + cursor + "/" + (target + 1) : message);
			float width = Mathf.Min(Screen.width - 40f * scale, Mathf.Max(200f * scale, _toast.CalcSize(content).x));
			float height = _toast.CalcHeight(content, width);
			GUI.Box(new Rect(20f * scale, Screen.height - height - 20f * scale, width, height), content, _toast);
		}
	}

	private void RefreshSlotDetail(string path)
	{
		if (detailPath == path && Time.realtimeSinceStartup < nextDetailRefresh) return;
		detailPath = path; nextDetailRefresh = Time.realtimeSinceStartup + 0.5f;
		if (!File.Exists(path)) { detailMeta = "存档 " + slot + "：空"; detailText = ""; return; }
		try
		{
			var d = ReadSave(path);
			detailMeta = "存档 " + slot + "：" + d.savedAt + "（" + (d.scene == "Free" ? "养成" : "剧情") + "）";
			detailText = RichText.Strip(d.preview ?? "");
		}
		catch (Exception ex) { detailMeta = "存档 " + slot + "：无法读取"; detailText = ex.Message; }
	}

	private void DrawWindow(int id)
	{
		// 不用窗口自带标题：它会随上边距一起下移，压住第一行
		GUILayout.Label("随时存档", _title);
		if (!installed)
		{
			GUILayout.Label(compatibilityFailure, _label);
			GUILayout.Label("当前游戏版本：" + Application.version, _label);
			if (GUILayout.Button("关闭", _button)) panel = false;
			GUI.DragWindow();
			return;
		}
		GUILayout.BeginHorizontal();
		for (int i = 1; i <= SlotCount; i++)
			if (GUILayout.Button(i.ToString(), slot == i ? _selected : _button)) { slot = i; detailPath = null; }
		GUILayout.EndHorizontal();
		string path = SlotPath(slot); RefreshSlotDetail(path);
		GUILayout.Label(detailMeta, _label);
		if (detailText.Length > 0) GUILayout.Label(detailText.Length > 120 ? detailText.Substring(0, 120) + "…" : detailText, _label);
		bool can = CanSave(out string reason);
		string status = Time.realtimeSinceStartup < messageUntil && !string.IsNullOrEmpty(message) ? message : can ? "" : "暂不能保存：" + reason;
		if (status.Length > 0) GUILayout.Label(status, _label);
		GUILayout.BeginHorizontal();
		GUI.enabled = can;
		if (GUILayout.Button("保存（" + ConfigManager.AnytimeSaveKey.Value + "）", _button)) { SaveSlot(); nextDetailRefresh = 0; }
		GUI.enabled = !Replaying && !Restoring && File.Exists(path);
		if (GUILayout.Button("读取（" + ConfigManager.AnytimeLoadKey.Value + "）", _button)) LoadSlot();
		GUI.enabled = !Replaying && !Restoring && RollbackCount > 0;
		if (GUILayout.Button("回退到判定前（" + ConfigManager.AnytimeRollbackKey.Value + "）", _button)) Rollback();
		GUI.enabled = true;
		if (Faulted && GUILayout.Button("返回标题", _button))
		{ Faulted = false; SetPanel(false); Time.timeScale = 1; Mortal.Core.SceneController.Instance.LoadTitle(); }
		if (GUILayout.Button("关闭", _button)) SetPanel(false);
		GUILayout.EndHorizontal();
		GUI.DragWindow();
	}
}
