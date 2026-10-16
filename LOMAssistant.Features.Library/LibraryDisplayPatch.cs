using HarmonyLib;
using LOMAssistant.Config;
using Mortal.Core;
using UnityEngine.UI;

namespace LOMAssistant.Features.Library;

public static class LibraryDisplayPatch
{
	private const int UnlockValue = 7777777;

	[HarmonyPrefix]
	[HarmonyPatch(typeof(LibraryAchievementItemPanel), "Setup")]
	[HarmonyPatch(typeof(LibraryEndItemPanel), "Setup")]
	[HarmonyPatch(typeof(LibraryDeadItemPanel), "Setup")]
	public static void Setup_Prefix(LibraryItemData data)
	{
		if (ConfigManager.EnableLibraryDisplay.Value && data.Count == 0)
		{
			data.Set(7777777);
		}
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(LibraryAchievementItemPanel), "Setup")]
	[HarmonyPatch(typeof(LibraryEndItemPanel), "Setup")]
	[HarmonyPatch(typeof(LibraryDeadItemPanel), "Setup")]
	public static void Setup_Postfix(LibraryItemPanel __instance, LibraryItemData data)
	{
		if (ConfigManager.EnableLibraryDisplay.Value)
		{
			Text text = null;
			if (__instance is LibraryEndItemPanel)
			{
				text = Traverse.Create(__instance).Field("_index").GetValue<Text>();
			}
			else if (__instance is LibraryAchievementItemPanel)
			{
				text = Traverse.Create(__instance).Field("_title").GetValue<Text>();
			}
			if (data.Count == 0 && text != null)
			{
				text.text += "(未解锁)";
			}
			FileLogger.Log("讲经堂显示：Setup_Postfix 被调用，data=" + ((data != null) ? data.name : "null"));
		}
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(LibraryItemPanel), "UpdateContent")]
	public static void UpdateContent_Prefix(LibraryItemPanel __instance)
	{
		if (ConfigManager.EnableLibraryDisplay.Value)
		{
			LibraryItemData value = Traverse.Create(__instance).Field("_data").GetValue<LibraryItemData>();
			if (value != null && value.Count == 7777777)
			{
				value.Reset();
			}
		}
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(LibraryItemPanel), "UpdateData")]
	public static void UpdateData_Prefix(LibraryItemPanel __instance)
	{
		if (ConfigManager.EnableLibraryDisplay.Value)
		{
			LibraryItemData value = Traverse.Create(__instance).Field("_data").GetValue<LibraryItemData>();
			if (value != null && value.Count == 0)
			{
				value.Set(7777777);
			}
		}
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(LibraryEndItemPanel), "OnClick")]
	public static bool OnClick_Prefix(LibraryItemPanel __instance)
	{
		if (!ConfigManager.EnableLibraryDisplay.Value)
		{
			return true;
		}
		LibraryItemEvent value = Traverse.Create(__instance).Field("_clickEvent").GetValue<LibraryItemEvent>();
		LibraryItemData value2 = Traverse.Create(__instance).Field("_data").GetValue<LibraryItemData>();
		if (value != null)
		{
			value.Raise(value2);
			LOMAssistantPlugin.LogInfo("讲经堂显示：点击结局项 " + ((value2 != null) ? value2.name : "null"));
			FileLogger.Log("讲经堂显示：点击结局项 " + ((value2 != null) ? value2.name : "null"));
		}
		return false;
	}
}
