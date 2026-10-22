using System;
using System.Collections.Generic;

namespace LOMAssistant;

/// <summary>
/// 咖啡伴侣窗口的深色模式。设置页「咖啡伴侣」组里选：浅色（默认）、深色、跟随系统（Windows 的「应用模式」）。
/// 窗口的底色、文字色在 StoryReviewPanel 里按 Dark 取；写在文字里的颜色标签（如 &lt;color=#73644F&gt;）是给浅色底配的，
/// 深色时在画字那一刻换成亮一些的同色系（见 DisplayTextPatch），只换咖啡伴侣窗口里的字。
/// </summary>
internal static class PanelTheme
{
	private static bool _systemDark;
	private static int _systemCheckedAt = int.MinValue;

	/// <summary>窗口现在用深色。</summary>
	public static bool Dark
	{
		get
		{
			switch (LOMAssistant.Config.ConfigManager.PanelTheme?.Value ?? 0)
			{
				case 1: return true;
				case 2: return SystemDark;
				default: return false;
			}
		}
	}

	// Windows 设置里「选择默认应用模式」为深色；几秒看一次注册表，切换系统主题后不用重启游戏
	private static bool SystemDark
	{
		get
		{
			if (Environment.TickCount - _systemCheckedAt < 3000)
			{
				return _systemDark;
			}
			_systemCheckedAt = Environment.TickCount;
			try
			{
				object value = Microsoft.Win32.Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "AppsUseLightTheme", 1);
				_systemDark = value is int light && light == 0;
			}
			catch
			{
				_systemDark = false;
			}
			return _systemDark;
		}
	}

	/// <summary>正在画咖啡伴侣窗口（窗口函数执行期间为 true），文字里的颜色标签才换。</summary>
	public static bool Painting { get; set; }

	// 浅色底上用的文字颜色 → 深色底上的对应颜色
	private static readonly Dictionary<string, string> DarkColors = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
	{
		{ "#73644F", "#A99C88" }, // 灰褐（次要文字）
		{ "#A3302A", "#E26A5C" }, // 朱砂
		{ "#B32B21", "#E26A5C" },
		{ "#2F6B3A", "#74C285" }, // 绿（满足、走过）
		{ "#38735C", "#74C2A2" },
		{ "#7A5A10", "#D9B45A" }, // 金褐
		{ "#B5AA98", "#6E655A" }, // 淡化的字：深色底上要更暗才显得淡
	};

	/// <summary>深色时把文字里给浅色底配的颜色标签换掉；不需要换时原样返回。</summary>
	public static string MapColors(string text)
	{
		if (!Painting || !Dark || text == null || text.IndexOf("<color=#", StringComparison.OrdinalIgnoreCase) < 0)
		{
			return text;
		}
		foreach (KeyValuePair<string, string> pair in DarkColors)
		{
			if (text.IndexOf(pair.Key, StringComparison.OrdinalIgnoreCase) >= 0)
			{
				text = text.Replace(pair.Key, pair.Value).Replace(pair.Key.ToLowerInvariant(), pair.Value);
			}
		}
		return text;
	}
}
