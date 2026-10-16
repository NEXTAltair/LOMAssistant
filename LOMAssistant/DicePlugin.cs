using System;
using System.Collections.Generic;
using System.Globalization;
using LOMAssistant.Config;
using Mortal.Core;
using Mortal.Story;
using UnityEngine;

namespace LOMAssistant;

public class DicePlugin : MonoBehaviour
{
	private List<(string, string)> optionEntries = new List<(string, string)>();

	private List<(string, string)> switchEntries = new List<(string, string)>();

	private List<(PositionEventData, string, string)> eventEntries = new List<(PositionEventData, string, string)>();

	private List<(MissionCheckData, string, string)> missionEntries = new List<(MissionCheckData, string, string)>();

	private Vector2 logScrollPosition;

	private Rect logWindowRect = new Rect(20f, 20f, 750f, 750f);

	private bool showLogWindow = true;

	private float baseFontSize = 14f;

	private float scaleFactor = 1f;

	private GUIStyle headerStyle;

	private GUIStyle logEntryStyle;

	private GUIStyle descriptionStyle;

	private GUIStyle buttonStyle;

	private GUIStyle darkBackgroundStyle;

	private GUIStyle lightBackgroundStyle;

	private bool isInitialized;

	private string currentTab = "选项";

	private string[] tabs = new string[4] { "选项", "开关", "事件", "任务" };

	private int lastScreenWidth;

	private int lastScreenHeight;

	private float lastScaleFactor;

	public static DicePlugin Instance { get; set; }

	private void Awake()
	{
		Instance = this;
		float num = Screen.width;
		float num2 = Screen.height;
		scaleFactor = Mathf.Min(num / 1920f, num2 / 1080f);
		logWindowRect = new Rect(20f * scaleFactor, 20f * scaleFactor, 750f * scaleFactor, 750f * scaleFactor);
	}

	private void SetGUI()
	{
		GUI.skin.label.fontSize = (int)(baseFontSize * scaleFactor);
		GUI.skin.window.fontSize = (int)(baseFontSize * scaleFactor);
		headerStyle = new GUIStyle(GUI.skin.label)
		{
			fontSize = (int)(baseFontSize * scaleFactor),
			fontStyle = FontStyle.Bold,
			alignment = TextAnchor.MiddleCenter,
			richText = true
		};
		logEntryStyle = new GUIStyle(GUI.skin.label)
		{
			fontSize = (int)(16f * scaleFactor),
			richText = true
		};
		descriptionStyle = new GUIStyle(GUI.skin.label)
		{
			fontSize = (int)(baseFontSize * scaleFactor),
			richText = true,
			wordWrap = true
		};
		buttonStyle = new GUIStyle(GUI.skin.button)
		{
			fontSize = (int)(baseFontSize * scaleFactor),
			fixedWidth = logWindowRect.width * 0.2f
		};
		darkBackgroundStyle = new GUIStyle(GUI.skin.box)
		{
			normal = 
			{
				background = MakeTex(4, 4, new Color(0.1f, 0.1f, 0.1f, 0.6f))
			}
		};
		lightBackgroundStyle = new GUIStyle(GUI.skin.box)
		{
			normal = 
			{
				background = MakeTex(4, 4, new Color(0.2f, 0.2f, 0.2f, 0.5f))
			}
		};
	}

	private void Update()
	{
		if (Input.GetKeyDown(ConfigManager.LogMenuToggleKey.Value))
		{
			showLogWindow = !showLogWindow;
		}
	}

	private void OnDestroy()
	{
	}

	private void OnGUI()
	{
		UpdateScale();
		if (!isInitialized)
		{
			isInitialized = true;
		}
		if (showLogWindow)
		{
			logWindowRect = GUILayout.Window(1, logWindowRect, DrawLogWindow, "活侠传拓展");
		}
	}

	public void AddOptionEntry(string key, string description = "")
	{
		key = RichText.Golden(TextConverter.ToSimplified(key));
		optionEntries.Add((key, TextConverter.ToSimplified(description)));
	}

	public void AddSwitchEntry(string key, string description)
	{
		key = RichText.Golden(TextConverter.ToSimplified(key));
		switchEntries.Add((key, TextConverter.ToSimplified(description)));
	}

	public void AddEventEntry(PositionEventData eventData, string key, string description)
	{
		eventEntries.Add((eventData, TextConverter.ToSimplified(key), TextConverter.ToSimplified(description)));
	}

	public void AddMissionEntry(MissionCheckData missionData, string key, string description)
	{
		missionEntries.Add((missionData, TextConverter.ToSimplified(key), TextConverter.ToSimplified(description)));
	}

	private void DrawLogWindow(int windowID)
	{
		GUILayout.Space(20f * scaleFactor);
		GUILayout.BeginHorizontal();
		string[] array = tabs;
		foreach (string text in array)
		{
			if (GUILayout.Toggle(currentTab == text, text, buttonStyle))
			{
				currentTab = text;
			}
		}
		GUILayout.EndHorizontal();
		GUILayout.Space(5f * scaleFactor);
		logScrollPosition = GUILayout.BeginScrollView(logScrollPosition);
		switch (currentTab)
		{
		case "任务":
			DrawMissionEntries(missionEntries);
			break;
		case "事件":
			DrawEventEntries(eventEntries);
			break;
		case "开关":
			DrawEntries(switchEntries);
			break;
		case "选项":
			DrawEntries(optionEntries);
			break;
		}
		GUILayout.EndScrollView();
		GUI.DragWindow();
	}

	private void DrawEntries(IEnumerable<(string, string)> entries)
	{
		bool flag = true;
		foreach (var entry in entries)
		{
			string item = entry.Item1;
			string item2 = entry.Item2;
			GUIStyle style = (flag ? darkBackgroundStyle : lightBackgroundStyle);
			flag = !flag;
			GUILayout.BeginVertical(style);
			GUILayout.Label(item, logEntryStyle);
			GUILayout.Label(item2, descriptionStyle);
			GUILayout.EndVertical();
		}
	}

	private void DrawEventEntries(IEnumerable<(PositionEventData, string, string)> entries)
	{
		bool flag = true;
		foreach (var entry in entries)
		{
			PositionEventData item = entry.Item1;
			string item2 = entry.Item2;
			string item3 = entry.Item3;
			GUIStyle style = (flag ? darkBackgroundStyle : lightBackgroundStyle);
			flag = !flag;
			GUILayout.BeginVertical(style);
			GUILayout.Label(item2, logEntryStyle);
			GUILayout.Label(item3, descriptionStyle);
			if (GUILayout.Button("设置事件", buttonStyle))
			{
				OptionInfoPatch.TargetEvent = item;
			}
			GUILayout.EndVertical();
		}
	}

	private void DrawMissionEntries(IEnumerable<(MissionCheckData, string, string)> entries)
	{
		bool flag = true;
		foreach (var entry in entries)
		{
			MissionCheckData item = entry.Item1;
			string item2 = entry.Item2;
			string item3 = entry.Item3;
			GUIStyle style = (flag ? darkBackgroundStyle : lightBackgroundStyle);
			flag = !flag;
			GUILayout.BeginVertical(style);
			GUILayout.Label(item2, logEntryStyle);
			GUILayout.Label(item3, descriptionStyle);
			if (GUILayout.Button("设置事件", buttonStyle))
			{
				OptionInfoPatch.TargetMission = item;
				MissionManagerData.Instance.ActiveSubMissions.Add(item.Key, item);
			}
			GUILayout.EndVertical();
		}
	}

	private Texture2D MakeTex(int width, int height, Color col)
	{
		Color[] array = new Color[width * height];
		for (int i = 0; i < array.Length; i++)
		{
			array[i] = col;
		}
		Texture2D texture2D = new Texture2D(width, height);
		texture2D.SetPixels(array);
		texture2D.Apply();
		texture2D.hideFlags = HideFlags.HideAndDontSave;
		return texture2D;
	}

	public void ClearLogEntries()
	{
		optionEntries.Clear();
		switchEntries.Clear();
		missionEntries.Clear();
	}

	public void ClearEventEntries()
	{
		eventEntries.Clear();
	}

	public static void LogInfo(object o)
	{
		LOMAssistantPlugin.LogInfo("[" + DateTime.Now.ToString(DateTimeFormatInfo.InvariantInfo) + "] " + o);
		if (ConfigManager.LogToFile.Value)
		{
			FileLogger.Log(o);
		}
	}

	public static void LogError(object o)
	{
		LOMAssistantPlugin.LogError("[" + DateTime.Now.ToString(DateTimeFormatInfo.InvariantInfo) + "] " + o);
		if (ConfigManager.LogToFile.Value)
		{
			FileLogger.LogError(o);
		}
	}

	private void UpdateScale()
	{
		if (!showLogWindow)
		{
			return;
		}
		int width = Screen.width;
		int height = Screen.height;
		if (lastScreenWidth != width || lastScreenHeight != height)
		{
			lastScreenWidth = width;
			lastScreenHeight = height;
			scaleFactor = Mathf.Min((float)width / 1920f, (float)height / 1080f);
			logWindowRect = new Rect(20f * scaleFactor, 20f * scaleFactor, 750f * scaleFactor, 750f * scaleFactor);
			if (scaleFactor != lastScaleFactor)
			{
				lastScaleFactor = scaleFactor;
				SetGUI();
			}
		}
	}
}
