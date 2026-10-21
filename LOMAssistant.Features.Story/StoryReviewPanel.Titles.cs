using System.Collections.Generic;
using LOMAssistant.Config;
using LOMAssistant.Features.Extras;
using Mortal.Core;
using UnityEngine;

namespace LOMAssistant.Features.Story;

/// <summary>数值页的「称号」：从游戏的全部称号里选一个，或者写一个自己的（自定义的优先显示）。</summary>
public partial class StoryReviewPanel
{
	private List<string> _titles;
	private string _customTitleInput;

	/// <summary>游戏里的全部称号（按编号），第一次用时取一遍。</summary>
	private List<string> GameTitles()
	{
		if (_titles != null)
		{
			return _titles;
		}
		_titles = new List<string>();
		for (int i = 0; i < 200; i++)
		{
			string text = TitlePatch.Original(i);
			if (text == null)
			{
				break;
			}
			_titles.Add(text);
		}
		return _titles;
	}

	private void DrawTitles(PlayerStatManagerData player)
	{
		GameStat stat = null;
		try
		{
			stat = player.Stats.Get(GameStatType.稱號);
		}
		catch
		{
		}
		List<string> titles = GameTitles();
		string custom = (ConfigManager.CustomTitle.Value ?? "").Trim();
		int current = stat != null ? stat.Value : -1;
		string currentName = current >= 0 && current < titles.Count ? titles[current] : current.ToString();

		GUILayout.Space(S(8));
		GUILayout.Label("称号", _heading);
		GUILayout.Label(custom.Length > 0
			? $"现在显示：<b>{custom}</b>（自定义）　<color=#73644F>游戏里的称号是「{currentName}」，清除自定义后恢复显示它</color>"
			: $"现在显示：<b>{currentName}</b>", _rich);

		// 自定义称号：所有存档都显示这个
		GUILayout.Space(S(6));
		GUILayout.BeginHorizontal();
		GUILayout.Label("自定义称号", Middle(_label), GUILayout.Width(S(130)), Ctl());
		_customTitleInput ??= custom;
		_customTitleInput = GUILayout.TextField(_customTitleInput, 16, _field, GUILayout.Width(S(320)), Ctl());
		bool changed = _customTitleInput.Trim() != custom;
		GUI.enabled = changed;
		if (GUILayout.Button("设定", changed ? _primary : _button, GUILayout.Width(S(80)), Ctl()))
		{
			ConfigManager.CustomTitle.Value = _customTitleInput.Trim();
			GUIUtility.keyboardControl = 0;
		}
		GUI.enabled = custom.Length > 0;
		if (GUILayout.Button("清除", _button, GUILayout.Width(S(80)), Ctl()))
		{
			ConfigManager.CustomTitle.Value = "";
			_customTitleInput = "";
			GUIUtility.keyboardControl = 0;
		}
		GUI.enabled = true;
		GUILayout.Label("对所有存档生效，下一句对话起显示", Middle(_muted), Ctl());
		GUILayout.FlexibleSpace();
		GUILayout.EndHorizontal();

		// 游戏里的称号：点哪个就换成哪个（改的是这个存档的称号）
		GUILayout.Space(S(10));
		GUILayout.Label("从游戏的称号里选（改的是这个存档的称号）", _muted);
		if (stat == null || titles.Count == 0)
		{
			GUILayout.Label("读档进入游戏后才能选", _muted);
			return;
		}
		const int perRow = 6;
		for (int i = 0; i < titles.Count; i += perRow)
		{
			GUILayout.BeginHorizontal();
			for (int k = i; k < i + perRow && k < titles.Count; k++)
			{
				bool on = k == current;
				if (GUILayout.Button(titles[k], on ? _primary : _button, GUILayout.Width(S(260)), Ctl()) && !on)
				{
					stat.SetValue(k);
				}
			}
			GUILayout.FlexibleSpace();
			GUILayout.EndHorizontal();
		}
	}
}
