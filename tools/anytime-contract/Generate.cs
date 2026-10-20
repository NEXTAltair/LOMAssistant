// 随时存档兼容契约生成器：游戏更新后，确认改动不影响存读档，再用它重新生成契约。
// 用法见 generate.sh。不参与 Mod 本体编译。
using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using LOMAssistant.Features.AnytimeSave;

internal static class Generate
{
	private static int Main(string[] args)
	{
		Console.OutputEncoding = new UTF8Encoding(false);
		string managed = args[0], output = args[1];
		if (args.Length > 2)
		{
			// 只对比：列出旧契约里和当前游戏不一致的项目
			var old = CompatibilityContract.Parse(Read(args[2]));
			var current = CompatibilityContract.Snapshot(managed);
			var diffs = CompatibilityContract.Differences(old, current);
			Console.WriteLine("契约共 " + old.Count + " 项，和当前游戏不一致的 " + diffs.Count + " 项" + (diffs.Count == 0 ? "，随时存档可以正常启用。" : "："));
			foreach (string d in diffs)
			{
				string kind = d.Substring(d.IndexOf('|') + 1, 2);
				string what = !current.ContainsKey(d) ? "找不到了" : kind == "T:" ? "类型结构变了" : kind == "S:" ? "签名变了" : "方法内容变了";
				Console.WriteLine("  [" + what + "] " + d);
			}
			if (diffs.Count != 0)
				Console.WriteLine("逐项确认这些改动不影响存读档、回放（对照 AnytimeSave.cs 里用到它们的地方），再不带参数运行 generate.sh 重新生成契约。");
			if (output == "-") return diffs.Count == 0 ? 0 : 1;
		}
		string text = CompatibilityContract.Serialize(CompatibilityContract.Snapshot(managed));
		using (var file = File.Create(output))
		using (var gz = new GZipStream(file, CompressionLevel.Optimal))
		{
			byte[] bytes = Encoding.UTF8.GetBytes(text);
			gz.Write(bytes, 0, bytes.Length);
		}
		Console.WriteLine("written: " + output + " (" + text.Split('\n').Length + " lines)");
		return 0;
	}

	private static string Read(string path)
	{
		if (!path.EndsWith(".gz")) return File.ReadAllText(path, Encoding.UTF8);
		using (var gz = new GZipStream(File.OpenRead(path), CompressionMode.Decompress))
		using (var reader = new StreamReader(gz, Encoding.UTF8)) return reader.ReadToEnd();
	}
}
