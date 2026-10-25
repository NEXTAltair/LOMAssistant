using System.Collections;
using System.Collections.Generic;
using System.Linq;
using LOMAssistant.Config;

namespace LOMAssistant.Features.Ai;

/// <summary>
/// AI 攻略对话：玩家问一句，AI 自己决定查哪些资料（AiKnowledge 的工具），查完再回答。
/// 一个问题最多查 MaxRounds 轮，最后一轮不给工具，逼它作答。
/// 答完以后只留问答本身，查资料的来回丢掉，下一个问题不用再为它们付 token。
/// </summary>
public static class AiChat
{
	public enum Kind { User, Assistant, Activity, Error }

	public sealed class Entry
	{
		public Kind Kind;
		public string Text = "";
	}

	private const int MaxRounds = 10;
	private const int KeepTurns = 10;
	private const int MaxToolChars = 12000;

	/// <summary>给玩家看的对话（含「正在查…」和出错提示）。</summary>
	public static readonly List<Entry> Shown = new List<Entry>();
	/// <summary>发给模型的历史（只有问答）。</summary>
	private static readonly List<ChatMessage> History = new List<ChatMessage>();

	public static bool Busy { get; private set; }
	public static int TokensUsed { get; private set; }
	private static bool _stop;

	public static LlmProvider Provider => LlmProvider.At(ConfigManager.AiProvider.Value);

	public static bool Configured => ConfigManager.AiBaseUrl.Value.Trim().Length > 0 && ConfigManager.AiModel.Value.Trim().Length > 0
		&& (Provider.NoKey || ConfigManager.AiApiKey.Value.Trim().Length > 0);

	public static void Clear()
	{
		if (Busy)
		{
			return;
		}
		Shown.Clear();
		History.Clear();
	}

	public static void Stop()
	{
		_stop = true;
		LlmClient.Abort();
	}

	public static void Ask(string question)
	{
		question = (question ?? "").Trim();
		if (Busy || question.Length == 0)
		{
			return;
		}
		LOMAssistantPlugin.Instance.StartCoroutine(Run(question));
	}

	private static IEnumerator Run(string question)
	{
		Busy = true;
		_stop = false;
		Shown.Add(new Entry { Kind = Kind.User, Text = question });
		var turn = new List<ChatMessage> { new ChatMessage { Role = ChatMessage.User, Text = question } };
		LlmProvider provider = Provider;
		string answer = null;
		for (int round = 0; round < MaxRounds && !_stop; round++)
		{
			LlmReply reply = null;
			bool tools = round < MaxRounds - 1;
			yield return LlmClient.Complete(provider, ConfigManager.AiBaseUrl.Value, ConfigManager.AiApiKey.Value, ConfigManager.AiModel.Value,
				SystemPrompt(), History.Concat(turn).ToList(), tools, 2000, r => reply = r);
			if (_stop)
			{
				break;
			}
			if (reply == null || reply.Error != null)
			{
				Shown.Add(new Entry { Kind = Kind.Error, Text = reply?.Error ?? "没有回复" });
				FileLogger.LogWarning("[AI] " + (reply?.Error ?? "没有回复"));
				break;
			}
			TokensUsed += reply.InputTokens + reply.OutputTokens;
			if (reply.Calls.Count == 0)
			{
				answer = reply.Text.Trim();
				break;
			}
			turn.Add(new ChatMessage { Role = ChatMessage.Assistant, Text = reply.Text, Calls = reply.Calls });
			foreach (ToolCall call in reply.Calls)
			{
				Shown.Add(new Entry { Kind = Kind.Activity, Text = AiKnowledge.Activity(call.Name, call.Args) });
				string result = AiKnowledge.Run(call.Name, call.Args);
				if (result.Length > MaxToolChars)
				{
					result = result.Substring(0, MaxToolChars) + "\n……（太长，后面省略）";
				}
				turn.Add(new ChatMessage { Role = ChatMessage.Tool, CallId = call.Id, Text = result });
			}
		}
		if (_stop)
		{
			Shown.Add(new Entry { Kind = Kind.Error, Text = "已停止" });
		}
		else if (answer != null)
		{
			Shown.Add(new Entry { Kind = Kind.Assistant, Text = answer.Length > 0 ? answer : "（AI 没有说话）" });
			History.Add(turn[0]);
			History.Add(new ChatMessage { Role = ChatMessage.Assistant, Text = answer });
			while (History.Count > KeepTurns * 2)
			{
				History.RemoveRange(0, 2);
			}
		}
		Busy = false;
	}

	/// <summary>试一下能不能连上：不带工具，问一句很短的话。</summary>
	public static IEnumerator Test(System.Action<string> done)
	{
		LlmReply reply = null;
		yield return LlmClient.Complete(Provider, ConfigManager.AiBaseUrl.Value, ConfigManager.AiApiKey.Value, ConfigManager.AiModel.Value,
			"用一句话回答。", new List<ChatMessage> { new ChatMessage { Text = "你好，请回复「连上了」。" } }, false, 30, r => reply = r);
		done(reply == null ? "没有回复" : reply.Error ?? "");
	}

	private static string ReplyLanguage => TextConverter.DisplayLanguage switch
	{
		"ChineseTraditional" => "繁体中文",
		UiText.Korean => "韩语（한국어）",
		UiText.Japanese => "日语（日本語）",
		UiText.English => "英语（English）",
		_ => "简体中文",
	};

	private static string Spoiler => ConfigManager.AiSpoiler.Value switch
	{
		1 => "只说玩家当前进度和已经经历过的剧情；问到后面的剧情时，只说「后面会有相关剧情」和要做的准备（数值、好感、选项），不讲具体发生了什么、谁会死、结局是什么。",
		2 => "只给提示，不直接说结果：告诉玩家该往哪个方向努力（提高什么数值、关注哪个人、在哪类选项上谨慎），让玩家自己去发现剧情。",
		_ => "玩家想知道什么就说什么，可以剧透。",
	};

	private static string SystemPrompt()
	{
		return "你是武侠游戏《活侠传》的攻略助手，在游戏 Mod「咖啡伴侣」里回答玩家的问题。玩家操控的主角是赵活。\n\n"
			+ "怎么查资料\n"
			+ "- 剧情、条件、数值、结局一律以工具查到的游戏数据为准，不要凭印象编。查不到就说查不到，不要猜。\n"
			+ "- 不知道编号时先 search，再用 quest 看任务、script 看剧本的分岔和对白、endings 看结局、heroine 看结缘路线。\n"
			+ "- 问题和玩家现在的进度有关（「我现在」「接下来」「来得及吗」）时，先用 player_state 看进度、数值和好感。\n"
			+ "- 玩家问「这里怎么选」「这个选项」「这次掷骰」时，player_state 的【眼前】是屏幕上正在显示的选项或掷骰，再用 script 查那个剧本，按选项文字找到对应的分岔，结合玩家现在的数值说每个选项的结果和推荐。\n"
			+ "- 资料里「若 …需>=40」是条件，「掷骰「…」（>=50）」是骰子要掷到的点数，「变化」是这条路上的数值和旗标变化。\n"
			+ "- 剧本的对白很长时会分页，需要时再翻页，不要一次全翻。\n\n"
			+ "怎么回答\n"
			+ "- 先给结论，再分条说清楚条件、每条路的结果和依据；可以引用一两句原文对白。\n"
			+ "- 好感、数值、旗标的变化只说资料「变化」里写了的；资料里没写某人的好感变化，就是这段剧情不改她的好感，不要说「好感容易涨」「好感涨得最多」这类话。\n"
			+ "- 「准备」只说具体能做的（提高哪项数值到多少、先存档、用 Mod 的骰子自选），不说空泛的话。\n"
			+ "- 用游戏里的叫法（人名、数值名、任务名）。任务编号可以括注在任务名后面，方便玩家在剧情图里找；剧本编号不要写给玩家，用「之后」「接下来的剧情」代替；不要提工具、文件、数据这些词。\n"
			+ "- 简洁：一般不超过 400 字，玩家要细节时再展开。\n"
			+ "- 剧透：" + Spoiler + "\n"
			+ "- 用" + ReplyLanguage + "回答。";
	}
}
