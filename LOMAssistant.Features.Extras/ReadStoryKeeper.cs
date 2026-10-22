using System.Collections.Generic;
using HarmonyLib;
using Mortal.Core;

namespace LOMAssistant.Features.Extras;

/// <summary>
/// 已读记录（对话变黄）只增不减：已读记录只在内存里，切场景时游戏会从磁盘的全局存档重新读一遍，
/// 读档路径没先把内存里的存下来时（原版读自动存档、最近存档，以及随时存档等），这一段刚读过的对话就又成了白字。
/// 这里记下本次开游戏以来登记过的每一句，游戏从磁盘读回已读记录后再补回去。
/// </summary>
public static class ReadStoryKeeper
{
	private static readonly HashSet<string> Session = new HashSet<string>();

	[HarmonyPostfix]
	[HarmonyPatch(typeof(ReadStorySystem), nameof(ReadStorySystem.AddKey))]
	public static void AddKey_Postfix(string key)
	{
		if (key != null)
		{
			Session.Add(key);
		}
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(ReadStorySystem), nameof(ReadStorySystem.Load))]
	public static void Load_Postfix(ReadStorySystem __instance)
	{
		int restored = 0;
		foreach (string key in Session)
		{
			if (!__instance.ExistKey(key))
			{
				__instance.AddKey(key);
				restored++;
			}
		}
		if (restored > 0)
		{
			FileLogger.Log($"[已读记录] 从磁盘读回全局存档时少了 {restored} 句本次读过的对话，已补回");
		}
	}
}
