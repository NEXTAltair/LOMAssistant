using System;
using System.Collections;
using HarmonyLib;
using Mortal.Core;
using Mortal.Free;
using UnityEngine;
using UnityEngine.UI;

namespace LOMAssistant.Features.Extras;

/// <summary>
/// 空过：在养成（唐门地图）界面的 ESC 菜单加一个“空过”按钮。
/// 消耗 1 次行动但不执行任何场所脚本，然后走与剧情结束回到地图相同的流程
/// （LuaManager.ChangeScene("Free")）：检查任务、行动次数归零时自动进入下一旬。
/// </summary>
public static class SkipActionPatch
{
	private const string ButtonName = "LOMAssistant_SkipButton";

	[HarmonyPostfix]
	[HarmonyPatch(typeof(FreeManager), "Awake")]
	public static void FreeManager_Awake_Postfix(FreeManager __instance)
	{
		try
		{
			CommonPanel menuPanel = Traverse.Create(__instance).Field("_menuPanel").GetValue<CommonPanel>();
			MenuPanel menu = menuPanel != null ? menuPanel.GetComponent<MenuPanel>() : null;
			if (menu == null)
			{
				LOMAssistantPlugin.LogWarning("空过：找不到养成界面菜单。");
				return;
			}

			var tr = Traverse.Create(menu);
			Button template = tr.Field("_continueButton").GetValue<Button>()
				?? tr.Field("_loadButton").GetValue<Button>()
				?? tr.Field("_titleButton").GetValue<Button>();
			if (template == null)
			{
				LOMAssistantPlugin.LogWarning("空过：菜单中没有可复制的按钮。");
				return;
			}

			Button skip = MenuButtonHelper.CloneButton(template, "空过", ButtonName);
			MenuNavChain.Ensure(skip.transform.parent);
			skip.onClick.AddListener(OnSkipClick);
			FileLogger.Log("空过：已添加按钮 " + MenuButtonHelper.DescribeHierarchy(skip.transform));
		}
		catch (Exception e)
		{
			LOMAssistantPlugin.LogError("空过：添加按钮失败: " + e);
		}
	}

	private static void OnSkipClick()
	{
		FreeManager free = FreeManager.Instance;
		if (free == null || free.IsProcessing)
		{
			return;
		}
		free.Menu(enabled: false);
		Time.timeScale = 1f;

		MissionManagerData missions = MissionManagerData.Instance;
		if (missions.MainMissionStart && missions.MainMissionPosition != PositionType.無)
		{
			free.StartCoroutine(free.SayDialog(PortraitType.平常, "眼下还有要事在身，不能虚度光阴。"));
			return;
		}
		if (PlayerStatManagerData.Instance.CurrentActionCount <= 0)
		{
			free.StartCoroutine(free.SayDialog(PortraitType.平常, "這會兒有些晚了，改日再說吧。"));
			return;
		}
		SceneController scenes = SceneController.Instance;
		if (scenes == null || scenes.IsPrepare || scenes.IsLoading)
		{
			return;
		}
		free.StartCoroutine(SkipRoutine(free));
	}

	private static IEnumerator SkipRoutine(FreeManager free)
	{
		free.SetHotKey(active: false);
		free.DisableButton();
		free.SetProcessing();

		PlayerStatManagerData stats = PlayerStatManagerData.Instance;
		GameTime before = stats.GameTime;
		string beforeText = $"{before.Year}年{before.Month}月{before.Stage}";
		string msg;
		if (ActionModePatch.Mode == ActionModePatch.Unlimited)
		{
			// 行动点不消耗时，空过就是「结束本旬」，否则永远到不了下一旬
			yield return ActionModePatch.SpendAll(free);
			msg = $"空过：{beforeText} 行动点不消耗模式，结束本旬。";
		}
		else
		{
			yield return free.UpdateActionValue(-1);
			msg = $"空过：{beforeText} 消耗 1 次行动，剩余 {stats.CurrentActionCount}。";
		}
		LOMAssistantPlugin.LogInfo(msg);
		FileLogger.Log(msg);

		yield return new WaitForSeconds(0.5f);
		yield return free.TransitionScreen();

		// Same as LuaManager.ChangeScene("Free", ...) at the end of a location script.
		SaveSystem.Instance.SaveUniverseData();
		MissionManagerData missions = MissionManagerData.Instance;
		missions.UpdateCheckMissions();
		if (missions.HasAnyMissionTrigger())
		{
			LOMAssistantPlugin.LogInfo("空过：触发任务剧情，进入剧情场景。");
			SceneController.Instance.LoadStory();
		}
		else
		{
			SceneController.Instance.LoadFree();
		}
	}
}
