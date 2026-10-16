using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;

namespace LOMAssistant.Config;

public static class ConfigManager
{
	internal static readonly Dictionary<string, IConfigElement> ConfigElements = new Dictionary<string, IConfigElement>();

	private const string SEC_NAME = "LOMAssistant";

	public static ConfigElement<bool> EnableOptionInfo;

	public static ConfigElement<bool> EnableOptionResultDisplay;

	public static ConfigElement<KeyCode> LogMenuToggleKey;

	public static ConfigElement<bool> EnableManualDiceSelection;

	public static ConfigElement<bool> EnableLibraryDisplay;

	public static ConfigElement<bool> EnableCombatAssist;

	public static ConfigElement<bool> EnableSkipAction;

	public static ConfigElement<bool> EnableBattleWin;

	public static ConfigElement<float> PinballScoreMultiplier;

	public static ConfigElement<int> PinballFixedScore;

	public static ConfigElement<KeyCode> PanelKey;

	public static ConfigElement<bool> CheatUnlimitedTraining;

	public static ConfigElement<bool> CheatFastTraining;

	public static ConfigElement<bool> CheatUnlimitedUpgrade;

	public static ConfigElement<bool> CheatFastUpgrade;

	public static ConfigElement<bool> CheatFreeUpgradeMoney;

	public static ConfigElement<int> SaveSlotCount;

	public static ConfigElement<bool> EnableDebugPanel;

	public static ConfigElement<KeyCode> DebugPanelKey;

	public static ConfigElement<bool> EnableSaveSystem;

	public static ConfigElement<bool> LogToFile;

	private static ConfigFile Config => LOMAssistantPlugin.Instance.Config;

	public static void Init()
	{
		CreateConfigElements();
		LoadConfig();
	}

	internal static void RegisterConfigElement<T>(ConfigElement<T> configElement)
	{
		ConfigEntry<T> entry = Config.Bind("LOMAssistant", configElement.Name, configElement.Value, configElement.Description);
		entry.SettingChanged += delegate
		{
			configElement.Value = entry.Value;
		};
		ConfigElements.Add(configElement.Name, configElement);
	}

	public static T GetConfigValue<T>(ConfigElement<T> element)
	{
		if (!Config.TryGetEntry("LOMAssistant", element.Name, out ConfigEntry<T> entry))
		{
			throw new Exception("Could not get config entry '" + element.Name + "'");
		}
		return entry.Value;
	}

	public static void SetConfigValue<T>(ConfigElement<T> element, T value)
	{
		if (Config.TryGetEntry("LOMAssistant", element.Name, out ConfigEntry<T> entry))
		{
			entry.Value = value;
		}
		else
		{
			DicePlugin.LogInfo("Could not get config entry '" + element.Name + "'");
		}
	}

	public static void LoadConfig()
	{
		foreach (KeyValuePair<string, IConfigElement> configElement in ConfigElements)
		{
			ConfigDefinition key = new ConfigDefinition("LOMAssistant", configElement.Key);
			if (Config.ContainsKey(key))
			{
				ConfigEntryBase configEntryBase = Config[key];
				if (configEntryBase != null)
				{
					configElement.Value.BoxedValue = configEntryBase.BoxedValue;
				}
			}
		}
	}

	public static void SaveConfig()
	{
		Config.Save();
	}

	private static void CreateConfigElements()
	{
		EnableOptionInfo = new ConfigElement<bool>("EnableOptionInfo", "是否启用信息窗口（选项/开关/事件/任务）。true=开，false=关，改后重启生效", defaultValue: true);
		EnableOptionResultDisplay = new ConfigElement<bool>("EnableOptionResultDisplay", "是否启用对话选项结果直接显示。true=开，false=关，改后重启生效", defaultValue: true);
		LogMenuToggleKey = new ConfigElement<KeyCode>("LogMenuToggleKey", "信息窗口开关热键，如 Tab / F12 / P 等", KeyCode.Tab);
		EnableManualDiceSelection = new ConfigElement<bool>("EnableManualDiceSelection", "是否启用骰子手动选择（点击线路直接选中）。true=开，false=关，改后重启生效", defaultValue: true);
		EnableLibraryDisplay = new ConfigElement<bool>("EnableLibraryDisplay", "是否启用讲经堂解锁显示。true=开，false=关，改后重启生效", defaultValue: true);
		EnableCombatAssist = new ConfigElement<bool>("EnableCombatAssist", "是否启用战斗辅助（ESC界面增加直接获胜按钮）。true=开，false=关，改后重启生效", defaultValue: true);
		EnableSkipAction = new ConfigElement<bool>("EnableSkipAction", "是否启用空过（养成界面 ESC 菜单增加“空过”按钮，消耗 1 次行动、不触发任何事件）。true=开，false=关，改后重启生效", defaultValue: true);
		EnableBattleWin = new ConfigElement<bool>("EnableBattleWin", "是否启用团战直接获胜（团战暂停界面增加“直接获胜”按钮）。true=开，false=关，改后重启生效", defaultValue: true);
		PinballScoreMultiplier = new ConfigElement<float>("PinballScoreMultiplier", "弹珠小游戏得分倍率（每次加分乘以该值，扣分不受影响）。1=原版，2=双倍", 1f);
		PinballFixedScore = new ConfigElement<int>("PinballFixedScore", "弹珠小游戏固定总分：离开小游戏时总分直接改为该值（影响剧情判定）。-1=关闭", -1);
		PanelKey = new ConfigElement<KeyCode>("PanelKey", "游戏内面板（修炼/锻造/炼丹开关、弹珠分数）的显示/隐藏热键", KeyCode.F8);
		CheatUnlimitedTraining = new ConfigElement<bool>("CheatUnlimitedTraining", "无限修炼次数（可在游戏内面板切换）", defaultValue: false);
		CheatFastTraining = new ConfigElement<bool>("CheatFastTraining", "快速修炼：投 1 点直接升级（可在游戏内面板切换）", defaultValue: false);
		CheatUnlimitedUpgrade = new ConfigElement<bool>("CheatUnlimitedUpgrade", "无限锻造、炼丹次数（可在游戏内面板切换）", defaultValue: false);
		CheatFastUpgrade = new ConfigElement<bool>("CheatFastUpgrade", "快速锻造、炼丹：投 1 点直接升级（可在游戏内面板切换）", defaultValue: false);
		CheatFreeUpgradeMoney = new ConfigElement<bool>("CheatFreeUpgradeMoney", "锻造、炼丹不消耗银两（可在游戏内面板切换）", defaultValue: false);
		SaveSlotCount = new ConfigElement<int>("SaveSlotCount", "手动存档栏位数量（原版 20，小于 20 时按 20）", 100);
		EnableDebugPanel = new ConfigElement<bool>("EnableDebugPanel", "是否启用游戏内置测试面板。true=开，false=关，改后重启生效", defaultValue: true);
		DebugPanelKey = new ConfigElement<KeyCode>("DebugPanelKey", "打开游戏内置测试面板的热键，如 F9 / P 等", KeyCode.F9);
		EnableSaveSystem = new ConfigElement<bool>("EnableSaveSystem", "是否启用增强存档系统（随时存档/读档）。true=开，false=关，改后重启生效", defaultValue: false);
		LogToFile = new ConfigElement<bool>("LogToFile", "是否写入独立日志文件。日志位置：BepInEx\\plugins\\LOMAssistant\\logs\\", defaultValue: true);
	}
}
