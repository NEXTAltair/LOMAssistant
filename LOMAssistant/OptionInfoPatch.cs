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

	[HarmonyPrefix]
	[HarmonyPatch(typeof(LuaEnvironment), "LoadLuaFunction")]
	public static void LuaEnvironment_LoadLuaFunction_Prefix(string luaString, string friendlyName)
	{
		var timer = System.Diagnostics.Stopwatch.StartNew();
		try
		{
			if (friendlyName == "tmp.LuaScript")
			{
				return;
			}
			// 剧本内容不会变：解析结果按内容缓存，同一个剧本再次载入（地点菜单等会反复载入）不用重新解析
			string key = friendlyName + "#" + (luaString ?? "").Length + "#" + (luaString ?? "").GetHashCode();
			long lap = 0;
			string laps = "";
			void Lap(string name)
			{
				long now = timer.ElapsedMilliseconds;
				laps += name + " " + (now - lap) + "，";
				lap = now;
			}
			// 选项结果、锁住选项：解析结果存在 LuaParser 里，显示选项文字时取用
			LuaParser.Instance.ExtractOptionActionsFromLua(key, luaString);
			Lap("选项");
			LuaParser.Instance.ExtractLockedOptions(key, luaString);
			Lap("锁住选项");
			if (timer.ElapsedMilliseconds >= 50)
			{
				FileLogger.Log($"[性能] 解析剧本 {friendlyName}：{laps.TrimEnd('，')}（毫秒）");
			}
		}
		catch (Exception ex)
		{
			DicePlugin.LogError("LoadLuaFunction Prefix 解析失败，已跳过以保证对话加载: " + ex);
		}
		LuaLoadTiming.ParseMs += timer.ElapsedMilliseconds;
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
				string condition = RichText.Strip(DataHelper.ParseConditionCheckpoint(locked.Condition));
				string effects = locked.Unlocked != null && LuaParser.Instance.OptionResult.TryGetValue(locked.Unlocked, out var unlockedActions)
					? RichText.Strip(TextConverter.ToSimplified(LuaParser.Description(unlockedActions)))
					: "";
				if (!condition.IsNullOrWhiteSpace())
				{
					__result = __result + "\n<size=16>(" + condition + (effects.IsNullOrWhiteSpace() ? "" : "；解锁后：" + effects) + ")</size>";
				}
				return;
			}
			// 满足某个分支条件就直接接一段剧情：写成「若 条件：触发剧情」
			string jumps = "";
			if (LuaParser.Instance.OptionJumps.TryGetValue(key, out var jumpList))
			{
				foreach (var jump in jumpList)
				{
					string condition = RichText.Strip(DataHelper.SwitchBranchCondition(jump.Switch, jump.Branch));
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
					string text2 = RichText.Strip(text);
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
