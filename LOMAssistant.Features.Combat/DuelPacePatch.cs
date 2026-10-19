using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using LOMAssistant.Config;
using Mortal.Combat;
using Mortal.Core;
using UnityEngine;

namespace LOMAssistant.Features.Combat;

/// <summary>
/// 决斗回合之间的等待缩短（移植自赵活系统 0.3.37，Toufff，MIT）。
/// 每回合所有状态（buff）的每个效果单元都要走一遍 Init、ApplyBeforeAction 等步骤，没写这些步骤的单元在基类里只是等一帧；
/// 状态、单元、关卡事件又是一层套一层的协程，Unity 在嵌套协程结束后还要再等一帧才继续。帧数不受加速影响，
/// 身上状态一多，回合之间就要等好几秒。这里让空步骤立即结束、嵌套协程就地跑完，只有真正的等待（动画、延时、点击）才让出一帧；
/// 效果、数值和结算顺序都不变。
/// </summary>
internal static class DuelPacePatch
{
	private static bool On => ConfigManager.EnableFastDuelRounds.Value;

	[HarmonyPrefix]
	[HarmonyPatch(typeof(CombatStateEffectUnitScriptable), "Init")]
	[HarmonyPatch(typeof(CombatStateEffectUnitScriptable), "ApplyBeforeAction")]
	[HarmonyPatch(typeof(CombatStateEffectUnitScriptable), "ApplyInitAction")]
	[HarmonyPatch(typeof(CombatStateEffectUnitScriptable), "ApplyAfterAction")]
	[HarmonyPatch(typeof(CombatStateEffectUnitScriptable), "ApplyBeforeResult")]
	[HarmonyPatch(typeof(CombatStateEffectUnitScriptable), "ApplyAfterResult")]
	[HarmonyPatch(typeof(CombatStateEffectUnitScriptable), "TriggerAfterAttacked")]
	[HarmonyPatch(typeof(CombatStateEffectUnitScriptable), "TriggerDuringAttack")]
	[HarmonyPatch(typeof(CombatStateEffectUnitScriptable), "Remove")]
	[HarmonyPatch(typeof(CombatStateEffectUnitScriptable), "OnStateEffectRemoved")]
	[HarmonyPatch(typeof(CombatStateEffectUI), "Cast")]
	[HarmonyPatch(typeof(CombatStateEffectUI), "CastFinish")]
	public static bool NoIdleFrame(ref IEnumerator __result)
	{
		if (!On)
		{
			return true;
		}
		__result = Nothing();
		return false;
	}

	// 回合开始和每次出招的协程：嵌套的协程就地跑完
	[HarmonyPostfix]
	[HarmonyPatch(typeof(CombatManager), "InitCombatRound")]
	[HarmonyPatch(typeof(CombatManager), "StartAction")]
	public static void Flat(ref IEnumerator __result)
	{
		if (On && __result != null)
		{
			__result = Flatten(__result);
		}
	}

	// 约五十个效果每一步之后都把 buff 图标送回原位，没动过也要播一整段补间：只有真的移开了才送回
	[HarmonyPrefix]
	[HarmonyPatch(typeof(CombatBuffStateUI), "CastFinish")]
	public static bool BuffIconBack(CombatBuffStateUI __instance, ref IEnumerator __result)
	{
		if (!On)
		{
			return true;
		}
		try
		{
			MovePanel panel = __instance.GetFieldValue<MovePanel>("_movePanel");
			RectTransform rect = panel == null ? null : panel.GetFieldValue<RectTransform>("_rectTransform");
			if (rect == null || ((Vector2)panel.OriginAnchoredPosition - rect.anchoredPosition).sqrMagnitude > 0.01f)
			{
				return true;
			}
			__result = Nothing();
			return false;
		}
		catch (System.Exception ex)
		{
			FileLogger.LogWarning("[决斗加速] buff 图标判断失败，按原样播放: " + ex.Message);
			return true;
		}
	}

	private static IEnumerator Nothing()
	{
		yield break;
	}

	/// <summary>把协程和它 yield 出来的嵌套协程一口气跑完，只在真正的等待（一帧、几秒、单独启动的协程、等条件）时让出。</summary>
	private static IEnumerator Flatten(IEnumerator root)
	{
		var stack = new Stack<IEnumerator>();
		stack.Push(root);
		while (stack.Count > 0)
		{
			IEnumerator top = stack.Peek();
			if (!top.MoveNext())
			{
				stack.Pop();
				continue;
			}
			object current = top.Current;
			if (current is IEnumerator nested && !(current is CustomYieldInstruction))
			{
				stack.Push(nested);
				continue;
			}
			yield return current;
		}
	}
}
