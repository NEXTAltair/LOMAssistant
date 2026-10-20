using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace LOMAssistant;

/// <summary>
/// 检测和其他 Mod 的冲突：等所有 Mod 都加载完，看本 Mod 改过的游戏方法有没有被别的 Mod 也改了
/// （例如旧版骰子大师、多功能 Mod 会让选项提示失效）。不靠文件名猜，只报真正撞到的。
/// 结果写进日志，并显示在 F8 面板顶部。
/// </summary>
public static class ConflictDetector
{
	/// <summary>给 F8 面板显示的提示，一条一个冲突的 Mod。空表示没有冲突。</summary>
	public static readonly List<string> Warnings = new List<string>();

	public static bool Checked { get; private set; }

	public static IEnumerator CheckLater(Harmony ours)
	{
		// 其他 Mod 可能在 Awake 之后才打补丁，等几秒再查
		yield return new UnityEngine.WaitForSeconds(5f);
		StoryWorkbenchCompat.Apply();
		Check(ours);
	}

	public static void Check(Harmony ours)
	{
		Warnings.Clear();
		Warnings.AddRange(BepInEx5Plugins.Notices);
		try
		{
			Dictionary<string, string> pluginNames = LoadedPluginNames();
			string others = string.Join("，", pluginNames.Where(p => p.Key != ours.Id).Select(p => $"{p.Value}（{p.Key}）").ToArray());
			FileLogger.Log("[冲突检测] 同时加载的其他 BepInEx 插件：" + (others.Length > 0 ? others : "无"));

			// owner -> 和本 Mod 撞到的方法
			var clashes = new Dictionary<string, List<string>>();
			foreach (MethodBase method in ours.GetPatchedMethods())
			{
				Patches info = Harmony.GetPatchInfo(method);
				if (info == null)
				{
					continue;
				}
				foreach (string owner in info.Owners)
				{
					// 剧情随行：已经做了兼容处理，不算冲突
					if (owner == ours.Id || owner == StoryWorkbenchCompat.Id)
					{
						continue;
					}
					if (!clashes.TryGetValue(owner, out List<string> list))
					{
						clashes[owner] = list = new List<string>();
					}
					list.Add(method.DeclaringType?.Name + "." + method.Name);
				}
			}

			foreach (KeyValuePair<string, List<string>> clash in clashes)
			{
				string name = pluginNames.TryGetValue(clash.Key, out string n) ? n : clash.Key;
				string methods = string.Join("、", clash.Value.Distinct().ToArray());
				string message = $"检测到冲突的 Mod：{name}（{clash.Key}），和本 Mod 同时修改了 {clash.Value.Distinct().Count()} 处游戏代码：{methods}。可能导致骰子或选项显示异常。请删除旧版骰子大师、多功能 Mod、活侠传助手等同类 Mod 后重新覆盖安装。";
				LOMAssistantPlugin.LogWarning(message);
				FileLogger.LogWarning("[冲突检测] " + message);
				Warnings.Add($"{name} 与本 Mod 冲突（{clash.Value.Distinct().Count()} 处），请删除后重新覆盖安装");
			}
			if (clashes.Count == 0)
			{
				FileLogger.Log("[冲突检测] 没有发现和本 Mod 冲突的 Mod。");
			}
		}
		catch (Exception e)
		{
			FileLogger.LogWarning("[冲突检测] 检测失败：" + e.Message);
		}
		Checked = true;
	}

	// GUID -> 插件名。用反射读 BepInEx 的插件列表，读不到就返回空。
	private static Dictionary<string, string> LoadedPluginNames()
	{
		var result = new Dictionary<string, string>();
		try
		{
			Type chainloader = AccessTools.TypeByName("BepInEx.Unity.Mono.Bootstrap.UnityChainloader");
			object instance = chainloader != null ? AccessTools.Property(chainloader, "Instance")?.GetValue(null, null) : null;
			if (instance == null)
			{
				return result;
			}
			if (AccessTools.Property(instance.GetType(), "Plugins")?.GetValue(instance, null) is IDictionary plugins)
			{
				foreach (DictionaryEntry entry in plugins)
				{
					object metadata = AccessTools.Property(entry.Value.GetType(), "Metadata")?.GetValue(entry.Value, null);
					string name = metadata != null ? AccessTools.Property(metadata.GetType(), "Name")?.GetValue(metadata, null) as string : null;
					result[(string)entry.Key] = name ?? (string)entry.Key;
				}
			}
		}
		catch
		{
		}
		return result;
	}
}
