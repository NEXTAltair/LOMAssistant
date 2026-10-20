using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using Mono.Cecil;
using UnityEngine;
using LOMAssistant.Config;

namespace LOMAssistant;

/// <summary>
/// 让 BepInEx 5 的插件也能用：本 Mod 跑在 BepInEx 6 上，BepInEx 5 的插件（比如立绘替换 Mod「邪神认知污染ON」）
/// 要的 BepInEx.dll 不存在，BepInEx 6 也不认它的插件类，装了等于没装。
/// 这里在 BepInEx\plugins 里找出 BepInEx 5 的插件，提供一个兼容层（tools\bepinex5-shim，嵌在本 DLL 里），由本 Mod 把它们挂起来。
/// 它们的配置照旧放在 BepInEx\config\GUID.cfg，日志进 BepInEx 的日志。
/// </summary>
public static class BepInEx5Plugins
{
	private const string ShimName = "BepInEx";

	private static Assembly _shim;

	/// <summary>挂上的 BepInEx 5 插件（名字），诊断里用。</summary>
	public static readonly List<string> Loaded = new List<string>();

	/// <summary>要给玩家看的提示（显示在设置页最上面）。</summary>
	public static readonly List<string> Notices = new List<string>();

	public static void Load()
	{
		try
		{
			string ours = Path.GetDirectoryName(typeof(BepInEx5Plugins).Assembly.Location) ?? "";
			foreach (string dll in Directory.GetFiles(Paths.PluginPath, "*.dll", SearchOption.AllDirectories))
			{
				if (dll.StartsWith(ours, StringComparison.OrdinalIgnoreCase) || !IsBepInEx5Plugin(dll))
				{
					continue;
				}
				LoadPlugin(dll);
			}
		}
		catch (Exception ex)
		{
			FileLogger.LogError("[BepInEx 5 兼容] 查找插件失败: " + ex.Message);
		}
	}

	// 引用了名叫 BepInEx 的程序集（BepInEx 6 里没有这个名字），并且有继承 BepInEx.BaseUnityPlugin 的类
	private static bool IsBepInEx5Plugin(string dll)
	{
		try
		{
			using (var assembly = AssemblyDefinition.ReadAssembly(dll))
			{
				ModuleDefinition module = assembly.MainModule;
				return module.AssemblyReferences.Any(r => r.Name == ShimName)
					&& module.Types.Any(t => t.BaseType != null && t.BaseType.FullName == "BepInEx.BaseUnityPlugin");
			}
		}
		catch
		{
			return false;
		}
	}

	private static void LoadPlugin(string dll)
	{
		try
		{
			EnsureShim();
			Assembly assembly = Assembly.LoadFile(dll);
			Type baseType = _shim.GetType("BepInEx.BaseUnityPlugin");
			foreach (Type type in assembly.GetTypes().Where(t => !t.IsAbstract && baseType.IsAssignableFrom(t)))
			{
				var meta = (BepInPlugin)Attribute.GetCustomAttribute(type, typeof(BepInPlugin));
				string name = meta?.Name ?? type.Name;
				var host = new GameObject("BepInEx5_" + (meta?.GUID ?? type.FullName));
				UnityEngine.Object.DontDestroyOnLoad(host);
				host.AddComponent(type);
				Loaded.Add(name);
				FileLogger.Log("[BepInEx 5 兼容] 已加载 " + name + "（" + Path.GetFileName(dll) + "）");
			}
			DaguiCompat.Apply(assembly);
		}
		catch (Exception ex)
		{
			FileLogger.LogError("[BepInEx 5 兼容] 加载 " + Path.GetFileName(dll) + " 失败: " + ex);
		}
	}

	// 兼容层写到 BepInEx\cache 下再加载（Unity 挂组件要能找到程序集文件），找 BepInEx 程序集时交给它
	private static void EnsureShim()
	{
		if (_shim != null)
		{
			return;
		}
		byte[] bytes;
		using (Stream stream = typeof(BepInEx5Plugins).Assembly.GetManifestResourceStream("LOMAssistant.BepInEx5Shim.dll"))
		using (var memory = new MemoryStream())
		{
			stream.CopyTo(memory);
			bytes = memory.ToArray();
		}
		string path = Path.Combine(Paths.CachePath, "LOMAssistant", "BepInEx.dll");
		Directory.CreateDirectory(Path.GetDirectoryName(path));
		if (!File.Exists(path) || !File.ReadAllBytes(path).SequenceEqual(bytes))
		{
			File.WriteAllBytes(path, bytes);
		}
		_shim = Assembly.LoadFile(path);
		AppDomain.CurrentDomain.AssemblyResolve += (sender, args) => new AssemblyName(args.Name).Name == ShimName ? _shim : null;
	}

	/// <summary>
	/// 「邪神认知污染ON」（立绘替换）的 F8（安全模式）、F9（重新载入立绘）写死不能改，和本 Mod 的设置页、存档备注、数值页撞了。
	/// 第一次检测到它时，本 Mod 还是默认 F8 / F9 的这几个热键改成不绑定，键让给它；玩家可以在设置页另绑别的键。只改一次。
	/// </summary>
	private static class DaguiCompat
	{
		public static void Apply(Assembly assembly)
		{
			if (assembly.GetType("Dagui.DaguiPlugin") == null)
			{
				return;
			}
			string mark = Path.Combine(Path.GetDirectoryName(typeof(DaguiCompat).Assembly.Location) ?? ".", "StoryReview", "compat-dagui");
			if (File.Exists(mark))
			{
				return;
			}
			var yielded = new List<string>();
			Yield(ConfigManager.PanelKey, KeyCode.F8, "设置页", yielded);
			Yield(ConfigManager.SaveNoteKey, KeyCode.F8, "存档备注", yielded);
			Yield(ConfigManager.DebugPanelKey, KeyCode.F9, "数值页", yielded);
			if (yielded.Count > 0)
			{
				string text = "检测到立绘替换 Mod「邪神认知污染ON」，它的 F8、F9 改不了，本 Mod 的" + string.Join("、", yielded.ToArray()) + "热键已让给它（改成不绑定）。可以按 Tab 打开窗口再点对应的页，或者在设置页「热键」里另绑别的键。";
				Notices.Add(text);
				FileLogger.Log("[BepInEx 5 兼容] " + text);
			}
			Directory.CreateDirectory(Path.GetDirectoryName(mark));
			File.WriteAllText(mark, "");
		}

		private static void Yield(ConfigElement<KeyCode> key, KeyCode taken, string name, List<string> yielded)
		{
			if (key.Value == taken)
			{
				key.Value = KeyCode.None;
				yielded.Add(name);
			}
		}
	}
}
