using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using BepInEx;
using Newtonsoft.Json;

namespace LOMAssistant.Features.Ai;

/// <summary>一段对话：和一位（攻略助手或某个女主角）。显示的内容和发给模型的问答历史都存着，重启游戏还在。</summary>
public sealed class AiSession
{
	public string Id = "";
	/// <summary>人设：空 = 攻略助手，否则是女主角的角色编号。</summary>
	public string Persona = "";
	/// <summary>第一句问题，列表里显示。</summary>
	public string Title = "";
	public DateTime Updated = DateTime.Now;
	public List<AiChat.Entry> Shown = new List<AiChat.Entry>();
	public List<ChatMessage> History = new List<ChatMessage>();
	/// <summary>玩家对上一张修改卡片的处理，下次提问时告诉 AI。</summary>
	public string Note = "";
}

/// <summary>
/// 对话存在 BepInEx\config\LOMAssistant_AI\ 下，一段一个 json（更新 Mod 时 plugins 文件夹会被覆盖，config 不会）。
/// 每位可以有好几段，默认接着最近那段聊；玩家可以开新对话、切回旧的、删掉。
/// 读回来的修改卡片要是还没点，标成失效：存档可能已经换了，当时找到的数值行不能再用。
/// </summary>
public static class AiSessions
{
	private static List<AiSession> _all;
	private static readonly Dictionary<string, string> Selected = new Dictionary<string, string>();

	private static string Folder => Path.Combine(Paths.ConfigPath, "LOMAssistant_AI");

	// 游戏裁剪过的 Mono 没有 Reflection.Emit，Json.NET 要改用反射（随时存档也是这么做的，但它可能没启用）
	static AiSessions()
	{
		try
		{
			typeof(JsonConvert).Assembly.GetType("Newtonsoft.Json.Serialization.JsonTypeReflector")
				.GetField("_dynamicCodeGeneration", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).SetValue(null, (bool?)false);
		}
		catch (Exception ex)
		{
			FileLogger.LogWarning("[AI] 设置 Json.NET 失败: " + ex.Message);
		}
	}

	private static List<AiSession> All
	{
		get
		{
			if (_all == null)
			{
				_all = new List<AiSession>();
				try
				{
					if (Directory.Exists(Folder))
					{
						foreach (string file in Directory.GetFiles(Folder, "*.json"))
						{
							try
							{
								AiSession s = JsonConvert.DeserializeObject<AiSession>(File.ReadAllText(file, Encoding.UTF8));
								if (s == null || s.Id.Length == 0)
								{
									continue;
								}
								foreach (AiChat.Entry e in s.Shown.Where(e => e.Kind == AiChat.Kind.Proposal && e.State == AiChat.ProposalState.Pending))
								{
									e.State = AiChat.ProposalState.Expired;
								}
								_all.Add(s);
							}
							catch (Exception ex)
							{
								FileLogger.LogWarning("[AI] 读不了对话 " + Path.GetFileName(file) + ": " + ex.Message);
							}
						}
					}
				}
				catch (Exception ex)
				{
					FileLogger.LogWarning("[AI] 读对话失败: " + ex.Message);
				}
			}
			return _all;
		}
	}

	/// <summary>这位的全部对话，新的在前（没说过话的新对话不算）。</summary>
	public static List<AiSession> Of(string persona) => All.Where(s => s.Persona == persona && s.Shown.Count > 0).OrderByDescending(s => s.Updated).ToList();

	/// <summary>这位现在用的对话：选过的，否则最近一段，都没有就开一段新的。</summary>
	public static AiSession Current(string persona)
	{
		if (Selected.TryGetValue(persona, out string id) && All.FirstOrDefault(s => s.Id == id) is AiSession picked)
		{
			return picked;
		}
		AiSession latest = Of(persona).FirstOrDefault() ?? New(persona);
		Selected[persona] = latest.Id;
		return latest;
	}

	public static AiSession New(string persona)
	{
		// 已经有一段空的就用它，不堆空对话
		AiSession empty = All.FirstOrDefault(s => s.Persona == persona && s.Shown.Count == 0);
		if (empty == null)
		{
			empty = new AiSession { Id = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff"), Persona = persona };
			All.Add(empty);
		}
		Selected[persona] = empty.Id;
		return empty;
	}

	public static void Select(AiSession session) => Selected[session.Persona] = session.Id;

	public static void Delete(AiSession session)
	{
		All.Remove(session);
		Selected.Remove(session.Persona);
		try
		{
			string file = Path.Combine(Folder, session.Id + ".json");
			if (File.Exists(file))
			{
				File.Delete(file);
			}
		}
		catch (Exception ex)
		{
			FileLogger.LogWarning("[AI] 删除对话失败: " + ex.Message);
		}
	}

	public static void Save(AiSession session)
	{
		if (session.Shown.Count == 0)
		{
			return;
		}
		session.Updated = DateTime.Now;
		if (session.Title.Length == 0)
		{
			string first = session.Shown.FirstOrDefault(e => e.Kind == AiChat.Kind.User)?.Text ?? "";
			session.Title = first.Length > 30 ? first.Substring(0, 30) + "…" : first;
		}
		try
		{
			Directory.CreateDirectory(Folder);
			string file = Path.Combine(Folder, session.Id + ".json"), tmp = file + ".tmp";
			File.WriteAllText(tmp, JsonConvert.SerializeObject(session, Formatting.Indented), Encoding.UTF8);
			if (File.Exists(file))
			{
				File.Delete(file);
			}
			File.Move(tmp, file);
		}
		catch (Exception ex)
		{
			FileLogger.LogWarning("[AI] 保存对话失败: " + ex.Message);
		}
	}
}
