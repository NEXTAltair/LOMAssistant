using System.Globalization;
using HarmonyLib;
using LOMAssistant.Config;
using Mortal.Core;
using Mortal.Pinball;
using UnityEngine;
using UnityEngine.EventSystems;

namespace LOMAssistant.Features.Extras;

/// <summary>
/// 游戏内面板（默认 F8）：修炼/锻造/炼丹/决斗开关；天命点；弹珠小游戏中额外显示分数设置；存档/读档界面中可给所选存档写备注。
/// 修改即时生效并保存到配置文件。
/// </summary>
public class ModPanel : MonoBehaviour
{
	private const int WindowId = 0x4C4F4D; // "LOM"

	private bool _visible;
	private Rect _windowRect;
	private float _scale = -1f;
	private GUIStyle _label;
	private GUIStyle _toggle;
	private GUIStyle _button;
	private GUIStyle _field;
	private GUIStyle _window;

	private PinballManager _boundPinball;
	private string _totalInput = "";
	private string _multiplierInput = "";
	private string _fateInput;

	private int _lastSections;

	// Last save/load slot the player selected (clicking this IMGUI window can clear the uGUI selection).
	private MonoBehaviour _noteSlot;

	private void Update()
	{
		if (Input.GetKeyDown(ConfigManager.PanelKey.Value))
		{
			_visible = !_visible;
		}
		TrackSelectedSlot();
		GameSpeed.Tick();
	}

	private void OnGUI()
	{
		if (!_visible)
		{
			return;
		}
		float scale = Mathf.Min(Screen.width / 1920f, Screen.height / 1080f);
		if (!Mathf.Approximately(scale, _scale))
		{
			BuildStyles(scale);
			_windowRect = new Rect(Screen.width - 400f * scale - 20f * scale, 20f * scale, 400f * scale, 10f);
		}
		// Shrink to content only when the pinball section appears or disappears; resetting
		// the height on every OnGUI call (Layout/Repaint/mouse events) makes the window flicker.
		int sections = (PinballManager.Instance != null && PinballManager.Instance.isActiveAndEnabled ? 1 : 0) | (GetNoteSlot() != null ? 2 : 0) | (InGame() ? 8 : 0);
		if (sections != _lastSections && Event.current.type == EventType.Layout)
		{
			_lastSections = sections;
			_windowRect.height = 10f;
		}
		_windowRect = GUILayout.Window(WindowId, _windowRect, DrawWindow, "活侠传拓展", _window);
	}

	private void DrawWindow(int id)
	{
		GUILayout.Space(8f * _scale);
		Toggle(ConfigManager.CheatUnlimitedTraining, "无限修炼次数");
		Toggle(ConfigManager.CheatFastTraining, "快速修炼");
		Toggle(ConfigManager.CheatUnlimitedUpgrade, "无限锻造、炼丹次数");
		Toggle(ConfigManager.CheatFastUpgrade, "快速锻造、炼丹");
		Toggle(ConfigManager.CheatFreeUpgradeMoney, "锻造、炼丹不消耗银两");
		Toggle(ConfigManager.CheatDuelInvincible, "决斗中无敌");
		Toggle(ConfigManager.CheatDuelNoDamage, "决斗中造成伤害为0");
		DrawSpeed();

		PinballManager pinball = PinballManager.Instance;
		if (pinball != null && pinball.isActiveAndEnabled)
		{
			DrawPinball(pinball);
		}
		DrawFate();
		DrawNote();
		GUI.DragWindow();
	}

	private void DrawPinball(PinballManager pinball)
	{
		if (_boundPinball != pinball)
		{
			_boundPinball = pinball;
			_totalInput = GetTotal(pinball).ToString();
			_multiplierInput = ConfigManager.PinballScoreMultiplier.Value.ToString(CultureInfo.InvariantCulture);
		}

		GUILayout.Space(8f * _scale);
		GUILayout.BeginHorizontal();
		GUILayout.Label($"弹珠总分 {GetTotal(pinball)}", _label, GUILayout.Width(170f * _scale));
		_totalInput = GUILayout.TextField(_totalInput, 9, _field, GUILayout.Width(100f * _scale));
		if (GUILayout.Button("设定", _button) && int.TryParse(_totalInput.Trim(), out int value))
		{
			SetTotal(pinball, value);
		}
		GUILayout.EndHorizontal();

		GUILayout.BeginHorizontal();
		GUILayout.Label("得分倍率", _label, GUILayout.Width(170f * _scale));
		_multiplierInput = GUILayout.TextField(_multiplierInput, 6, _field, GUILayout.Width(100f * _scale));
		if (GUILayout.Button("设定", _button)
			&& float.TryParse(_multiplierInput.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float mult)
			&& mult >= 0f)
		{
			ConfigManager.PinballScoreMultiplier.Value = mult;
			ConfigManager.SetConfigValue(ConfigManager.PinballScoreMultiplier, mult);
			FileLogger.Log($"弹珠分数：得分倍率设为 {mult}");
		}
		GUILayout.EndHorizontal();

		if (ConfigManager.PinballFixedScore.Value >= 0)
		{
			GUILayout.Label($"配置中固定总分 {ConfigManager.PinballFixedScore.Value}，离开时覆盖", _label);
		}
	}

	private void DrawSpeed()
	{
		GUILayout.Space(8f * _scale);
		GUILayout.BeginHorizontal();
		GUILayout.Label("游戏倍速", _label, GUILayout.Width(120f * _scale));
		float current = ConfigManager.GameSpeed.Value;
		foreach (float option in GameSpeed.Options)
		{
			bool selected = Mathf.Approximately(current, option);
			bool on = GUILayout.Toggle(selected, option + "x", _button);
			if (on && !selected)
			{
				GameSpeed.Set(option);
			}
		}
		GUILayout.EndHorizontal();
	}

	private void TrackSelectedSlot()
	{
		GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
		if (selected == null)
		{
			return;
		}
		MonoBehaviour slot = (MonoBehaviour)selected.GetComponentInParent<SaveSlotPanel>()
			?? (MonoBehaviour)selected.GetComponentInParent<LoadSlotPanel>()
			?? selected.GetComponentInParent<RecentSaveSlotPanel>();
		if (slot != null)
		{
			_noteSlot = slot;
		}
	}

	// The remembered slot, if its screen is still open and it holds a manual save.
	private MonoBehaviour GetNoteSlot()
	{
		if (_noteSlot == null || !_noteSlot.isActiveAndEnabled)
		{
			return null;
		}
		var tr = Traverse.Create(_noteSlot);
		if (!SaveNotePatch.IsManualSlot(tr.Field("_slot").GetValue<string>()) || tr.Field("_currentData").GetValue() == null)
		{
			return null;
		}
		return _noteSlot;
	}

	// 天命点（GameStatType.命運），骰子“逆天”时消耗；只在进入存档后（地图/剧情）显示。
	private static bool InGame()
	{
		return (Mortal.Free.FreeManager.Instance != null && Mortal.Free.FreeManager.Instance.isActiveAndEnabled)
			|| (Mortal.Story.StoryManager.Instance != null && Mortal.Story.StoryManager.Instance.isActiveAndEnabled);
	}

	private void DrawFate()
	{
		if (!InGame())
		{
			_fateInput = null;
			return;
		}
		GameStat fate = PlayerStatManagerData.Instance.Stats.Get(GameStatType.命運);
		if (fate == null)
		{
			return;
		}
		if (_fateInput == null)
		{
			_fateInput = fate.Value.ToString();
		}
		GUILayout.Space(8f * _scale);
		GUILayout.BeginHorizontal();
		GUILayout.Label($"天命点 {fate.FinalValue}/{fate.Max}", _label, GUILayout.Width(170f * _scale));
		_fateInput = GUILayout.TextField(_fateInput, 6, _field, GUILayout.Width(100f * _scale));
		if (GUILayout.Button("设定", _button) && int.TryParse(_fateInput.Trim(), out int value))
		{
			int old = fate.Value;
			fate.SetValue(value);
			_fateInput = fate.Value.ToString();
			FileLogger.Log($"天命点：{old} -> {fate.Value}（面板修改）");
		}
		GUILayout.EndHorizontal();
	}

	private void DrawNote()
	{
		MonoBehaviour slotPanel = GetNoteSlot();
		if (slotPanel == null)
		{
			return;
		}
		string slot = Traverse.Create(slotPanel).Field("_slot").GetValue<string>();
		string note = SaveNotePatch.GetNote(slot);
		GUILayout.Space(8f * _scale);
		GUILayout.BeginHorizontal();
		GUILayout.Label(note.Length > 0 ? $"存档 {slot}｜{note}" : $"存档 {slot}", _label);
		if (GUILayout.Button("备注", _button, GUILayout.Width(90f * _scale)))
		{
			SaveNoteEditor.Open(slot, () => Traverse.Create(slotPanel).Method("Setup").GetValue());
		}
		GUILayout.EndHorizontal();
	}

	private void Toggle(ConfigElement<bool> element, string text)
	{
		bool value = GUILayout.Toggle(element.Value, " " + text, _toggle);
		if (value != element.Value)
		{
			element.Value = value;
			ConfigManager.SetConfigValue(element, value);
			string msg = $"面板：{text} -> {(value ? "开" : "关")}";
			LOMAssistantPlugin.LogInfo(msg);
			FileLogger.Log(msg);
		}
	}

	private static int GetTotal(PinballManager manager)
	{
		return Traverse.Create(manager).Field("_totalScore").GetValue<int>();
	}

	private static void SetTotal(PinballManager manager, int value)
	{
		var tr = Traverse.Create(manager);
		int old = tr.Field("_totalScore").GetValue<int>();
		tr.Field("_totalScore").SetValue(value);
		tr.Method("UpdateTotalText").GetValue();
		FileLogger.Log($"弹珠分数：总分 {old} -> {value}（面板修改）");
	}

	private void BuildStyles(float scale)
	{
		_scale = scale;
		int fontSize = Mathf.Max(12, (int)(22f * scale));
		_label = new GUIStyle(GUI.skin.label) { fontSize = fontSize, wordWrap = true };
		_toggle = new GUIStyle(GUI.skin.toggle) { fontSize = fontSize };
		_button = new GUIStyle(GUI.skin.button) { fontSize = fontSize };
		_field = new GUIStyle(GUI.skin.textField) { fontSize = fontSize };
		_window = new GUIStyle(GUI.skin.window) { fontSize = fontSize };
		_window.padding.top = (int)(30f * scale);
	}
}
