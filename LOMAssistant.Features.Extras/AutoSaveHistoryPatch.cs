using System;
using System.IO;
using HarmonyLib;
using LOMAssistant.Config;
using Mortal.Core;

namespace LOMAssistant.Features.Extras;

/// <summary>
/// 自动存档历史：游戏每类自动存档（剧情 auto / 养成 auto_free / 战斗 auto_battle）只有一个文件，
/// 新的会覆盖旧的。这里在覆盖前把旧文件依次挪到 Save_{类别}_1.dat … _N.dat（1 为最近），
/// 读档界面每张自动存档卡片右侧的 ▲/▼ 按钮可以翻看并读取这些旧档。
/// </summary>
public static class AutoSaveHistoryPatch
{
	public static readonly string[] Categories = { "auto_free", "auto", "auto_battle" };

	public static int MaxHistory => Math.Max(0, Math.Min(ConfigManager.AutoSaveHistory.Value, 50));

	public static string HistorySlot(string category, int index) => category + "_" + index;

	[HarmonyPrefix]
	[HarmonyPatch(typeof(SaveSystem), "AutoSaveData")]
	public static void SaveSystem_AutoSaveData_Prefix(SaveSystem __instance, string slot)
	{
		int max = MaxHistory;
		if (max <= 0 || Array.IndexOf(Categories, slot) < 0)
		{
			return;
		}
		try
		{
			string current = GetFile(__instance, slot);
			if (!File.Exists(current) || IsSamePoint(__instance, slot))
			{
				return;
			}
			// Shift _1.._(max-1) up by one (dropping the oldest), then the current file becomes _1.
			for (int i = max; i >= 1; i--)
			{
				string from = i == 1 ? current : GetFile(__instance, HistorySlot(slot, i - 1));
				string to = GetFile(__instance, HistorySlot(slot, i));
				if (!File.Exists(from))
				{
					continue;
				}
				if (i == 1)
				{
					File.Copy(from, to, overwrite: true);
				}
				else
				{
					if (File.Exists(to))
					{
						File.Delete(to);
					}
					File.Move(from, to);
				}
			}
			FileLogger.Log($"自动存档历史：{slot} 已保留旧档（最多 {max} 个）");
		}
		catch (Exception e)
		{
			LOMAssistantPlugin.LogError($"自动存档历史：{slot} 备份失败: {e.Message}");
		}
	}

	/// <summary>
	/// The game autosaves again whenever the scene reloads at the same point (e.g. re-entering
	/// the hub with full actions); don't push those duplicates into the history.
	/// </summary>
	private static bool IsSamePoint(SaveSystem system, string slot)
	{
		AutoGameSave old = system.GetAutoSaveData(slot);
		GameSave now = Traverse.Create(system).Method("CreateSaveData").GetValue<GameSave>();
		if (old?.GameSave == null || now == null)
		{
			return false;
		}
		GameSave o = old.GameSave;
		return o.CurrentYear == now.CurrentYear && o.CurrentMonth == now.CurrentMonth && o.CurrentStage == now.CurrentStage
			&& o.CurrentScene == now.CurrentScene && o.CurrentStoryScript == now.CurrentStoryScript;
	}

	private static string GetFile(SaveSystem system, string slot)
	{
		return Traverse.Create(system).Method("GetSaveDataFile", new[] { typeof(string) }).GetValue<string>(slot);
	}

	// ---- 读档界面：自动存档卡片的 ▲/▼ 按钮 ----

	[HarmonyPostfix]
	[HarmonyPatch(typeof(AutoSaveSlotPanel), "Awake")]
	public static void AutoSaveSlotPanel_Awake_Postfix(AutoSaveSlotPanel __instance)
	{
		string slot = Traverse.Create(__instance).Field("_slot").GetValue<string>();
		if (MaxHistory > 0 && Array.IndexOf(Categories, slot) >= 0)
		{
			try
			{
				AutoSaveCardHistory.Attach(__instance, slot);
			}
			catch (Exception e)
			{
				LOMAssistantPlugin.LogError("自动存档历史：添加 ▲/▼ 按钮失败: " + e);
			}
		}
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(AutoSaveSlotPanel), "Setup")]
	public static void AutoSaveSlotPanel_Setup_Postfix(AutoSaveSlotPanel __instance)
	{
		__instance.GetComponent<AutoSaveCardHistory>()?.Refresh();
	}

	// Older autosaves show “｜旧档N” after the title.
	[HarmonyPostfix]
	[HarmonyPatch(typeof(AutoSaveSlotPanel), "SetupText")]
	public static void AutoSaveSlotPanel_SetupText_Postfix(AutoSaveSlotPanel __instance, GameSave data)
	{
		var tr = Traverse.Create(__instance);
		string slot = tr.Field("_slot").GetValue<string>();
		int sep = slot?.LastIndexOf('_') ?? -1;
		if (data == null || sep < 0 || !int.TryParse(slot.Substring(sep + 1), out int index) || Array.IndexOf(Categories, slot.Substring(0, sep)) < 0)
		{
			return;
		}
		foreach (UnityEngine.UI.Text t in tr.Field("_titleText").GetValue<UnityEngine.UI.Text[]>() ?? new UnityEngine.UI.Text[0])
		{
			t.text += "｜旧档" + index;
		}
	}

	// Every time the load screen opens, cards start from the newest autosave again.
	[HarmonyPrefix]
	[HarmonyPatch(typeof(LoadGamePanel), "OnPanelOpen")]
	public static void LoadGamePanel_OnPanelOpen_Prefix(LoadGamePanel __instance)
	{
		foreach (AutoSaveSlotPanel card in Traverse.Create(__instance).Field("_autoSaveSlot").GetValue<AutoSaveSlotPanel[]>() ?? new AutoSaveSlotPanel[0])
		{
			card?.GetComponent<AutoSaveCardHistory>()?.ResetToLatest();
		}
	}
}
