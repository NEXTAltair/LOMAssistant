using HarmonyLib;
using LOMAssistant.Config;
using Mortal.Combat;

namespace LOMAssistant.Features.Extras;

/// <summary>
/// 决斗（回合制战斗）修改：所有体力变化都经过 CombatActionController.ModifyHealth（负数为伤害），
/// 飘字经过 CombatManager.DisplayHealth。开关在 F8 面板中切换，即时生效。
/// </summary>
public static class DuelCheatPatch
{
	[HarmonyPrefix]
	[HarmonyPatch(typeof(CombatActionController), "ModifyHealth")]
	public static void ModifyHealth_Prefix(CombatActionController __instance, ref int value)
	{
		if (value < 0 && Blocked(__instance))
		{
			value = 0;
		}
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(CombatManager), "DisplayHealth")]
	public static void DisplayHealth_Prefix(ref int health, CombatActionController target)
	{
		if (health < 0 && Blocked(target))
		{
			health = 0;
		}
	}

	/// <summary>Should damage to this side be cancelled?</summary>
	private static bool Blocked(CombatActionController target)
	{
		CombatManager manager = CombatManager.Instance;
		if (manager == null || target == null)
		{
			return false;
		}
		if (target == manager.PlayerAction)
		{
			return ConfigManager.CheatDuelInvincible.Value;
		}
		if (target == manager.EnemyAction)
		{
			return ConfigManager.CheatDuelNoDamage.Value;
		}
		return false;
	}
}
