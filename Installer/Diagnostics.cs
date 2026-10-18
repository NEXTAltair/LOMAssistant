using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;

namespace LOMAssistantInstaller;

/// <summary>
/// 安装器里的「导出诊断」：Mod 装了不生效时，游戏里的导出按钮用不了，就从这里把排查需要的东西打包到桌面。
/// 比游戏内的导出多查几样「Mod 为什么没加载」的关键项：winhttp.dll 是 32 位还是 64 位、加载器配置、BepInEx 是否完整、Steam 是否在运行。
/// 不包含存档。
/// </summary>
internal static class Diagnostics
{
	private static readonly string GameData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "Low", "Obb Studio", "Mortal");

	public static string Export(string game, string installerLog)
	{
		string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
		string zipPath = Path.Combine(desktop, "活侠传拓展诊断_安装器_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".zip");
		using (FileStream stream = File.Create(zipPath))
		using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, false, Encoding.UTF8))
		{
			AddText(zip, "说明.txt", Summary(game));
			AddText(zip, "安装器日志.txt", installerLog ?? "");
			if (game != null)
			{
				string logs = Path.Combine(game, @"BepInEx\plugins\LOMAssistant\logs");
				if (Directory.Exists(logs))
				{
					foreach (string log in Directory.GetFiles(logs, "*.log").OrderByDescending(File.GetLastWriteTimeUtc).Take(3))
					{
						AddFile(zip, log, "Mod日志/" + Path.GetFileName(log));
					}
				}
				AddFile(zip, Path.Combine(game, @"BepInEx\LogOutput.log"), "BepInEx/LogOutput.log");
				AddFile(zip, Path.Combine(game, @"BepInEx\config\LOMAssistant.cfg"), "BepInEx/LOMAssistant.cfg");
				AddFile(zip, Path.Combine(game, "doorstop_config.ini"), "游戏目录/doorstop_config.ini");
			}
			AddFile(zip, Path.Combine(GameData, "Player.log"), "游戏日志/Player.log");
			AddFile(zip, Path.Combine(GameData, "Player-prev.log"), "游戏日志/Player-prev.log");
		}
		return zipPath;
	}

	private static string Summary(string game)
	{
		var sb = new StringBuilder();
		sb.AppendLine("活侠传拓展 诊断信息（安装器导出）  " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
		sb.AppendLine("系统：" + Environment.OSVersion + (Environment.Is64BitOperatingSystem ? "，64 位" : "，32 位") + "，.NET " + Environment.Version);
		sb.AppendLine("Steam 在运行：" + (Process.GetProcessesByName("steam").Length > 0 ? "是" : "否"));
		sb.AppendLine("游戏在运行：" + (Process.GetProcessesByName("Mortal").Length > 0 ? "是" : "否"));
		sb.AppendLine("游戏目录：" + (game ?? "（没找到）"));
		if (game == null)
		{
			return sb.ToString();
		}
		sb.AppendLine();
		sb.AppendLine("加载器文件：");
		sb.AppendLine("  winhttp.dll：" + DescribeWinHttp(Path.Combine(game, "winhttp.dll")));
		sb.AppendLine("  doorstop_config.ini：" + Describe(Path.Combine(game, "doorstop_config.ini")));
		sb.AppendLine("  .doorstop_version：" + Describe(Path.Combine(game, ".doorstop_version")));
		foreach (string core in new[] { "BepInEx.Core.dll", "BepInEx.Unity.Mono.dll", "BepInEx.Preloader.Core.dll", "0Harmony.dll" })
		{
			sb.AppendLine("  BepInEx\\core\\" + core + "：" + Describe(Path.Combine(game, @"BepInEx\core", core)));
		}
		sb.AppendLine();
		sb.AppendLine("BepInEx\\plugins 里的插件：");
		string plugins = Path.Combine(game, @"BepInEx\plugins");
		if (Directory.Exists(plugins))
		{
			foreach (string dll in Directory.GetFiles(plugins, "*.dll", SearchOption.AllDirectories))
			{
				sb.AppendLine("  " + dll.Substring(plugins.Length + 1) + "  " + Version(dll));
			}
		}
		else
		{
			sb.AppendLine("  （没有 plugins 文件夹）");
		}
		string disabled = Path.Combine(game, @"BepInEx\已停用的Mod");
		if (Directory.Exists(disabled))
		{
			sb.AppendLine();
			sb.AppendLine("已停用的 Mod：");
			foreach (string dll in Directory.GetFiles(disabled, "*.dll", SearchOption.AllDirectories))
			{
				sb.AppendLine("  " + dll.Substring(disabled.Length + 1));
			}
		}
		return sb.ToString();
	}

	// 旧版 BepInEx 带的是 32 位 winhttp.dll，新版游戏是 64 位，用错了所有 Mod 都不会加载
	private static string DescribeWinHttp(string path)
	{
		string basic = Describe(path);
		if (!File.Exists(path))
		{
			return basic;
		}
		try
		{
			using (var reader = new BinaryReader(File.OpenRead(path)))
			{
				reader.BaseStream.Seek(0x3C, SeekOrigin.Begin);
				int pe = reader.ReadInt32();
				reader.BaseStream.Seek(pe + 4, SeekOrigin.Begin);
				ushort machine = reader.ReadUInt16();
				string bits = machine == 0x8664 ? "64 位（正确）" : machine == 0x14C ? "32 位（错误！新版游戏需要 64 位，请重新安装）" : "未知架构 0x" + machine.ToString("X");
				return basic + "，" + bits;
			}
		}
		catch (Exception e)
		{
			return basic + "，读取失败：" + e.Message;
		}
	}

	private static string Describe(string path)
	{
		if (!File.Exists(path))
		{
			return "不存在";
		}
		var info = new FileInfo(path);
		return "存在，" + info.Length + " 字节，修改于 " + info.LastWriteTime.ToString("yyyy-MM-dd HH:mm");
	}

	private static string Version(string dll)
	{
		try
		{
			return AssemblyName.GetAssemblyName(dll).Version.ToString();
		}
		catch
		{
			return "";
		}
	}

	private static void AddText(ZipArchive zip, string name, string text)
	{
		using (var writer = new StreamWriter(zip.CreateEntry(name).Open(), new UTF8Encoding(true)))
		{
			writer.Write(text);
		}
	}

	// 日志文件可能正被游戏写着，用共享读取的方式复制
	private static void AddFile(ZipArchive zip, string path, string name)
	{
		try
		{
			if (!File.Exists(path))
			{
				return;
			}
			using (var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
			using (Stream output = zip.CreateEntry(name).Open())
			{
				input.CopyTo(output);
			}
		}
		catch (Exception e)
		{
			AddText(zip, name + ".读取失败.txt", e.Message);
		}
	}
}
