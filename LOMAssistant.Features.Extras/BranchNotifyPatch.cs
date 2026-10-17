using System;
using System.Collections.Generic;
using HarmonyLib;
using LOMAssistant.Config;
using Mortal.Core;
using Mortal.Story;

namespace LOMAssistant.Features.Extras;

/// <summary>
/// 剧情分支条件提示（F8 开关，默认关）：剧情走到分支判断（SwitchResultData.Execute）时，
/// 在屏幕上列出这个判断的全部分支条件（第一条写走了第几个，下面每个分支一条），
/// 满足的绿色、不满足的红色。条件文字用信息窗口同一套格式（需 X → 当前 Y）。
/// 按次数判断的分支不提示；同一判断 2 秒内重复执行只显示一次。
/// </summary>
public static class BranchNotifyPatch
{
	private const int FontSize = 26;

	private static string _lastName;
	private static float _lastTime;

	[HarmonyPostfix]
	[HarmonyPatch(typeof(SwitchResultData), "Execute")]
	public static void Execute_Postfix(SwitchResultData __instance, int __result)
	{
		if (!ConfigManager.ShowBranchChecks.Value)
		{
			return;
		}
		try
		{
			StoryMainUI ui = StoryMainUI.Instance;
			if (ui == null || !ui.isActiveAndEnabled)
			{
				return;
			}
			var tr = Traverse.Create(__instance);
			if (tr.Field("_useCount").GetValue<bool>())
			{
				return;
			}
			List<ConditionResultItem> items = tr.Field("_items").GetValue<List<ConditionResultItem>>();
			if (items == null || items.Count == 0)
			{
				return;
			}
			string name = __instance.name ?? "";
			if (name == _lastName && UnityEngine.Time.unscaledTime - _lastTime < 2f)
			{
				return;
			}
			_lastName = name;
			_lastTime = UnityEngine.Time.unscaledTime;

			string note = tr.Field("_devNote").GetValue<string>() ?? "";
			int newline = note.IndexOf('\n');
			if (newline >= 0)
			{
				note = note.Substring(0, newline);
			}
			note = TextConverter.ToSimplified(note.Trim());

			// 第一行写明走了哪个分支，下面每个分支一行；字号缩小，长条件也不被截断
			string label = note.Length > 0 ? name + " " + note : name;
			string result = __result <= items.Count ? (items.Count > 1 ? "走第 " + __result + " 个" : "满足") : "都不满足";
			var lines = new List<string> { "【分支】" + label + " · " + result };
			for (int i = 0; i < items.Count; i++)
			{
				string conditions = DataHelper.ExtractConditions(items[i], out bool _);
				if (string.IsNullOrEmpty(conditions))
				{
					conditions = "无条件";
				}
				lines.Add((items.Count > 1 ? (i + 1) + ". " : "") + conditions);
			}
			// 游戏的飘字框只有一行高，多行塞进一条会被裁掉，所以每行单独一条
			foreach (string line in lines)
			{
				ui.DisplayMessageText("<size=" + FontSize + ">" + line + "</size>");
			}
			FileLogger.Log($"分支：{label} 共 {items.Count} 个，结果 {__result}");
		}
		catch (Exception e)
		{
			FileLogger.LogWarning("分支条件提示失败: " + e.Message);
		}
	}
}
