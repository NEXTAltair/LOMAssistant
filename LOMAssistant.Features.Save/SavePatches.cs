using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Fungus;
using HarmonyLib;
using MoonSharp.Interpreter;
using Mortal.Core;
using Mortal.Story;
using UnityEngine;

namespace LOMAssistant.Features.Save;

public static class SavePatches
{
	private static string _rollLastKey;

	private const string LuaScriptText = "\nif not LOMS_HELPERS_LOADED then\nLOMS_HELPERS_LOADED = true\n\nfunction LOMS_Command(cmd, tbParam)\n    param = json.serialize(tbParam)\n    rs = luamanager.GetStoryText(\"__LOMS__\"..cmd..\"|\"..param)\n    return json.parse(rs)\nend\n\nfunction LOMS_SaveKV(key, value)\n    LOMS_Command(\"SaveKV\", {K=key, V=value})\nend\n\nfunction LOMS_GetKV(key)\n    return LOMS_Command(\"GetKV\", {K=key})[\"rs\"]\nend\n\nfunction LOMS_Hash(key)\n    return LOMS_Command(\"Hash\", {K=key})[\"rs\"]\nend\n\nfunction LOMS_Finished()\n    return LOMS_Command(\"Finish\", {})[\"rs\"] == \"1\"\nend\n\nfunction LOMS_GetPortrait(key)\n    local data = LOMS_Command(\"GetPortrait\", {K=key})\n    if data[\"rs\"] ~= \"1\" then\n        return nil\n    end\n    return data\nend\n\n-- say 替换：读档时查 KV 跳过已读，正常游戏记录但不查\noldsay = say\nfunction new_say(text, voiceclip)\n    if not LOMS_Finished() then\n        local key = LOMS_Hash(\"Say_\"..text)\n        if LOMS_GetKV(key) == \"1\" then\n            return\n        end\n        if lastsetcharacter ~= nil then\n            oldwait(0.1)\n            for k,v in pairs(lastsetcharacter) do\n                local por = LOMS_GetPortrait(k.name)\n                if por ~= nil then\n                    runwait(characters.LoadCharacterAsset(k.name))\n                    stage.show{character=k, fromPosition=\"M\", toPosition=por[\"to\"], facing=por[\"face\"], useDefaultSettings=false}\n                    stage.showPortrait(k,v)\n                end\n            end\n            lastsetcharacter = nil\n        end\n        oldsay(text,voiceclip)\n        LOMS_SaveKV(key,1)\n    else\n        oldsay(text,voiceclip)\n        LOMS_SaveKV(LOMS_Hash(\"Say_\"..text), 1)\n    end\nend\nsay = new_say\n\n-- choose 替换：读档时查 KV 回放选择，正常游戏记录但不查\noldchoose = choose\nfunction new_choose(options)\n    local optxt = \"\"\n    for k=1,#options do\n        optxt = optxt ..\"&\"..options[k]\n    end\n    local key = LOMS_Hash(\"Choose_\"..optxt)\n    if not LOMS_Finished() then\n        local lastS = tonumber(LOMS_GetKV(key))\n        if lastS ~= nil then\n            return lastS\n        end\n        local rs = oldchoose(options)\n        LOMS_SaveKV(key, rs)\n        return rs\n    end\n    local rs = oldchoose(options)\n    LOMS_SaveKV(key, rs)\n    return rs\nend\nchoose = new_choose\n\n-- wait 替换：读档时跳过 wait，正常游戏执行 oldwait\noldwait = wait\nfunction new_wait(duration)\n    if LOMS_Finished() then\n        oldwait(duration)\n        return\n    end\nend\nwait = new_wait\n\n-- setcharacter 替换：读档时记录立绘变化，正常游戏直接透传\nlastsetcharacter = nil\noldsetcharacter = setcharacter\nfunction new_setcharacter(a, b)\n    if not LOMS_Finished() then\n        lastsetcharacter = lastsetcharacter or {}\n        lastsetcharacter[a] = b\n    end\n    oldsetcharacter(a, b)\nend\nsetcharacter = new_setcharacter\n\nend -- if not LOMS_HELPERS_LOADED\n";

	internal static bool _luaHelpersInstalled;

	internal static int _executeLuaScriptCallCount;

	private static int _audioDiagCount;

	[HarmonyPrefix]
	[HarmonyPatch(typeof(SaveSystem), "SaveGameData", new Type[] { typeof(string) })]
	private static bool SaveGameDataPrefix(SaveSystem __instance, string slot)
	{
		UnityEngine.Debug.LogWarning("[LOMAssistant.Save] 存档！");
		FileLogger.Log("[SavePatch] SaveGameDataPrefix: slot=" + slot);
		string text = (string)Traverse.Create(__instance).Method("GetSaveDataFile", new Type[1] { typeof(string) }).GetValue(slot);
		text += ".ss";
		GameSave gameSave = Traverse.Create(__instance).Method("CreateSaveData").GetValue() as GameSave;
		FileLogger.Log("[SavePatch] SaveGameDataPrefix: ssFile=" + text + ", gameSave.CurrentStoryScript=" + ((gameSave != null) ? gameSave.CurrentStoryScript : "null"));
		SaveManager.Instance.SaveDataToFile(text, gameSave);
		return true;
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(SaveSystem), "DeleteSaveData", new Type[] { typeof(string) })]
	private static void DeleteSaveDataPrefix(SaveSystem __instance, string slot)
	{
		string text = (string)Traverse.Create(__instance).Method("GetSaveDataFile", new Type[1] { typeof(string) }).GetValue(slot);
		text += ".ss";
		if (File.Exists(text))
		{
			File.Delete(text);
		}
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(SaveSystem), "NewGameData", new Type[] { })]
	private static void NewGameDataPrefix(SaveSystem __instance)
	{
		SaveManager.Instance.ResetData();
		FileLogger.Log("[SavePatch] NewGameDataPrefix: ResetData called for new game");
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(SaveSystem), "ExecuteLoadGameData", new Type[] { typeof(GameSave) })]
	private static bool ExecuteLoadGameDataPrefix(SaveSystem __instance, ref GameSave gameSave)
	{
		UnityEngine.Debug.LogWarning("[LOMAssistant.Save] 读档！");
		string text = (string)Traverse.Create(__instance).Field("_currentSlot").GetValue();
		string text2 = (string)Traverse.Create(__instance).Method("GetSaveDataFile", new Type[1] { typeof(string) }).GetValue(text);
		text2 += ".ss";
		FileLogger.Log("[SavePatch] ExecuteLoadGameDataPrefix: slot=" + text + ", ssFile=" + text2 + ", gameSave.CurrentStoryScript=" + gameSave.CurrentStoryScript + ", gameSave.StartStoryScript=" + gameSave.StartStoryScript);
		SaveManager.Instance.LoadData(text2, gameSave);
		bool num = gameSave.CurrentScene == "Story";
		if (!string.IsNullOrEmpty(gameSave.CurrentStoryScript))
		{
			gameSave.StartStoryScript = gameSave.CurrentStoryScript;
		}
		if (num)
		{
			SaveManager.Instance.SavedGameSave = gameSave;
			FileLogger.Log("[SavePatch] ExecuteLoadGameDataPrefix: Scene=Story, saved gameSave for post-load restore, StartStoryScript=" + gameSave.StartStoryScript);
		}
		else
		{
			FileLogger.Log("[SavePatch] ExecuteLoadGameDataPrefix: Scene=" + gameSave.CurrentScene + " (not Story), using gameSave directly, StartStoryScript=" + gameSave.StartStoryScript);
		}
		if (SaveManager.Instance.Data.NeedSpeed && SaveManager.Instance.IsLoading)
		{
			SaveManager.Instance.ShowLoadScene();
		}
		return true;
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(MenuPanel), "ToggleSaveButton", new Type[] { typeof(bool) })]
	private static bool ToggleSaveButtonPrefix(MenuPanel __instance, ref bool active)
	{
		active = true;
		return true;
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(LuaManager), "ChangeScene")]
	private static bool ChangeScenePrefix(LuaManager __instance, string name, string key, string nextScene)
	{
		if (name == "Battle")
		{
			SaveManager.Instance.SetRealScript();
		}
		return true;
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(PlayerStatManagerData), "SetStoryScript")]
	private static bool PlayerStatManagerData_Save_Prefix(PlayerStatManagerData __instance, string script)
	{
		SaveManager.Instance.SaveSafe(script);
		return true;
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(PlayerStatManagerData), "Save", new Type[] { typeof(GameSave) })]
	private static bool PlayerStatManagerData_Save_Prefix(PlayerStatManagerData __instance, ref GameSave gameSave)
	{
		string text = (string)Traverse.Create(__instance).Field("_currentStoryScript").GetValue();
		gameSave.CurrentStoryScript = text;
		FileLogger.Log("[SavePatch] PlayerStatManagerData.Save: _currentStoryScript=" + text + ", gameSave.CurrentStoryScript set");
		return true;
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(LuaManager), "GetStoryText")]
	private static bool GetStoryText_Prefix(LuaManager __instance, ref string __result, string key)
	{
		if (key.StartsWith("__LOMS__"))
		{
			__result = SaveManager.Instance.LuaTool(key);
			return false;
		}
		return true;
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(LuaManager), "PlayMusic")]
	private static void LuaManagerPlayMusicHarmonyPostfix(LuaManager __instance, string name)
	{
		SaveManager.Instance.SetKV("Music", name, withScript: false);
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(LuaManager), "PlayEnvSound")]
	private static void LuaManagerPlayEnvSoundHarmonyPostfix(LuaManager __instance, string name)
	{
		SaveManager.Instance.SetKV("EnvSound", name, withScript: false);
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(CharacterIntroPanel), "Show", new Type[]
	{
		typeof(string),
		typeof(int)
	})]
	private static bool CharacterIntroPanel_Show_Prefix(CharacterIntroPanel __instance, IEnumerator __result, string key)
	{
		string md5Hash = SaveManager.GetMd5Hash("IntroShow_" + key);
		string value = SaveManager.Instance.GetValue(md5Hash);
		if (SaveManager.Instance.IsLoading && value == "1")
		{
			try
			{
				if (DataHelper.TryParseEnum<RelationshipStatType>(key, out var result))
				{
					RelationshipStat relationshipStat = PlayerStatManagerData.Instance.Relationships.Get(result);
					if (relationshipStat != null && !relationshipStat.Active)
					{
						relationshipStat.SetActive(active: true);
						FileLogger.Log("[SavePatch] CharacterIntroPanel_Show_Prefix: skipped panel for '" + key + "', manually set RelationshipStat.Active=true");
					}
				}
			}
			catch (Exception ex)
			{
				FileLogger.LogError("[SavePatch] CharacterIntroPanel_Show_Prefix: failed to set Active for '" + key + "': " + ex.Message);
			}
			return false;
		}
		if (value != "1")
		{
			SaveManager.Instance.SetKV(md5Hash, "1");
		}
		return true;
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(DiceMenuDialog), "ExecuteRoll")]
	private static bool ExecuteRoll_Prefix(DiceMenuDialog __instance, IEnumerator __result, string[] options, int destinyPoint, string checkName)
	{
		string text = "";
		foreach (string text2 in options)
		{
			text = text + "|" + text2;
		}
		string md5Hash = SaveManager.GetMd5Hash("Dice_" + text);
		if (SaveManager.Instance.IsLoading)
		{
			string value = SaveManager.Instance.GetValue(md5Hash);
			if (!string.IsNullOrEmpty(value))
			{
				int num = int.Parse(value);
				Traverse.Create(__instance).Field("_resultSelection").SetValue(num);
				return false;
			}
		}
		_rollLastKey = md5Hash;
		return true;
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(DiceMenuDialog), "OnDisable")]
	private static bool ExecuteRoll_OnDisable_Prefix(DiceMenuDialog __instance)
	{
		if (!SaveManager.Instance.IsLoading)
		{
			SaveManager.Instance.SetKV(_rollLastKey, __instance.ResultSelection.ToString());
		}
		return true;
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(CharacterPlaceholder), "Get")]
	private static bool CharacterPlaceholderGet_Prefix(CharacterPlaceholder __instance, ref Character __result, string key)
	{
		if (!Traverse.Create(__instance).Field<Dictionary<string, Character>>("_characters").Value.ContainsKey(key))
		{
			LuaManager.Instance.StartCoroutine(__instance.LoadCharacterAsset(key));
		}
		return true;
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(PortraitController), "Show", new Type[] { typeof(Table) })]
	private static bool PortraitControllerShow_Prefix(PortraitController __instance, Table optionsTable)
	{
		Character character = optionsTable.Get("character").ToObject<Character>();
		if (character == null)
		{
			return true;
		}
		SaveData.PortraitData portrait = SaveManager.Instance.GetPortrait(character.name);
		if (!optionsTable.Get("toPosition").IsNil())
		{
			portrait.toPosition = optionsTable.Get("toPosition").CastToString();
		}
		if (!optionsTable.Get("facing").IsNil())
		{
			portrait.face = optionsTable.Get("facing").CastToString();
		}
		return true;
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(PortraitController), "Hide", new Type[] { typeof(Table) })]
	private static bool PortraitController_Prefix(PortraitController __instance, Table optionsTable)
	{
		Character character = optionsTable.Get("character").ToObject<Character>();
		if (character == null)
		{
			return true;
		}
		SaveManager.Instance.RemovePortrait(character.name);
		return true;
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(LuaManager), "ExecuteLuaScript", new Type[] { })]
	private static void ExecuteLuaScript_HarmonyPrefix(LuaManager __instance)
	{
		_executeLuaScriptCallCount++;
		string text = "";
		try
		{
			text = PlayerStatManagerData.Instance.CurrentStoryScript ?? "null";
		}
		catch
		{
			text = "error";
		}
		__instance.ExecuteScript("\nif not LOMS_HELPERS_LOADED then\nLOMS_HELPERS_LOADED = true\n\nfunction LOMS_Command(cmd, tbParam)\n    param = json.serialize(tbParam)\n    rs = luamanager.GetStoryText(\"__LOMS__\"..cmd..\"|\"..param)\n    return json.parse(rs)\nend\n\nfunction LOMS_SaveKV(key, value)\n    LOMS_Command(\"SaveKV\", {K=key, V=value})\nend\n\nfunction LOMS_GetKV(key)\n    return LOMS_Command(\"GetKV\", {K=key})[\"rs\"]\nend\n\nfunction LOMS_Hash(key)\n    return LOMS_Command(\"Hash\", {K=key})[\"rs\"]\nend\n\nfunction LOMS_Finished()\n    return LOMS_Command(\"Finish\", {})[\"rs\"] == \"1\"\nend\n\nfunction LOMS_GetPortrait(key)\n    local data = LOMS_Command(\"GetPortrait\", {K=key})\n    if data[\"rs\"] ~= \"1\" then\n        return nil\n    end\n    return data\nend\n\n-- say 替换：读档时查 KV 跳过已读，正常游戏记录但不查\noldsay = say\nfunction new_say(text, voiceclip)\n    if not LOMS_Finished() then\n        local key = LOMS_Hash(\"Say_\"..text)\n        if LOMS_GetKV(key) == \"1\" then\n            return\n        end\n        if lastsetcharacter ~= nil then\n            oldwait(0.1)\n            for k,v in pairs(lastsetcharacter) do\n                local por = LOMS_GetPortrait(k.name)\n                if por ~= nil then\n                    runwait(characters.LoadCharacterAsset(k.name))\n                    stage.show{character=k, fromPosition=\"M\", toPosition=por[\"to\"], facing=por[\"face\"], useDefaultSettings=false}\n                    stage.showPortrait(k,v)\n                end\n            end\n            lastsetcharacter = nil\n        end\n        oldsay(text,voiceclip)\n        LOMS_SaveKV(key,1)\n    else\n        oldsay(text,voiceclip)\n        LOMS_SaveKV(LOMS_Hash(\"Say_\"..text), 1)\n    end\nend\nsay = new_say\n\n-- choose 替换：读档时查 KV 回放选择，正常游戏记录但不查\noldchoose = choose\nfunction new_choose(options)\n    local optxt = \"\"\n    for k=1,#options do\n        optxt = optxt ..\"&\"..options[k]\n    end\n    local key = LOMS_Hash(\"Choose_\"..optxt)\n    if not LOMS_Finished() then\n        local lastS = tonumber(LOMS_GetKV(key))\n        if lastS ~= nil then\n            return lastS\n        end\n        local rs = oldchoose(options)\n        LOMS_SaveKV(key, rs)\n        return rs\n    end\n    local rs = oldchoose(options)\n    LOMS_SaveKV(key, rs)\n    return rs\nend\nchoose = new_choose\n\n-- wait 替换：读档时跳过 wait，正常游戏执行 oldwait\noldwait = wait\nfunction new_wait(duration)\n    if LOMS_Finished() then\n        oldwait(duration)\n        return\n    end\nend\nwait = new_wait\n\n-- setcharacter 替换：读档时记录立绘变化，正常游戏直接透传\nlastsetcharacter = nil\noldsetcharacter = setcharacter\nfunction new_setcharacter(a, b)\n    if not LOMS_Finished() then\n        lastsetcharacter = lastsetcharacter or {}\n        lastsetcharacter[a] = b\n    end\n    oldsetcharacter(a, b)\nend\nsetcharacter = new_setcharacter\n\nend -- if not LOMS_HELPERS_LOADED\n");
		FileLogger.Log("[SavePatch] ExecuteLuaScript #" + _executeLuaScriptCallCount + ": helpers injected, IsLoading=" + SaveManager.Instance.IsLoading + ", CurrentStoryScript=" + text + ", _tempKeys.Count=" + (SaveManager.Instance.IsLoading ? SaveManager.Instance.Data.KV.Count.ToString() : "N/A"));
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(ViewFlowchartController), "ShowView", new Type[] { typeof(string) })]
	private static bool ViewFlowchartController_ShowView_Prefix(ViewFlowchartController __instance, string name)
	{
		SaveManager.Instance.SetCurView(name);
		return true;
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(LuaManager), "Init")]
	private static void LuaManager_Postfix(LuaManager __instance)
	{
		FileLogger.Log("[SavePatch] LuaManager_Postfix: Init called. IsLoading=" + SaveManager.Instance.IsLoading);
		if (SaveManager.Instance.IsLoading)
		{
			string value = SaveManager.Instance.GetValue("Music", withScript: false);
			string value2 = SaveManager.Instance.GetValue("EnvSound", withScript: false);
			FileLogger.Log("[SavePatch] LuaManager_Postfix: consumed Music/EnvSound (Music=" + (value ?? "null") + ", EnvSound=" + (value2 ?? "null") + ")");
			if (!string.IsNullOrEmpty(value))
			{
				LuaManager.Instance.PlayMusic(value);
			}
			if (!string.IsNullOrEmpty(value2))
			{
				LuaManager.Instance.PlayEnvSound(value2);
			}
		}
	}

	private static bool ShouldSuppressAudio()
	{
		if (SaveManager.Instance != null && SaveManager.Instance.IsLoading && SaveManager.Instance.Data.NeedSpeed)
		{
			return !SaveManager.Instance.AllowAudio;
		}
		return false;
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(AudioSource), "Play", new Type[] { })]
	private static bool AudioSource_Play_Prefix(AudioSource __instance)
	{
		if (_audioDiagCount < 5)
		{
			_audioDiagCount++;
			FileLogger.Log("[AudioPatch] AudioSource.Play() called on " + ((__instance != null) ? __instance.gameObject.name : "null") + ", suppress=" + ShouldSuppressAudio());
		}
		return !ShouldSuppressAudio();
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(AudioSource), "Play", new Type[] { typeof(ulong) })]
	private static bool AudioSource_Play_Delay_Prefix(ulong delay)
	{
		if (_audioDiagCount < 5)
		{
			_audioDiagCount++;
			FileLogger.Log("[AudioPatch] AudioSource.Play(ulong) called, suppress=" + ShouldSuppressAudio());
		}
		return !ShouldSuppressAudio();
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(AudioSource), "PlayOneShot", new Type[] { typeof(AudioClip) })]
	private static bool AudioSource_PlayOneShot_Prefix1(AudioClip clip)
	{
		if (_audioDiagCount < 5)
		{
			_audioDiagCount++;
			FileLogger.Log("[AudioPatch] AudioSource.PlayOneShot(clip) called, clip=" + ((clip != null) ? clip.name : "null") + ", suppress=" + ShouldSuppressAudio());
		}
		return !ShouldSuppressAudio();
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(AudioSource), "PlayOneShot", new Type[]
	{
		typeof(AudioClip),
		typeof(float)
	})]
	private static bool AudioSource_PlayOneShot_Prefix2(AudioClip clip, float volumeScale)
	{
		if (_audioDiagCount < 5)
		{
			_audioDiagCount++;
			FileLogger.Log("[AudioPatch] AudioSource.PlayOneShot(clip,scale) called, clip=" + ((clip != null) ? clip.name : "null") + ", suppress=" + ShouldSuppressAudio());
		}
		return !ShouldSuppressAudio();
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(AudioSource), "PlayDelayed", new Type[] { typeof(float) })]
	private static bool AudioSource_PlayDelayed_Prefix(float delay)
	{
		if (_audioDiagCount < 5)
		{
			_audioDiagCount++;
			FileLogger.Log("[AudioPatch] AudioSource.PlayDelayed called, suppress=" + ShouldSuppressAudio());
		}
		return !ShouldSuppressAudio();
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(AudioSource), "PlayScheduled", new Type[] { typeof(double) })]
	private static bool AudioSource_PlayScheduled_Prefix(double time)
	{
		if (_audioDiagCount < 5)
		{
			_audioDiagCount++;
			FileLogger.Log("[AudioPatch] AudioSource.PlayScheduled called, suppress=" + ShouldSuppressAudio());
		}
		return !ShouldSuppressAudio();
	}

	public static bool AkSoundEngine_PostEvent_Prefix(string in_pszEventName, GameObject in_gameObjectID)
	{
		bool flag = ShouldSuppressAudio();
		if (_audioDiagCount < 15)
		{
			_audioDiagCount++;
			FileLogger.Log("[AudioPatch] AkSoundEngine.PostEvent: event=" + (in_pszEventName ?? "null") + ", obj=" + ((in_gameObjectID != null) ? in_gameObjectID.name : "null") + ", suppress=" + flag);
		}
		return !flag;
	}

	public static void RestoreMusicOnLoadComplete()
	{
		try
		{
			string value;
			string text = (SaveManager.Instance.Data.KV.TryGetValue("Music", out value) ? value : null);
			string value2;
			string text2 = (SaveManager.Instance.Data.KV.TryGetValue("EnvSound", out value2) ? value2 : null);
			FileLogger.Log("[SavePatch] RestoreMusicOnLoadComplete: Music=" + (text ?? "null") + ", EnvSound=" + (text2 ?? "null"));
			StopAllWwiseEvents();
			SoundManager instance = SoundManager.Instance;
			if (instance != null)
			{
				Traverse.Create(instance).Field("_currentMusic").SetValue(null);
				Traverse.Create(instance).Field("_currentEnvSound").SetValue(null);
			}
			if (!string.IsNullOrEmpty(text))
			{
				LuaManager.Instance.PlayMusic(text);
				FileLogger.Log("[SavePatch] RestoreMusicOnLoadComplete: restored Music=" + text);
			}
			if (!string.IsNullOrEmpty(text2))
			{
				LuaManager.Instance.PlayEnvSound(text2);
				FileLogger.Log("[SavePatch] RestoreMusicOnLoadComplete: restored EnvSound=" + text2);
			}
		}
		catch (Exception ex)
		{
			FileLogger.LogError("[SavePatch] RestoreMusicOnLoadComplete failed: " + ex.Message);
		}
		RestoreStatsFromGameSave();
	}

	private static void StopAllWwiseEvents()
	{
		try
		{
			Type type = AccessTools.TypeByName("AkSoundEngine");
			if (type == null)
			{
				FileLogger.LogWarning("[SavePatch] StopAllWwiseEvents: AkSoundEngine type not found, skip");
				return;
			}
			MethodInfo methodInfo = AccessTools.Method(type, "PostEvent", new Type[2]
			{
				typeof(string),
				typeof(GameObject)
			});
			if (methodInfo == null)
			{
				FileLogger.LogWarning("[SavePatch] StopAllWwiseEvents: PostEvent method not found, skip");
				return;
			}
			GameObject gameObject = ((SoundManager.Instance != null) ? SoundManager.Instance.gameObject : null);
			if (gameObject == null)
			{
				FileLogger.LogWarning("[SavePatch] StopAllWwiseEvents: SoundManager gameObject is null, skip");
				return;
			}
			methodInfo.Invoke(null, new object[2] { "Stop_All", gameObject });
			FileLogger.Log("[SavePatch] StopAllWwiseEvents: Stop_All posted to clear residual audio");
		}
		catch (Exception ex)
		{
			FileLogger.LogError("[SavePatch] StopAllWwiseEvents failed: " + ex.Message);
		}
	}

	private static void RestoreStatsFromGameSave()
	{
		try
		{
			GameSave savedGameSave = SaveManager.Instance.SavedGameSave;
			if (savedGameSave == null)
			{
				FileLogger.Log("[SavePatch] RestoreStatsFromGameSave: no SavedGameSave, skip");
				return;
			}
			PlayerStatManagerData instance = PlayerStatManagerData.Instance;
			if (instance == null)
			{
				FileLogger.LogWarning("[SavePatch] RestoreStatsFromGameSave: PlayerStatManagerData.Instance is null, skip");
				return;
			}
			BattleSkillDataCollection fieldValue = instance.GetFieldValue<BattleSkillDataCollection>("_battleSkills");
			PlayerTalentDataCollection fieldValue2 = instance.GetFieldValue<PlayerTalentDataCollection>("_talents");
			UpgradeItemCollectionData fieldValue3 = instance.GetFieldValue<UpgradeItemCollectionData>("_poisonUpgradeItems");
			UpgradeItemCollectionData fieldValue4 = instance.GetFieldValue<UpgradeItemCollectionData>("_weaponUpgradeItems");
			string fieldValue5 = instance.GetFieldValue<string>("_battleSkill_1");
			string fieldValue6 = instance.GetFieldValue<string>("_battleSkill_2");
			string fieldValue7 = instance.GetFieldValue<string>("_battleSkill_3");
			string fieldValue8 = instance.GetFieldValue<string>("_battleSkill_4");
			FacilityConfigData fieldValue9 = instance.GetFieldValue<FacilityConfigData>("_facility");
			MethodInfo methodInfo = AccessTools.Method(typeof(PlayerStatManagerData), "Load", new Type[1] { typeof(GameSave) });
			if (methodInfo != null)
			{
				methodInfo.Invoke(instance, new object[1] { savedGameSave });
				FileLogger.Log("[SavePatch] RestoreStatsFromGameSave: PlayerStatManagerData.Load called with SavedGameSave, stats restored to save-time values");
			}
			else
			{
				FileLogger.LogError("[SavePatch] RestoreStatsFromGameSave: PlayerStatManagerData.Load(GameSave) method not found!");
			}
			instance.SetPrivateField("_battleSkills", fieldValue);
			instance.SetPrivateField("_talents", fieldValue2);
			instance.SetPrivateField("_poisonUpgradeItems", fieldValue3);
			instance.SetPrivateField("_weaponUpgradeItems", fieldValue4);
			instance.SetPrivateField("_battleSkill_1", fieldValue5);
			instance.SetPrivateField("_battleSkill_2", fieldValue6);
			instance.SetPrivateField("_battleSkill_3", fieldValue7);
			instance.SetPrivateField("_battleSkill_4", fieldValue8);
			instance.SetPrivateField("_facility", fieldValue9);
			FileLogger.Log("[SavePatch] RestoreStatsFromGameSave: restored post-script skills/talents/upgrades/facility");
			SaveManager.Instance.SavedGameSave = null;
		}
		catch (Exception ex)
		{
			FileLogger.LogError("[SavePatch] RestoreStatsFromGameSave failed: " + ex.Message + "\n" + ex.StackTrace);
		}
	}
}
