using System;
using HarmonyLib;
using LOMAssistant.Config;
using Mortal.Core;
using Mortal.Story;
using UnityEngine.UI;

namespace LOMAssistant.Features.Extras;

/// <summary>
/// 剧情旗标变化提示（F8 开关，默认关）：剧情脚本改动旗标时，在屏幕上用游戏自带的消息提示显示
/// 「旗标名 旧值 → 新值」，例如救没救下大师兄、神兵帖进度，可以当场知道。
/// 只拦剧情脚本用的 StatModifyManager.SetFlag/AddFlag，读档时逐个恢复旗标不经过这里，不会刷屏。
/// 「旗标暂存」这类脚本内部临时用的旗标不提示。
/// 另外：F9 面板的「心上人」只显示编号，这里在旁边显示人名。
/// </summary>
public static class FlagNotifyPatch
{
	[HarmonyPrefix]
	[HarmonyPatch(typeof(StatModifyManager), "SetFlag")]
	public static void SetFlag_Prefix(string id, out int __state) => __state = CurrentState(id);

	[HarmonyPostfix]
	[HarmonyPatch(typeof(StatModifyManager), "SetFlag")]
	public static void SetFlag_Postfix(string id, int __state) => Notify(id, __state);

	[HarmonyPrefix]
	[HarmonyPatch(typeof(StatModifyManager), "AddFlag")]
	public static void AddFlag_Prefix(string id, out int __state) => __state = CurrentState(id);

	[HarmonyPostfix]
	[HarmonyPatch(typeof(StatModifyManager), "AddFlag")]
	public static void AddFlag_Postfix(string id, int __state) => Notify(id, __state);

	private static int CurrentState(string id)
	{
		if (!ConfigManager.ShowFlagChanges.Value)
		{
			return 0;
		}
		try
		{
			FlagData flag = MissionManagerData.Instance?.GetFlag(id);
			return flag != null ? flag.State : 0;
		}
		catch
		{
			return 0;
		}
	}

	private static void Notify(string id, int before)
	{
		if (!ConfigManager.ShowFlagChanges.Value)
		{
			return;
		}
		try
		{
			FlagData flag = MissionManagerData.Instance?.GetFlag(id);
			if (flag == null || flag.State == before)
			{
				return;
			}
			string note = flag.DevNote ?? "";
			int newline = note.IndexOf('\n');
			if (newline >= 0)
			{
				note = note.Substring(0, newline);
			}
			note = TextConverter.ToSimplified(note.Trim());
			if (note.Contains("暂存"))
			{
				return;
			}
			string label = note.Length > 0 ? note : id;
			string message = $"【旗标】{label}  {before} → {flag.State}";
			FileLogger.Log($"旗标变化：{id} {label} {before} -> {flag.State}");
			StoryMainUI ui = StoryMainUI.Instance;
			if (ui != null && ui.isActiveAndEnabled)
			{
				ui.DisplayMessageText(message);
				Linger(ui);
			}
		}
		catch (Exception e)
		{
			FileLogger.LogWarning("旗标变化提示失败: " + e.Message);
		}
	}

	/// <summary>
	/// 游戏的提示只停 1 秒、按游戏时间算，开倍速时一闪就没。把刚弹出的这条改成按真实时间停留配置的秒数，
	/// 往上飘的距离不变（停得久就飘得慢）。
	/// </summary>
	private static void Linger(StoryMainUI ui)
	{
		float seconds = ConfigManager.FlagNoticeSeconds.Value;
		if (seconds <= 0f)
		{
			return;
		}
		GameMessagePanel panel = Traverse.Create(ui).Field("_messagePanel").GetValue<GameMessagePanel>();
		if (panel == null || panel.transform.childCount == 0)
		{
			return;
		}
		FloatingText text = panel.transform.GetChild(panel.transform.childCount - 1).GetComponent<FloatingText>();
		if (text == null)
		{
			return;
		}
		var tr = Traverse.Create(text);
		float original = tr.Field("_showTime").GetValue<float>();
		float scaled = seconds * UnityEngine.Mathf.Max(0.01f, UnityEngine.Time.timeScale);
		tr.Field("_showTime").SetValue(scaled);
		if (original > 0f)
		{
			tr.Field("_moveSpeed").SetValue(tr.Field("_moveSpeed").GetValue<float>() * original / scaled);
		}
	}

	// F9「心上人」：括号里原本是加成值（永远是 +0），改成显示人名
	[HarmonyPostfix]
	[HarmonyPatch(typeof(TestGameStatPanel), "UpdateValue")]
	public static void TestGameStatPanel_UpdateValue_Postfix(TestGameStatPanel __instance)
	{
		try
		{
			var tr = Traverse.Create(__instance);
			GameStat stat = tr.Field("_stat").GetValue<GameStat>();
			if (stat == null || stat.StatType != GameStatType.愛人)
			{
				return;
			}
			Text add = tr.Field("_addText").GetValue<Text>();
			if (add == null)
			{
				return;
			}
			string key = "PlayerInfo/Lover/" + stat.FinalValue;
			string name = LocalizationManager.Instance.LocaleResolver.GetString(key);
			if (!string.IsNullOrEmpty(name) && name != key)
			{
				add.text = "(" + TextConverter.ToSimplified(name) + ")";
			}
		}
		catch
		{
		}
	}
}
