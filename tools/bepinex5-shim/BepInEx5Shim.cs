// BepInEx 5 插件兼容层：编译成名叫 BepInEx 的小程序集，嵌进 LOMAssistant.dll。
// 本 Mod 跑在 BepInEx 6 上，BepInEx 5 的插件（如立绘替换 Mod「邪神认知污染ON」）找不到它要的 BepInEx.dll，加载不了。
// 这里把它常用的类型转到 BepInEx 6 的同名类型上，再补一个 BepInEx 5 样子的 BaseUnityPlugin，由本 Mod 负责把插件挂起来。
// 用法见 build.sh；不参与 Mod 本体编译。
using System;
using System.IO;
using System.Runtime.CompilerServices;
using BepInEx.Configuration;
using BepInEx.Logging;
using UnityEngine;

[assembly: TypeForwardedTo(typeof(BepInEx.BepInPlugin))]
[assembly: TypeForwardedTo(typeof(BepInEx.BepInDependency))]
[assembly: TypeForwardedTo(typeof(BepInEx.BepInProcess))]
[assembly: TypeForwardedTo(typeof(BepInEx.BepInIncompatibility))]
[assembly: TypeForwardedTo(typeof(BepInEx.Paths))]
[assembly: TypeForwardedTo(typeof(ConfigFile))]
[assembly: TypeForwardedTo(typeof(ConfigEntry<>))]
[assembly: TypeForwardedTo(typeof(ConfigEntryBase))]
[assembly: TypeForwardedTo(typeof(ConfigDefinition))]
[assembly: TypeForwardedTo(typeof(ConfigDescription))]
[assembly: TypeForwardedTo(typeof(AcceptableValueBase))]
[assembly: TypeForwardedTo(typeof(AcceptableValueRange<>))]
[assembly: TypeForwardedTo(typeof(AcceptableValueList<>))]
[assembly: TypeForwardedTo(typeof(ManualLogSource))]
[assembly: TypeForwardedTo(typeof(BepInEx.Logging.Logger))]
[assembly: TypeForwardedTo(typeof(LogLevel))]
[assembly: TypeForwardedTo(typeof(ILogSource))]

namespace BepInEx
{
	/// <summary>BepInEx 5 的插件基类：挂上去时按插件信息建好日志和配置文件（BepInEx\config\GUID.cfg）。</summary>
	public abstract class BaseUnityPlugin : MonoBehaviour
	{
		public ManualLogSource Logger { get; }

		public ConfigFile Config { get; }

		protected BaseUnityPlugin()
		{
			var meta = (BepInPlugin)Attribute.GetCustomAttribute(GetType(), typeof(BepInPlugin));
			string guid = meta?.GUID ?? GetType().FullName;
			Logger = BepInEx.Logging.Logger.CreateLogSource(meta?.Name ?? GetType().Name);
			Config = new ConfigFile(Path.Combine(Paths.ConfigPath, guid + ".cfg"), false, meta);
		}
	}
}
