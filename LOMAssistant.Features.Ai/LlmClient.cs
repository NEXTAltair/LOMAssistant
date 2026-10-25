using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine.Networking;

namespace LOMAssistant.Features.Ai;

/// <summary>对话里的一条：玩家说的、AI 说的（可能带工具调用）、工具查到的结果。</summary>
public sealed class ChatMessage
{
	public const string User = "user", Assistant = "assistant", Tool = "tool";

	public string Role = User;
	public string Text = "";
	/// <summary>AI 这一轮要调用的工具。</summary>
	public List<ToolCall> Calls = new List<ToolCall>();
	/// <summary>工具结果：对应哪一次调用。</summary>
	public string CallId = "";
}

public sealed class ToolCall
{
	public string Id = "", Name = "";
	public JObject Args = new JObject();
}

/// <summary>
/// 一家服务商：大多数（DeepSeek、Kimi、通义、智谱、硅基流动、OpenRouter、OpenAI、Gemini、本地 Ollama）都兼容 OpenAI 的接口，
/// Claude 用它自己的接口。模型名只是建议，服务商出了新模型，玩家自己改。
/// </summary>
public sealed class LlmProvider
{
	public string Name = "", BaseUrl = "", Model = "", KeysPage = "";
	/// <summary>深度模式默认用的更强模型；空 = 和平时同一个模型。</summary>
	public string DeepModel = "";
	public bool Claude;
	/// <summary>本地模型不用 Key。</summary>
	public bool NoKey;
	/// <summary>地址要玩家自己填（自定义）。</summary>
	public bool Custom;

	public static readonly LlmProvider[] All =
	{
		new LlmProvider { Name = "DeepSeek", BaseUrl = "https://api.deepseek.com/v1", Model = "deepseek-flash", DeepModel = "deepseek-v4-pro", KeysPage = "https://platform.deepseek.com/api_keys" },
		new LlmProvider { Name = "Kimi（月之暗面）", BaseUrl = "https://api.moonshot.cn/v1", Model = "kimi-k3.1-pro", KeysPage = "https://platform.moonshot.cn/console/api-keys" },
		new LlmProvider { Name = "通义千问（阿里云百炼）", BaseUrl = "https://dashscope.aliyuncs.com/compatible-mode/v1", Model = "qwen3.8-flash", DeepModel = "qwen3.8-max", KeysPage = "https://bailian.console.aliyun.com/?apiKey=1" },
		new LlmProvider { Name = "智谱 GLM", BaseUrl = "https://open.bigmodel.cn/api/paas/v4", Model = "glm-5.3-flash", DeepModel = "glm-5.3", KeysPage = "https://open.bigmodel.cn/usercenter/apikeys" },
		new LlmProvider { Name = "硅基流动", BaseUrl = "https://api.siliconflow.cn/v1", Model = "deepseek-ai/DeepSeek-V4.1-Flash", KeysPage = "https://cloud.siliconflow.cn/account/ak" },
		new LlmProvider { Name = "OpenRouter", BaseUrl = "https://openrouter.ai/api/v1", Model = "deepseek/deepseek-v4.1-flash", KeysPage = "https://openrouter.ai/keys" },
		new LlmProvider { Name = "OpenAI", BaseUrl = "https://api.openai.com/v1", Model = "gpt-6-luna", KeysPage = "https://platform.openai.com/api-keys" },
		new LlmProvider { Name = "Claude（Anthropic）", BaseUrl = "https://api.anthropic.com/v1", Model = "claude-sonnet-5-5", DeepModel = "claude-opus-5-5", KeysPage = "https://console.anthropic.com/settings/keys", Claude = true },
		new LlmProvider { Name = "Gemini（Google）", BaseUrl = "https://generativelanguage.googleapis.com/v1beta/openai", Model = "gemini-3.8-flash", KeysPage = "https://aistudio.google.com/apikey" },
		new LlmProvider { Name = "本地模型（Ollama）", BaseUrl = "http://localhost:11434/v1", Model = "qwen3", NoKey = true },
		new LlmProvider { Name = "自定义（OpenAI 兼容）", BaseUrl = "", Model = "", Custom = true },
	};

	public static LlmProvider At(int index) => All[Math.Max(0, Math.Min(All.Length - 1, index))];
}

/// <summary>AI 一次回复：说的话、要调用的工具，或者出错的原因。</summary>
public sealed class LlmReply
{
	public string Text = "";
	public List<ToolCall> Calls = new List<ToolCall>();
	public string Error;
	/// <summary>这一轮用掉的 token（服务商有返回才有）。</summary>
	public int InputTokens, OutputTokens;
}

/// <summary>
/// 发请求、解析回复。用 UnityWebRequest，跑在 Unity 主线程的协程里，查资料的工具（读游戏数据）可以直接在回调里执行。
/// 先不做流式输出：一次拿到整段回复。
/// </summary>
public static class LlmClient
{
	private static UnityWebRequest _current;

	/// <summary>停掉正在进行的请求。</summary>
	public static void Abort()
	{
		try
		{
			_current?.Abort();
		}
		catch
		{
		}
	}

	public static IEnumerator Complete(LlmProvider provider, string baseUrl, string key, string model, string system,
		List<ChatMessage> messages, bool useTools, int maxTokens, Action<LlmReply> done, bool noMoreCalls = false)
	{
		string url = (baseUrl ?? "").Trim().TrimEnd('/');
		if (url.Length == 0)
		{
			done(new LlmReply { Error = "没有填 API 地址" });
			yield break;
		}
		if ((model ?? "").Trim().Length == 0)
		{
			done(new LlmReply { Error = "没有填模型名" });
			yield break;
		}
		JObject body = provider.Claude ? ClaudeBody(model, system, messages, useTools, maxTokens) : OpenAiBody(model, system, messages, useTools, maxTokens, url);
		// 最后一轮：工具定义照给（历史里有工具调用，不给定义有的服务商会报错，DeepSeek 还会把调用格式当正文写出来），但不许再调用
		if (useTools && noMoreCalls)
		{
			body["tool_choice"] = provider.Claude ? (JToken)new JObject { ["type"] = "none" } : "none";
		}
		var request = new UnityWebRequest(url + (provider.Claude ? "/messages" : "/chat/completions"), "POST")
		{
			uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body.ToString(Formatting.None))),
			downloadHandler = new DownloadHandlerBuffer(),
			timeout = 180
		};
		request.SetRequestHeader("Content-Type", "application/json");
		if (provider.Claude)
		{
			request.SetRequestHeader("x-api-key", key ?? "");
			request.SetRequestHeader("anthropic-version", "2023-06-01");
		}
		else if (!string.IsNullOrEmpty(key))
		{
			request.SetRequestHeader("Authorization", "Bearer " + key.Trim());
		}
		_current = request;
		using (request)
		{
			yield return request.SendWebRequest();
			_current = null;
			string text = request.downloadHandler?.text ?? "";
			if (request.result != UnityWebRequest.Result.Success)
			{
				done(new LlmReply { Error = Describe(request, text) });
				yield break;
			}
			try
			{
				done(provider.Claude ? ParseClaude(JObject.Parse(text)) : ParseOpenAi(JObject.Parse(text)));
			}
			catch (Exception ex)
			{
				FileLogger.LogWarning("[AI] 回复解析失败: " + ex.Message + "\n" + (text.Length > 500 ? text.Substring(0, 500) : text));
				done(new LlmReply { Error = "看不懂服务商的回复\n" + ex.Message });
			}
		}
	}

	// 出错时给玩家看的原因：常见状态码说人话，后面附上服务商自己的说明
	private static string Describe(UnityWebRequest request, string body)
	{
		if (request.result == UnityWebRequest.Result.ConnectionError)
		{
			return "连不上服务商，检查网络和 API 地址\n" + request.error;
		}
		string detail = "";
		try
		{
			JToken json = JToken.Parse(body);
			detail = json["error"]?["message"]?.ToString() ?? json["error"]?.ToString() ?? json["message"]?.ToString() ?? "";
		}
		catch
		{
			detail = body.Length > 200 ? body.Substring(0, 200) : body;
		}
		long code = request.responseCode;
		string reason = code switch
		{
			400 => "请求不对（模型名写错，或这个模型不支持工具调用）",
			401 => "API Key 不对",
			403 => "没有权限（Key 不能用这个模型，或地区受限）",
			402 => "余额不足",
			404 => "地址或模型名不对",
			429 => "请求太频繁或额度用完了，稍后再试",
			_ when code >= 500 => "服务商那边出错了，稍后再试",
			_ => "请求失败",
		};
		// 原因单独一行（界面语言不是中文时查表翻译），服务商的原话放下一行
		return reason + "\n" + code + (detail.Length > 0 ? " " + detail.Trim() : "");
	}

	private static JObject OpenAiBody(string model, string system, List<ChatMessage> messages, bool useTools, int maxTokens, string url)
	{
		var list = new JArray { new JObject { ["role"] = "system", ["content"] = system } };
		foreach (ChatMessage m in messages)
		{
			if (m.Role == ChatMessage.Tool)
			{
				list.Add(new JObject { ["role"] = "tool", ["tool_call_id"] = m.CallId, ["content"] = m.Text });
			}
			else if (m.Role == ChatMessage.Assistant && m.Calls.Count > 0)
			{
				list.Add(new JObject
				{
					["role"] = "assistant",
					["content"] = m.Text.Length > 0 ? m.Text : null,
					["tool_calls"] = new JArray(m.Calls.Select(c => new JObject
					{
						["id"] = c.Id,
						["type"] = "function",
						["function"] = new JObject { ["name"] = c.Name, ["arguments"] = c.Args.ToString(Formatting.None) }
					}))
				});
			}
			else
			{
				list.Add(new JObject { ["role"] = m.Role, ["content"] = m.Text });
			}
		}
		var body = new JObject { ["model"] = model.Trim(), ["messages"] = list };
		// OpenAI 官方的新模型（GPT-5 以后）不认 max_tokens，要用 max_completion_tokens；其他兼容接口还是 max_tokens
		body[url.IndexOf("api.openai.com", StringComparison.OrdinalIgnoreCase) >= 0 ? "max_completion_tokens" : "max_tokens"] = maxTokens;
		// OpenAI 的 Luna 系列在 Chat Completions 里只有推理强度为 none 时才能调用工具（官方文档写明）
		if (useTools && model.IndexOf("luna", StringComparison.OrdinalIgnoreCase) >= 0)
		{
			body["reasoning_effort"] = "none";
		}
		if (useTools)
		{
			body["tools"] = new JArray(AiKnowledge.Tools.Select(t => new JObject
			{
				["type"] = "function",
				["function"] = new JObject { ["name"] = t.Name, ["description"] = t.Description, ["parameters"] = t.Parameters }
			}));
		}
		return body;
	}

	private static LlmReply ParseOpenAi(JObject json)
	{
		var reply = new LlmReply();
		JToken message = json["choices"]?[0]?["message"];
		if (message == null)
		{
			reply.Error = "服务商没有返回内容";
			return reply;
		}
		reply.Text = message["content"]?.Type == JTokenType.String ? message["content"].ToString() : "";
		foreach (JToken call in message["tool_calls"] as JArray ?? new JArray())
		{
			string args = call["function"]?["arguments"]?.ToString() ?? "{}";
			JObject parsed;
			try
			{
				parsed = string.IsNullOrWhiteSpace(args) ? new JObject() : JObject.Parse(args);
			}
			catch
			{
				parsed = new JObject();
			}
			reply.Calls.Add(new ToolCall { Id = call["id"]?.ToString() ?? Guid.NewGuid().ToString("N"), Name = call["function"]?["name"]?.ToString() ?? "", Args = parsed });
		}
		reply.InputTokens = json["usage"]?["prompt_tokens"]?.Value<int>() ?? 0;
		reply.OutputTokens = json["usage"]?["completion_tokens"]?.Value<int>() ?? 0;
		return reply;
	}

	private static JObject ClaudeBody(string model, string system, List<ChatMessage> messages, bool useTools, int maxTokens)
	{
		var list = new JArray();
		JArray pendingResults = null;
		foreach (ChatMessage m in messages)
		{
			// 连续的工具结果合成一条 user 消息
			if (m.Role == ChatMessage.Tool)
			{
				if (pendingResults == null)
				{
					pendingResults = new JArray();
					list.Add(new JObject { ["role"] = "user", ["content"] = pendingResults });
				}
				pendingResults.Add(new JObject { ["type"] = "tool_result", ["tool_use_id"] = m.CallId, ["content"] = m.Text });
				continue;
			}
			pendingResults = null;
			if (m.Role == ChatMessage.Assistant)
			{
				var content = new JArray();
				if (m.Text.Length > 0)
				{
					content.Add(new JObject { ["type"] = "text", ["text"] = m.Text });
				}
				foreach (ToolCall c in m.Calls)
				{
					content.Add(new JObject { ["type"] = "tool_use", ["id"] = c.Id, ["name"] = c.Name, ["input"] = c.Args });
				}
				list.Add(new JObject { ["role"] = "assistant", ["content"] = content });
			}
			else
			{
				list.Add(new JObject { ["role"] = "user", ["content"] = m.Text });
			}
		}
		var body = new JObject { ["model"] = model.Trim(), ["max_tokens"] = maxTokens, ["system"] = system, ["messages"] = list };
		if (useTools)
		{
			body["tools"] = new JArray(AiKnowledge.Tools.Select(t => new JObject { ["name"] = t.Name, ["description"] = t.Description, ["input_schema"] = t.Parameters }));
		}
		return body;
	}

	private static LlmReply ParseClaude(JObject json)
	{
		var reply = new LlmReply();
		var text = new StringBuilder();
		foreach (JToken block in json["content"] as JArray ?? new JArray())
		{
			string type = block["type"]?.ToString();
			if (type == "text")
			{
				text.Append(block["text"]);
			}
			else if (type == "tool_use")
			{
				reply.Calls.Add(new ToolCall { Id = block["id"]?.ToString() ?? "", Name = block["name"]?.ToString() ?? "", Args = block["input"] as JObject ?? new JObject() });
			}
		}
		reply.Text = text.ToString();
		reply.InputTokens = json["usage"]?["input_tokens"]?.Value<int>() ?? 0;
		reply.OutputTokens = json["usage"]?["output_tokens"]?.Value<int>() ?? 0;
		return reply;
	}
}
