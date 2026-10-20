using System;
using System.Collections.Generic;
using System.Linq;
using LOMAssistant.LuaParse;
using Mortal.Core;
using Mortal.Story;
using UnityEngine;

namespace LOMAssistant.Features.Story;

/// <summary>
/// 「此刻」页：只讲当下。这一段剧情的选项（完整结果）和分支判定、当前地点的随机事件（概率、条件、结果，
/// 满足条件的可以选定下次触发）、现在能触发和差一点的任务。打开面板时整理一次，换了剧本再整理。
/// </summary>
public partial class StoryReviewPanel
{
	private sealed class NowEvent
	{
		public PositionEventData Data;
		public string Name = "", Desc = "";
		public int Rate, Percent;
	}

	private sealed class NowMission
	{
		public string Title = "", Desc = "", Place = "";
		public bool Ready, Main;
	}

	private sealed class NowSnapshot
	{
		public string Script = "";
		public readonly List<(string Option, string Result)> Options = new List<(string, string)>();
		/// <summary>这一段剧情的去处：走过哪些选项、掷骰、条件，接到哪里（和剧情图里「接下来」一样）。</summary>
		public readonly List<(string Way, string Effects)> Ways = new List<(string, string)>();
		public readonly List<NowEvent> Events = new List<NowEvent>();
		public readonly List<NowMission> Missions = new List<NowMission>();
	}

	private NowSnapshot _now;
	private Vector2 _nowScroll;

	private void DrawNow(Rect r)
	{
		if (_now == null || _now.Script != (OptionInfoPatch.LastScriptKey ?? ""))
		{
			_now = BuildNow();
		}
		if (GUI.Button(new Rect(r.xMax - S(130), r.y, S(130), S(36)), "刷新", _button))
		{
			_now = BuildNow();
		}
		var area = new Rect(r.x, r.y + S(46), r.width, r.height - S(46));
		DragScroll("now", area, ref _nowScroll);
		GUILayout.BeginArea(area);
		_nowScroll = GUILayout.BeginScrollView(_nowScroll);
		bool any = false;
		if (_now.Options.Count > 0)
		{
			any = true;
			GUILayout.Label("这一段的选项", _heading);
			foreach (var (option, result) in _now.Options)
			{
				GUILayout.BeginVertical(_card);
				GUILayout.Label(option, _label);
				if (result.Length > 0)
				{
					GUILayout.Label(result, _muted);
				}
				GUILayout.EndVertical();
			}
			GUILayout.Space(S(10));
		}
		if (_now.Ways.Count > 0)
		{
			any = true;
			GUILayout.Label("这一段接下来会怎么走", _heading);
			foreach (var (way, effects) in _now.Ways)
			{
				GUILayout.BeginVertical(_card);
				GUILayout.Label(way, _rich);
				if (effects.Length > 0)
				{
					GUILayout.Label(effects, _muted);
				}
				GUILayout.EndVertical();
			}
			GUILayout.Space(S(10));
		}
		if (_now.Events.Count > 0)
		{
			any = true;
			GUILayout.Label("这里的随机事件", _heading);
			foreach (NowEvent ev in _now.Events)
			{
				GUILayout.BeginVertical(_card);
				GUILayout.BeginHorizontal();
				GUILayout.Label(ev.Name + "　" + (ev.Rate > 0 ? $"<color=#2F6B3A>{ev.Percent}%</color>" : "<color=#A3302A>条件不满足</color>"), _rich);
				GUILayout.FlexibleSpace();
				if (ev.Rate > 0)
				{
					bool chosen = EventPicker.Pending == ev.Data;
					if (GUILayout.Button(chosen ? "已选定" : "下次触发这个", chosen ? _primary : _button, GUILayout.Width(S(150))))
					{
						EventPicker.Pending = chosen ? null : ev.Data;
					}
				}
				GUILayout.EndHorizontal();
				if (ev.Desc.Length > 0)
				{
					GUILayout.Label(ev.Desc, _rich);
				}
				GUILayout.EndVertical();
			}
			GUILayout.Space(S(10));
		}
		if (_now.Missions.Count > 0)
		{
			any = true;
			GUILayout.Label("现在的任务", _heading);
			foreach (NowMission m in _now.Missions)
			{
				GUILayout.BeginVertical(_card);
				GUILayout.Label((m.Main ? "<color=#A3302A><b>主线</b></color>　" : "") + m.Title + "　" + (m.Ready ? "<color=#2F6B3A>条件已满足" + (m.Place.Length > 0 ? "，去「" + m.Place + "」就会触发" : "") + "</color>" : "<color=#73644F>还差条件</color>"), _rich);
				if (m.Desc.Length > 0)
				{
					GUILayout.Label(m.Desc, _rich);
				}
				GUILayout.EndVertical();
			}
		}
		if (!any)
		{
			GUILayout.Label("进游戏后才有内容", _muted);
		}
		GUILayout.EndScrollView();
		GUILayout.EndArea();
	}

	private static NowSnapshot BuildNow()
	{
		var now = new NowSnapshot { Script = OptionInfoPatch.LastScriptKey ?? "" };
		try
		{
			if (now.Script.Length > 0 && StoryManager.Instance != null)
			{
				foreach (KeyValuePair<string, List<LuaParse.Action>> option in LuaParser.Instance.ExtractOptionActionsFromLua(now.Script))
				{
					string text = RichText.Strip(StoryScripts.OptionText(option.Key)).Trim();
					string result = RichText.Strip(TextConverter.ToSimplified(LuaParser.Description(option.Value))).Trim();
					// 选项文字里已经被选项提示接上了「(结果)」，去掉，结果单独写在下面
					if (result.Length > 0 && text.EndsWith(result + ")"))
					{
						int cut = text.LastIndexOf('(', text.Length - result.Length - 1);
						if (cut > 0)
						{
							text = text.Substring(0, cut).Trim();
						}
					}
					now.Options.Add((text.Length > 0 ? text : option.Key, result));
				}
				StoryScript script = StoryScripts.Get(PlayerStatManagerData.Instance?.CurrentStoryScript);
				if (script != null && script.Exits.Count > 1)
				{
					foreach (StoryExit exit in script.Exits)
					{
						now.Ways.Add((OnPaper(TextConverter.ToSimplified(StoryScripts.Describe(exit))), string.Join("　", script.EffectsOn(exit).ToArray())));
					}
				}
				foreach (string pool in LuaParser.Instance.ExtractPositionWorkFromLua(now.Script))
				{
					var events = PositionEventCache.Instance.GetPositionEventsSortedByRate(pool);
					int total = Math.Max(1, events.Sum(e => Math.Max(0, e.GetRate())));
					foreach (PositionEventData data in events)
					{
						int rate = data.GetRate();
						now.Events.Add(new NowEvent
						{
							Data = data,
							Name = TextConverter.ToSimplified(PositionEventCache.Instance.GetPositionEventName(data)),
							Desc = OnPaper(TextConverter.ToSimplified(PositionEventCache.Instance.GetPositionEventDesc(data)).Trim()),
							Rate = rate,
							Percent = rate > 0 ? Mathf.RoundToInt(rate * 100f / total) : 0
						});
					}
				}
			}
			// 主线当前这一步：游戏判定条件满足时 MainMissionStart 为真，去 MainMissionPosition 就会触发
			MissionManagerData missions = MissionManagerData.Instance;
			if (DataHelper.GetMainMission(out MissionCheckData main, out string mainTitle, out string mainDesc))
			{
				string place = TextConverter.ToSimplified(main.Position.ToString());
				now.Missions.Add(new NowMission
				{
					Main = true,
					Title = TextConverter.ToSimplified(mainTitle.Replace("\n", " ")),
					Desc = OnPaper(TextConverter.ToSimplified(mainDesc).Trim()),
					Place = place == "無" ? "" : place,
					Ready = missions != null && missions.MainMissionStart && missions.MainCheckData == main && !string.IsNullOrEmpty(main.StartScript)
				});
			}
			var active = missions?.ActiveSubMissions;
			foreach (var pair in DataHelper.GetMissions())
			{
				MissionCheckData check = pair.Key;
				string place = TextConverter.ToSimplified(check.Position.ToString());
				now.Missions.Add(new NowMission
				{
					Title = TextConverter.ToSimplified(pair.Value.Item1.Replace("\n", " ")),
					Desc = OnPaper(TextConverter.ToSimplified(pair.Value.Item2).Trim()),
					Place = place == "無" ? "" : place,
					Ready = active != null && active.Values.Contains(check) && !string.IsNullOrEmpty(check.StartScript)
				});
			}
			// 主线在最前，其余条件满足的在前；OrderBy 是稳定排序，同组保持游戏里的顺序
			var sorted = now.Missions.OrderByDescending(m => m.Main).ThenByDescending(m => m.Ready).ToList();
			now.Missions.Clear();
			now.Missions.AddRange(sorted);
		}
		catch (Exception ex)
		{
			FileLogger.LogWarning("[剧情回顾] 整理「此刻」失败: " + ex.Message);
		}
		return now;
	}
}
