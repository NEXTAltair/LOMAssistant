using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using LOMAssistant.Config;
using Mortal.Core;

namespace LOMAssistant.Features.Extras;

/// <summary>
/// 修炼 / 锻造 / 炼丹 相关修改（参考风灵月影）：
/// 无限：本次可用次数（_currentCount）保持为 99，武学点/锻造点/炼丹点补满到上限。
/// （以前的「快速：投 1 点直接升级」游戏自己已经支持了，删掉了。）开关在「设置」页，即时生效。
/// </summary>
public static class TrainingCheatPatch
{
	private const int UnlimitedCount = 99;

	// ---- 修炼（MartialPanel / Book） ----

	[HarmonyPatch]
	public static class MartialCountPatch
	{
		public static IEnumerable<MethodBase> TargetMethods()
		{
			foreach (string name in new[] { "UpdateMartialPointInfo", "LearnButtonClick", "LearnNextButtonClick", "HandleLearnItem", "HandleLearnNextItem" })
			{
				MethodInfo m = AccessTools.Method(typeof(MartialPanel), name);
				if (m != null)
				{
					yield return m;
				}
			}
		}

		public static void Prefix(MartialPanel __instance)
		{
			if (ConfigManager.CheatUnlimitedTraining.Value)
			{
				var tr = Traverse.Create(__instance);
				Refill(tr.Field("_currentCount"));
				TopUp(tr.Field("_martialPoint").GetValue<GameStat>());
			}
		}
	}

	// ---- 锻造 / 炼丹（UpgradePanel / UpgradeItemData） ----

	[HarmonyPatch]
	public static class UpgradeCountPatch
	{
		public static IEnumerable<MethodBase> TargetMethods()
		{
			foreach (string name in new[] { "UpdateStatus", "UpdateButton", "UpgradeButtonClick", "NextLevelButtonClick", "HandleUpgradeItem", "HandleUpgradeNextItem" })
			{
				MethodInfo m = AccessTools.Method(typeof(UpgradePanel), name);
				if (m != null)
				{
					yield return m;
				}
			}
		}

		public static void Prefix(UpgradePanel __instance)
		{
			if (ConfigManager.CheatUnlimitedUpgrade.Value)
			{
				var tr = Traverse.Create(__instance);
				Refill(tr.Field("_currentCount"));
				TopUp(tr.Field("_pointData").GetValue<GameStat>());
			}
		}
	}

	// 锻造 / 炼丹不消耗银两：花费读取的都是 CostMoney，返回 0 即不扣钱、不受银两限制。
	[HarmonyPostfix]
	[HarmonyPatch(typeof(UpgradeItemData), "CostMoney", MethodType.Getter)]
	public static void UpgradeItemData_CostMoney_Postfix(ref int __result)
	{
		if (ConfigManager.CheatFreeUpgradeMoney.Value)
		{
			__result = 0;
		}
	}

	private static void Refill(Traverse field)
	{
		if (field.GetValue<int>() < UnlimitedCount)
		{
			field.SetValue(UnlimitedCount);
		}
	}

	// 武学点 / 锻造点 / 炼丹点：补满到该数值的上限。
	private static void TopUp(GameStat stat)
	{
		if (stat != null && stat.Value < stat.Max)
		{
			stat.SetValue(stat.Max);
		}
	}
}
