using HarmonyLib;
using UnityEngine;

namespace LOMAssistant;

/// <summary>
/// 游戏切到繁体中文时，本 Mod 用 IMGUI 画的窗口、小窗、提示（文字都是简体写的）画之前转成繁体。
/// IMGUI 画字最后都经过 GUIStyle 的这两个内部方法：画的那一下把文字换成繁体，画完换回来，不改动调用方的 GUIContent。
/// 输入框正在编辑时走的是另一条路（DrawWithTextSelection），打的字原样显示，不受影响。
/// </summary>
public static class TraditionalDisplayPatch
{
	[HarmonyPrefix]
	[HarmonyPatch(typeof(GUIStyle), "Internal_Draw")]
	public static void Draw_Prefix(GUIContent __1, out string __state) => __state = Swap(__1);

	[HarmonyFinalizer]
	[HarmonyPatch(typeof(GUIStyle), "Internal_Draw")]
	public static void Draw_Finalizer(GUIContent __1, string __state) => Restore(__1, __state);

	[HarmonyPrefix]
	[HarmonyPatch(typeof(GUIStyle), "Internal_Draw2")]
	public static void Draw2_Prefix(GUIContent __1, out string __state) => __state = Swap(__1);

	[HarmonyFinalizer]
	[HarmonyPatch(typeof(GUIStyle), "Internal_Draw2")]
	public static void Draw2_Finalizer(GUIContent __1, string __state) => Restore(__1, __state);

	/// <summary>换成繁体，返回原来的文字；没换返回 null。</summary>
	private static string Swap(GUIContent content)
	{
		if (content == null || string.IsNullOrEmpty(content.text) || !TextConverter.Traditional)
		{
			return null;
		}
		string original = content.text;
		string converted = TextConverter.ToTraditional(original);
		if (converted == original)
		{
			return null;
		}
		content.text = converted;
		return original;
	}

	private static void Restore(GUIContent content, string original)
	{
		if (original != null)
		{
			content.text = original;
		}
	}
}
