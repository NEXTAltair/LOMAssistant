using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using HarmonyLib;
using Mortal.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LOMAssistant.Features.Save;

public class SaveManager
{
	private int _loading;

	private List<string> _tempKeys = new List<string>();

	private bool _showLoading;

	private bool _keepCloseLoad;

	private bool _firstSet = true;

	private float _originalVolume = 1f;

	private int _lastTempKeysCount = -1;

	private float _tempKeysStuckTime;

	private string _saveFileCurView;

	private bool _softTimeoutDone;

	private bool _loadCompleteNotified;

	public static SaveManager Instance { get; private set; }

	public SaveData Data { get; private set; } = new SaveData();

	public string SaveFileCurView => _saveFileCurView;

	public bool IsLoading => _loading == 1;

	public bool AllowAudio { get; set; }

	public GameSave SavedGameSave { get; set; }

	public static event Action OnLoadComplete;

	public static void Init()
	{
		Instance = new SaveManager();
	}

	public void ResetData()
	{
		Data = new SaveData();
		_loading = -1;
		_firstSet = true;
		_tempKeys = new List<string>();
		_saveFileCurView = null;
		_softTimeoutDone = false;
		_loadCompleteNotified = false;
		SavedGameSave = null;
		AllowAudio = false;
		FileLogger.Log("[Save] ResetData: all data cleared for new game, _loading=-1, _firstSet=true");
	}

	public void SaveDataToFile(string file, GameSave gs)
	{
		string contents = JsonConvert.SerializeObject(Data);
		File.WriteAllText(file, contents);
		string contents2 = JsonConvert.SerializeObject(gs);
		File.WriteAllText(file + ".json", contents2);
		FileLogger.Log("[Save] SaveDataToFile: file=" + file + ", KV.Count=" + Data.KV.Count + ", LastScript=" + Data.LastScript + ", RealLastScript=" + Data.RealLastScript + ", lastSave=" + ((Data.lastSave != null) ? "not null" : "null"));
	}

	public void SetCurView(string name)
	{
		UnityEngine.Debug.LogWarning("[LOMAssistant.Save] SetCurView: " + name);
		Data.CurView = name;
	}

	public void LoadData(string file, GameSave gameSave)
	{
		if (!File.Exists(file))
		{
			Data = new SaveData();
			FileLogger.Log("[Save] LoadData: file NOT exist, created empty Data. file=" + file);
		}
		else
		{
			try
			{
				Data = JsonConvert.DeserializeObject<SaveData>(File.ReadAllText(file));
				UnityEngine.Debug.LogWarning("[LOMAssistant.Save] 读档成功");
				FileLogger.Log("[Save] LoadData: file exist, deserialized. KV.Count=" + ((Data.KV != null) ? Data.KV.Count.ToString() : "null") + ", LastScript=" + Data.LastScript + ", RealLastScript=" + Data.RealLastScript + ", lastSave=" + ((Data.lastSave != null) ? "not null" : "null"));
			}
			catch (Exception ex)
			{
				LOMAssistantPlugin.LogError("读档解析失败: " + ex.Message);
				FileLogger.LogError("[Save] LoadData: deserialize failed: " + ex.Message);
				Data = new SaveData();
			}
		}
		if (Data.portrait == null)
		{
			Data.portrait = new Dictionary<string, SaveData.PortraitData>();
		}
		Data.Scene = gameSave.CurrentScene;
		_saveFileCurView = Data.CurView;
		UnityEngine.Debug.Log("[LOMAssistant.Save] Scene: " + Data.Scene);
		_loading = 1;
		_firstSet = true;
		_loadCompleteNotified = false;
		_softTimeoutDone = false;
		SavePatches._luaHelpersInstalled = false;
		SavePatches._executeLuaScriptCallCount = 0;
		_tempKeys = new List<string>(Data.KV.Keys);
		FileLogger.Log("[Save] LoadData: set _loading=1, _firstSet=true, _tempKeys.Count=" + _tempKeys.Count + " (no filtering, all KV entries included), Scene=" + Data.Scene);
		FileLogger.Log("[Save] LoadData DIAG: portrait.Count=" + ((Data.portrait != null) ? Data.portrait.Count.ToString() : "null") + ", KV.Count=" + ((Data.KV != null) ? Data.KV.Count.ToString() : "null") + ", NeedSpeed=" + Data.NeedSpeed + ", CurView=" + Data.CurView);
		if (Data.Scene != "Story")
		{
			_loading = -1;
			FileLogger.Log("[Save] LoadData: Scene=" + Data.Scene + " (not Story), set _loading=-1 directly (no fast-forward needed)");
			return;
		}
		bool flag = false;
		foreach (string tempKey in _tempKeys)
		{
			if (tempKey.Contains("|") || (tempKey.Length == 32 && !tempKey.Equals("Music") && !tempKey.Equals("EnvSound")))
			{
				flag = true;
				break;
			}
		}
		if (!flag)
		{
			_loading = -1;
			UnityEngine.Debug.Log("[LOMAssistant.Save] 无已读对话 KV，跳过读档加速");
			FileLogger.Log("[Save] LoadData: no dialogue entries in _tempKeys, set _loading=-1 (Count=" + _tempKeys.Count + ")");
		}
		if (Data.KV == null || Data.KV.Count <= 0)
		{
			return;
		}
		StringBuilder stringBuilder = new StringBuilder("[Save] LoadData: KV keys:");
		int num = 0;
		foreach (string key in Data.KV.Keys)
		{
			stringBuilder.Append("\n  [").Append(num++).Append("] ")
				.Append(key);
			if (num >= 50)
			{
				stringBuilder.Append("\n  ... (truncated, total=").Append(Data.KV.Count).Append(")");
				break;
			}
		}
		FileLogger.Log(stringBuilder.ToString());
	}

	public static string GetMd5Hash(string input)
	{
		using MD5 mD = MD5.Create();
		byte[] bytes = Encoding.UTF8.GetBytes(input);
		byte[] array = mD.ComputeHash(bytes);
		StringBuilder stringBuilder = new StringBuilder();
		for (int i = 0; i < array.Length; i++)
		{
			stringBuilder.Append(array[i].ToString("x2"));
		}
		return stringBuilder.ToString();
	}

	public void SetKV(string k, string v, bool withScript = true)
	{
		if (withScript)
		{
			Data.KV[k] = v;
			FileLogger.Log("[Save] SetKV: key=" + k + ", value=" + v);
		}
		else
		{
			Data.KV[k] = v;
			FileLogger.Log("[Save] SetKV (noScript): key=" + k + ", value=" + v);
		}
	}

	public string GetValue(string k, bool withScript = true)
	{
		if (_loading == 1)
		{
			UnityEngine.Debug.Log("[LOMAssistant.Save] GetValue: " + k);
		}
		string text = k;
		if (withScript && !Data.KV.TryGetValue(k, out var _))
		{
			string text2 = FindOldFormatKey(k);
			if (text2 != null)
			{
				text = text2;
				FileLogger.Log("[Save] GetValue: key=" + k + " not found, found old format: " + text2);
			}
		}
		if (!Data.KV.TryGetValue(text, out var value2))
		{
			if (_loading == 1)
			{
				FileLogger.Log("[Save] GetValue MISS: key=" + text + ", _loading=" + _loading);
			}
			if (withScript && _loading == 1)
			{
				_loading = -1;
				FileLogger.Log("[Save] GetValue: _loading 1→-1 (first miss during loading)");
				if (Data.NeedSpeed)
				{
					Time.timeScale = 1f;
					AudioListener.volume = _originalVolume;
					AudioListener.pause = false;
					HideLoadScene();
				}
				NotifyLoadComplete();
			}
			return "";
		}
		if (_loading == 1)
		{
			FileLogger.Log("[Save] GetValue HIT: fullKey=" + text + ", value=" + value2);
		}
		_tempKeys.Remove(text);
		if (_tempKeys.Count == 0)
		{
			FileLogger.Log("[Save] GetValue: _tempKeys empty, restoring timeScale=1, _loading=-1");
			_loading = -1;
			Time.timeScale = 1f;
			AudioListener.volume = _originalVolume;
			AudioListener.pause = false;
			HideLoadScene();
			NotifyLoadComplete();
		}
		return value2;
	}

	private string FindOldFormatKey(string hash)
	{
		foreach (string key in Data.KV.Keys)
		{
			int num = key.IndexOf('|');
			if (num >= 0 && key.Substring(num + 1) == hash)
			{
				return key;
			}
		}
		return null;
	}

	private void NotifyLoadComplete()
	{
		if (_loadCompleteNotified)
		{
			return;
		}
		_loadCompleteNotified = true;
		FileLogger.Log("[Save] NotifyLoadComplete: invoking OnLoadComplete event");
		try
		{
			SaveManager.OnLoadComplete?.Invoke();
		}
		catch (Exception ex)
		{
			FileLogger.LogError("[Save] NotifyLoadComplete event handler exception: " + ex.Message);
		}
	}

	public void ShowLoadScene()
	{
		UnityEngine.Debug.Log("[LOMAssistant.Save] ShowLoadScene");
		_originalVolume = AudioListener.volume;
		AudioListener.volume = 0f;
		AudioListener.pause = true;
		SceneManager.LoadScene("Loading1", new LoadSceneParameters(LoadSceneMode.Additive));
		_showLoading = true;
	}

	public void HideLoadScene()
	{
		if (_showLoading)
		{
			SceneManager.UnloadScene("Loading1");
			_showLoading = false;
			_keepCloseLoad = false;
			AudioListener.volume = _originalVolume;
			AudioListener.pause = false;
			UnityEngine.Debug.Log("[LOMAssistant.Save] HideLoadScene done");
		}
	}

	public string LuaTool(string key)
	{
		string rs = "";
		key = key.Substring(8);
		string[] array = key.Split('|');
		string text = array[0].ToLower();
		JObject jObject = JsonConvert.DeserializeObject<JObject>(array[1]);
		switch (text)
		{
		case "savekv":
			SetKV(jObject["K"].ToString(), jObject["V"].ToString());
			break;
		case "getkv":
			rs = GetValue(jObject["K"].ToString());
			break;
		case "hash":
			rs = GetMd5Hash(jObject["K"].ToString());
			break;
		case "finish":
			rs = (IsLoading ? "0" : "1");
			break;
		case "getportrait":
		{
			string key2 = jObject["K"].ToString();
			Data.portrait.TryGetValue(key2, out var value);
			if (value != null)
			{
				return JsonConvert.SerializeObject(new
				{
					rs = "1",
					to = value.toPosition,
					face = value.face
				});
			}
			rs = "0";
			break;
		}
		case "log":
			FileLogger.Log("[LuaLog] " + jObject["M"]);
			rs = "1";
			break;
		case "pauseload":
			if (IsLoading)
			{
				FileLogger.Log("[LuaLog] pauseload called from Lua (loading)");
				SetLoadingFinished();
			}
			rs = "1";
			break;
		}
		return JsonConvert.SerializeObject(new { rs });
	}

	public void SetLoadingFinished()
	{
		_loading = -1;
		Time.timeScale = 1f;
		AudioListener.volume = _originalVolume;
		AudioListener.pause = false;
		HideLoadScene();
		NotifyLoadComplete();
		FileLogger.Log("[Save] SetLoadingFinished: _loading=-1, timeScale=1, load scene hidden");
	}

	public void SetRealScript()
	{
		if (!string.IsNullOrEmpty(Data.RealLastScript))
		{
			Data.LastScript = null;
			SaveSafe(Data.RealLastScript);
		}
	}

	public void SaveSafe(string script)
	{
		FileLogger.Log("[Save] SaveSafe: called with script=" + script + ", _firstSet=" + _firstSet + ", LastScript(before)=" + Data.LastScript);
		if (_firstSet)
		{
			_firstSet = false;
			try
			{
				GameSave initialSave = Traverse.Create(SaveSystem.Instance).Method("CreateSaveData").GetValue() as GameSave;
				Data.initialSave = initialSave;
				FileLogger.Log("[Save] SaveSafe: first call, captured initialSave (clean state before any script modifications in this story)");
			}
			catch (Exception ex)
			{
				FileLogger.LogError("[Save] SaveSafe: failed to capture initialSave: " + ex.Message);
			}
			FileLogger.Log("[Save] SaveSafe: first call, set _firstSet=false, LastScript unchanged");
			return;
		}
		Data.RealLastScript = script;
		try
		{
			if (!IsLoading)
			{
				GameSave lastSave = Traverse.Create(SaveSystem.Instance).Method("CreateSaveData").GetValue() as GameSave;
				Data.lastSave = lastSave;
				FileLogger.Log("[Save] SaveSafe: set lastSave, KV.Count=" + Data.KV.Count + " (no cleanup of old script entries)");
			}
			else
			{
				FileLogger.Log("[Save] SaveSafe: IsLoading=true, skip lastSave update to preserve original snapshot");
			}
		}
		catch (Exception ex2)
		{
			LOMAssistantPlugin.LogError("SaveSafe 失败: " + ex2.Message);
			FileLogger.LogError("[Save] SaveSafe failed: " + ex2.Message);
		}
		Data.LastScript = script;
		FileLogger.Log("[Save] SaveSafe: set LastScript=" + Data.LastScript);
	}

	public GameSave GetSave()
	{
		return Data.lastSave;
	}

	public void RemovePortrait(string character)
	{
		Data.portrait.Remove(character);
	}

	public SaveData.PortraitData GetPortrait(string character)
	{
		Data.portrait.TryGetValue(character, out var value);
		if (value == null)
		{
			value = new SaveData.PortraitData();
			Data.portrait[character] = value;
		}
		return value;
	}

	public void Update()
	{
		if (IsLoading && Data.NeedSpeed && !_softTimeoutDone)
		{
			Time.timeScale = 50f;
			AudioListener.volume = 0f;
			AudioListener.pause = true;
		}
		if (_keepCloseLoad)
		{
			HideLoadScene();
		}
		if (IsLoading)
		{
			int count = _tempKeys.Count;
			if (count != _lastTempKeysCount)
			{
				_lastTempKeysCount = count;
				_tempKeysStuckTime = Time.realtimeSinceStartup;
			}
			else if (!_softTimeoutDone && Time.realtimeSinceStartup - _tempKeysStuckTime > 8f)
			{
				_softTimeoutDone = true;
				FileLogger.Log("[Save] Update: _tempKeys stuck at " + count + " for 8s, soft timeout");
				Time.timeScale = 1f;
				AudioListener.volume = _originalVolume;
				AudioListener.pause = false;
				AllowAudio = true;
				HideLoadScene();
			}
		}
	}

	public void LateUpdate()
	{
		if (IsLoading && Data.NeedSpeed && !_softTimeoutDone)
		{
			AudioListener.volume = 0f;
			AudioListener.pause = true;
		}
	}

	public void OnDestroy()
	{
		Instance = null;
	}
}
