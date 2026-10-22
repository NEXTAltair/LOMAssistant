using System;
using System.IO;
using System.Text;
using Mortal.Core;

namespace LOMAssistant.Features.Story;

/// <summary>
/// 导出当前存档的传奇文本，就是游戏「状态 → 传奇」里那段还没完结的传奇（PlayerStatManagerData.StoryKeyList，
/// 每段文字在本地化表 LegendInfo/编号 里，和游戏的 LegendInfoPanel 拼法一样）。
/// 游戏原版要走到结局、存进传奇栏才能导出；这里随时导出成桌面上的 txt。
/// </summary>
internal static class LegendExport
{
	/// <summary>写出 txt，返回文件路径；没有进行中的一局（标题画面、还没有任何传奇片段）返回 null。</summary>
	public static string ExportCurrent()
	{
		PlayerStatManagerData stat = PlayerStatManagerData.Instance;
		string scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
		if (stat == null || scene == "Title" || stat.StoryKeyList == null || stat.StoryKeyList.Count == 0)
		{
			return null;
		}
		var text = new StringBuilder();
		foreach (string key in stat.StoryKeyList)
		{
			string value = LocalizationManager.Instance.LocaleResolver.GetString("LegendInfo/" + key);
			if (!string.IsNullOrEmpty(value) && value != "LegendInfo/" + key)
			{
				text.AppendLine(RichText.Strip(value).Replace("\r\n", "\n").Replace("\n", "\r\n"));
			}
		}
		string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "活侠传传奇_" + DateTime.Now.ToString("yyyyMMdd_HHmm") + ".txt");
		// 带 BOM，记事本打开不乱码
		File.WriteAllText(path, text.ToString(), new UTF8Encoding(true));
		FileLogger.Log($"[传奇导出] {stat.StoryKeyList.Count} 段，写到 {path}");
		return path;
	}
}
