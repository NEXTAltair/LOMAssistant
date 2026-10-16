using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LOMAssistant.Features.Extras;

internal static class MenuButtonHelper
{
	// Only the components that write text; font switchers (LeanLocalizedTextFont etc.)
	// must stay so every state (normal/hover/...) gets the current language's font.
	private static readonly HashSet<string> TextLocalizers = new HashSet<string>
	{
		"LeanLocalizedText",
		"LeanLocalizedTextMesh",
		"LeanLocalizedTextMeshPro",
		"LeanLocalizedTextMeshProUGUI"
	};

	/// <summary>
	/// Clones an existing menu button right after the template, relabels it, replaces
	/// its click handlers and tightens the parent layout so all buttons still fit.
	/// </summary>
	public static Button CloneButton(Button template, string label, string objectName)
	{
		Transform parent = template.transform.parent;
		Transform existing = parent.Find(objectName);
		if (existing != null)
		{
			return existing.GetComponent<Button>();
		}

		GameObject clone = Object.Instantiate(template.gameObject, parent, worldPositionStays: false);
		clone.name = objectName;
		clone.transform.SetSiblingIndex(template.transform.GetSiblingIndex() + 1);

		foreach (MonoBehaviour mb in clone.GetComponentsInChildren<MonoBehaviour>(includeInactive: true))
		{
			if (mb != null && TextLocalizers.Contains(mb.GetType().Name))
			{
				Object.DestroyImmediate(mb);
			}
		}
		foreach (Text t in clone.GetComponentsInChildren<Text>(includeInactive: true))
		{
			t.text = label;
		}
		foreach (TMP_Text t in clone.GetComponentsInChildren<TMP_Text>(includeInactive: true))
		{
			t.text = label;
		}

		Button button = clone.GetComponent<Button>();
		button.onClick = new Button.ButtonClickedEvent();
		button.interactable = true;
		clone.SetActive(true);

		FitVerticalLayout(parent, (RectTransform)template.transform);
		return button;
	}

	/// <summary>
	/// The menus stack buttons in a VerticalLayoutGroup laid out for the original count;
	/// an extra button pushes the last one off screen. Shrink the spacing so the stack
	/// keeps the height it had before the new button was added.
	/// </summary>
	private static void FitVerticalLayout(Transform parent, RectTransform sample)
	{
		VerticalLayoutGroup layout = parent.GetComponent<VerticalLayoutGroup>();
		if (layout == null)
		{
			FileLogger.Log("菜单按钮：父节点没有 VerticalLayoutGroup，不调整间距。");
			return;
		}

		int count = 0;
		foreach (Transform child in parent)
		{
			if (child.gameObject.activeSelf && child.GetComponent<LayoutElement>()?.ignoreLayout != true)
			{
				count++;
			}
		}
		if (count < 2)
		{
			return;
		}

		float h = sample.rect.height;
		if (h <= 0f)
		{
			h = LayoutUtility.GetPreferredHeight(sample);
		}
		// (count-1) buttons with old spacing == count buttons with new spacing.
		int oldCount = count - 1;
		float oldHeight = oldCount * h + (oldCount - 1) * layout.spacing;
		float newSpacing = (oldHeight - count * h) / (count - 1);

		string msg = $"菜单按钮：{MenuButtonHelper.DescribeHierarchy(parent)} 按钮数 {count}，按钮高 {h:0.#}，" +
			$"间距 {layout.spacing:0.#} -> {newSpacing:0.#}，controlHeight={layout.childControlHeight}，" +
			$"fitter={(parent.GetComponent<ContentSizeFitter>() != null)}";
		LOMAssistantPlugin.LogInfo(msg);
		FileLogger.Log(msg);

		layout.spacing = newSpacing;
		LayoutRebuilder.MarkLayoutForRebuild((RectTransform)parent);
	}

	public static string DescribeHierarchy(Transform t)
	{
		var sb = new StringBuilder();
		for (Transform cur = t; cur != null; cur = cur.parent)
		{
			sb.Insert(0, "/" + cur.name);
		}
		return sb.ToString();
	}
}
