using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LOMAssistant;

/// <summary>
/// 排查用日志：启动环境、Steam 初始化/重启、场景切换、游戏退出、游戏报错。
/// 玩家反馈「装了没用」时，看这些就能知道是哪个进程、Mod 有没有在真正运行的那个进程里加载。
/// </summary>
public static class Diagnostics
{
	private static readonly DateTime StartTime = DateTime.Now;
	private static int _errorCount;
	private const int MaxLoggedErrors = 50;

	private static void Both(string message)
	{
		LOMAssistantPlugin.LogInfo(message);
		FileLogger.Log(message);
	}

	private static void Safe(string label, Func<string> value)
	{
		string text;
		try
		{
			text = value();
		}
		catch (Exception e)
		{
			text = "（读取失败：" + e.GetType().Name + "）";
		}
		Both("[诊断] " + label + "：" + text);
	}

	/// <summary>插件 Awake 时调用，记录这一次启动的环境。</summary>
	public static void LogStartup(PluginInfo info)
	{
		Process process = Process.GetCurrentProcess();
		Safe("Mod 版本", () => Assembly.GetExecutingAssembly().GetName().Version.ToString());
		Safe("BepInEx 版本", () => typeof(Paths).Assembly.GetName().Version.ToString());
		Safe("游戏版本", () => Application.version);
		Safe("Unity 版本", () => Application.unityVersion);
		Safe("进程 ID", () => process.Id.ToString());
		Safe("进程启动时间", () => process.StartTime.ToString("yyyy-MM-dd HH:mm:ss"));
		Safe("64 位进程", () => Environment.Is64BitProcess ? "是" : "否");
		Safe("系统", () => Environment.OSVersion + "，" + (Environment.Is64BitOperatingSystem ? "64 位" : "32 位"));
		Safe("游戏程序", () => process.MainModule.FileName);
		Safe("工作目录", () => Environment.CurrentDirectory);
		Safe("命令行", () => Environment.CommandLine);
		Safe("由 Steam 启动", () =>
		{
			string appId = Environment.GetEnvironmentVariable("SteamAppId") ?? Environment.GetEnvironmentVariable("SteamGameId");
			return string.IsNullOrEmpty(appId) ? "否（没有 SteamAppId 环境变量，可能是直接双击 exe）" : "是（SteamAppId=" + appId + "）";
		});
		Safe("Steam 客户端在运行", () => Process.GetProcessesByName("steam").Length > 0 ? "是" : "否");
		Safe("Doorstop 环境变量", () =>
		{
			var names = new System.Collections.Generic.List<string>();
			foreach (System.Collections.DictionaryEntry entry in Environment.GetEnvironmentVariables())
			{
				if (entry.Key is string name && name.StartsWith("DOORSTOP_", StringComparison.OrdinalIgnoreCase))
				{
					names.Add(name);
				}
			}
			names.Sort();
			return names.Count > 0 ? string.Join("，", names.ToArray()) : "无";
		});
		Safe("游戏目录", () => Paths.GameRootPath);
		Safe("winhttp.dll", () => DescribeFile(Path.Combine(Paths.GameRootPath, "winhttp.dll")));
		Safe("doorstop_config.ini", () => DescribeFile(Path.Combine(Paths.GameRootPath, "doorstop_config.ini")));
		Safe("Mod 文件", () => DescribeFile(info.Location));
		Safe("配置文件", () => DescribeFile(Path.Combine(Paths.ConfigPath, "LOMAssistant.cfg")));
		Safe("plugins 里的 DLL", () =>
		{
			string[] dlls = Directory.GetFiles(Paths.PluginPath, "*.dll", SearchOption.AllDirectories)
				.Select(p => p.Substring(Paths.PluginPath.Length).TrimStart('\\', '/'))
				.ToArray();
			return dlls.Length + " 个：" + string.Join("，", dlls);
		});

		SceneManager.sceneLoaded += (scene, mode) => Both($"[诊断] 进入场景：{scene.name}（启动后 {Uptime()}）");
		Application.quitting += () => Both($"[诊断] 游戏退出（本次运行 {Uptime()}，期间游戏报错 {_errorCount} 条）");
		Application.logMessageReceived += OnUnityLog;
	}

	/// <summary>补丁全部打完后调用。</summary>
	public static void LogPatchSummary(Harmony harmony)
	{
		Safe("已打补丁的游戏方法", () => harmony.GetPatchedMethods().Count().ToString() + " 个");
	}

	/// <summary>记录游戏的 Steam 初始化结果，包括「闪退一下重进」的 Steam 重启。</summary>
	public static void PatchSteam(Harmony harmony)
	{
		try
		{
			try
			{
				Assembly.Load("com.rlabrecque.steamworks.net");
			}
			catch
			{
			}
			Type steamApi = AccessTools.TypeByName("Steamworks.SteamAPI");
			MethodInfo restart = steamApi != null ? AccessTools.Method(steamApi, "RestartAppIfNecessary") : null;
			MethodInfo init = steamApi != null ? AccessTools.Method(steamApi, "Init") : null;
			if (restart != null)
			{
				harmony.Patch(restart, prefix: new HarmonyMethod(typeof(Diagnostics), nameof(RestartPrefix)), postfix: new HarmonyMethod(typeof(Diagnostics), nameof(RestartPostfix)));
			}
			if (init != null)
			{
				harmony.Patch(init, postfix: new HarmonyMethod(typeof(Diagnostics), nameof(InitPostfix)));
			}
			if (restart == null || init == null)
			{
				FileLogger.LogWarning("[诊断] 没找到 Steam 接口，无法记录 Steam 初始化结果。");
			}
		}
		catch (Exception e)
		{
			FileLogger.LogWarning("[诊断] 挂接 Steam 接口失败：" + e.Message);
		}
	}

	// Doorstop 在游戏进程里设置 DOORSTOP_* 环境变量，并用 DOORSTOP_INITIALIZED 防止重复加载。
	// Steam 没在运行时，RestartAppIfNecessary 会由游戏进程拉起 Steam，Steam 继承这些变量，
	// 再由它启动的游戏就会被 Doorstop 当成「已经加载过」而跳过，Mod 不生效，而且这个 Steam 不退就一直如此。
	// 所以在交给 Steam 之前把这些变量清掉。此时 BepInEx 早已启动完毕，不再需要它们。
	private static void RestartPrefix()
	{
		try
		{
			var cleared = new System.Collections.Generic.List<string>();
			foreach (System.Collections.DictionaryEntry entry in Environment.GetEnvironmentVariables())
			{
				string name = entry.Key as string;
				if (name != null && name.StartsWith("DOORSTOP_", StringComparison.OrdinalIgnoreCase))
				{
					Environment.SetEnvironmentVariable(name, null);
					cleared.Add(name);
				}
			}
			FileLogger.Log("[诊断] 交给 Steam 检查前清除 Doorstop 环境变量：" + (cleared.Count > 0 ? string.Join("，", cleared.ToArray()) : "无"));
		}
		catch (Exception e)
		{
			FileLogger.LogWarning("[诊断] 清除 Doorstop 环境变量失败：" + e.Message);
		}
	}

	private static void RestartPostfix(bool __result)
	{
		Both(__result
			? "[诊断] Steam 要求重启游戏：这个进程马上会退出，由 Steam 重新启动游戏（玩家看到的「闪退一下重进」）。请看下一个日志文件。"
			: "[诊断] Steam 检查通过，不需要重启。");
	}

	private static void InitPostfix(bool __result)
	{
		Both(__result
			? "[诊断] Steam 初始化成功。"
			: "[诊断] Steam 初始化失败（Steam 没在运行、没登录或连不上）。");
	}

	private static void OnUnityLog(string condition, string stackTrace, LogType type)
	{
		if (type != LogType.Exception && type != LogType.Error)
		{
			return;
		}
		_errorCount++;
		if (_errorCount > MaxLoggedErrors)
		{
			return;
		}
		string first = (stackTrace ?? "").Split('\n').FirstOrDefault(l => l.Trim().Length > 0) ?? "";
		FileLogger.LogWarning($"[游戏报错 {_errorCount}] {condition} {first.Trim()}");
		if (_errorCount == MaxLoggedErrors)
		{
			FileLogger.LogWarning("[诊断] 游戏报错太多，后面的不再记录。");
		}
	}

	private static string DescribeFile(string path)
	{
		if (!File.Exists(path))
		{
			return "不存在（" + path + "）";
		}
		FileInfo f = new FileInfo(path);
		return $"存在，{f.Length} 字节，修改于 {f.LastWriteTime:yyyy-MM-dd HH:mm}";
	}

	private static string Uptime()
	{
		TimeSpan t = DateTime.Now - StartTime;
		return t.TotalMinutes >= 1 ? $"{(int)t.TotalMinutes} 分 {t.Seconds} 秒" : $"{t.TotalSeconds:0.0} 秒";
	}
}
