using HarmonyLib;
using Mortal.Core;
using Mortal.Free;
using Mortal.Story;
using UnityEngine;

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
			AddMartialPointRow(value2);
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

	private const string MartialRowName = "LOMAssistant_MartialPoint";

	/// <summary>原版测试面板的属性列没有武学点：复制一行现成的属性行，改绑到武学点。</summary>
	private static void AddMartialPointRow(GameObject testPanel)
	{
		try
		{
			TestGameStatPanel[] rows = testPanel.GetComponentsInChildren<TestGameStatPanel>(includeInactive: true);
			if (rows.Length == 0)
			{
				return;
			}
			foreach (TestGameStatPanel row in rows)
			{
				if (row.name == MartialRowName)
				{
					return;
				}
			}
			GameStat stat = PlayerStatManagerData.Instance?.Stats.Get(GameStatType.武學點數);
			if (stat == null)
			{
				return;
			}
			// 复制「锻造」那一行（±5 按钮），放在它上面
			GameStat forge = PlayerStatManagerData.Instance.Stats.Get(GameStatType.鍛造);
			TestGameStatPanel forgeRow = null;
			foreach (TestGameStatPanel row in rows)
			{
				if (Traverse.Create(row).Field("_stat").GetValue<GameStat>() == forge)
				{
					forgeRow = row;
					break;
				}
			}
			if (forgeRow == null)
			{
				FileLogger.LogWarning("F9 面板：找不到锻造那一行，不加武学点。");
				return;
			}
			GameObject template = forgeRow.gameObject;
			// 先让模板失活再复制，复制品的 Awake 要等换好属性、重新激活时才跑（Awake 里会按属性订阅变化事件）
			bool wasActive = template.activeSelf;
			template.SetActive(false);
			GameObject copy = Object.Instantiate(template, template.transform.parent, worldPositionStays: false);
			template.SetActive(wasActive);
			copy.name = MartialRowName;
			copy.transform.SetSiblingIndex(template.transform.GetSiblingIndex());
			Traverse.Create(copy.GetComponent<TestGameStatPanel>()).Field("_stat").SetValue(stat);
			copy.SetActive(true);
			FileLogger.Log("F9 面板：已加入武学点一行 " + copy.transform.parent.name);
		}
		catch (System.Exception e)
		{
			FileLogger.LogWarning("F9 面板：加武学点失败: " + e.Message);
		}
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
