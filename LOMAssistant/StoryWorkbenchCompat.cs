using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace LOMAssistant;

/// <summary>
/// 和「剧情随行」（MortalStoryWorkbench，local.mortal.storyworkbench）一起装时的兼容处理。
/// 两边重叠的功能用本 Mod 的：第一次检测到它时，把它的 100 个手动存档、直接选择骰子、选项属性增减、
/// 悬停地点提示这四项关掉，并把它的 F6 / F8 / F9 快捷键改成 Ctrl+F6 / Ctrl+F8 / Ctrl+F9（免得决斗里按 F8 被它直接判赢）。
/// 改的是它自己的配置，只改一次：之后玩家在它的面板里再打开，不会被改回去。快捷键只改还是原默认值的。
/// </summary>
public static class StoryWorkbenchCompat
{
	public const string Id = "local.mortal.storyworkbench";

	/// <summary>它装着没有（冲突检测据此不把它报成冲突）。</summary>
	public static bool Present { get; private set; }

	private static readonly (string Section, string Key, string Name)[] Features =
	{
		("存档", "启用100个手动存档", "100 个手动存档"),
		("剧情", "直接选择骰子选项", "直接选择骰子"),
		("剧情", "显示选项属性增减", "选项属性增减"),
		("剧情", "悬停地点显示事件提示", "悬停地点提示"),
	};

	private static readonly (string Key, KeyCode Main)[] Keys =
	{
		("主面板", KeyCode.F6),
		("战斗胜利", KeyCode.F8),
		("显示隐藏Mod界面", KeyCode.F9),
	};

	private static string Mark => Path.Combine(Path.GetDirectoryName(typeof(StoryWorkbenchCompat).Assembly.Location) ?? ".", "StoryReview", "compat-storyworkbench");

	public static void Apply()
	{
		try
		{
			ConfigFile config = FindConfig();
			if (config == null)
			{
				return;
			}
			Present = true;
			if (File.Exists(Mark))
			{
				FileLogger.Log("[兼容] 检测到剧情随行，之前已经调整过它的设置，这次不再改动");
				return;
			}
			var changed = new List<string>();
			foreach (var (section, key, name) in Features)
			{
				if (config.TryGetEntry(section, key, out ConfigEntry<bool> entry) && entry.Value)
				{
					entry.Value = false;
					changed.Add("关掉" + name);
				}
			}
			foreach (var (key, main) in Keys)
			{
				var definition = new ConfigDefinition("按键", key);
				if (!config.ContainsKey(definition))
				{
					continue;
				}
				ConfigEntryBase entry = config[definition];
				object value = entry.BoxedValue;
				if (value == null || !Equals(Traverse.Create(value).Property("MainKey").GetValue(), main)
					|| (Traverse.Create(value).Property("Modifiers").GetValue() as IEnumerable)?.Cast<object>().Any() == true)
				{
					continue; // 玩家改过的键不动
				}
				entry.BoxedValue = Activator.CreateInstance(value.GetType(), main, new[] { KeyCode.LeftControl });
				changed.Add(key + " 改成 Ctrl+" + main);
			}
			config.Save();
			Directory.CreateDirectory(Path.GetDirectoryName(Mark));
			File.WriteAllText(Mark, "");
			FileLogger.Log("[兼容] 检测到剧情随行，已调整它的设置：" + (changed.Count > 0 ? string.Join("，", changed.ToArray()) : "无需调整"));
		}
		catch (Exception ex)
		{
			FileLogger.LogWarning("[兼容] 调整剧情随行的设置失败: " + ex.Message);
		}
	}

	/// <summary>它的配置；没装它返回 null。</summary>
	private static ConfigFile FindConfig()
	{
		Type chainloader = AccessTools.TypeByName("BepInEx.Unity.Mono.Bootstrap.UnityChainloader");
		object loader = chainloader != null ? AccessTools.Property(chainloader, "Instance")?.GetValue(null, null) : null;
		if (loader == null || !(AccessTools.Property(loader.GetType(), "Plugins")?.GetValue(loader, null) is IDictionary plugins) || !plugins.Contains(Id))
		{
			return null;
		}
		object info = plugins[Id];
		object instance = AccessTools.Property(info.GetType(), "Instance")?.GetValue(info, null);
		return instance != null ? AccessTools.Property(instance.GetType(), "Config")?.GetValue(instance, null) as ConfigFile : null;
	}
}
