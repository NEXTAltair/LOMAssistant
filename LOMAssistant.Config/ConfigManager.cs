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

	public static ConfigElement<float> TabPanelScale;

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

	public static ConfigElement<bool> CheatDuelInvincible;

	public static ConfigElement<bool> CheatDuelNoDamage;

	public static ConfigElement<float> GameSpeed;

	public static ConfigElement<int> ActionMode;

	public static ConfigElement<bool> ShowFlagChanges;

	public static ConfigElement<bool> ShowBranchChecks;

	public static ConfigElement<bool> EnableEventPicker;

	public static ConfigElement<bool> EnableFastDuelRounds;

	public static ConfigElement<float> BranchPanelX;

	public static ConfigElement<float> BranchPanelY;

	public static ConfigElement<float> BranchPanelAutoHide;

	public static ConfigElement<float> PopupOpacity;

	public static ConfigElement<int> SaveSlotCount;

	public static ConfigElement<int> AutoSaveHistory;

	public static ConfigElement<bool> EnableShopAllItems;

	public static ConfigElement<bool> EnableDebugPanel;

	public static ConfigElement<KeyCode> DebugPanelKey;

	public static ConfigElement<bool> EnableAnytimeSave;

	public static ConfigElement<KeyCode> AnytimeSaveKey;

	public static ConfigElement<KeyCode> AnytimeLoadKey;

	public static ConfigElement<KeyCode> AnytimePanelKey;

	public static ConfigElement<KeyCode> AnytimeRollbackKey;

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
		TabPanelScale = new ConfigElement<float>("TabPanelScale", "信息窗口（Tab）的缩放（0.5～2，1=默认大小）。拖动窗口右下角会自动保存", 1f);
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
		CheatDuelInvincible = new ConfigElement<bool>("CheatDuelInvincible", "决斗中无敌：我方不受伤害（可在游戏内面板切换）", defaultValue: false);
		CheatDuelNoDamage = new ConfigElement<bool>("CheatDuelNoDamage", "决斗中造成伤害为 0：敌方不受伤害（可在游戏内面板切换）", defaultValue: false);
		GameSpeed = new ConfigElement<float>("GameSpeed", "游戏倍速（在游戏原本速度上再乘以该值；1=原速）。可在游戏内面板切换", 1f);
		ActionMode = new ConfigElement<int>("ActionMode", "养成行动点：0=正常，1=不消耗（扣到剩 1 点后不再扣，可一直行动；用「空过」结束本旬），2=每旬只有 1 点。可在游戏内面板切换", 0);
		ShowFlagChanges = new ConfigElement<bool>("ShowFlagChanges", "剧情改动旗标时在屏幕上提示（可能剧透）。true=开，false=关。可在游戏内面板切换", defaultValue: false);
		EnableEventPicker = new ConfigElement<bool>("EnableEventPicker", "随机事件自选：剧情走到随机事件（工作、闲逛等）时先停下，列出满足条件的事件选一个，也可以选随机。true=开，false=关。可在游戏内面板切换", defaultValue: false);
		EnableFastDuelRounds = new ConfigElement<bool>("EnableFastDuelRounds", "决斗回合之间的等待缩短：身上状态（buff）多时回合间不再干等几秒，效果和结算不变。true=开，false=关。可在游戏内面板切换", defaultValue: true);
		ShowBranchChecks = new ConfigElement<bool>("ShowBranchChecks", "剧情走到分支判断时在屏幕上列出各分支条件（满足为绿、不满足为红，可能剧透）。true=开，false=关。可在游戏内面板切换", defaultValue: false);
		BranchPanelX = new ConfigElement<float>("BranchPanelX", "剧情分支小窗的位置（占屏幕宽度的比例，-1=默认左侧）。拖动小窗会自动保存", -1f);
		BranchPanelY = new ConfigElement<float>("BranchPanelY", "剧情分支小窗的位置（占屏幕高度的比例，-1=默认）。拖动小窗会自动保存", -1f);
		BranchPanelAutoHide = new ConfigElement<float>("BranchPanelAutoHide", "剧情分支小窗多少秒没有新判断就自动收起（0=不自动收起）", 15f);
		PopupOpacity = new ConfigElement<float>("PopupOpacity", "剧情分支小窗的背景不透明度（0=完全透明，1=不透明），改后重启游戏生效", 0.5f);
		SaveSlotCount = new ConfigElement<int>("SaveSlotCount", "手动存档栏位数量（原版 20，小于 20 时按 20）", 100);
		AutoSaveHistory = new ConfigElement<int>("AutoSaveHistory", "每类自动存档（养成/剧情/战斗）额外保留的旧档数量，0=不保留", 5);
		EnableShopAllItems = new ConfigElement<bool>("EnableShopAllItems", "四师兄商店售卖全部物品（书籍、杂物、特殊物品；不卖《证类本草》，提前买会导致六月后山事件出 bug）。true=开，false=关，改后重启生效", defaultValue: false);
		EnableDebugPanel = new ConfigElement<bool>("EnableDebugPanel", "是否启用游戏内置测试面板。true=开，false=关，改后重启生效", defaultValue: true);
		DebugPanelKey = new ConfigElement<KeyCode>("DebugPanelKey", "打开游戏内置测试面板的热键，如 F9 / P 等", KeyCode.F9);
		EnableAnytimeSave = new ConfigElement<bool>("EnableAnytimeSave", "是否启用随时存档（剧情台词、选项、掷骰和养成界面都能存，存在独立的 8 个槽位，不占原版存档栏）。true=开，false=关，改后重启生效", defaultValue: true);
		AnytimeSaveKey = new ConfigElement<KeyCode>("AnytimeSaveKey", "随时存档：保存到当前槽位的热键", KeyCode.F5);
		AnytimeLoadKey = new ConfigElement<KeyCode>("AnytimeLoadKey", "随时存档：读取当前槽位的热键", KeyCode.F7);
		AnytimePanelKey = new ConfigElement<KeyCode>("AnytimePanelKey", "随时存档面板（选槽位、存、读）的热键", KeyCode.F6);
		AnytimeRollbackKey = new ConfigElement<KeyCode>("AnytimeRollbackKey", "回退到剧情判定前一句的热键（只在内存里保留最近几个判定点，切场景后清空）", KeyCode.F4);
		LogToFile = new ConfigElement<bool>("LogToFile", "是否写入独立日志文件。日志位置：BepInEx\\plugins\\LOMAssistant\\logs\\", defaultValue: true);
	}
}
