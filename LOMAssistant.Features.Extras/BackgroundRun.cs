using HarmonyLib;
using LOMAssistant.Config;
using UnityEngine;

namespace LOMAssistant.Features.Extras;

/// <summary>
/// 后台继续运行：切到别的窗口时游戏不暂停，背景音乐也不停。
/// 游戏本身切走就停（Unity 不在后台运行），音乐由 Wwise 在失去焦点时挂起（AkSoundEngineController.ActivateAudio）。
/// 打开时让 Unity 在后台继续跑，并拦下 Wwise 的挂起；关掉就恢复游戏原来的做法。
/// </summary>
public static class BackgroundRun
{
	private static bool? _original;

	/// <summary>每帧调用：按开关设置 Unity 是否在后台运行。</summary>
	public static void Tick()
	{
		if (_original == null)
		{
			_original = Application.runInBackground;
		}
		bool want = ConfigManager.EnableBackgroundRun.Value || _original.Value;
		if (Application.runInBackground != want)
		{
			Application.runInBackground = want;
		}
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(AkSoundEngineController), "ActivateAudio")]
	public static bool ActivateAudio_Prefix(bool activate)
	{
		// 失去焦点时不挂起声音；回到前台照常（没挂起过，唤醒也没影响）
		return activate || !ConfigManager.EnableBackgroundRun.Value;
	}
}
