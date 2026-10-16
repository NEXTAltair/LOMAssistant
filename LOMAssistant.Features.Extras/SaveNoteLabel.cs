// Adapted from LOM_SaveSlotExpander's CommentLabel (MIT License, Copyright (c) 2026 ame-chan;
// Chinese edition by shiluodeqiou: https://github.com/shiluodeqiou/LOM_SaveSlotExpander-zh).
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LOMAssistant.Features.Extras;

/// <summary>Remembers the note labels created on a save-slot row.</summary>
internal class SaveNoteLabelSet : MonoBehaviour
{
	public Text[] Labels;
}

/// <summary>
/// 把存档备注显示成存档栏下方单独的一行“【备注】”。
/// 不往标题（第○年○月○旬）上接文字：那个 Text 开着 Best Fit，接上去会把日期一起缩小。
/// 这里复制日期时间那个 Text 做独立标签，放在它下面一行，固定一行高，长备注自动缩小字号。
/// </summary>
internal static class SaveNoteLabel
{
	private const string ObjectName = "LOMAssistant_NoteLabel";
	private const string Format = "【{0}】";
	private const int MinFontSize = 12;

	public static void Apply(Component panel, Text[] titles, Text[] times, string note)
	{
		if (panel == null)
		{
			return;
		}
		SaveNoteLabelSet set = panel.GetComponent<SaveNoteLabelSet>();
		if (set == null)
		{
			if (string.IsNullOrEmpty(note))
			{
				return; // build only when first needed
			}
			set = Build(panel, titles, times);
			if (set == null)
			{
				return;
			}
		}
		bool show = !string.IsNullOrEmpty(note);
		foreach (Text label in set.Labels)
		{
			if (label == null)
			{
				continue;
			}
			if (show)
			{
				label.text = string.Format(Format, note);
			}
			label.gameObject.SetActive(show);
		}
	}

	private static SaveNoteLabelSet Build(Component panel, Text[] titles, Text[] times)
	{
		List<Text> sources = Usable(times);
		if (sources.Count == 0)
		{
			sources = Usable(titles);
		}
		if (sources.Count == 0)
		{
			return null;
		}
		// Wrap at the title column's width; the date column spans the whole row.
		List<Text> titleList = Usable(titles);
		float wrapWidth = titleList.Count > 0 ? titleList[0].rectTransform.rect.width : 0f;

		var labels = new List<Text>();
		foreach (Text source in sources)
		{
			Text label = CreateLabel(source, wrapWidth);
			if (label != null)
			{
				labels.Add(label);
			}
		}
		if (labels.Count == 0)
		{
			return null;
		}
		SaveNoteLabelSet set = panel.gameObject.AddComponent<SaveNoteLabelSet>();
		set.Labels = labels.ToArray();
		return set;
	}

	private static List<Text> Usable(Text[] array)
	{
		var list = new List<Text>();
		if (array != null)
		{
			foreach (Text t in array)
			{
				if (t != null && t.transform.parent != null)
				{
					list.Add(t);
				}
			}
		}
		return list;
	}

	private static Text CreateLabel(Text source, float wrapWidth)
	{
		GameObject go = Object.Instantiate(source.gameObject, source.transform.parent, false);
		go.name = ObjectName;
		Text label = go.GetComponent<Text>();
		if (label == null)
		{
			Object.Destroy(go);
			return null;
		}
		label.supportRichText = false;
		label.resizeTextForBestFit = true;
		label.resizeTextMaxSize = label.fontSize;
		label.resizeTextMinSize = Mathf.Min(MinFontSize, label.fontSize);

		LayoutElement element = label.GetComponent<LayoutElement>() ?? label.gameObject.AddComponent<LayoutElement>();
		element.ignoreLayout = true;
		ContentSizeFitter fitter = label.GetComponent<ContentSizeFitter>();
		if (fitter != null)
		{
			fitter.enabled = false;
		}

		RectTransform src = source.rectTransform;
		RectTransform rt = label.rectTransform;
		rt.anchorMin = src.anchorMin;
		rt.anchorMax = src.anchorMax;
		rt.pivot = src.pivot;
		rt.sizeDelta = src.sizeDelta;

		float width = wrapWidth > 1f ? wrapWidth : src.rect.width;
		if (width > 1f)
		{
			rt.sizeDelta = new Vector2(rt.sizeDelta.x + (width - rt.rect.width), rt.sizeDelta.y);
		}
		float line = src.rect.height;
		if (line < 1f)
		{
			line = label.fontSize * 1.2f;
		}
		// One line below the date/time text.
		rt.anchoredPosition = src.anchoredPosition + new Vector2(0f, -line);
		return label;
	}
}
