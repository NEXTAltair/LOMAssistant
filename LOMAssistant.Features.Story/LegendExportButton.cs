using System;
using System.Collections;
using HarmonyLib;
using Mortal.Core;
using UnityEngine;
using UnityEngine.UI;

namespace LOMAssistant.Features.Story;

/// <summary>
/// 游戏「状态 → 传奇」面板加一个「导出传奇」按钮：把这一局还没完结的传奇导出成桌面上的 txt 并打开（见 LegendExport）。
/// 按钮是自己搭的 uGUI（半透明墨色底、宣纸色字），字体用面板里传奇正文的字体；面板每次打开时确认按钮在。
/// 位置贴着传奇正文滚动区的右上角、在正文上方（正文区以外，不会被滚动区裁掉，也不压面板边框）。
/// </summary>
public static class LegendExportButton
{
	private const string ButtonName = "LOMAssistant.LegendExport";

	[HarmonyPostfix]
	[HarmonyPatch(typeof(LegendInfoPanel), nameof(LegendInfoPanel.OnPanelOpen))]
	public static void OnPanelOpen_Postfix(LegendInfoPanel __instance)
	{
		try
		{
			ScrollRect scroll = __instance.GetComponentInChildren<ScrollRect>(includeInactive: true);
			var host = (RectTransform)(scroll != null ? scroll.transform : __instance.transform);
			Transform existing = host.Find(ButtonName);
			if (existing != null)
			{
				SetLabel(existing.GetComponentInChildren<Text>(), "导出传奇");
				return;
			}
			Create(host, (RectTransform)__instance.transform);
		}
		catch (Exception e)
		{
			FileLogger.LogWarning("[传奇导出] 加按钮失败: " + e.Message);
		}
	}

	private static void Create(RectTransform host, RectTransform panel)
	{
		var go = new GameObject(ButtonName, typeof(RectTransform), typeof(Image), typeof(Button));
		go.transform.SetParent(host, false);
		var rt = (RectTransform)go.transform;
		// 底边对齐滚动区上沿、右边对齐滚动区右沿（让开滚动条）
		rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
		rt.pivot = new Vector2(1f, 0f);
		rt.anchoredPosition = new Vector2(-24f, 10f);
		rt.sizeDelta = new Vector2(170f, 46f);
		var image = go.GetComponent<Image>();
		image.color = new Color(0.08f, 0.07f, 0.06f, 0.75f);
		var button = go.GetComponent<Button>();
		ColorBlock colors = button.colors;
		colors.highlightedColor = colors.selectedColor = new Color(1.4f, 1.3f, 1.2f, 1f);
		button.colors = colors;

		var textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
		textGo.transform.SetParent(go.transform, false);
		var trt = (RectTransform)textGo.transform;
		trt.anchorMin = Vector2.zero;
		trt.anchorMax = Vector2.one;
		trt.offsetMin = trt.offsetMax = Vector2.zero;
		var text = textGo.GetComponent<Text>();
		// 和传奇正文同一个字体（游戏按语言换的字体）
		Text sample = null;
		foreach (Text t in panel.GetComponentsInChildren<Text>(includeInactive: true))
		{
			if (t != text && t.font != null)
			{
				sample = t;
				break;
			}
		}
		text.font = sample != null ? sample.font : Font.CreateDynamicFontFromOSFont("Microsoft YaHei", 26);
		text.fontSize = 24;
		text.alignment = TextAnchor.MiddleCenter;
		text.color = new Color(0.95f, 0.91f, 0.84f, 1f);
		text.raycastTarget = false;
		SetLabel(text, "导出传奇");

		button.onClick.AddListener(() => OnClick(button, text));
	}

	private static void SetLabel(Text text, string label)
	{
		if (text != null)
		{
			text.text = TextConverter.ForDisplay(label);
		}
	}

	private static void OnClick(Button button, Text text)
	{
		try
		{
			string path = LegendExport.ExportCurrent();
			if (path == null)
			{
				SetLabel(text, "没有传奇");
			}
			else
			{
				SetLabel(text, "已导出到桌面");
				Application.OpenURL("file:///" + path.Replace('\\', '/'));
			}
		}
		catch (Exception e)
		{
			FileLogger.LogError("[传奇导出] 失败: " + e);
			SetLabel(text, "导出失败");
		}
		button.StartCoroutine(RestoreLabel(text));
	}

	// 提示文字停两秒再换回「导出传奇」
	private static IEnumerator RestoreLabel(Text text)
	{
		yield return new WaitForSecondsRealtime(2f);
		SetLabel(text, "导出传奇");
	}
}
