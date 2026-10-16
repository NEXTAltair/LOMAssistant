namespace LOMAssistant;

public class RichText
{
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
