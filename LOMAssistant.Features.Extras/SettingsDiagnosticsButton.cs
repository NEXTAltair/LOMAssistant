using System;
using System.Collections;
using HarmonyLib;
using Mortal.Core;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace LOMAssistant.Features.Extras;

/// <summary>
/// 游戏 ESC 菜单「环境设定」面板里加一个「导出诊断」按钮：复制右下角的「储存」按钮（长方形、横排字），放在它正上方。
/// 点一下把日志等打包到桌面、路径复制到剪贴板，按钮文字临时显示结果，不弹窗口。
/// </summary>
public static class SettingsDiagnosticsButton
{
	private const string ButtonName = "LOMAssistant_ExportDiagnostics";
	private const string Label = "导出诊断";

	[HarmonyPostfix]
	[HarmonyPatch(typeof(SystemSettingPanel), "Start")]
	public static void Start_Postfix(SystemSettingPanel __instance)
	{
		try
		{
			Button template = FindSaveButton(__instance);
			if (template == null)
			{
				FileLogger.LogWarning("导出诊断：环境设定面板里没找到「储存」按钮，不加导出按钮。");
				return;
			}
			Transform parent = template.transform.parent;
			if (parent.Find(ButtonName) != null)
			{
				return;
			}
			bool laidOut = parent.GetComponent<LayoutGroup>() != null;
			Button button = MenuButtonHelper.CloneButton(template, Label, ButtonName);
			if (!laidOut)
			{
				// 没有自动排版：放在「储存」正上方，不要叠在一起
				var rt = (RectTransform)button.transform;
				rt.anchoredPosition += new Vector2(0f, ((RectTransform)template.transform).rect.height * 1.25f);
			}
			// 四个字比「储存」长，字号自动缩小以放得下
			foreach (Text t in button.GetComponentsInChildren<Text>(includeInactive: true))
			{
				t.resizeTextForBestFit = true;
				t.resizeTextMinSize = 10;
				t.resizeTextMaxSize = t.fontSize;
				t.horizontalOverflow = HorizontalWrapMode.Wrap;
			}
			foreach (TMP_Text t in button.GetComponentsInChildren<TMP_Text>(includeInactive: true))
			{
				t.enableAutoSizing = true;
				t.fontSizeMax = t.fontSize;
				t.fontSizeMin = 10f;
			}
			button.onClick.AddListener(() => OnClick(button));
			FileLogger.Log("导出诊断：已在环境设定面板加入按钮 " + MenuButtonHelper.DescribeHierarchy(button.transform) + (laidOut ? "" : "（手动摆放）"));
		}
		catch (Exception e)
		{
			FileLogger.LogWarning("导出诊断：添加按钮失败: " + e.Message);
		}
	}

	// 面板里绑定了 Save 的按钮（右下角「储存」）。找不到就不加，免得复制到形状不合适的按钮
	private static Button FindSaveButton(SystemSettingPanel panel)
	{
		foreach (Button b in panel.GetComponentsInChildren<Button>(includeInactive: true))
		{
			if (b.name == ButtonName)
			{
				continue;
			}
			UnityEvent click = b.onClick;
			for (int i = 0; i < click.GetPersistentEventCount(); i++)
			{
				if (click.GetPersistentMethodName(i) == "Save")
				{
					return b;
				}
			}
		}
		return null;
	}

	private static void OnClick(Button button)
	{
		string text;
		try
		{
			DiagnosticsExport.Export();
			text = "已导出";
		}
		catch (Exception e)
		{
			text = "导出失败";
			FileLogger.LogWarning("导出诊断失败: " + e);
		}
		LOMAssistantPlugin.Instance.StartCoroutine(ShowResult(button, text));
	}

	private static IEnumerator ShowResult(Button button, string text)
	{
		SetText(button, text);
		yield return new WaitForSecondsRealtime(3f);
		if (button != null)
		{
			SetText(button, Label);
		}
	}

	private static void SetText(Button button, string text)
	{
		text = TextConverter.ForDisplay(text);
		foreach (Text t in button.GetComponentsInChildren<Text>(includeInactive: true))
		{
			t.text = text;
		}
		foreach (TMP_Text t in button.GetComponentsInChildren<TMP_Text>(includeInactive: true))
		{
			t.text = text;
		}
	}
}
