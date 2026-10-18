using System.Collections.Generic;
using HarmonyLib;
using LOMAssistant.Config;
using Mortal.Core;

namespace LOMAssistant.Features.Extras;

/// <summary>
/// 增加手动存档栏位：存档/读档界面在 Awake 时按 _slotCount（原版 20）生成栏位，
/// 存档文件名为 Save_{n}.dat，没有其他上限，所以只需在生成前改大数量。
/// </summary>
public static class SaveSlotPatch
{
	[HarmonyPrefix]
	[HarmonyPatch(typeof(SaveGamePanel), "Awake")]
	public static void SaveGamePanel_Awake_Prefix(SaveGamePanel __instance)
	{
		Apply(Traverse.Create(__instance).Field("_slotCount"));
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(LoadGamePanel), "Awake")]
	public static void LoadGamePanel_Awake_Prefix(LoadGamePanel __instance)
	{
		Apply(Traverse.Create(__instance).Field("_slotCount"));
	}

	// ---- 延迟建栏位 ----
	// 游戏每次加载场景都会在 Awake 里把存档、读档面板的所有栏位实例化一遍（100 个栏位每块面板约 0.15 秒，
	// 一次切场景要建好几块，玩家感觉剧情节点之间会卡）。而打开面板时 OnPanelOpen → UpdateSlot 本来就会把每个栏位刷新一遍，
	// 所以 Awake 时先不建，第一次用到时再建。

	private static readonly HashSet<object> Pending = new HashSet<object>();

	private static bool _building;

	[HarmonyPrefix]
	[HarmonyPatch(typeof(SaveGamePanel), "CreateSlot")]
	public static bool SaveGamePanel_CreateSlot_Prefix(SaveGamePanel __instance) => Defer(__instance);

	[HarmonyPrefix]
	[HarmonyPatch(typeof(SaveGamePanel), "SetButtonNavigation")]
	public static bool SaveGamePanel_SetButtonNavigation_Prefix(SaveGamePanel __instance) => _building || !Pending.Contains(__instance);

	[HarmonyPrefix]
	[HarmonyPatch(typeof(LoadGamePanel), "CreateSlot")]
	public static bool LoadGamePanel_CreateSlot_Prefix(LoadGamePanel __instance) => Defer(__instance);

	[HarmonyPrefix]
	[HarmonyPatch(typeof(LoadGamePanel), "SetButtonNavigation")]
	public static bool LoadGamePanel_SetButtonNavigation_Prefix(LoadGamePanel __instance) => _building || !Pending.Contains(__instance);

	[HarmonyPrefix]
	[HarmonyPatch(typeof(SaveGamePanel), "OnPanelOpen")]
	public static void SaveGamePanel_OnPanelOpen_Prefix(SaveGamePanel __instance) => Ensure(__instance);

	[HarmonyPrefix]
	[HarmonyPatch(typeof(SaveGamePanel), "UpdateSlot")]
	public static void SaveGamePanel_UpdateSlot_Prefix(SaveGamePanel __instance) => Ensure(__instance);

	[HarmonyPrefix]
	[HarmonyPatch(typeof(LoadGamePanel), "OnPanelOpen")]
	public static void LoadGamePanel_OnPanelOpen_Prefix(LoadGamePanel __instance) => Ensure(__instance);

	[HarmonyPrefix]
	[HarmonyPatch(typeof(LoadGamePanel), "UpdateSlot")]
	public static void LoadGamePanel_UpdateSlot_Prefix(LoadGamePanel __instance) => Ensure(__instance);

	private static bool Defer(object panel)
	{
		if (_building)
		{
			return true;
		}
		// 只推迟 Awake 里的第一次；之后（比如 Ensure 里）照常建
		if (Traverse.Create(panel).Field("_saveSlots").GetValue() == null && !Pending.Contains(panel))
		{
			Pending.RemoveWhere(p => p == null || p.Equals(null));
			Pending.Add(panel);
			return false;
		}
		return true;
	}

	private static void Ensure(object panel)
	{
		if (!Pending.Remove(panel))
		{
			return;
		}
		_building = true;
		try
		{
			var tr = Traverse.Create(panel);
			tr.Method("CreateSlot").GetValue();
			tr.Method("SetButtonNavigation").GetValue();
		}
		finally
		{
			_building = false;
		}
	}

	private static void Apply(Traverse field)
	{
		int count = ConfigManager.SaveSlotCount.Value;
		if (count > field.GetValue<int>())
		{
			field.SetValue(count);
		}
	}
}
