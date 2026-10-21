using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace LOMAssistant;

/// <summary>
/// 简繁转换。游戏数据里的开发备注、事件名等多为繁体，本 Mod 内部（比较、搜索、拼字）统一用简体（ToSimplified）；
/// 游戏切到繁体中文时，显示前再转回繁体（ForDisplay）。都用 Windows 自带的 LCMapStringEx 逐字转换；失败时原样返回。
/// </summary>
public static class TextConverter
{
	private const uint LCMAP_SIMPLIFIED_CHINESE = 0x02000000;
	private const uint LCMAP_TRADITIONAL_CHINESE = 0x04000000;

	/// <summary>界面上的文字带数字（倒计时、数值），转过的会越积越多，超过这么多条就清掉重来。</summary>
	private const int CacheLimit = 20000;

	private static readonly Dictionary<string, string> _cache = new Dictionary<string, string>();
	private static readonly Dictionary<string, string> _traditionalCache = new Dictionary<string, string>();

	private static bool _unavailable;

	[DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
	private static extern int LCMapStringEx(string lpLocaleName, uint dwMapFlags, string lpSrcStr, int cchSrc,
		[Out] char[] lpDestStr, int cchDest, IntPtr lpVersionInformation, IntPtr lpReserved, IntPtr sortHandle);

	public static string ToSimplified(string text) => Cached(_cache, text, "zh-CN", LCMAP_SIMPLIFIED_CHINESE);

	public static string ToTraditional(string text) => Cached(_traditionalCache, text, "zh-TW", LCMAP_TRADITIONAL_CHINESE);

	/// <summary>游戏现在的语言是繁体中文。</summary>
	public static bool Traditional
	{
		get
		{
			try
			{
				return Lean.Localization.LeanLocalization.CurrentLanguage == "ChineseTraditional";
			}
			catch
			{
				return false;
			}
		}
	}

	/// <summary>要显示给玩家的文字：游戏是繁体中文时转成繁体，否则原样。</summary>
	public static string ForDisplay(string text) => Traditional ? ToTraditional(text) : text;

	private static string Cached(Dictionary<string, string> cache, string text, string locale, uint flags)
	{
		if (string.IsNullOrEmpty(text))
		{
			return text;
		}
		if (cache.TryGetValue(text, out var value))
		{
			return value;
		}
		if (cache.Count >= CacheLimit)
		{
			cache.Clear();
		}
		value = Convert(text, locale, flags);
		cache[text] = value;
		return value;
	}

	private static string Convert(string text, string locale, uint flags)
	{
		if (_unavailable)
		{
			return text;
		}
		try
		{
			int size = LCMapStringEx(locale, flags, text, text.Length, null, 0, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
			if (size <= 0)
			{
				return text;
			}
			var buffer = new char[size];
			int written = LCMapStringEx(locale, flags, text, text.Length, buffer, size, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
			return written > 0 ? new string(buffer, 0, written) : text;
		}
		catch (Exception e)
		{
			// Not on Windows / API missing: stop trying and show the original text.
			_unavailable = true;
			FileLogger.LogError("简繁转换不可用: " + e.Message);
			return text;
		}
	}

	public static void ClearCache()
	{
		_cache.Clear();
		_traditionalCache.Clear();
	}
}
