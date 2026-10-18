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
		string managed = args[0], output = args[1];
		if (args.Length > 2)
		{
			// 只对比：列出旧契约里和当前游戏不一致的项目
			var old = CompatibilityContract.Parse(Read(args[2]));
			var diffs = CompatibilityContract.Differences(old, CompatibilityContract.Snapshot(managed));
			Console.WriteLine("changed: " + diffs.Count);
			foreach (string d in diffs) Console.WriteLine("  " + d);
			if (output == "-") return 0;
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
