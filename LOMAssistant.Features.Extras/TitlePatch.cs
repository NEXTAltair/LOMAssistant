using HarmonyLib;
using LOMAssistant.Config;
using Mortal.Core;

namespace LOMAssistant.Features.Extras;

/// <summary>
/// 自定义称号：设了自定义称号（数值页「称号」），对话框名字上方、人物介绍、属性页、养成地图上显示的称号都换成它。
/// 游戏这几处都是按称号编号取「PlayerInfo/Title/编号」这条文字，这里在取文字时换掉；没设就照游戏原样。
/// </summary>
public static class TitlePatch
{
	private const string Prefix = "PlayerInfo/Title/";

	/// <summary>为真时取原本的称号文字（数值页列出游戏称号时用）。</summary>
	public static bool Raw;

	[HarmonyPostfix]
	[HarmonyPatch(typeof(LeanLocalizationResolver), "GetString")]
	public static void GetString_Postfix(string key, ref string __result)
	{
		if (Raw || key == null || !key.StartsWith(Prefix))
		{
			return;
		}
		string custom = (ConfigManager.CustomTitle.Value ?? "").Trim();
		if (custom.Length > 0)
		{
			__result = custom;
		}
	}

	/// <summary>游戏里第几个称号的原文（不受自定义影响）；没有这一条返回 null。</summary>
	public static string Original(int index)
	{
		Raw = true;
		try
		{
			string key = Prefix + index;
			string text = LocalizationManager.Instance?.LocaleResolver?.GetString(key);
			return string.IsNullOrWhiteSpace(text) || text == key ? null : TextConverter.ToSimplified(text.Trim());
		}
		catch
		{
			return null;
		}
		finally
		{
			Raw = false;
		}
	}
}
