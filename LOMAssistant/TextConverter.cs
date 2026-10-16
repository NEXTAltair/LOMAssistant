using System.Collections.Generic;

namespace LOMAssistant;

public static class TextConverter
{
	private static readonly Dictionary<string, string> _cache = new Dictionary<string, string>();

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
		value = text;
		_cache[text] = value;
		return value;
	}

	public static void ClearCache()
	{
		_cache.Clear();
	}
}
