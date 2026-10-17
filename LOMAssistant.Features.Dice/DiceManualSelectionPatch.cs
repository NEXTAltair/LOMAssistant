using System.Collections;
using HarmonyLib;
using LOMAssistant.Config;
using Mortal.Story;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LOMAssistant.Features.Dice;

public static class DiceManualSelectionPatch
{
	public static bool IsClicked;

	public static int IsClickedIndex;

	public static DiceMenuDialog Menu;

	public static bool RollHasStarted;

	[HarmonyPostfix]
	[HarmonyPatch(typeof(DiceMenuDialog), "OnEnable")]
	public static void OnEnable_Postfix(DiceMenuDialog __instance)
	{
		if (!ConfigManager.EnableManualDiceSelection.Value)
		{
			return;
		}
		Menu = __instance;
		IsClicked = false;
		RollHasStarted = false;
		Button[] cachedButtons = __instance.CachedButtons;
		if (cachedButtons == null)
		{
			return;
		}
		for (int i = 0; i < cachedButtons.Length; i++)
		{
			if (!(cachedButtons[i] == null))
			{
				DiceManualSelectionButton diceManualSelectionButton = cachedButtons[i].gameObject.GetComponent<DiceManualSelectionButton>();
				if (diceManualSelectionButton == null)
				{
					diceManualSelectionButton = cachedButtons[i].gameObject.AddComponent<DiceManualSelectionButton>();
					diceManualSelectionButton.PointerClickEvent += OnButtonClick;
				}
				diceManualSelectionButton.index = i + 1;
			}
		}
		DiceGamepadSelector.Attach(__instance);
		LOMAssistantPlugin.LogInfo("骰子手动选择：已注册按钮点击事件。");
		FileLogger.Log("骰子手动选择：已注册按钮点击事件。");
	}

	public static void OnButtonClick(PointerEventData obj, int i)
	{
		if (obj.button == PointerEventData.InputButton.Left)
		{
			Choose(i);
		}
	}

	/// <summary>选定第 i 条线路（鼠标点击和手柄共用）。</summary>
	public static void Choose(int i)
	{
		if (!(Menu == null))
		{
			LOMAssistantPlugin.LogInfo("骰子手动选择：选中线路 " + i);
			FileLogger.Log("[DicePatch] OnButtonClick: line=" + i);
			Traverse traverse = Traverse.Create(Menu);
			IsClickedIndex = i;
			IsClicked = true;
			traverse.Field("_resultSelection").SetValue(i);
			Menu.UpdateSelection();
			FileLogger.Log("[DicePatch] OnButtonClick: RollHasStarted=" + RollHasStarted);
			if (!RollHasStarted)
			{
				Menu.PressStartButton();
				RollHasStarted = true;
				FileLogger.Log("[DicePatch] OnButtonClick: called PressStartButton (Roll not started)");
			}
			else
			{
				traverse.Field("_triggerNextInput").SetValue(true);
				Menu.PressContinueButton();
				FileLogger.Log("[DicePatch] OnButtonClick: set _triggerNextInput=true + PressContinueButton (in Roll/after)");
			}
		}
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(DiceMenuDialog), "Roll")]
	public static bool Roll_Prefix(DiceMenuDialog __instance, ref IEnumerator __result)
	{
		Traverse traverse = Traverse.Create(__instance);
		if (IsClicked)
		{
			traverse.Field("_resultSelection").SetValue(IsClickedIndex);
			__instance.UpdateSelection();
			traverse.Field("_useFatePoint").SetValue(false);
			__result = EmptyEnumerator();
			LOMAssistantPlugin.LogInfo("骰子手动选择：跳过 Roll 协程，直接设置结果为线路 " + IsClickedIndex);
			FileLogger.Log("[DicePatch] Roll_Prefix: IsClicked=true, skipped Roll, set _resultSelection=" + IsClickedIndex + ", cleared _useFatePoint");
			return false;
		}
		bool value = traverse.Field("_pressRestartButton").GetValue<bool>();
		FileLogger.Log("[DicePatch] Roll_Prefix: RollHasStarted=" + RollHasStarted + ", _pressRestartButton=" + value);
		if (RollHasStarted && value)
		{
			__instance.UpdateSelection();
			__result = EmptyEnumerator();
			LOMAssistantPlugin.LogInfo("骰子手动选择：逆天后重新 Roll，跳过转盘动画，等待玩家选择线路。");
			FileLogger.Log("[DicePatch] Roll_Prefix: revolution re-roll skipped, UpdateSelection() called");
			return false;
		}
		RollHasStarted = true;
		FileLogger.Log("[DicePatch] Roll_Prefix: normal Roll started, RollHasStarted=true");
		return true;
	}

	private static IEnumerator EmptyEnumerator()
	{
		yield break;
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(DiceMenuDialog), "CheckRevolution")]
	public static bool CheckRevolution_Prefix(DiceMenuDialog __instance, ref bool __result)
	{
		FileLogger.Log("[DicePatch] CheckRevolution_Prefix: called, IsClicked=" + IsClicked);
		if (!IsClicked)
		{
			return true;
		}
		Traverse traverse = Traverse.Create(__instance);
		traverse.Field("_resultSelection").SetValue(IsClickedIndex);
		__instance.UpdateSelection();
		__result = false;
		IsClicked = false;
		traverse.Field("_triggerNextInput").SetValue(true);
		__instance.PressContinueButton();
		LOMAssistantPlugin.LogInfo("骰子手动选择：已跳过逆天判定，直接继续。");
		FileLogger.Log("[DicePatch] CheckRevolution_Prefix: forced __result=false, set _triggerNextInput=true, reset IsClicked=false");
		return false;
	}
}
