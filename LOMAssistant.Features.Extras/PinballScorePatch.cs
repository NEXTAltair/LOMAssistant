using HarmonyLib;
using LOMAssistant.Config;
using Mortal.Pinball;
using UnityEngine;

namespace LOMAssistant.Features.Extras;

/// <summary>
/// 弹珠小游戏分数自定义：
/// PinballScoreMultiplier 放大每次得分（扣分不受影响）；
/// PinballFixedScore >= 0 时，离开小游戏时把总分直接改成该值（写入剧情用的分数旗标）。
/// </summary>
public static class PinballScorePatch
{
	[HarmonyPrefix]
	[HarmonyPatch(typeof(PinballManager), "AddScore")]
	public static void AddScore_Prefix(ref int score)
	{
		float multiplier = ConfigManager.PinballScoreMultiplier.Value;
		if (score > 0 && multiplier >= 0f && !Mathf.Approximately(multiplier, 1f))
		{
			score = Mathf.RoundToInt(score * multiplier);
		}
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(PinballManager), "ExitScene")]
	public static void ExitScene_Prefix(PinballManager __instance)
	{
		int fixedScore = ConfigManager.PinballFixedScore.Value;
		var tr = Traverse.Create(__instance);
		int total = tr.Field("_totalScore").GetValue<int>();
		if (fixedScore >= 0)
		{
			tr.Field("_totalScore").SetValue(fixedScore);
			tr.Method("UpdateTotalText").GetValue();
		}
		string msg = fixedScore >= 0
			? $"弹珠分数：总分 {total} -> {fixedScore}（固定分数）"
			: $"弹珠分数：总分 {total}";
		LOMAssistantPlugin.LogInfo(msg);
		FileLogger.Log(msg);
	}
}
