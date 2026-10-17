using System.Collections;
using HarmonyLib;
using LOMAssistant.Config;
using Mortal.Core;
using Mortal.Free;

namespace LOMAssistant.Features.Extras;

/// <summary>
/// 养成行动点模式（F8 面板切换）：
/// 不消耗：行动点扣到剩 1 点后不再扣。不锁在满值，是因为不少剧情（包括主线）要「行动次数 ≤ 1 / ≤ 2」才触发，
///   锁满会卡剧情；剩 1 点时这些条件都满足。想进入下一旬就用「空过」，此时空过会用掉本旬全部行动点。
/// 每旬 1 点：每旬行动点上限改为 1（地图上的点数显示、旬初自动存档都按 1 算）。
/// </summary>
public static class ActionModePatch
{
	public const int Normal = 0;
	public const int Unlimited = 1;
	public const int OnePerRound = 2;

	public static readonly int[] Options = { Normal, Unlimited, OnePerRound };

	// 空过结束本旬时置 true，让扣点不被「不消耗」拦下。
	internal static bool Bypass;

	public static int Mode => ConfigManager.ActionMode.Value;

	public static string Label(int mode)
	{
		switch (mode)
		{
			case Unlimited: return "不消耗";
			case OnePerRound: return "每旬1点";
			default: return "正常";
		}
	}

	public static void Set(int mode)
	{
		ConfigManager.ActionMode.Value = mode;
		ConfigManager.SetConfigValue(ConfigManager.ActionMode, mode);
		FileLogger.Log("行动点模式 -> " + Label(mode));
		// 切到每旬 1 点时，本旬多出来的点立即收掉
		if (mode == OnePerRound && PlayerStatManagerData.Instance != null && PlayerStatManagerData.Instance.CurrentActionCount > 1)
		{
			PlayerStatManagerData.Instance.Stats.Set(GameStatType.行動次數, 1);
		}
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(FreeManager), "UpdateActionValue")]
	public static void UpdateActionValue_Prefix(ref int value)
	{
		if (Mode != Unlimited || Bypass || value >= 0)
		{
			return;
		}
		int current = PlayerStatManagerData.Instance.CurrentActionCount;
		int floor = current > 1 ? 1 : current;
		int target = current + value < floor ? floor : current + value;
		value = target - current;
	}

	// 留学、在外行动等剧情里的自由行动菜单，扣行动点走的是剧情脚本的 statmodifymanager.Player("action", -1, ...)
	[HarmonyPrefix]
	[HarmonyPatch(typeof(Mortal.Story.StatModifyManager), "Player")]
	public static void StoryPlayer_Prefix(string name, ref int value)
	{
		if (name == "action")
		{
			UpdateActionValue_Prefix(ref value);
		}
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(PlayerStatManagerData), "GetMaxActionCount")]
	public static void GetMaxActionCount_Postfix(ref int __result)
	{
		if (Mode == OnePerRound && __result > 1)
		{
			__result = 1;
		}
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(PlayerStatManagerData), "MaxActionCount", MethodType.Getter)]
	public static void MaxActionCount_Postfix(ref int __result)
	{
		if (Mode == OnePerRound && __result > 1)
		{
			__result = 1;
		}
	}

	// 有些剧情会让下一旬少行动几次（旗标 T0002）。每旬只有 1 点时再扣就成了 0 点，
	// 地图上什么都做不了、空过也用不了，会卡住，所以保底 1 点。
	[HarmonyPostfix]
	[HarmonyPatch(typeof(PlayerStatManagerData), "ResetActionCount")]
	public static void ResetActionCount_Postfix(PlayerStatManagerData __instance)
	{
		if (Mode == OnePerRound && __instance.CurrentActionCount < 1)
		{
			__instance.Stats.Set(GameStatType.行動次數, 1);
		}
	}

	/// <summary>「不消耗」模式下空过：用掉本旬剩下的全部行动点，进入下一旬。</summary>
	internal static IEnumerator SpendAll(FreeManager free)
	{
		Bypass = true;
		try
		{
			yield return free.UpdateActionValue(-PlayerStatManagerData.Instance.CurrentActionCount);
		}
		finally
		{
			Bypass = false;
		}
	}
}
