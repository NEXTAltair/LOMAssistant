using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Mortal.Core;

namespace LOMAssistant.Features.Extras;

/// <summary>
/// 存档栏位显示用的读档缓存。
/// 游戏每次切场景（每结束一个剧情节点）都会重建存档、读档面板，每个栏位都把存档文件完整读一遍、
/// 反序列化一遍（栏位 Awake 和 Setup 各一次）。栏位从 20 扩到 100 后，存档多的玩家每次切场景都会卡好几秒。
/// 这里只在栏位显示（Setup）期间缓存：文件的修改时间和大小没变，就直接用上次读到的结果。
/// 真正读档走 SaveSystem.LoadGameData，不经过栏位 Setup，仍然每次从文件读。
/// </summary>
public static class SaveDataCache
{
	private struct Entry
	{
		public DateTime WriteTime;
		public long Length;
		public object Data;
	}

	private static readonly Dictionary<string, Entry> Cache = new Dictionary<string, Entry>();
	private static int _panelDepth;
	private static int _hits;
	private static int _misses;

	// ---- 只在栏位 Setup 期间启用 ----

	[HarmonyPrefix]
	[HarmonyPatch(typeof(SaveSlotPanel), "Setup")]
	public static void SaveSlotPanel_Setup_Prefix() => EnterPanel();

	[HarmonyFinalizer]
	[HarmonyPatch(typeof(SaveSlotPanel), "Setup")]
	public static void SaveSlotPanel_Setup_Finalizer() => LeavePanel();

	[HarmonyPrefix]
	[HarmonyPatch(typeof(LoadSlotPanel), "Setup")]
	public static void LoadSlotPanel_Setup_Prefix() => EnterPanel();

	[HarmonyFinalizer]
	[HarmonyPatch(typeof(LoadSlotPanel), "Setup")]
	public static void LoadSlotPanel_Setup_Finalizer() => LeavePanel();

	[HarmonyPrefix]
	[HarmonyPatch(typeof(RecentSaveSlotPanel), "Setup")]
	public static void RecentSaveSlotPanel_Setup_Prefix() => EnterPanel();

	[HarmonyFinalizer]
	[HarmonyPatch(typeof(RecentSaveSlotPanel), "Setup")]
	public static void RecentSaveSlotPanel_Setup_Finalizer() => LeavePanel();

	[HarmonyPrefix]
	[HarmonyPatch(typeof(AutoSaveSlotPanel), "Setup")]
	public static void AutoSaveSlotPanel_Setup_Prefix() => EnterPanel();

	[HarmonyFinalizer]
	[HarmonyPatch(typeof(AutoSaveSlotPanel), "Setup")]
	public static void AutoSaveSlotPanel_Setup_Finalizer() => LeavePanel();

	private static void EnterPanel()
	{
		_panelDepth++;
	}

	/// <summary>在这个范围里读存档也走缓存（新版存档界面一次列出全部存档时用）。</summary>
	internal static IDisposable Scope()
	{
		EnterPanel();
		return new ScopeExit();
	}

	private sealed class ScopeExit : IDisposable
	{
		public void Dispose() => LeavePanel();
	}

	private static void LeavePanel()
	{
		if (_panelDepth > 0)
		{
			_panelDepth--;
		}
	}

	// SaveSystem 里同时有 GetSaveData(string) 和泛型 GetSaveData<T>(string)，
	// 按名字加参数找方法在部分玩家的环境里会报「Ambiguous match」，所以这里明确挑非泛型的那个。
	[HarmonyPatch]
	public static class GetSaveDataPatch
	{
		public static MethodBase TargetMethod()
		{
			return AccessTools.GetDeclaredMethods(typeof(SaveSystem)).First(m =>
				m.Name == "GetSaveData" && !m.IsGenericMethodDefinition && m.ReturnType == typeof(GameSave)
				&& m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == typeof(string));
		}

		public static bool Prefix(SaveSystem __instance, string slot, ref GameSave __result, out string __state)
		{
			return TryCached(__instance, slot, ref __result, out __state);
		}

		public static void Postfix(GameSave __result, string __state)
		{
			Store(__state, __result);
		}
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(SaveSystem), "GetAutoSaveData")]
	public static bool GetAutoSaveData_Prefix(SaveSystem __instance, string slot, ref AutoGameSave __result, out string __state)
	{
		return TryCached(__instance, slot, ref __result, out __state);
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(SaveSystem), "GetAutoSaveData")]
	public static void GetAutoSaveData_Postfix(AutoGameSave __result, string __state)
	{
		Store(__state, __result);
	}

	// 返回 false 表示用了缓存、跳过原方法。__state 记下要缓存的文件路径（null 表示不缓存）。
	private static bool TryCached<T>(SaveSystem system, string slot, ref T result, out string path) where T : class
	{
		path = null;
		if (_panelDepth <= 0)
		{
			return true;
		}
		try
		{
			string file = Traverse.Create(system).Method("GetSaveDataFile", new[] { typeof(string) }).GetValue<string>(slot);
			var info = new FileInfo(file);
			if (!info.Exists)
			{
				Cache.Remove(file);
				return true; // 原方法会直接返回 null，很快
			}
			if (Cache.TryGetValue(file, out Entry entry) && entry.WriteTime == info.LastWriteTimeUtc && entry.Length == info.Length && entry.Data is T cached)
			{
				result = cached;
				_hits++;
				return false;
			}
			path = file;
			_misses++;
		}
		catch
		{
			path = null;
		}
		return true;
	}

	private static void Store(string path, object data)
	{
		if (path == null || data == null)
		{
			return;
		}
		try
		{
			var info = new FileInfo(path);
			Cache[path] = new Entry { WriteTime = info.LastWriteTimeUtc, Length = info.Length, Data = data };
		}
		catch
		{
		}
	}

	// ---- 计时：切场景时重建存档、读档面板花了多久（超过 0.1 秒才记） ----

	[HarmonyPrefix]
	[HarmonyPatch(typeof(SaveGamePanel), "Awake")]
	public static void SaveGamePanel_Awake_Prefix(out Stopwatch __state) => __state = StartTiming();

	[HarmonyPostfix]
	[HarmonyPatch(typeof(SaveGamePanel), "Awake")]
	public static void SaveGamePanel_Awake_Postfix(Stopwatch __state) => StopTiming("SaveGamePanel", __state);

	[HarmonyPrefix]
	[HarmonyPatch(typeof(LoadGamePanel), "Awake")]
	public static void LoadGamePanel_Awake_Prefix(out Stopwatch __state) => __state = StartTiming();

	[HarmonyPostfix]
	[HarmonyPatch(typeof(LoadGamePanel), "Awake")]
	public static void LoadGamePanel_Awake_Postfix(Stopwatch __state) => StopTiming("LoadGamePanel", __state);

	private static Stopwatch StartTiming()
	{
		_hits = 0;
		_misses = 0;
		return Stopwatch.StartNew();
	}

	private static void StopTiming(string panel, Stopwatch watch)
	{
		if (watch == null)
		{
			return;
		}
		watch.Stop();
		if (watch.ElapsedMilliseconds >= 100)
		{
			FileLogger.Log($"[性能] {panel} 建立栏位用时 {watch.ElapsedMilliseconds} 毫秒（读存档 {_misses} 次，缓存命中 {_hits} 次）");
		}
	}
}
