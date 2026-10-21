using System.Collections.Generic;
using HarmonyLib;
using LOMAssistant.Config;
using Mortal.Core;

namespace LOMAssistant.Features.Extras;

/// <summary>
/// 突破属性上限（设置页「养成与战斗」，默认关）：体力、内力、心相等属性原本最高 100，打开后上限改成 9999。
/// 游戏加值、设值、算最终值都按 GameStat 的上限截断，这里直接把这些属性的上限抬高；上限是游戏数据，不进存档，
/// 所以每次读档前（不然存档里超过 100 的值读进来又被截回去）、新开局后、开关变化时都重新套一次。关掉就改回原来的上限。
/// </summary>
public static class StatLimitPatch
{
	private const int RaisedMax = 9999;

	private static readonly GameStatType[] Types =
	{
		GameStatType.體力, GameStatType.內力, GameStatType.輕功, GameStatType.學問, GameStatType.嘴力, GameStatType.心理衛生,
		GameStatType.性情, GameStatType.處世, GameStatType.修養, GameStatType.道德, GameStatType.陰陽內功, GameStatType.命運,
		GameStatType.魅力, GameStatType.廚藝, GameStatType.抗毒, GameStatType.抗麻,
	};

	/// <summary>每个属性原来的上限（第一次抬高前记下，关掉时改回去）。</summary>
	private static readonly Dictionary<GameStat, int> Original = new Dictionary<GameStat, int>();

	public static void Apply()
	{
		PlayerStatManagerData player = PlayerStatManagerData.Instance;
		if (player == null || player.Stats == null)
		{
			return;
		}
		bool raise = ConfigManager.BreakStatLimit.Value;
		foreach (GameStatType type in Types)
		{
			GameStat stat;
			try
			{
				stat = player.Stats.Get(type);
			}
			catch
			{
				continue;
			}
			if (stat == null)
			{
				continue;
			}
			if (raise)
			{
				if (!Original.ContainsKey(stat))
				{
					Original[stat] = stat.Max;
				}
				stat.SetMax(RaisedMax);
			}
			else if (Original.TryGetValue(stat, out int max))
			{
				stat.SetMax(max);
			}
		}
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(PlayerStatManagerData), "Load")]
	public static void Load_Prefix() => Apply();

	[HarmonyPostfix]
	[HarmonyPatch(typeof(PlayerStatManagerData), "ResetData")]
	public static void ResetData_Postfix() => Apply();
}
