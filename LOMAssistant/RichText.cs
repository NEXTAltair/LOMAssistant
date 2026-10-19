namespace LOMAssistant;

public class RichText
{
	// 只去掉富文本标签。不能用 "<.*?>"：条件里的比较符「<」会把后面一大段当成标签吃掉（「道德 需<40 → 当前…」只剩「道德 需」）
	private static readonly System.Text.RegularExpressions.Regex Tags = new System.Text.RegularExpressions.Regex("</?(color|size|b|i|material|quad)(=[^>]*)?>");

	public static string Strip(string text)
	{
		return text == null ? null : Tags.Replace(text, string.Empty);
	}

	public static string Green(string text)
	{
		return "<color=#A3B86C>" + text + "</color>";
	}

	public static string Red(string text)
	{
		return "<color=#CD594A>" + text + "</color>";
	}

	public static string Golden(string text)
	{
		return "<color=#BCA136>" + text + "</color>";
	}

	public static string SeparateLine(string text)
	{
		int num = 68 - text.Length;
		return new string('═', num / 2) + text + new string('═', num / 2);
	}

	public static string ColorText(string text)
	{
		if (!text.Contains("+"))
		{
			return Red(text);
		}
		return Green(text);
	}
}
