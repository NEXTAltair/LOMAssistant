using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using BepInEx;
using Fungus;
using HarmonyLib;
using LOMAssistant.Config;
using LOMAssistant.LuaParse;
using Mortal.Core;
using Mortal.Free;
using Mortal.Story;

namespace LOMAssistant;

internal static class OptionInfoPatch
{
	private static string _originalDialogKey = string.Empty;

	private static PositionController _positionCtrl = null;

	public static PositionEventData TargetEvent = null;

	public static MissionCheckData TargetMission = null;

	[HarmonyPrefix]
	[HarmonyPatch(typeof(LuaEnvironment), "LoadLuaFunction")]
	public static void LuaEnvironment_LoadLuaFunction_Prefix(string luaString, string friendlyName)
	{
		try
		{
			if (friendlyName == "tmp.LuaScript")
			{
				return;
			}
			DicePlugin.Instance.ClearLogEntries();
			LuaParser.Instance.ClearCache();
			foreach (KeyValuePair<string, List<LOMAssistant.LuaParse.Action>> item in LuaParser.Instance.ExtractOptionActionsFromLua(friendlyName, luaString))
			{
				DicePlugin.Instance.AddOptionEntry(item.Key, LuaParser.Description(item.Value));
			}
			LuaParser.Instance.ExtractLockedOptions(luaString);
			foreach (string item2 in LuaParser.Instance.ExtractSwitchCheckFromLua(friendlyName, luaString))
			{
				string description = DataHelper.ParseSwitchCheckpoint(item2);
				DicePlugin.Instance.AddSwitchEntry(item2, description);
			}
			TargetEvent = null;
			List<string> list = LuaParser.Instance.ExtractPositionWorkFromLua(friendlyName, luaString);
			if (list.Count > 0)
			{
				DicePlugin.Instance.ClearEventEntries();
				foreach (string item3 in list)
				{
					foreach (PositionEventData item4 in PositionEventCache.Instance.GetPositionEventsSortedByRate(item3))
					{
						string positionEventName = PositionEventCache.Instance.GetPositionEventName(item4);
						string positionEventDesc = PositionEventCache.Instance.GetPositionEventDesc(item4);
						DicePlugin.Instance.AddEventEntry(item4, positionEventName, positionEventDesc);
					}
				}
			}
			else if (!friendlyName.StartsWith("tmp.") && !friendlyName.StartsWith("Flowchart.Say.ExecuteLua"))
			{
				DicePlugin.Instance.ClearEventEntries();
			}
			foreach (KeyValuePair<MissionCheckData, (string, string)> mission in DataHelper.GetMissions())
			{
				DicePlugin.Instance.AddMissionEntry(mission.Key, mission.Value.Item1, mission.Value.Item2);
			}
		}
		catch (Exception ex)
		{
			DicePlugin.LogError("LoadLuaFunction Prefix 解析失败，已跳过以保证对话加载: " + ex);
		}
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(LeanLocalizationResolver), "GetStoryText")]
	public static void LeanLocalizationResolver_GetStoryText_Postfix(string key, ref string __result)
	{
		if (!ConfigManager.EnableOptionResultDisplay.Value)
		{
			return;
		}
		try
		{
			// 被锁的选项（「解锁条件：？？？」「银两不足」这类）：写出真正的解锁条件和当前值，以及解锁后的效果
			if (LuaParser.Instance.LockedOptions.TryGetValue(key, out var locked))
			{
				string condition = Regex.Replace(DataHelper.ParseConditionCheckpoint(locked.Condition), "<.*?>", string.Empty);
				string effects = locked.Unlocked != null && LuaParser.Instance.OptionResult.TryGetValue(locked.Unlocked, out var unlockedActions)
					? Regex.Replace(TextConverter.ToSimplified(LuaParser.Description(unlockedActions)), "<.*?>", string.Empty)
					: "";
				if (!condition.IsNullOrWhiteSpace())
				{
					__result = __result + "\n<size=16>(需 " + condition + (effects.IsNullOrWhiteSpace() ? "" : "；解锁后：" + effects) + ")</size>";
				}
				return;
			}
			// 满足某个分支条件就直接接一段剧情：写成「若 条件：触发剧情」
			string jumps = "";
			if (LuaParser.Instance.OptionJumps.TryGetValue(key, out var jumpList))
			{
				foreach (var jump in jumpList)
				{
					string condition = Regex.Replace(DataHelper.SwitchBranchCondition(jump.Switch, jump.Branch), "<.*?>", string.Empty);
					if (!condition.IsNullOrWhiteSpace())
					{
						jumps += (jumps.Length > 0 ? "；" : "") + "若 " + condition + "：触发剧情";
					}
				}
			}
			if (LuaParser.Instance.OptionResult.TryGetValue(key, out var value))
			{
				string text = TextConverter.ToSimplified(LuaParser.Description(value));
				if (jumps.Length > 0)
				{
					text = text.IsNullOrWhiteSpace() ? jumps : text + " | " + jumps;
				}
				if (!text.IsNullOrWhiteSpace())
				{
					string text2 = Regex.Replace(text, "<.*?>", string.Empty);
					__result = __result + "\n<size=16>(" + text2 + ")</size>";
				}
			}
		}
		catch (Exception ex)
		{
			DicePlugin.LogError("GetStoryText Postfix 追加选项结果失败，已跳过以保证对话显示: " + ex);
		}
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(PositionController), "OnPositionEnter")]
	public static void PositionController_OnPositionEnter_Prefix(PositionController __instance)
	{
		PositionType fieldValue = __instance.GetFieldValue<PositionType>("_position");
		if (_positionCtrl != null)
		{
			HandleDialogChange(_positionCtrl, addSuffix: false);
		}
		if (HasTriggerSubMissions(fieldValue, out var missionKey))
		{
			HandleDialogChange(__instance, addSuffix: true, "存在事件：" + missionKey);
		}
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(PositionController), "OnPositionClick")]
	public static void PositionController_OnPositionClick_Prefix()
	{
		if (_positionCtrl != null)
		{
			HandleDialogChange(_positionCtrl, addSuffix: false);
		}
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(ConditionResultData), "Execute")]
	public static void PrefixExecuteConditionResult(ConditionResultData __instance)
	{
		List<ConditionResultItem> fieldValue = __instance.GetFieldValue<List<ConditionResultItem>>("_items");
		LogCheckpoint("触发剧情检查点:", fieldValue);
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(MissionCheckData), "Check")]
	public static void PrefixCheckMission(MissionCheckData __instance)
	{
		ConditionResultItem[] fieldValue = __instance.GetFieldValue<ConditionResultItem[]>("_conditions");
		StringBuilder stringBuilder = new StringBuilder("触发任务检查点:");
		AppendConditions(fieldValue, stringBuilder);
		if (__instance.GetFieldValue<bool>("_dayCheck"))
		{
			DataHelper.GetMissionPeriod(__instance, out var periodText);
			if (!periodText.IsNullOrWhiteSpace())
			{
				stringBuilder.AppendLine("    时限：" + periodText);
			}
		}
		PrintToNarrativeLog(stringBuilder.ToString());
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(SwitchResultData), "Execute")]
	public static void PrefixExecuteSwitchResult(SwitchResultData __instance)
	{
		List<ConditionResultItem> fieldValue = __instance.GetFieldValue<List<ConditionResultItem>>("_items");
		LogCheckpoint("触发剧情分支检查点:", fieldValue);
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(PositionEventData), "GetRate")]
	public static void PositionEventData_GetRate_Postfix(PositionEventData __instance, ref int __result)
	{
		if (TargetEvent == __instance)
		{
			__result = 99999;
			TargetEvent = null;
		}
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(MissionCheckData), "Check")]
	public static void MissionCheckData_Postfix(MissionCheckData __instance, ref bool __result)
	{
		if (TargetMission == __instance)
		{
			__result = true;
			TargetMission = null;
		}
	}

	private static void HandleDialogChange(PositionController instance, bool addSuffix, string suffix = "")
	{
		DicePlugin.LogInfo($"HandleDialogChange called, addSuffix: {addSuffix}, suffix: {suffix}");
		try
		{
			PositionDialogItem dialog = instance.GetFieldValue<FreePositionData>("_positionData").GetDialog();
			string text = dialog.GetFieldValue<string>("_dialog");
			if (addSuffix)
			{
				if (text.Contains("存在事件："))
				{
					int num = text.LastIndexOf("(");
					if (num > 0)
					{
						text = text.Substring(0, num);
					}
				}
				_originalDialogKey = text;
				_positionCtrl = instance;
				string text2 = LocalizationManager.Instance.LocaleResolver.GetString("Position/Dialog/" + text);
				if (string.IsNullOrEmpty(text2))
				{
					text2 = text;
				}
				dialog.SetPrivateField("_dialog", text2 + "(" + suffix + ")");
				return;
			}
			if (!string.IsNullOrEmpty(_originalDialogKey) && _originalDialogKey.Contains("存在事件："))
			{
				int num2 = _originalDialogKey.LastIndexOf("(");
				if (num2 > 0)
				{
					_originalDialogKey = _originalDialogKey.Substring(0, num2);
				}
			}
			dialog.SetPrivateField("_dialog", _originalDialogKey);
			_positionCtrl = null;
			_originalDialogKey = string.Empty;
		}
		catch (Exception o)
		{
			DicePlugin.LogError(o);
		}
	}

	private static bool HasTriggerSubMissions(PositionType position, out string missionKey)
	{
		List<MissionData> missionList = null;
		try
		{
			SubMissionsData fieldValue = MissionManagerData.Instance.GetFieldValue<SubMissionsData>("_subMissions");
			if (fieldValue != null)
			{
				missionList = fieldValue.GetFieldValue<List<MissionData>>("_list");
			}
		}
		catch
		{
		}
		foreach (KeyValuePair<string, MissionCheckData> activeSubMission in MissionManagerData.Instance.ActiveSubMissions)
		{
			MissionCheckData value = activeSubMission.Value;
			if (value.Position == position && value.Check(PlayerStatManagerData.Instance) && !IsMissionCompleted(missionList, activeSubMission.Key))
			{
				string fieldValue2 = value.GetFieldValue<string>("_desc");
				if (!string.IsNullOrEmpty(fieldValue2))
				{
					missionKey = TextConverter.ToSimplified(fieldValue2);
				}
				else
				{
					missionKey = value.Key;
				}
				return true;
			}
		}
		missionKey = string.Empty;
		return false;
	}

	private static bool IsMissionCompleted(List<MissionData> missionList, string missionName)
	{
		if (missionList == null || string.IsNullOrEmpty(missionName))
		{
			return false;
		}
		try
		{
			foreach (MissionData mission in missionList)
			{
				if (mission.Name == missionName && mission.CurrentKey == "clear")
				{
					return true;
				}
			}
		}
		catch
		{
		}
		return false;
	}

	private static void LogCheckpoint(string header, IEnumerable<ConditionResultItem> items)
	{
		StringBuilder stringBuilder = new StringBuilder(header);
		AppendConditions(items, stringBuilder);
		PrintToNarrativeLog(stringBuilder.ToString());
	}

	private static void AppendConditions(IEnumerable<ConditionResultItem> items, StringBuilder logBuilder)
	{
		foreach (ConditionResultItem item in items)
		{
			bool isInPeriod;
			string text = DataHelper.ExtractConditions(item, out isInPeriod);
			if (!text.IsNullOrWhiteSpace())
			{
				logBuilder.AppendLine("    " + text);
			}
		}
	}

	private static void PrintToNarrativeLog(string message)
	{
		string text = Regex.Replace(message, "^\\s*$\\n|\\r", string.Empty, RegexOptions.Multiline).Trim();
		NarrativeLog narrativeLog = FungusManager.Instance.NarrativeLog;
		if (narrativeLog != null)
		{
			NarrativeLogEntry entry = new NarrativeLogEntry
			{
				name = "LOMAssistant",
				text = "<size=18>" + text + "</size>"
			};
			narrativeLog.AddLine(entry);
		}
	}
}
