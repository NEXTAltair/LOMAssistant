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

	/// <summary>最近载入的剧本（解析缓存的键），剧情回顾「此刻」页用它取这一段的选项、判定和事件。</summary>
	public static string LastScriptKey;

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
			LuaParser.Instance.ExtractSwitchCheckFromLua(key, luaString);
			LuaParser.Instance.ExtractPositionWorkFromLua(key, luaString);
			if (!friendlyName.StartsWith("tmp.") && !friendlyName.StartsWith("Flowchart.Say.ExecuteLua"))
			{
				LastScriptKey = key;
			}
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
			HandleDialogChange(__instance, addSuffix: true, "可触发：" + missionKey);
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
				if (text.Contains("可触发："))
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
			if (!string.IsNullOrEmpty(_originalDialogKey) && _originalDialogKey.Contains("可触发："))
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

	/// <summary>
	/// 点这个地点会不会触发任务：和游戏点地点时的判断一样（本旬生效的支线里，地点是这里、有开头剧本的那个），
	/// 主线正在等时点哪都是走主线，不提示。返回任务的名字。
	/// </summary>
	private static bool HasTriggerSubMissions(PositionType position, out string missionKey)
	{
		missionKey = string.Empty;
		MissionManagerData missions = MissionManagerData.Instance;
		if (missions?.ActiveSubMissions == null || (missions.MainMissionStart && missions.MainMissionPosition != PositionType.無))
		{
			return false;
		}
		foreach (KeyValuePair<string, MissionCheckData> active in missions.ActiveSubMissions)
		{
			MissionCheckData check = active.Value;
			if (check.Position != position || string.IsNullOrEmpty(check.StartScript))
			{
				continue;
			}
			LOMAssistant.Features.Story.StoryMission known = LOMAssistant.Features.Story.StoryAtlas.Find(check.Key);
			string desc = TextConverter.ToSimplified(check.GetFieldValue<string>("_desc") ?? "").Split('\n')[0].Trim();
			missionKey = known != null && known.Title.Length > 0 ? known.Title : desc.Length > 0 ? desc : active.Key;
			return true;
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
