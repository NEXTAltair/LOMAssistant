using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace LOMAssistant;

/// <summary>
/// 繁体转简体。游戏数据里的开发备注、事件名等多为繁体，这里用 Windows 自带的
/// LCMapStringEx（LCMAP_SIMPLIFIED_CHINESE）逐字转换；失败时原样返回。
/// </summary>
public static class TextConverter
{
	private const uint LCMAP_SIMPLIFIED_CHINESE = 0x02000000;

	private static readonly Dictionary<string, string> _cache = new Dictionary<string, string>();

	private static bool _unavailable;

	[DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
	private static extern int LCMapStringEx(string lpLocaleName, uint dwMapFlags, string lpSrcStr, int cchSrc,
		[Out] char[] lpDestStr, int cchDest, IntPtr lpVersionInformation, IntPtr lpReserved, IntPtr sortHandle);

	public static string ToSimplified(string text)
	{
		if (string.IsNullOrEmpty(text))
		{
			return text;
		}
		if (_cache.TryGetValue(text, out var value))
		{
			return value;
		}
		value = Convert(text);
		_cache[text] = value;
		return value;
	}

	private static string Convert(string text)
	{
		if (_unavailable)
		{
			return text;
		}
		try
		{
			int size = LCMapStringEx("zh-CN", LCMAP_SIMPLIFIED_CHINESE, text, text.Length, null, 0, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
			if (size <= 0)
			{
				return text;
			}
			var buffer = new char[size];
			int written = LCMapStringEx("zh-CN", LCMAP_SIMPLIFIED_CHINESE, text, text.Length, buffer, size, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
			return written > 0 ? new string(buffer, 0, written) : text;
		}
		catch (Exception e)
		{
			// Not on Windows / API missing: stop trying and show the original text.
			_unavailable = true;
			FileLogger.LogError("繁转简不可用: " + e.Message);
			return text;
		}
	}

	public static void ClearCache()
	{
		_cache.Clear();
	}
}
