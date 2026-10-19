using HarmonyLib;
using Mortal.Core;
using Mortal.Free;
using Mortal.Story;
using UnityEngine;
using UnityEngine.UI;

namespace LOMAssistant.Features.Debug;

public static class DebugPanelFeature
{
	public static void ToggleTestPanel()
	{
		CommonPanel value;
		if (FreeManager.Instance != null)
		{
			value = Traverse.Create(FreeManager.Instance).Field("_statPanel").GetValue<CommonPanel>();
		}
		else
		{
			if (!(StoryManager.Instance != null))
			{
				return;
			}
			value = Traverse.Create(StoryManager.Instance).Field("_statusPanel").GetValue<CommonPanel>();
		}
		if (value == null)
		{
			return;
		}
		PlayerStatusPanel component = value.GetComponent<PlayerStatusPanel>();
		if (component == null)
		{
			return;
		}
		GameObject value2 = Traverse.Create(component).Field("_testPanel").GetValue<GameObject>();
		if (value2 == null)
		{
			return;
		}
		FixDevelopment(value2);
		foreach (Transform item in value2.transform)
		{
			FixDevelopment(item.gameObject);
			if (!item.gameObject.activeSelf)
			{
				item.gameObject.SetActive(value: true);
			}
		}
		value2.SetActive(!value2.activeSelf);
		if (value2.activeSelf)
		{
			FlagBrowser.Attach(value2);
			AddExtraRows(value2);
		}
		StoryManager instance = StoryManager.Instance;
		if (instance != null && !Traverse.Create(instance).Field("_statusOpen").GetValue<bool>())
		{
			instance.PlayerStatus(enabled: true);
		}
		FreeManager instance2 = FreeManager.Instance;
		if (instance2 != null)
		{
			instance2.PlayerStatus(enabled: true);
		}
	}

	private const string RowPrefix = "LOMAssistant_";

	/// <summary>
	/// 原版测试面板的属性列缺几项：复制现成的属性行改绑。
	/// 武学（锻造行，±5，放锻造上面）；变心、战术天赋等级（门派规模行，±1）放在好感度列最下面：角色数值那一列已经满了，再加会把门派数值挤下去。
	/// </summary>
	private static void AddExtraRows(GameObject testPanel)
	{
		try
		{
			TestGameStatPanel[] rows = testPanel.GetComponentsInChildren<TestGameStatPanel>(includeInactive: true);
			if (rows.Length == 0 || PlayerStatManagerData.Instance == null)
			{
				return;
			}
			foreach (TestGameStatPanel row in rows)
			{
				if (row.name.StartsWith(RowPrefix))
				{
					return;
				}
			}
			var stats = PlayerStatManagerData.Instance.Stats;
			TestGameStatPanel forge = FindRow(rows, stats.Get(GameStatType.鍛造));
			TestGameStatPanel scale = FindRow(rows, stats.Get(GameStatType.門派規模));
			TestRelationshipStatPanel relationship = testPanel.GetComponentInChildren<TestRelationshipStatPanel>(includeInactive: true);
			if (forge != null)
			{
				CloneStatRow(forge, stats.Get(GameStatType.武學點數), "MartialPoint", forge.transform.GetSiblingIndex());
			}
			if (scale != null && relationship != null)
			{
				Transform column = relationship.transform.parent;
				GameObject heart = CloneStatRow(scale, stats.Get(GameStatType.變心), "ChangeHeart", column.childCount, column);
				HideAdditionColumn(heart);
				MatchLayout(heart, relationship.gameObject);
				PlayerTalentData tactics = PlayerStatManagerData.Instance.Talents.Get("9100");
				if (tactics != null)
				{
					MatchLayout(CloneTalentRow(scale, tactics, "战术", column.childCount, column), relationship.gameObject);
				}
			}
			FileLogger.Log("F9 面板：已加入武学、变心、战术");
		}
		catch (System.Exception e)
		{
			FileLogger.LogWarning("F9 面板：加属性行失败: " + e.Message);
		}
	}

	private static TestGameStatPanel FindRow(TestGameStatPanel[] rows, GameStat stat)
	{
		foreach (TestGameStatPanel row in rows)
		{
			if (Traverse.Create(row).Field("_stat").GetValue<GameStat>() == stat)
			{
				return row;
			}
		}
		return null;
	}

	// 先让模板失活再复制：复制品的 Awake 要等改好绑定、重新激活时才跑（Awake 里会按属性订阅变化事件）
	private static GameObject CloneInactive(TestGameStatPanel template, string name, int sibling, Transform parent)
	{
		GameObject source = template.gameObject;
		bool wasActive = source.activeSelf;
		source.SetActive(false);
		GameObject copy = Object.Instantiate(source, parent ?? source.transform.parent, worldPositionStays: false);
		source.SetActive(wasActive);
		copy.name = RowPrefix + name;
		copy.transform.SetSiblingIndex(sibling);
		return copy;
	}

	private static GameObject CloneStatRow(TestGameStatPanel template, GameStat stat, string name, int sibling, Transform parent = null)
	{
		if (stat == null)
		{
			return null;
		}
		GameObject copy = CloneInactive(template, name, sibling, parent);
		Traverse.Create(copy.GetComponent<TestGameStatPanel>()).Field("_stat").SetValue(stat);
		copy.SetActive(true);
		return copy;
	}

	/// <summary>天赋不是属性，没法直接绑：拿掉属性组件，按钮改成调天赋等级，数值写「等级/上限」。</summary>
	private static GameObject CloneTalentRow(TestGameStatPanel template, PlayerTalentData talent, string label, int sibling, Transform parent)
	{
		GameObject copy = CloneInactive(template, "Talent_" + talent.Id, sibling, parent);
		var panel = Traverse.Create(copy.GetComponent<TestGameStatPanel>());
		Text nameText = panel.Field("_nameText").GetValue<Text>();
		Text valueText = panel.Field("_valueText").GetValue<Text>();
		Text addText = panel.Field("_addText").GetValue<Text>();
		Object.DestroyImmediate(copy.GetComponent<TestGameStatPanel>());
		int max = Traverse.Create(talent).Field("_maxLevel").GetValue<int>();
		System.Action refresh = () =>
		{
			if (valueText != null) valueText.text = talent.Level + "/" + max;
		};
		if (nameText != null)
		{
			nameText.text = label;
		}
		foreach (Button button in copy.GetComponentsInChildren<Button>(includeInactive: true))
		{
			Text text = button.GetComponentInChildren<Text>(includeInactive: true);
			if (text == null || !int.TryParse(text.text.Replace("+", ""), out int delta))
			{
				continue;
			}
			int step = System.Math.Sign(delta);
			button.onClick = new Button.ButtonClickedEvent();
			button.onClick.AddListener(() => { talent.AddLevel(step); refresh(); });
			text.text = step > 0 ? "+1" : "-1";
		}
		if (addText != null) addText.gameObject.SetActive(false);
		copy.SetActive(true);
		refresh();
		return copy;
	}

	/// <summary>
	/// 属性行和好感度行内部宽度不一样（属性行给加成那一列留了位置），放进好感度列会错位。
	/// 把好感度行的排版照搬过来：行本身的大小和间距，以及名字、数值、两个按钮按顺序一一对应的大小和位置。
	/// </summary>
	private static void MatchLayout(GameObject row, GameObject reference)
	{
		if (row == null || reference == null)
		{
			return;
		}
		try
		{
			var rowRect = (RectTransform)row.transform;
			var refRect = (RectTransform)reference.transform;
			rowRect.sizeDelta = refRect.sizeDelta;
			var refGroup = reference.GetComponent<HorizontalLayoutGroup>();
			var rowGroup = row.GetComponent<HorizontalLayoutGroup>();
			if (refGroup != null && rowGroup != null)
			{
				rowGroup.padding = refGroup.padding;
				rowGroup.spacing = refGroup.spacing;
				rowGroup.childAlignment = refGroup.childAlignment;
			}
			var mine = new System.Collections.Generic.List<RectTransform>();
			var theirs = new System.Collections.Generic.List<RectTransform>();
			foreach (Transform child in row.transform) if (child.gameObject.activeSelf) mine.Add((RectTransform)child);
			foreach (Transform child in reference.transform) if (child.gameObject.activeSelf) theirs.Add((RectTransform)child);
			if (mine.Count != theirs.Count)
			{
				FileLogger.LogWarning($"F9 面板：{row.name} 和好感度行的元素数不同（{mine.Count} / {theirs.Count}），不对齐");
				return;
			}
			for (int i = 0; i < mine.Count; i++)
			{
				mine[i].anchorMin = theirs[i].anchorMin;
				mine[i].anchorMax = theirs[i].anchorMax;
				mine[i].pivot = theirs[i].pivot;
				mine[i].sizeDelta = theirs[i].sizeDelta;
				mine[i].anchoredPosition = theirs[i].anchoredPosition;
				var refElement = theirs[i].GetComponent<LayoutElement>();
				var element = mine[i].GetComponent<LayoutElement>();
				if (refElement != null && element != null)
				{
					element.minWidth = refElement.minWidth;
					element.preferredWidth = refElement.preferredWidth;
					element.flexibleWidth = refElement.flexibleWidth;
				}
				var refText = theirs[i].GetComponent<Text>();
				var text = mine[i].GetComponent<Text>();
				if (refText != null && text != null)
				{
					text.alignment = refText.alignment;
				}
			}
		}
		catch (System.Exception e)
		{
			FileLogger.LogWarning("F9 面板：对齐失败: " + e.Message);
		}
	}

	/// <summary>属性行的「(+N)」是装备、天赋带来的加成，变心这类没有加成的属性一直是 +0，藏起来。</summary>
	private static void HideAdditionColumn(GameObject row)
	{
		Text addText = row == null ? null : Traverse.Create(row.GetComponent<TestGameStatPanel>()).Field("_addText").GetValue<Text>();
		if (addText != null) addText.gameObject.SetActive(false);
	}

	private static void FixDevelopment(GameObject gameObject)
	{
		DevelopmentOnly component = gameObject.GetComponent<DevelopmentOnly>();
		if (component != null)
		{
			Object.Destroy(component);
		}
	}
}
