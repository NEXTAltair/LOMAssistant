using System;
using System.IO;
using System.Text;

/// <summary>
/// 跳过开头的 Logo：游戏开头的两张 Logo 是 Unity 启动画面，由游戏设置文件 Mortal_Data\globalgamemanagers 里的
/// 「显示启动画面」开关决定，放的时候 Mod 还没开始运行，只能改这个开关。
///
/// 只改这一个字节（1 = 显示，0 = 不显示），改之前把原文件备份成 globalgamemanagers.lom-original（游戏更新后再改会覆盖旧备份）。
/// 位置这样找：设置里依次存着公司名「Obb Studio」、产品名「Mortal」、默认鼠标（12 字节）、鼠标热点（8 字节）、
/// 启动画面背景色（16 字节），后面紧跟着这个开关和「显示 Unity Logo」开关，再补两个 0 对齐。
/// 找不到或者对不上，就什么都不改。游戏更新、Steam 验证文件后会变回原样，重新运行安装器即可。
/// </summary>
internal static class SplashPatch
{
	private const string File_ = @"Mortal_Data\globalgamemanagers";

	/// <summary>按勾选设置开关；返回给日志看的一句话。</summary>
	public static string Apply(string game, bool skip)
	{
		string path = Path.Combine(game, File_);
		if (!File.Exists(path))
		{
			return InstallerText.L("没有找到游戏设置文件，没有改动开头 Logo。");
		}
		byte[] data = File.ReadAllBytes(path);
		int at = Find(data);
		if (at < 0)
		{
			return InstallerText.L("游戏设置文件的格式和预期不同（可能游戏更新了），没有改动开头 Logo。");
		}
		byte want = skip ? (byte)0 : (byte)1;
		if (data[at] == want)
		{
			return skip ? InstallerText.L("开头 Logo 已经是跳过的。") : InstallerText.L("开头 Logo 保持原样。");
		}
		// 走到这里说明开关还是游戏原样：每次都重新备份，游戏更新后留着的旧备份是上一版的文件，不能再用
		string backup = path + ".lom-original";
		if (skip)
		{
			File.Copy(path, backup, overwrite: true);
		}
		using (var stream = new FileStream(path, FileMode.Open, FileAccess.Write))
		{
			stream.Position = at;
			stream.WriteByte(want);
		}
		return skip ? InstallerText.L("已设置跳过开头 Logo（改了游戏设置文件里的一个开关，原文件备份为 globalgamemanagers.lom-original）。") : InstallerText.L("已恢复开头 Logo。");
	}

	private static int Find(byte[] data)
	{
		byte[] company = Record("Obb Studio");
		byte[] product = Record("Mortal");
		int found = -1;
		for (int i = 0; i + company.Length + product.Length + 40 < data.Length; i++)
		{
			if (!Match(data, i, company) || !Match(data, i + company.Length, product))
			{
				continue;
			}
			int at = i + company.Length + product.Length + 12 + 8 + 16;
			bool shape = data[at] <= 1 && data[at + 1] <= 1 && data[at + 2] == 0 && data[at + 3] == 0;
			if (!shape || found >= 0)
			{
				return -1; // 对不上或者不止一处，不冒险
			}
			found = at;
		}
		return found;
	}

	// 长度（4 字节）+ 文字，补 0 到 4 的倍数
	private static byte[] Record(string text)
	{
		byte[] bytes = Encoding.UTF8.GetBytes(text);
		int padded = (bytes.Length + 3) / 4 * 4;
		var record = new byte[4 + padded];
		BitConverter.GetBytes(bytes.Length).CopyTo(record, 0);
		bytes.CopyTo(record, 4);
		return record;
	}

	private static bool Match(byte[] data, int at, byte[] pattern)
	{
		for (int k = 0; k < pattern.Length; k++)
		{
			if (data[at + k] != pattern[k])
			{
				return false;
			}
		}
		return true;
	}
}
