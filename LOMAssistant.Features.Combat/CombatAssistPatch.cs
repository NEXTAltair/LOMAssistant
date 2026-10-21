using HarmonyLib;
using LOMAssistant.Config;
using Lean.Localization;
using Mortal.Combat;
using Mortal.Core;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace LOMAssistant.Features.Combat;

public static class CombatAssistPatch
{
	[HarmonyPostfix]
	[HarmonyPatch(typeof(CombatManager), "InitCombatRound")]
	public static void InitCombatRound_Postfix(CombatManager __instance)
	{
		FileLogger.Log("战斗辅助：InitCombatRound Postfix 被调用。");
		if (!ConfigManager.EnableCombatAssist.Value)
		{
			return;
		}
		if (CombatUIManager.Instance == null)
		{
			LOMAssistantPlugin.LogWarning("战斗辅助：CombatUIManager 为空。");
			return;
		}
		CommonPanel menuPanel = Traverse.Create(CombatUIManager.Instance).Field("_menuPanel").GetValue<CommonPanel>();
		if (menuPanel == null)
		{
			LOMAssistantPlugin.LogWarning("战斗辅助：菜单面板为空。");
			return;
		}
		MenuPanel component = menuPanel.gameObject.GetComponent<MenuPanel>();
		if (component == null)
		{
			LOMAssistantPlugin.LogWarning("战斗辅助：菜单面板上没有 MenuPanel 组件。");
			return;
		}
		Button value = Traverse.Create(component).Field("_giveupButton").GetValue<Button>();
		if (value == null)
		{
			LOMAssistantPlugin.LogWarning("战斗辅助：投降按钮为空。");
			return;
		}
		GameObject gameObject = Object.Instantiate(value.gameObject, value.gameObject.transform.parent);
		LeanLocalizedText[] componentsInChildren = gameObject.GetComponentsInChildren<LeanLocalizedText>(includeInactive: true);
		foreach (LeanLocalizedText obj in componentsInChildren)
		{
			obj.TranslationName = "直接获胜";
			obj.FallbackText = obj.TranslationName;
		}
		Text[] componentsInChildren2 = gameObject.GetComponentsInChildren<Text>(includeInactive: true);
		for (int i = 0; i < componentsInChildren2.Length; i++)
		{
			componentsInChildren2[i].text = TextConverter.ForDisplay("直接获胜");
		}
		Button.ButtonClickedEvent onClick = gameObject.GetComponent<Button>().onClick;
		onClick.SetPersistentListenerState(0, UnityEventCallState.Off);
		onClick.AddListener(delegate
		{
			BattleWin(menuPanel);
		});
		gameObject.SetActive(value: true);
		LOMAssistant.Features.Extras.MenuNavChain.Ensure(gameObject.transform.parent);
		LOMAssistantPlugin.LogInfo("战斗辅助：已添加直接获胜按钮。");
		FileLogger.Log("战斗辅助：已添加直接获胜按钮。");
	}

	public static void BattleWin(CommonPanel menuPanel)
	{
		if (CombatManager.Instance == null || menuPanel == null)
		{
			LOMAssistantPlugin.LogWarning("战斗辅助：BattleWin 时 CombatManager 或 menuPanel 为空。");
			return;
		}
		CombatManager.Instance.EnableHotKey = false;
		menuPanel.Show(show: false);
		LOMAssistantPlugin.Instance.StartCoroutine(CombatManager.Instance.GameOver(win: true));
		LOMAssistantPlugin.LogInfo("战斗辅助：已执行直接获胜。");
		FileLogger.Log("战斗辅助：已执行直接获胜。");
	}
}
