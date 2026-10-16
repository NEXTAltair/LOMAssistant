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

	private static void FixDevelopment(GameObject gameObject)
	{
		DevelopmentOnly component = gameObject.GetComponent<DevelopmentOnly>();
		if (component != null)
		{
			Object.Destroy(component);
		}
	}
}
