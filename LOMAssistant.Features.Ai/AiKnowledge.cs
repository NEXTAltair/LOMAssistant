using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using LOMAssistant.Features.Story;
using Mortal.Core;
using Newtonsoft.Json.Linq;

namespace LOMAssistant.Features.Ai;

/// <summary>
/// AI 查资料用的工具：都从 Mod 已经读好的剧情数据（StoryAtlas、StoryScripts）现场整理成文字，不另外打包知识库。
/// 剧情知识里不带玩家当前的数值（条件里的「→ 当前…」都去掉），玩家现在的进度、数值只从 player_state 拿。
/// 每个工具的结果有长度上限，太长的分页，免得一次塞给模型太多。
/// </summary>
public static class AiKnowledge
{
	private const int PageChars = 6000;

	/// <summary>给模型看的工具说明（名字、用途、参数），OpenAI 和 Claude 的格式由 LlmClient 各自包装。</summary>
	public static readonly (string Name, string Description, JObject Parameters)[] Tools =
	{
		("search", "按关键词搜游戏剧情：任务名、任务说明和摘要、对白原文、结局和成就、可结缘的女主角。不知道编号时先用它。返回匹配的任务编号、剧本编号、结局编号和片段。",
			Schema(("query", "string", "关键词，如人名、任务名、地点、一句对白里的词。用简体中文"))),
		("quest", "看一个任务的详情：所属任务组、主线还是支线、发生时间、地点、触发条件、从哪些剧本接进来、之后接到哪些任务、包含的剧本和每个剧本的剧情摘要。",
			Schema(("key", "string", "任务编号，如 S2700_01（从 search 结果里拿）"))),
		("script", "看一个剧本的详情：游戏的剧情记录、选项、每条分岔路径上的数值和旗标变化、接到哪个剧本、结局和死亡、决斗；以及按分岔分开的对白原文。对白多时分页。",
			Schema(("name", "string", "剧本编号，如 S2700_02_01"), ("page", "integer", "对白第几页，从 1 开始，默认 1"))),
		("endings", "按关键词查结局、死亡、成就、结局 CG：名称、说明，以及在哪个剧本、走哪条分岔能达成。关键词留空则列出全部名称。",
			Schema(("query", "string", "关键词；传空字符串列出全部名称"))),
		("heroine", "看一位可结缘女主角的结缘路线：要经过哪些任务、每一站的好感门槛和要设的结缘旗标、玩家现在做到哪一步。",
			Schema(("name", "string", "女主角的名字或称呼，如 小师妹、唐默铃、小竹、郁竹"))),
		("player_state", "看玩家现在的进度：游戏日期、主线和可触发的支线、正在进行的剧情、主要数值、所有人的好感、结缘对象。回答和玩家当前进度有关的问题前先看一次。",
			new JObject { ["type"] = "object", ["properties"] = new JObject() }),
	};

	private static JObject Schema(params (string Name, string Type, string Description)[] props)
	{
		var properties = new JObject();
		foreach (var (name, type, description) in props)
		{
			properties[name] = new JObject { ["type"] = type, ["description"] = description };
		}
		// 字符串参数都必填，页码可省
		return new JObject { ["type"] = "object", ["properties"] = properties, ["required"] = new JArray(props.Where(p => p.Type == "string").Select(p => p.Name)) };
	}

	/// <summary>执行一个工具调用，返回给模型的文字（出错也返回文字，让模型自己换个办法）。</summary>
	public static string Run(string name, JObject args)
	{
		try
		{
			if (!StoryAtlas.Ready)
			{
				return "剧情数据还在读取（打开一次咖啡伴侣窗口的「路线图」会开始读），请稍后再问。";
			}
			string Arg(string key) => args?[key]?.ToString().Trim() ?? "";
			switch (name)
			{
				case "search": return Search(Arg("query"));
				case "quest": return Quest(Arg("key"));
				case "script": return Script(Arg("name"), int.TryParse(Arg("page"), out int p) ? p : 1);
				case "endings": return EndingList(Arg("query"));
				case "heroine": return Heroine(Arg("name"));
				case "player_state": return StoryReviewPanel.AiPlayerState();
				default: return "没有这个工具：" + name;
			}
		}
		catch (Exception ex)
		{
			FileLogger.LogWarning("[AI] 工具 " + name + " 出错: " + ex);
			return "查询出错：" + ex.Message;
		}
	}

	/// <summary>给玩家看的一行说明：AI 正在查什么。</summary>
	public static string Activity(string name, JObject args)
	{
		string Arg(string key) => args?[key]?.ToString().Trim() ?? "";
		switch (name)
		{
			case "search": return "搜索「" + Arg("query") + "」";
			case "quest": return "查看任务 " + Arg("key") + (StoryAtlas.Find(Arg("key")) is StoryMission m ? "「" + Clean(m.Title) + "」" : "");
			case "script": return "查看剧本 " + Arg("name");
			case "endings": return "查结局「" + Arg("query") + "」";
			case "heroine": return "查看「" + Arg("name") + "」的结缘路线";
			case "player_state": return "查看当前进度";
			default: return name;
		}
	}

	private static readonly Regex Current = new Regex(@" ?→ ?当前[^ ，；、。)）\n]*");

	/// <summary>去掉富文本标记、条件里的当前值，转成简体。</summary>
	internal static string Clean(string text) => Current.Replace(TextConverter.ToSimplified(RichText.Strip(text ?? "")), "").Trim();

	private static string PathText(List<StoryStep> path) => path.Count == 0 ? "（开头，不经分岔）" : string.Join(" → ", path.Select(s => Clean(StoryScripts.Describe(s))).ToArray());

	private static string Speaker(string id)
	{
		if (id.Length == 0)
		{
			return "旁白";
		}
		try
		{
			string text = LocalizationManager.Instance.LocaleResolver.GetString("Character/" + id);
			return string.IsNullOrWhiteSpace(text) || text.StartsWith("Character/") ? id : TextConverter.ToSimplified(text);
		}
		catch
		{
			return id;
		}
	}

	private static string MissionLine(StoryMission m) => m.Key + "「" + Clean(m.Title) + "」" + (m.Main ? "（主线）" : "（支线）") + (m.When.Length > 0 ? " " + Clean(m.When) : "");

	private static string Search(string query)
	{
		if (query.Length == 0)
		{
			return "请给关键词。";
		}
		string q = TextConverter.ToSimplified(query);
		var sb = new StringBuilder();
		var missions = StoryAtlas.Missions.Where(m => (m.Key + m.Title + m.Desc + m.Summary + m.SeriesTitle).Contains(q) || TextConverter.ToSimplified(m.Summary).Contains(q)).Take(15).ToList();
		if (missions.Count > 0)
		{
			sb.Append("【任务】\n");
			foreach (StoryMission m in missions)
			{
				sb.Append("- ").Append(MissionLine(m));
				string summary = Clean(m.Summary).Replace("\n", " ");
				if (summary.Length > 0)
				{
					sb.Append("：").Append(summary.Length > 60 ? summary.Substring(0, 60) + "…" : summary);
				}
				sb.Append('\n');
			}
		}
		if (FindHeroine(q) is StoryRomance.Heroine heroine)
		{
			sb.Append("【可结缘女主角】").Append(StoryRomance.Name(heroine)).Append("（用 heroine 看她的结缘路线）\n");
		}
		var endings = StoryAtlas.Endings.Concat(StoryAtlas.Achievements).Concat(StoryAtlas.Cgs).Where(e => (e.Title + e.Desc).Contains(q)).Take(10).ToList();
		if (endings.Count > 0)
		{
			sb.Append("【结局、死亡、成就、CG】（用 endings 看达成方法）\n");
			foreach (StoryEnding e in endings)
			{
				sb.Append("- ").Append(Clean(e.Title)).Append(e.Death ? "（死亡）" : "").Append('\n');
			}
		}
		var lines = StoryAtlas.SearchDialogue(q, 15);
		if (lines.Count > 0)
		{
			sb.Append("【对白】（剧本编号：片段）\n");
			foreach (var (script, snippet) in lines)
			{
				sb.Append("- ").Append(script).Append(StoryAtlas.Owner.TryGetValue(script, out StoryMission owner) ? "（任务 " + owner.Key + "「" + Clean(owner.Title) + "」）" : "").Append("：").Append(Clean(snippet)).Append('\n');
			}
		}
		return sb.Length > 0 ? sb.ToString() : "没有找到「" + query + "」。换个说法试试（人名、地名、任务名里的词）。";
	}

	private static string Quest(string key)
	{
		StoryMission m = StoryAtlas.Find(key) ?? StoryAtlas.Missions.FirstOrDefault(x => Clean(x.Title).Contains(key));
		if (m == null)
		{
			return "没有任务「" + key + "」。先用 search 找编号。";
		}
		var sb = new StringBuilder("任务 ").Append(MissionLine(m)).Append('\n');
		sb.Append("任务组：").Append(Clean(m.SeriesTitle)).Append(m.Inactive ? "；不会在地图上自己触发，只能由别的剧本接进来" : "").Append('\n');
		if (m.Desc.Length > 0)
		{
			sb.Append("说明：").Append(Clean(m.Desc)).Append('\n');
		}
		sb.Append("时间：").Append(Clean(m.When)).Append("；地点：").Append(Clean(m.Place)).Append('\n');
		string cond = Clean(StoryAtlas.ConditionText(m)).Replace("\n", " ");
		if (cond.Length > 0)
		{
			sb.Append("触发条件：").Append(cond).Append('\n');
		}
		foreach (StoryEntry e in m.Entries)
		{
			sb.Append("从").Append(e.From != null ? "任务 " + e.From.Key + "「" + Clean(e.From.Title) + "」的" : "").Append("剧本 ").Append(e.Script).Append(" 接进来：").Append(PathText(e.Path)).Append('\n');
		}
		if (m.Next.Count > 0)
		{
			sb.Append("之后接到：").Append(string.Join("、", m.Next.Select(k => StoryAtlas.Find(k) is StoryMission n ? n.Key + "「" + Clean(n.Title) + "」" : k).ToArray())).Append('\n');
		}
		sb.Append("\n剧本（编号：剧情记录）：\n");
		foreach (string name in m.Scripts.Where(s => StoryAtlas.Owner.TryGetValue(s, out StoryMission o) && o == m))
		{
			StoryScript s = StoryScripts.Get(name);
			string first = s == null ? "" : s.Records.Select(StoryScripts.Legend).FirstOrDefault(t => t.Length > 0) ?? "";
			sb.Append("- ").Append(name).Append(s != null && s.Branches ? "（有分岔）" : "").Append(first.Length > 0 ? "：" + (first.Length > 80 ? first.Substring(0, 80) + "…" : first) : "").Append('\n');
		}
		return sb.ToString();
	}

	private static string Script(string name, int page)
	{
		StoryScript s = StoryScripts.Get(name);
		if (s == null)
		{
			return "没有剧本「" + name + "」。先用 search 或 quest 找编号。";
		}
		var sb = new StringBuilder("剧本 ").Append(name);
		if (StoryAtlas.Owner.TryGetValue(name, out StoryMission owner))
		{
			sb.Append("，属于任务 ").Append(MissionLine(owner));
		}
		sb.Append('\n');
		if (page <= 1)
		{
			var legends = s.Records.Select(StoryScripts.Legend).Where(t => t.Length > 0).Distinct().ToList();
			if (legends.Count > 0)
			{
				sb.Append("\n【剧情记录】（游戏回想里的文字，按剧本顺序，分岔各自的都在）\n");
				foreach (string t in legends)
				{
					sb.Append("- ").Append(t).Append('\n');
				}
			}
			if (s.Menus.Count > 0)
			{
				sb.Append("\n【选项】\n");
				foreach (List<StoryStep> menu in s.Menus)
				{
					sb.Append("- ").Append(string.Join(" / ", menu.Select(x => Clean(StoryScripts.Describe(x))).ToArray())).Append('\n');
				}
			}
			if (s.Exits.Count > 0)
			{
				sb.Append("\n【去处】（分岔路径 ⇒ 接到哪；变化是这条路上的数值、好感、旗标、物品变化）\n");
				foreach (StoryExit exit in s.Exits)
				{
					string target = exit.ToTitle ? "回到标题（结局）" : exit.ToMap ? "回地图" : exit.Target.Length > 0 ? "剧本 " + exit.Target + (StoryAtlas.Owner.TryGetValue(exit.Target, out StoryMission t) && t != owner ? "（任务 " + t.Key + "「" + Clean(t.Title) + "」）" : "") : "（剧本结束）";
					sb.Append("- ").Append(PathText(exit.Path)).Append(exit.AfterScene ? "，战斗后" : "").Append(" ⇒ ").Append(target);
					if (exit.MainMission.Length > 0)
					{
						sb.Append("，主线推进到 ").Append(exit.MainMission);
					}
					var effects = s.EffectsOn(exit).Select(Clean).ToList();
					if (effects.Count > 0)
					{
						sb.Append("\n  变化：").Append(string.Join("；", effects.ToArray()));
					}
					sb.Append('\n');
				}
			}
			foreach (var (path, death, id) in s.Endings)
			{
				StoryEnding e = StoryAtlas.Endings.FirstOrDefault(x => x.Id == id);
				sb.Append(death ? "【死亡】" : "【结局】").Append(e != null ? Clean(e.Title) : id).Append("：").Append(PathText(path)).Append('\n');
			}
			foreach (var (path, level) in s.Fights)
			{
				sb.Append("【决斗】关卡 ").Append(level).Append("：").Append(PathText(path)).Append('\n');
			}
		}
		// 对白按分岔分组：同一条路上的连在一起
		var dialogue = new StringBuilder();
		string lastPath = null;
		foreach (var (path, speaker, key) in s.Talk)
		{
			string text = StoryAtlas.LineText(key);
			if (text.Length == 0)
			{
				continue;
			}
			string p = PathText(path);
			if (p != lastPath)
			{
				dialogue.Append("\n〔").Append(p).Append("〕\n");
				lastPath = p;
			}
			dialogue.Append(Speaker(speaker)).Append("：").Append(text).Append('\n');
		}
		if (dialogue.Length > 0)
		{
			string all = dialogue.ToString();
			int pages = (all.Length + PageChars - 1) / PageChars;
			page = Math.Max(1, Math.Min(page, pages));
			string part = all.Substring((page - 1) * PageChars, Math.Min(PageChars, all.Length - (page - 1) * PageChars));
			sb.Append("\n【对白】第 ").Append(page).Append('/').Append(pages).Append(" 页（〔〕里是这段对白所在的分岔）\n").Append(part);
			if (page < pages)
			{
				sb.Append("\n……还有下一页：script 加 page=").Append(page + 1);
			}
		}
		return sb.ToString();
	}

	private static string EndingList(string query)
	{
		string q = TextConverter.ToSimplified(query);
		var sb = new StringBuilder();
		void List(string title, List<StoryEnding> items)
		{
			var hits = items.Where(e => q.Length == 0 || (e.Title + e.Desc).Contains(q)).ToList();
			if (hits.Count == 0)
			{
				return;
			}
			sb.Append("【").Append(title).Append("】\n");
			foreach (StoryEnding e in hits.Take(q.Length == 0 ? 200 : 12))
			{
				sb.Append("- ").Append(e.Number > 0 ? e.Number + ". " : "").Append(Clean(e.Title)).Append(e.Death ? "（死亡）" : "").Append(e.Achieved ? "（玩家达成过）" : "");
				if (q.Length > 0)
				{
					if (e.Desc.Length > 0)
					{
						sb.Append("：").Append(Clean(e.Desc).Replace("\n", " "));
					}
					foreach (StoryEndingWay w in e.Ways.Take(6))
					{
						sb.Append("\n  · 剧本 ").Append(w.Script).Append(w.Mission != null ? "（任务 " + w.Mission.Key + "「" + Clean(w.Mission.Title) + "」）" : "").Append("：").Append(PathText(w.Path)).Append(w.LostDuel ? "，决斗输了" : "");
					}
				}
				sb.Append('\n');
			}
		}
		List("结局与死亡", StoryAtlas.Endings);
		List("成就", StoryAtlas.Achievements);
		List("结局 CG", StoryAtlas.Cgs);
		return sb.Length > 0 ? sb.ToString() : "没有找到「" + query + "」相关的结局。";
	}

	// 玩家常用的叫法（好感里的人名、昵称），按 StoryRomance.All 的顺序
	private static readonly string[][] Aliases =
	{
		new[] { "小师妹", "唐默铃", "默铃" },
		new[] { "瑞杏", "杏花仙", "咖啡" },
		new[] { "叶云裳", "云裳" },
		new[] { "虞小梅", "小梅" },
		new[] { "上官萤", "上官" },
		new[] { "夏侯兰", "小兰" },
		new[] { "郁竹", "小竹" },
		new[] { "魏菊", "小菊", "魏招弟" },
		new[] { "龙湘", "湘姊" },
	};

	internal static StoryRomance.Heroine FindHeroine(string name)
	{
		string q = TextConverter.ToSimplified(name ?? "");
		if (q.Length == 0)
		{
			return null;
		}
		for (int i = 0; i < StoryRomance.All.Length && i < Aliases.Length; i++)
		{
			StoryRomance.Heroine h = StoryRomance.All[i];
			if (Aliases[i].Any(a => q.Contains(a)) || q.Contains(StoryRomance.Name(h)))
			{
				return h;
			}
		}
		return null;
	}

	private static string Heroine(string name)
	{
		var h = FindHeroine(name);
		if (h == null)
		{
			return "「" + name + "」不是可结缘的女主角。可结缘的有：" + string.Join("、", StoryRomance.All.Select(StoryRomance.Name).ToArray());
		}
		var stops = StoryRomance.StopsOf(h);
		if (stops == null)
		{
			return "结缘路线还在整理，请稍后再问。";
		}
		var sb = new StringBuilder(StoryRomance.Name(h)).Append(" 的结缘路线");
		sb.Append("（玩家现在对她的好感 ").Append(StoryRomance.Affection(h)).Append(StoryRomance.IsLover(h) ? "，已结缘" : "").Append("）\n");
		foreach (var stop in stops)
		{
			sb.Append("- ").Append(MissionLine(stop.Mission));
			if (stop.Affection != 0)
			{
				sb.Append("，这个任务里好感最多 ").Append(stop.Affection > 0 ? "+" : "").Append(stop.Affection);
			}
			if (stop.Steps.Count > 0)
			{
				sb.Append("，结缘旗标：").Append(string.Join("、", stop.Steps.Select(f => StoryRomance.StepName(f) + (StoryRomance.StepDone(f) ? "（已做到）" : "（未做到）")).ToArray()));
			}
			sb.Append('\n');
		}
		return sb.ToString();
	}
}
