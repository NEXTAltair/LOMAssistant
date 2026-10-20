using System.Collections.Generic;
using HarmonyLib;
using LOMAssistant.Config;
using UnityEngine;
using UnityEngine.UI;

namespace LOMAssistant.Features.Extras;

/// <summary>
/// 只放大剧情对白的字，不放大界面：对白框（普通对白、内心独白、旁白，都叫 StoryText，在 SayDialogs 下面）排版时
/// 先取一份排版参数，在这里把字号乘上设定的比例。不改文字组件本身记的字号，关掉（100%）就完全是原样。
/// 菜单、按钮、数值这些别的字不动，免得撑破界面。
/// </summary>
public static class FontScale
{
	private static int _applied = 100;
	private static readonly Dictionary<int, bool> Dialogue = new Dictionary<int, bool>();

	private static float Factor => Mathf.Clamp(ConfigManager.FontScale.Value, 50, 200) / 100f;

	/// <summary>每帧调用：比例改了就让对白重新排版。</summary>
	public static void Tick()
	{
		int now = ConfigManager.FontScale.Value;
		if (now == _applied)
		{
			return;
		}
		_applied = now;
		foreach (Text text in Resources.FindObjectsOfTypeAll<Text>())
		{
			if (text != null && text.gameObject.scene.IsValid() && IsDialogue(text))
			{
				text.SetAllDirty();
			}
		}
	}

	private static bool IsDialogue(Text text)
	{
		int id = text.GetInstanceID();
		if (!Dialogue.TryGetValue(id, out bool yes))
		{
			yes = false;
			// 屏幕正中的大字旁白（CenterNarrativeSayDialog，StoryText 直接挂在 Panel 下）本来就大，不动
			if (text.name == "StoryText" && text.transform.parent != null && text.transform.parent.name == "Background")
			{
				for (Transform t = text.transform.parent; t != null; t = t.parent)
				{
					if (t.name == "SayDialogs")
					{
						yes = true;
						break;
					}
				}
			}
			Dialogue[id] = yes;
		}
		return yes;
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(Text), nameof(Text.GetGenerationSettings))]
	public static void GetGenerationSettings_Postfix(Text __instance, ref TextGenerationSettings __result)
	{
		float factor = Factor;
		if (factor == 1f || !IsDialogue(__instance))
		{
			return;
		}
		__result.fontSize = Mathf.Max(1, Mathf.RoundToInt(__result.fontSize * factor));
		__result.resizeTextMinSize = Mathf.Max(1, Mathf.RoundToInt(__result.resizeTextMinSize * factor));
		__result.resizeTextMaxSize = Mathf.Max(1, Mathf.RoundToInt(__result.resizeTextMaxSize * factor));
	}
}
