using HarmonyLib;
using Mortal.Core;
using UnityEngine;

namespace LOMAssistant.Features.Extras;

/// <summary>
/// 防止读档被连点两次。游戏的自动存档、最近存档卡片读档前不检查是否已经在读档，
/// 点击被触发两次时会同时跑两个读档流程，把声音管理器等常驻对象弄坏，一直卡在读取中。
/// 这里在读档开始后 3 秒内，或场景正在切换时，忽略再来的读档点击。
/// </summary>
public static class LoadClickGuard
{
	private const float Window = 3f;
	private static float _lastLoad = -100f;

	[HarmonyPrefix]
	[HarmonyPatch(typeof(AutoSaveSlotPanel), "OnTitleClick")]
	public static bool AutoSave_Prefix() => Allow("自动存档");

	[HarmonyPrefix]
	[HarmonyPatch(typeof(RecentSaveSlotPanel), "OnTitleClick")]
	public static bool RecentSave_Prefix() => Allow("最近存档");

	[HarmonyPrefix]
	[HarmonyPatch(typeof(LoadSlotPanel), "OnTitleClick")]
	public static bool LoadSlot_Prefix() => Allow("手动存档");

	// 已读记录（对话变黄）只在内存里，进场景时游戏会从磁盘重新读一遍全局存档。原版读手动存档前会先存一次全局存档，
	// 读自动存档、最近存档却不存，这一段刚读过的对话读完档又都成了白字。这里补上同样的一次保存。
	[HarmonyPostfix]
	[HarmonyPatch(typeof(AutoSaveSlotPanel), "OnTitleClick")]
	public static void AutoSave_Postfix(bool __runOriginal) => SaveReadStory(__runOriginal);

	[HarmonyPostfix]
	[HarmonyPatch(typeof(RecentSaveSlotPanel), "OnTitleClick")]
	public static void RecentSave_Postfix(bool __runOriginal) => SaveReadStory(__runOriginal);

	private static void SaveReadStory(bool loaded)
	{
		if (loaded && SaveSystem.Instance != null)
		{
			SaveSystem.Instance.SaveUniverseData();
		}
	}

	private static bool Allow(string kind)
	{
		// 存档备注框开着或刚关掉：这一下是点在备注框的按钮上，不是要读档
		if (SaveNoteEditor.IsOpen || Time.unscaledTime - SaveNoteEditor.ClosedAt < 0.5f)
		{
			FileLogger.Log($"读档：忽略{kind}读档点击（刚在写存档备注）");
			return false;
		}
		SceneController scenes = SceneController.Instance;
		bool busy = scenes != null && (scenes.IsPrepare || scenes.IsLoading);
		if (busy || Time.realtimeSinceStartup - _lastLoad < Window)
		{
			FileLogger.Log($"读档：忽略重复的{kind}读档点击（正在读档）");
			return false;
		}
		_lastLoad = Time.realtimeSinceStartup;
		return true;
	}
}
