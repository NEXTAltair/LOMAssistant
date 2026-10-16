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

	private static void Apply(Traverse field)
	{
		int count = ConfigManager.SaveSlotCount.Value;
		if (count > field.GetValue<int>())
		{
			field.SetValue(count);
		}
	}
}
