using System;
using System.Collections.Generic;
using HarmonyLib;
using LOMAssistant.Config;
using Mortal.Core;
using UnityEngine;
using UnityEngine.UI;

namespace LOMAssistant.Features.Extras;

/// <summary>
/// 传奇栏位翻页：结局后「保存传奇」和「图鉴 → 传奇」都只有 12 格。存档文件名是 Legend_{三位数}.dat，
/// 栏位号本身没有上限，所以不多建格子，只在 12 格下面加「< n / N >」，翻页时把这 12 格改指第 n 页的栏位号再刷新。
/// 打开保存传奇时自动翻到第一个有空格的页。
/// </summary>
public static class LegendSlotPager
{
	private const int PageSize = 12;
	private const string PagerName = "LOMAssistant.LegendPager";

	// 每个格子原来的栏位号（1～12），翻页时按它算新栏位号
	private static readonly Dictionary<MonoBehaviour, int> BaseSlot = new Dictionary<MonoBehaviour, int>();

	private static int _page;

	private static int Pages => Mathf.Max(1, ConfigManager.LegendSlotPages.Value);

	[HarmonyPostfix]
	[HarmonyPatch(typeof(EndGamePanel), nameof(EndGamePanel.OnSaveLegendPanelOpen))]
	public static void OnSaveLegendPanelOpen_Postfix(EndGamePanel __instance)
	{
		try
		{
			var tr = Traverse.Create(__instance);
			SaveLegendSlotPanel[] slots = tr.Field("_saveSlots").GetValue<SaveLegendSlotPanel[]>();
			var panel = tr.Field("_saveLegendPanel").GetValue<CommonPanel>();
			if (slots == null || slots.Length == 0 || panel == null)
			{
				return;
			}
			_page = FirstPageWithEmptySlot();
			Attach(panel.transform, slots, () => __instance.OnHoverHandler(0));
		}
		catch (Exception e)
		{
			FileLogger.LogWarning("[传奇翻页] 保存传奇界面加翻页失败: " + e.Message);
		}
	}

	// 点「活侠传」按钮走的是 OpenLegendPanel；OnLegendPanelOpen 只在面板事件里调用，有时不会触发
	[HarmonyPostfix]
	[HarmonyPatch(typeof(LibraryPanel), nameof(LibraryPanel.OpenLegendPanel))]
	public static void OpenLegendPanel_Postfix(LibraryPanel __instance)
	{
		try
		{
			var tr = Traverse.Create(__instance);
			var panel = tr.Field("_legendPanel").GetValue<CommonPanel>();
			var grid = tr.Field("_legendPanelTransform").GetValue<Transform>();
			var detail = tr.Field("_legendDetailObj").GetValue<GameObject>();
			if (panel == null || grid == null)
			{
				return;
			}
			LoadLegendSlotPanel[] slots = grid.GetComponentsInChildren<LoadLegendSlotPanel>(includeInactive: true);
			if (slots.Length == 0)
			{
				return;
			}
			_page = 0;
			// 挂在书架那一排格子（grid）上、放在它正下方：面板打开时有动画，按格子当时的位置算会算偏
			Attach(grid, slots, () =>
			{
				if (detail != null)
				{
					detail.SetActive(false);
				}
			}, belowHost: true);
			FileLogger.Log($"[传奇翻页] 图鉴传奇：{slots.Length} 格，{Pages} 页");
		}
		catch (Exception e)
		{
			FileLogger.LogWarning("[传奇翻页] 图鉴传奇界面加翻页失败: " + e.Message);
		}
	}

	// 原版存完传奇后按「栏位号 - 1」去数组里找格子刷新，第 2 页以后会越界，改成按当前栏位号找
	[HarmonyPrefix]
	[HarmonyPatch(typeof(EndGamePanel), "UpdateSaveLegendSlotPanel")]
	public static bool UpdateSaveLegendSlotPanel_Prefix(EndGamePanel __instance, int slot)
	{
		SaveLegendSlotPanel[] slots = Traverse.Create(__instance).Field("_saveSlots").GetValue<SaveLegendSlotPanel[]>();
		if (slots == null)
		{
			return true;
		}
		foreach (SaveLegendSlotPanel s in slots)
		{
			if (s != null && Traverse.Create(s).Field("_slot").GetValue<int>() == slot)
			{
				s.Setup();
				return false;
			}
		}
		return true;
	}

	private static int FirstPageWithEmptySlot()
	{
		for (int page = 0; page < Pages; page++)
		{
			for (int i = 1; i <= PageSize; i++)
			{
				if (SaveSystem.Instance.GetLegendSaveData(page * PageSize + i) == null)
				{
					return page;
				}
			}
		}
		return 0;
	}

	private static void Attach(Transform host, MonoBehaviour[] slots, Action clearDetail, bool belowHost = false)
	{
		foreach (MonoBehaviour dead in new List<MonoBehaviour>(BaseSlot.Keys))
		{
			if (dead == null)
			{
				BaseSlot.Remove(dead);
			}
		}
		foreach (MonoBehaviour s in slots)
		{
			if (!BaseSlot.ContainsKey(s))
			{
				BaseSlot[s] = Traverse.Create(s).Field("_slot").GetValue<int>();
			}
		}

		Transform existing = host.Find(PagerName);
		if (Pages <= 1)
		{
			_page = 0;
			if (existing != null)
			{
				existing.gameObject.SetActive(false);
			}
			Apply(slots, clearDetail, null);
			return;
		}

		Text label;
		if (existing != null)
		{
			existing.gameObject.SetActive(true);
			label = existing.Find("Label").GetComponent<Text>();
		}
		else
		{
			label = Create(host, slots, clearDetail);
		}
		var pager = (RectTransform)host.Find(PagerName);
		if (belowHost)
		{
			pager.anchorMin = pager.anchorMax = new Vector2(0.5f, 0f);
			pager.pivot = new Vector2(0.5f, 1f);
			// 书架下面压着一块木台（画在格子后面），翻页放到木台下面才不被挡住
			pager.anchoredPosition = new Vector2(0f, -118f);
			pager.SetAsLastSibling();
		}
		else
		{
			Place(pager, slots);
		}
		Apply(slots, clearDetail, label);
	}

	private static void Apply(MonoBehaviour[] slots, Action clearDetail, Text label)
	{
		foreach (MonoBehaviour s in slots)
		{
			if (s == null || !BaseSlot.TryGetValue(s, out int baseSlot))
			{
				continue;
			}
			Traverse.Create(s).Field("_slot").SetValue(_page * PageSize + baseSlot);
			Traverse.Create(s).Method("Setup").GetValue();
		}
		clearDetail();
		if (label != null)
		{
			label.text = (_page + 1) + " / " + Pages;
		}
	}

	private static Text Create(Transform host, MonoBehaviour[] slots, Action clearDetail)
	{
		var go = new GameObject(PagerName, typeof(RectTransform), typeof(LayoutElement));
		go.transform.SetParent(host, false);
		go.GetComponent<LayoutElement>().ignoreLayout = true;
		var rt = (RectTransform)go.transform;
		rt.sizeDelta = new Vector2(360f, 48f);

		Font font = null;
		foreach (Text t in host.GetComponentsInChildren<Text>(includeInactive: true))
		{
			if (t.font != null)
			{
				font = t.font;
				break;
			}
		}
		font ??= Font.CreateDynamicFontFromOSFont("Microsoft YaHei", 26);

		Text label = AddText(go.transform, "Label", font, new Vector2(0.25f, 0f), new Vector2(0.75f, 1f));
		Button prev = AddButton(go.transform, "Prev", "<", font, new Vector2(0f, 0f), new Vector2(0.22f, 1f));
		Button next = AddButton(go.transform, "Next", ">", font, new Vector2(0.78f, 0f), new Vector2(1f, 1f));
		prev.onClick.AddListener(() =>
		{
			_page = (_page + Pages - 1) % Pages;
			Apply(slots, clearDetail, label);
		});
		next.onClick.AddListener(() =>
		{
			_page = (_page + 1) % Pages;
			Apply(slots, clearDetail, label);
		});
		return label;
	}

	// 放在 12 格的正下方、水平居中（按格子实际位置算，不依赖面板布局）
	private static void Place(RectTransform pager, MonoBehaviour[] slots)
	{
		var parent = (RectTransform)pager.parent;
		float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue;
		var corners = new Vector3[4];
		foreach (MonoBehaviour s in slots)
		{
			if (s == null)
			{
				continue;
			}
			((RectTransform)s.transform).GetWorldCorners(corners);
			foreach (Vector3 c in corners)
			{
				Vector3 p = parent.InverseTransformPoint(c);
				minX = Mathf.Min(minX, p.x);
				maxX = Mathf.Max(maxX, p.x);
				minY = Mathf.Min(minY, p.y);
			}
		}
		pager.anchorMin = pager.anchorMax = new Vector2(0.5f, 0.5f);
		pager.pivot = new Vector2(0.5f, 1f);
		Vector2 center = parent.rect.center;
		pager.anchoredPosition = new Vector2((minX + maxX) / 2f - center.x, minY - 8f - center.y);
		pager.SetAsLastSibling();
	}

	private static Text AddText(Transform parent, string name, Font font, Vector2 anchorMin, Vector2 anchorMax)
	{
		var go = new GameObject(name, typeof(RectTransform), typeof(Text));
		go.transform.SetParent(parent, false);
		var rt = (RectTransform)go.transform;
		rt.anchorMin = anchorMin;
		rt.anchorMax = anchorMax;
		rt.offsetMin = rt.offsetMax = Vector2.zero;
		var text = go.GetComponent<Text>();
		text.font = font;
		text.fontSize = 26;
		text.alignment = TextAnchor.MiddleCenter;
		text.color = new Color(0.95f, 0.91f, 0.84f, 1f);
		text.raycastTarget = false;
		return text;
	}

	private static Button AddButton(Transform parent, string name, string label, Font font, Vector2 anchorMin, Vector2 anchorMax)
	{
		var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
		go.transform.SetParent(parent, false);
		var rt = (RectTransform)go.transform;
		rt.anchorMin = anchorMin;
		rt.anchorMax = anchorMax;
		rt.offsetMin = rt.offsetMax = Vector2.zero;
		go.GetComponent<Image>().color = new Color(0.08f, 0.07f, 0.06f, 0.75f);
		var button = go.GetComponent<Button>();
		ColorBlock colors = button.colors;
		colors.highlightedColor = colors.selectedColor = new Color(1.4f, 1.3f, 1.2f, 1f);
		button.colors = colors;
		AddText(go.transform, "Text", font, Vector2.zero, Vector2.one).text = label;
		return button;
	}
}
