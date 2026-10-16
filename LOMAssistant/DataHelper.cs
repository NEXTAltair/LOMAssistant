using System;
using System.Collections.Generic;
using System.Text;
using BepInEx;
using Mortal.Core;
using Mortal.Story;
using OBB.Framework.Data;
using OBB.Framework.Utils;
using UnityEngine;

namespace LOMAssistant;

public static class DataHelper
{
	public static string ExtractConditions(ConditionResultItem condition, out bool isInPeriod)
	{
		try
		{
			List<string> list = new List<string>();
			isInPeriod = true;
			foreach (StatCompareItem item in condition.GetFieldValue<List<StatCompareItem>>("_items"))
			{
				StatGroupVariable fieldValue = item.GetFieldValue<StatGroupVariable>("_value1");
				StatGroupVariable fieldValue2 = item.GetFieldValue<StatGroupVariable>("_value2");
				string compareOperator = GetCompareOperator(item.GetFieldValue<StatCompareType>("_compareOp"));
				if (fieldValue.name.Contains("时间_") && !item.Check())
				{
					isInPeriod = false;
				}
				string text = FormatConditionValName(fieldValue);
				FormatConditionValName(fieldValue2);
				string text2 = string.Format("{0} 需{2}{3} → 当前{1}", text, fieldValue.GetResult(), compareOperator, fieldValue2.GetResult());
				text2 = (item.Check() ? RichText.Green(text2) : RichText.Red(text2));
				list.Add(text2);
			}
			LogicOperatorType fieldValue3 = condition.GetFieldValue<LogicOperatorType>("_logicOp");
			return string.Join(" " + GetLogicOperator(fieldValue3) + " ", list);
		}
		catch (Exception ex)
		{
			DicePlugin.LogError("Error extracting conditions: " + ex.Message);
			isInPeriod = false;
			return string.Empty;
		}
	}

	private static string FormatConditionValName(StatGroupVariable variable)
	{
		string name = variable.name;
		if (name.StartsWith("标记_") || name.StartsWith("旗标_") || name.StartsWith("標記_") || name.StartsWith("旗標_") || IsEnglishAlphanumeric(name))
		{
			string text = name;
			if (text.StartsWith("标记_") || text.StartsWith("旗标_") || text.StartsWith("標記_") || text.StartsWith("旗標_"))
			{
				text = text.Substring(3);
			}
			try
			{
				List<StatValueReference> fieldValue = variable.GetFieldValue<List<StatValueReference>>("_value");
				if (fieldValue != null && fieldValue.Count > 0)
				{
					FlagData fieldValue2 = fieldValue[0].GetFieldValue<FlagData>("_flag");
					if (fieldValue2 != null)
					{
						string devNote = fieldValue2.DevNote;
						if (!string.IsNullOrEmpty(devNote))
						{
							return TextConverter.ToSimplified(devNote.Contains("\n") ? devNote.Split('\n')[0] : devNote) + " (" + text + ")";
						}
					}
				}
			}
			catch
			{
			}
			try
			{
				string flagName = GetFlagName(text);
				if (!string.IsNullOrEmpty(flagName))
				{
					return flagName + " (" + text + ")";
				}
			}
			catch
			{
			}
		}
		return TextConverter.ToSimplified(name.Replace("数值_", "").Replace("时间_", "").Replace("常数_", "")
			.Replace("旗标_", "")
			.Replace("标记_", "")
			.Replace("數值_", "")
			.Replace("時間_", "")
			.Replace("常數_", "")
			.Replace("旗標_", "")
			.Replace("標記_", ""));
	}

	private static bool IsEnglishAlphanumeric(string s)
	{
		if (string.IsNullOrEmpty(s))
		{
			return false;
		}
		foreach (char c in s)
		{
			if ((c < 'a' || c > 'z') && (c < 'A' || c > 'Z') && (c < '0' || c > '9') && c != '_' && c != '-')
			{
				return false;
			}
		}
		return true;
	}

	public static string ParseSwitchCheckpoint(string checkpoint)
	{
		CheckPointManager instance = CheckPointManager.Instance;
		if (instance == null)
		{
			return string.Empty;
		}
		StringBuilder stringBuilder = new StringBuilder();
		SwitchResultConfig[] fieldValue = instance.GetFieldValue<SwitchResultConfig[]>("_switchList");
		SwitchResultData switchResultData = null;
		for (int i = 0; i < fieldValue.Length; i++)
		{
			switchResultData = fieldValue[i].GetData(checkpoint);
			if (switchResultData != null)
			{
				break;
			}
		}
		if (switchResultData == null)
		{
			return string.Empty;
		}
		foreach (ConditionResultItem item in switchResultData.GetFieldValue<List<ConditionResultItem>>("_items"))
		{
			stringBuilder.AppendLine("    " + ExtractConditions(item, out var _));
		}
		return stringBuilder.ToString();
	}

	public static Dictionary<MissionCheckData, (string, string)> GetMissions()
	{
		Dictionary<MissionCheckData, (string, string)> dictionary = new Dictionary<MissionCheckData, (string, string)>();
		MissionManagerData instance = MissionManagerData.Instance;
		SubMissionsData subMissionsData = ((instance != null) ? instance.GetFieldValue<SubMissionsData>("_subMissions") : null);
		if (subMissionsData == null)
		{
			return dictionary;
		}
		foreach (MissionData item2 in subMissionsData.List)
		{
			string fieldValue = item2.GetFieldValue<string>("_desc");
			fieldValue = (fieldValue.IsNullOrWhiteSpace() ? item2.Name : fieldValue);
			fieldValue.Replace("\n", "");
			string currentKey = item2.CurrentKey;
			if (currentKey == "clear")
			{
				continue;
			}
			MissionCheckData byKey = item2.GetByKey(currentKey);
			if (byKey == null || !IsValidMission(byKey))
			{
				continue;
			}
			string fieldValue2 = byKey.GetFieldValue<string>("_desc");
			fieldValue2 = (fieldValue2.IsNullOrWhiteSpace() ? byKey.name : fieldValue2);
			string item = fieldValue + " - " + fieldValue2;
			string text = byKey.Position.ToString();
			string periodText;
			bool isInPeriod = GetMissionPeriod(byKey, out periodText);
			if (!isInPeriod)
			{
				continue;
			}
			List<string> list = ExtractConditions(byKey, out isInPeriod);
			if (!isInPeriod)
			{
				continue;
			}
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.AppendLine("位置：" + text);
			stringBuilder.AppendLine("时间: " + periodText);
			stringBuilder.AppendLine("条件:");
			foreach (string item3 in list)
			{
				stringBuilder.AppendLine("    " + item3);
			}
			dictionary[byKey] = (item, stringBuilder.ToString());
		}
		return dictionary;
	}

	private static bool IsValidMission(MissionCheckData checkData)
	{
		bool fieldValue = checkData.GetFieldValue<bool>("_dayCheck");
		bool flag = checkData.Position > PositionType.無;
		bool flag2 = checkData.GetFieldValue<ConditionResultItem[]>("_conditions").Length != 0;
		if (LoadLuaScript(checkData.StartScript) != string.Empty)
		{
			return fieldValue || flag || flag2;
		}
		return false;
	}

	private static List<string> ExtractConditions(MissionCheckData checkData, out bool isInPeriod)
	{
		List<string> list = new List<string>();
		ConditionResultItem[] fieldValue = checkData.GetFieldValue<ConditionResultItem[]>("_conditions");
		isInPeriod = true;
		ConditionResultItem[] array = fieldValue;
		foreach (ConditionResultItem condition in array)
		{
			list.Add(ExtractConditions(condition, out var isInPeriod2));
			if (!isInPeriod2)
			{
				isInPeriod = false;
			}
		}
		return list;
	}

	public static bool GetMissionPeriod(MissionCheckData checkData, out string periodText)
	{
		if (!checkData.GetFieldValue<bool>("_dayCheck"))
		{
			periodText = "不限";
			return true;
		}
		PlayerStatManagerData instance = PlayerStatManagerData.Instance;
		GameTime gameTime = ((instance != null) ? instance.GameTime : null);
		switch (checkData.GetFieldValue<TimeCheckType>("_timeCheckType"))
		{
		case TimeCheckType.特定:
		{
			GameTime fieldValue3 = checkData.GetFieldValue<GameTime>("_specificTime");
			periodText = "第" + fieldValue3.YearText + "年" + fieldValue3.MonthText + "月" + fieldValue3.StageText;
			return gameTime == fieldValue3;
		}
		default:
			periodText = "不限";
			return true;
		case TimeCheckType.區間:
		{
			GameTime fieldValue = checkData.GetFieldValue<GameTime>("_minTime");
			GameTime fieldValue2 = checkData.GetFieldValue<GameTime>("_maxTime");
			periodText = "第" + fieldValue.YearText + "年" + fieldValue.MonthText + "月" + fieldValue.StageText + " - 第" + fieldValue2.YearText + "年" + fieldValue2.MonthText + "月" + fieldValue2.StageText;
			if (gameTime >= fieldValue)
			{
				return gameTime <= fieldValue2;
			}
			return false;
		}
		}
	}

	public static bool TryParseEnum<TEnum>(string value, out TEnum result) where TEnum : struct
	{
		return EnumUtils.TryParseByStringValue<TEnum>(value, out result);
	}

	public static string GetGameStatLocalizedName(string statStringValue)
	{
		if (string.IsNullOrEmpty(statStringValue))
		{
			return string.Empty;
		}
		string text = LocalizationManager.Instance.LocaleResolver.GetString("PlayerStat/" + statStringValue);
		if (!text.IsNullOrWhiteSpace())
		{
			return text;
		}
		if (TryParseEnum<GameStatType>(statStringValue, out var result))
		{
			return TextConverter.ToSimplified(result.ToString());
		}
		return statStringValue;
	}

	public static string GetCharacterLocalizedName(string characterStringValue)
	{
		if (string.IsNullOrEmpty(characterStringValue))
		{
			return string.Empty;
		}
		string text = LocalizationManager.Instance.LocaleResolver.GetString("Character/" + characterStringValue);
		if (!text.IsNullOrWhiteSpace())
		{
			return text;
		}
		if (TryParseEnum<RelationshipStatType>(characterStringValue, out var result))
		{
			return TextConverter.ToSimplified(result.ToString());
		}
		return characterStringValue;
	}

	public static string GetItemName(GameItemType itemType, string id)
	{
		IGameItem item = ItemDatabase.Instance.GetItem(itemType, id);
		if (item == null)
		{
			return string.Empty;
		}
		return LocalizationManager.Instance.LocaleResolver.GetString(item.GetNameKey());
	}

	public static string GetTalentName(string id)
	{
		PlayerTalentData playerTalentData = PlayerStatManagerData.Instance.Talents.Get(id);
		if (playerTalentData == null)
		{
			return string.Empty;
		}
		return LocalizationManager.Instance.LocaleResolver.GetString(playerTalentData.GetIdKey());
	}

	public static string GetStoryName(string id)
	{
		string key = "LegendInfo/" + id;
		return LocalizationManager.Instance.LocaleResolver.GetString(key);
	}

	public static string GetTeamName(string id)
	{
		BattleTeamStat battleTeamStat = PlayerStatManagerData.Instance.EnemyTeam.Get(id);
		if (battleTeamStat == null)
		{
			return string.Empty;
		}
		return LocalizationManager.Instance.LocaleResolver.GetString(battleTeamStat.GetNameKey());
	}

	public static string GetEventName(PositionEventData eventData)
	{
		StoryMappingItem fieldValue = eventData.GetFieldValue<StoryMappingItem>("_config");
		string text = ((fieldValue != null) ? fieldValue.name : null);
		string fieldValue2 = eventData.GetFieldValue<string>("_devNote");
		if (!fieldValue2.IsNullOrWhiteSpace())
		{
			text = fieldValue2;
		}
		return RichText.Golden(TextConverter.ToSimplified(text));
	}

	public static string LoadLuaScript(string scriptName)
	{
		string text = scriptName.ToLower();
		TextAsset textAsset = Resources.Load<TextAsset>("story/chinesetraditional/" + text);
		return ((textAsset != null) ? textAsset.text : null) ?? string.Empty;
	}

	public static string GetCompareOperator(StatCompareType compareType)
	{
		return compareType switch
		{
			StatCompareType.Equal => "=", 
			StatCompareType.NotEqual => "!=", 
			StatCompareType.Less => "<", 
			StatCompareType.LessEqual => "<=", 
			StatCompareType.Greater => ">", 
			StatCompareType.GreaterEqual => ">=", 
			_ => "unknown", 
		};
	}

	public static string GetLogicOperator(LogicOperatorType logicOp)
	{
		return logicOp switch
		{
			LogicOperatorType.OR => "或", 
			LogicOperatorType.AND => "且", 
			_ => "unknown", 
		};
	}

	public static string GetFlagName(string id)
	{
		return GetFlagName(MissionManagerData.Instance.GetFlag(id));
	}

	public static string GetFlagName(FlagData flagData)
	{
		if (flagData == null)
		{
			return string.Empty;
		}
		string text = flagData.DevNote;
		if (text.Contains("\n"))
		{
			text = text.Split('\n')[0];
		}
		if (!string.IsNullOrEmpty(text))
		{
			return TextConverter.ToSimplified(text);
		}
		return string.Empty;
	}
}
