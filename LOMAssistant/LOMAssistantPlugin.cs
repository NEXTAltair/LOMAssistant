using System;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.Mono;
using HarmonyLib;
using LOMAssistant.Config;
using LOMAssistant.Features.Combat;
using LOMAssistant.Features.Debug;
using LOMAssistant.Features.Dice;
using LOMAssistant.Features.Extras;
using LOMAssistant.Features.Library;
using LOMAssistant.Features.Save;
using Mortal.Story;
using UnityEngine;

namespace LOMAssistant;

[BepInPlugin("LOMAssistant", "活侠传拓展", "1.0.4")]
public class LOMAssistantPlugin : BaseUnityPlugin
{
	private Harmony _harmony;

	public static LOMAssistantPlugin Instance { get; private set; }

	public static ManualLogSource LogSource { get; private set; }

	private void Awake()
	{
		Instance = this;
		LogSource = base.Logger;
		FileLogger.Init(Path.GetDirectoryName(base.Info.Location));
		DicePlugin.Instance = base.gameObject.AddComponent<DicePlugin>();
		ConfigManager.Init();
		SaveManager.Init();
		SaveManager.OnLoadComplete += SavePatches.RestoreMusicOnLoadComplete;
		LogInfo("活侠传拓展已加载。");
		FileLogger.Log("活侠传拓展已加载。");
		_harmony = new Harmony(base.Info.Metadata.GUID);
		ApplyPatches();
	}

	private void Update()
	{
		if (ConfigManager.EnableDebugPanel.Value && Input.GetKeyDown(ConfigManager.DebugPanelKey.Value))
		{
			DebugPanelFeature.ToggleTestPanel();
		}
		if (ConfigManager.EnableSaveSystem.Value && SaveManager.Instance != null)
		{
			SaveManager.Instance.Update();
		}
	}

	private void LateUpdate()
	{
		if (ConfigManager.EnableSaveSystem.Value && SaveManager.Instance != null)
		{
			SaveManager.Instance.LateUpdate();
		}
	}

	private void OnDestroy()
	{
		if (_harmony != null)
		{
			_harmony.UnpatchSelf();
		}
	}

	private void EnsurePatched(string name, MethodBase method, string prefixName)
	{
		if (method == null)
		{
			FileLogger.LogError("[Init] " + name + " method NOT FOUND via AccessTools!");
			LogError(name + " 方法未找到，骰子手动选择功能可能无法工作！");
			return;
		}
		int valueOrDefault = (Harmony.GetPatchInfo(method)?.Prefixes?.Count).GetValueOrDefault();
		FileLogger.Log("[Init] " + name + " patch status: prefixes=" + valueOrDefault);
		LogInfo(name + " 补丁状态: prefixes=" + valueOrDefault);
		if (valueOrDefault == 0)
		{
			FileLogger.LogWarning("[Init] " + name + " NOT patched by PatchAll, manually patching...");
			LogWarning(name + " 未通过 PatchAll 应用，尝试手动补丁...");
			HarmonyMethod prefix = new HarmonyMethod(typeof(DiceManualSelectionPatch), prefixName);
			_harmony.Patch(method, prefix);
			int valueOrDefault2 = (Harmony.GetPatchInfo(method)?.Prefixes?.Count).GetValueOrDefault();
			FileLogger.Log("[Init] " + name + " manual patch done, prefixes=" + valueOrDefault2);
			LogInfo(name + " 手动补丁完成: prefixes=" + valueOrDefault2);
		}
	}

	private void ApplyPatches()
	{
		try
		{
			if (ConfigManager.EnableOptionInfo.Value)
			{
				_harmony.PatchAll(typeof(OptionInfoPatch));
				LogInfo("选项信息补丁已应用。");
			}
			if (ConfigManager.EnableManualDiceSelection.Value)
			{
				_harmony.PatchAll(typeof(DiceManualSelectionPatch));
				LogInfo("骰子手动选择补丁已应用。");
				EnsurePatched("Roll", AccessTools.Method(typeof(DiceMenuDialog), "Roll"), "Roll_Prefix");
				EnsurePatched("CheckRevolution", AccessTools.Method(typeof(DiceMenuDialog), "CheckRevolution"), "CheckRevolution_Prefix");
			}
			if (ConfigManager.EnableLibraryDisplay.Value)
			{
				_harmony.PatchAll(typeof(LibraryDisplayPatch));
				LogInfo("讲经堂显示补丁已应用。");
			}
			if (ConfigManager.EnableCombatAssist.Value)
			{
				_harmony.PatchAll(typeof(CombatAssistPatch));
				LogInfo("战斗辅助补丁已应用。");
			}
			if (ConfigManager.EnableSkipAction.Value)
			{
				_harmony.PatchAll(typeof(SkipActionPatch));
				LogInfo("空过补丁已应用。");
			}
			if (ConfigManager.EnableBattleWin.Value)
			{
				_harmony.PatchAll(typeof(BattleWinPatch));
				LogInfo("团战直接获胜补丁已应用。");
			}
			_harmony.PatchAll(typeof(PinballScorePatch));
			LogInfo("弹珠分数补丁已应用。");
			_harmony.PatchAll(typeof(TrainingCheatPatch));
			_harmony.PatchAll(typeof(TrainingCheatPatch.MartialCountPatch));
			_harmony.PatchAll(typeof(TrainingCheatPatch.UpgradeCountPatch));
			if (base.gameObject.GetComponent<ModPanel>() == null)
			{
				base.gameObject.AddComponent<ModPanel>();
			}
			LogInfo("修炼/锻造/炼丹修改补丁已应用。");
			_harmony.PatchAll(typeof(DuelCheatPatch));
			LogInfo("决斗修改补丁已应用。");
			_harmony.PatchAll(typeof(SaveSlotPatch));
			LogInfo("存档栏位补丁已应用。");
			_harmony.PatchAll(typeof(SaveNotePatch));
			LogInfo("存档备注补丁已应用。");
			_harmony.PatchAll(typeof(AutoSaveHistoryPatch));
			LogInfo("自动存档历史补丁已应用。");
			_harmony.PatchAll(typeof(ShopAllItemsPatch));
			LogInfo(ConfigManager.EnableShopAllItems.Value ? "四师兄商店补丁已应用（售卖全部物品：开）。" : "四师兄商店补丁已应用（售卖全部物品：关）。");
			if (!ConfigManager.EnableSaveSystem.Value)
			{
				return;
			}
			_harmony.PatchAll(typeof(SavePatches));
			LogInfo("增强存档补丁已应用。");
			try
			{
				Type type = AccessTools.TypeByName("AkSoundEngine");
				if (type != null)
				{
					MethodInfo methodInfo = AccessTools.Method(type, "PostEvent", new Type[2]
					{
						typeof(string),
						typeof(GameObject)
					});
					if (methodInfo != null)
					{
						HarmonyMethod prefix = new HarmonyMethod(typeof(SavePatches), "AkSoundEngine_PostEvent_Prefix");
						_harmony.Patch(methodInfo, prefix);
						FileLogger.Log("[Init] AkSoundEngine.PostEvent(string, GameObject) patch APPLIED");
						LogInfo("Wwise 音频屏蔽补丁已应用。");
					}
					else
					{
						FileLogger.LogError("[Init] AkSoundEngine.PostEvent method NOT FOUND");
						LogError("AkSoundEngine.PostEvent 方法未找到，音频屏蔽无法工作！");
					}
				}
				else
				{
					FileLogger.LogError("[Init] AkSoundEngine type NOT FOUND (Wwise assembly not loaded?)");
					LogError("AkSoundEngine 类型未找到，Wwise 音频屏蔽不可用。");
				}
			}
			catch (Exception ex)
			{
				FileLogger.LogError("[Init] Failed to patch AkSoundEngine.PostEvent: " + ex);
				LogError("Wwise 音频屏蔽补丁应用失败: " + ex.Message);
			}
		}
		catch (Exception ex2)
		{
			LogError("应用补丁失败: " + ex2);
			FileLogger.LogError("应用补丁失败: " + ex2);
		}
	}

	public static void LogInfo(object message)
	{
		if (LogSource != null)
		{
			LogSource.LogInfo(message);
		}
	}

	public static void LogError(object message)
	{
		if (LogSource != null)
		{
			LogSource.LogError(message);
		}
	}

	public static void LogWarning(object message)
	{
		if (LogSource != null)
		{
			LogSource.LogWarning(message);
		}
	}
}
