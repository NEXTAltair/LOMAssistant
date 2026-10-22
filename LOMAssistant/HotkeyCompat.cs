using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx.Configuration;
using BepInEx.Unity.Mono.Configuration;
using HarmonyLib;
using UnityEngine;

namespace LOMAssistant;

/// <summary>
/// 别的 Mod 在自己配置文件里设的快捷键（KeyCode 或不带 Ctrl 等修饰键的 KeyboardShortcut）和本 Mod 的热键撞了：
/// 本 Mod 的那几个热键改成不绑定，键让给它，设置页最上面提示一次。
/// 每个 Mod 的每个快捷键只处理一次（记在 StoryReview/compat-keys），之后玩家在本 Mod 里再绑回这个键就不管了；
/// 那个 Mod 换了新的键，会再按新键处理一次。键写死在代码里的（日语补丁、邪神认知污染ON）读不到，另有专门的处理。
/// </summary>
public static class HotkeyCompat
{
	/// <summary>要给玩家看的提示（显示在设置页最上面）。</summary>
	public static readonly List<string> Notices = new List<string>();

	private static string Mark => Path.Combine(Path.GetDirectoryName(typeof(HotkeyCompat).Assembly.Location) ?? ".", "StoryReview", "compat-keys");

	public static void Apply(string ourId)
	{
		try
		{
			var handled = new HashSet<string>(File.Exists(Mark) ? File.ReadAllLines(Mark) : new string[0]);
			var added = new List<string>();
			foreach (var (id, name, config) in OtherConfigs(ourId))
			{
				var keys = new List<KeyCode>();
				foreach (ConfigEntryBase entry in config.Values)
				{
					KeyCode key = PlainKey(entry.BoxedValue);
					string record = id + "|" + entry.Definition.Section + "|" + entry.Definition.Key + "|" + key;
					if (key == KeyCode.None || handled.Contains(record))
					{
						continue;
					}
					handled.Add(record);
					added.Add(record);
					keys.Add(key);
				}
				List<string> yielded = keys.Count > 0 ? ConflictDetector.YieldHotkeys(keys.ToArray()) : new List<string>();
				if (yielded.Count > 0)
				{
					string text = $"检测到「{name}」的快捷键和本 Mod 撞了，本 Mod 的{string.Join("、", yielded.ToArray())}热键已让给它（改成不绑定）。可以在设置页「热键」里另绑别的键。";
					Notices.Add(text);
					FileLogger.Log("[兼容] " + text);
				}
			}
			if (added.Count > 0)
			{
				Directory.CreateDirectory(Path.GetDirectoryName(Mark));
				File.AppendAllLines(Mark, added);
			}
		}
		catch (Exception e)
		{
			FileLogger.LogWarning("[兼容] 检查别的 Mod 的快捷键失败: " + e.Message);
		}
	}

	// 单个键：KeyCode，或者不带修饰键的 KeyboardShortcut（带 Ctrl 等的和本 Mod 的单键不算撞）
	private static KeyCode PlainKey(object value)
	{
		if (value is KeyCode key)
		{
			return key;
		}
		if (value is KeyboardShortcut shortcut && !shortcut.Modifiers.Any())
		{
			return shortcut.MainKey;
		}
		return KeyCode.None;
	}

	// 别的 BepInEx 6 插件的 GUID、名字、配置
	private static IEnumerable<(string, string, ConfigFile)> OtherConfigs(string ourId)
	{
		Type chainloader = AccessTools.TypeByName("BepInEx.Unity.Mono.Bootstrap.UnityChainloader");
		object loader = chainloader != null ? AccessTools.Property(chainloader, "Instance")?.GetValue(null, null) : null;
		if (loader == null || !(AccessTools.Property(loader.GetType(), "Plugins")?.GetValue(loader, null) is IDictionary plugins))
		{
			yield break;
		}
		foreach (DictionaryEntry pair in plugins)
		{
			var id = (string)pair.Key;
			object instance = AccessTools.Property(pair.Value.GetType(), "Instance")?.GetValue(pair.Value, null);
			if (id == ourId || instance == null || !(AccessTools.Property(instance.GetType(), "Config")?.GetValue(instance, null) is ConfigFile config))
			{
				continue;
			}
			object metadata = AccessTools.Property(pair.Value.GetType(), "Metadata")?.GetValue(pair.Value, null);
			string name = metadata != null ? AccessTools.Property(metadata.GetType(), "Name")?.GetValue(metadata, null) as string : null;
			yield return (id, name ?? id, config);
		}
	}
}
