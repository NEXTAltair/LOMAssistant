using System;
using System.IO;
using System.Text;
using HarmonyLib;
using Mortal.Core;
using UnityEngine.UI;

namespace LOMAssistant.Features.Extras;

/// <summary>
/// 存档备注：每个手动存档旁边存一个 Save_XXX.dat.note（UTF-8 文本），会随云存档一起同步。
/// 存档/读档界面的标题行显示为“第X年X月X旬｜备注”；在存档/读档界面按 F8，可给所选存档写备注。
/// </summary>
public static class SaveNotePatch
{
	private const string Separator = "｜";

	// slot -> note, so the F8 panel doesn't re-read the file on every GUI event.
	private static readonly System.Collections.Generic.Dictionary<string, string> Cache = new System.Collections.Generic.Dictionary<string, string>();

	public static string GetNote(string slot)
	{
		if (slot != null && Cache.TryGetValue(slot, out string cached))
		{
			return cached;
		}
		try
		{
			string path = GetNotePath(slot);
			string note = path != null && File.Exists(path) ? File.ReadAllText(path, Encoding.UTF8).Trim() : "";
			if (path != null)
			{
				Cache[slot] = note;
			}
			return note;
		}
		catch (Exception e)
		{
			FileLogger.LogError($"存档备注：读取 {slot} 失败: {e.Message}");
			return "";
		}
	}

	public static void SetNote(string slot, string note)
	{
		try
		{
			string path = GetNotePath(slot);
			if (path == null)
			{
				return;
			}
			note = (note ?? "").Trim();
			if (note.Length == 0)
			{
				if (File.Exists(path))
				{
					File.Delete(path);
				}
			}
			else
			{
				File.WriteAllText(path, note, new UTF8Encoding(false));
			}
			Cache[slot] = note;
			FileLogger.Log($"存档备注：{slot} -> {note}");
		}
		catch (Exception e)
		{
			LOMAssistantPlugin.LogError($"存档备注：保存 {slot} 失败: {e.Message}");
		}
	}

	/// <summary>Only numbered manual slots ("001"...) get notes.</summary>
	public static bool IsManualSlot(string slot)
	{
		return !string.IsNullOrEmpty(slot) && int.TryParse(slot, out _);
	}

	private static string GetNotePath(string slot)
	{
		if (!IsManualSlot(slot) || SaveSystem.Instance == null)
		{
			return null;
		}
		string dat = Traverse.Create(SaveSystem.Instance).Method("GetSaveDataFile", new[] { typeof(string) }).GetValue<string>(slot);
		return dat + ".note";
	}

	// ---- 显示：在各存档栏位的标题后追加备注 ----

	[HarmonyPostfix]
	[HarmonyPatch(typeof(SaveSlotPanel), "SetupText")]
	public static void SaveSlotPanel_SetupText_Postfix(SaveSlotPanel __instance, GameSave data)
	{
		AppendNote(__instance, data);
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(LoadSlotPanel), "SetupText")]
	public static void LoadSlotPanel_SetupText_Postfix(LoadSlotPanel __instance, GameSave data)
	{
		AppendNote(__instance, data);
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(RecentSaveSlotPanel), "SetupText")]
	public static void RecentSaveSlotPanel_SetupText_Postfix(RecentSaveSlotPanel __instance, GameSave data)
	{
		AppendNote(__instance, data);
	}

	private static void AppendNote(object panel, GameSave data)
	{
		if (data == null)
		{
			return;
		}
		var tr = Traverse.Create(panel);
		string note = GetNote(tr.Field("_slot").GetValue<string>());
		if (note.Length == 0)
		{
			return;
		}
		Text[] titles = tr.Field("_titleText").GetValue<Text[]>();
		if (titles == null)
		{
			return;
		}
		foreach (Text t in titles)
		{
			if (t != null && !t.text.Contains(Separator))
			{
				t.text = t.text + Separator + note;
			}
		}
	}

	// ---- 删除存档时一并删除备注 ----

	[HarmonyPostfix]
	[HarmonyPatch(typeof(SaveSystem), "DeleteSaveData", new[] { typeof(string) })]
	public static void SaveSystem_DeleteSaveData_Postfix(string slot)
	{
		SetNote(slot, "");
	}
}
