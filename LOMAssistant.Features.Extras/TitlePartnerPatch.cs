using HarmonyLib;
using LOMAssistant.Config;
using Mortal.Core;

namespace LOMAssistant.Features.Extras;

/// <summary>
/// 标题画面人物自选：游戏按结局记一个编号（SaveSystem.TitlePartner），标题画面按它显示结缘的人。
/// 设置页「标题画面人物」选了某个人时，标题画面读这个编号时换成她（不改全局存档里记的值，选回「跟随结局」就恢复）。
/// </summary>
public static class TitlePartnerPatch
{
	/// <summary>设置里的选项（下标）对应的编号：-1 跟随结局，0 不显示；标题画面能显示的只有这几个。</summary>
	internal static readonly int[] Values = { -1, 0, 1, 2, 3, 5, 6, 8, 20 };

	internal static readonly string[] Names = { "跟随结局", "不显示", "小师妹", "龙湘", "叶云裳", "小兰", "小梅", "小竹", "大师兄" };

	[HarmonyPostfix]
	[HarmonyPatch(typeof(SaveSystem), nameof(SaveSystem.TitlePartner), MethodType.Getter)]
	public static void TitlePartner_Postfix(ref int __result)
	{
		int choice = ConfigManager.TitlePartnerChoice.Value;
		if (choice > 0 && choice < Values.Length)
		{
			__result = Values[choice];
		}
	}
}
