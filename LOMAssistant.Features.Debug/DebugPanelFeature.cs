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
				CloneStatRow(scale, stats.Get(GameStatType.變心), "ChangeHeart", column.childCount, column);
				PlayerTalentData tactics = PlayerStatManagerData.Instance.Talents.Get("9100");
				if (tactics != null)
				{
					CloneTalentRow(scale, tactics, "战术", column.childCount, column);
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
	private static void CloneTalentRow(TestGameStatPanel template, PlayerTalentData talent, string label, int sibling, Transform parent)
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
			if (valueText != null) valueText.text = talent.Level.ToString();
			if (addText != null) addText.text = "(/" + max + ")";
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
		copy.SetActive(true);
		refresh();
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
