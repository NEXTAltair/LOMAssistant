using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Mortal.Combat;
using Mortal.Core;
using OBB.Framework.Extensions;

namespace LOMAssistant.Features.Combat;

/// <summary>
/// 决斗指令的说明框里写出这一招叠着哪几门功法。好几门功法都改同一个指令（比如都改「捅人」）时，
/// 它们的效果全都生效、叠加计算，只是按钮上只显示一个名字（动画、气力消耗也只用一门的），看着像别的功法用不出来。
/// 鼠标停在指令上、手柄选中指令时说明框弹出，弹出前把这一行加在游戏原本的说明后面。
/// </summary>
public static class CombatStackTipPatch
{
	private static readonly AccessTools.FieldRef<CombatActionController, List<CombatStateEffect>> Effects =
		AccessTools.FieldRefAccess<CombatActionController, List<CombatStateEffect>>("_effects");
	private static readonly AccessTools.FieldRef<C_Effect_100, CombatActionType> ActionType =
		AccessTools.FieldRefAccess<C_Effect_100, CombatActionType>("_actionType");
	private static readonly AccessTools.FieldRef<C_Effect_100, string> NameKey =
		AccessTools.FieldRefAccess<C_Effect_100, string>("_name");

	// 指令按钮在面板上的字段名，和它对应的指令（绝招按钮显示的是第一级绝招的改名）
	private static readonly (string Field, CombatActionType Type)[] Buttons =
	{
		("_talkingButton", CombatActionType.嘴砲),
		("_attackButton", CombatActionType.捅人),
		("_weaponButton", CombatActionType.暗器),
		("_blockButton", CombatActionType.備揍),
		("_ultimateButton", CombatActionType.絕招1),
	};

	[HarmonyPrefix]
	[HarmonyPatch(typeof(CombatActionButton), nameof(CombatActionButton.ShowTip))]
	public static void ShowTip_Prefix(CombatActionButton __instance)
	{
		try
		{
			CombatActionButtonPanel panel = __instance.GetComponentInParent<CombatActionButtonPanel>();
			if (panel == null)
			{
				return;
			}
			var traverse = Traverse.Create(panel);
			CombatActionType? type = null;
			foreach (var (field, t) in Buttons)
			{
				if (traverse.Field(field).GetValue<CombatActionButton>() == __instance)
				{
					type = t;
				}
			}
			var player = traverse.Field("_playerAction").GetValue<CombatActionController>();
			if (type == null || player == null)
			{
				return;
			}
			string tip = LocalizationManager.Instance.LocaleResolver.GetString("CombatInfo/" + type.Value.GetStringValue());
			List<string> arts = StackedArts(player, type.Value);
			if (arts.Count >= 2)
			{
				// 说明框是浅色底，用深绿色，和上面的黑字、红字分开又看得清
				tip += "\n<color=#2F6B3A>" + TextConverter.ForDisplay("同时生效的功法：")
					+ string.Join(TextConverter.ForDisplay("、"), arts.ToArray()) + "</color>";
			}
			__instance.SetTipText(tip);
		}
		catch (Exception ex)
		{
			FileLogger.LogWarning("[功法叠加提示] " + ex.Message);
		}
	}

	/// <summary>我方身上改这个指令名字的功法（按挂上的先后），名字按游戏语言。</summary>
	private static List<string> StackedArts(CombatActionController player, CombatActionType type)
	{
		var names = new List<string>();
		foreach (CombatStateEffect effect in Effects(player) ?? new List<CombatStateEffect>())
		{
			if (effect == null || effect.IsRemove || effect.Disable)
			{
				continue;
			}
			foreach (C_Effect_100 unit in effect.Effects.OfType<C_Effect_100>())
			{
				if (ActionType(unit) != type || string.IsNullOrEmpty(NameKey(unit)))
				{
					continue;
				}
				string name = LocalizationManager.Instance.LocaleResolver.GetString("CombatAction/" + NameKey(unit));
				if (!string.IsNullOrEmpty(name) && !names.Contains(name))
				{
					names.Add(name);
				}
			}
		}
		return names;
	}
}
