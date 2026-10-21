using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using LOMAssistant.Config;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace LOMAssistant;

/// <summary>
/// 更新内容和新版本检查：更新内容来自嵌在 DLL 里的 CHANGELOG.md；新版本看 GitHub 最新发布的版本号，
/// 每次启动只发这一个请求（几 KB）；有新版时再取一次最近的发布列表，把还没装的每一版说明都加在最前面。在后台做，失败了不提示，只记日志。
/// </summary>
public static class UpdateInfo
{
	private const string Repo = "kevinnie2003/LOMAssistant";
	public const string DownloadPage = "https://github.com/" + Repo + "/releases/latest";
	public const string QuarkPage = "https://pan.quark.cn/s/e78c41abcebc";

	public sealed class Entry
	{
		public Version Version;
		public string Title; // 「2.4.2（2026-10-21）」
		public string Body; // 已转成 IMGUI 富文本
	}

	// 要在 Entries 之前：静态字段按书写顺序初始化，Entries 一初始化就要用它
	private static readonly Regex Heading = new Regex(@"^##\s+v?([0-9]+\.[0-9]+(?:\.[0-9]+)?)\s*(.*)$");

	public static readonly Version Current = Trim(typeof(UpdateInfo).Assembly.GetName().Version);

	/// <summary>GitHub 上的最新版本；还没查到（或没联网、关了检查）时为 null。</summary>
	public static Version Latest { get; private set; }

	public static bool HasNewer => Latest != null && Latest > Current;

	public static List<Entry> Entries { get; private set; } = Parse(ReadEmbedded());

	/// <summary>这次启动时是刚更新过（上次记下的版本和现在不一样）：窗口题头提示一下，这次启动内一直显示。</summary>
	public static readonly bool JustUpdated = ConfigManager.LastSeenVersion.Value != Current.ToString();

	public static void MarkSeen()
	{
		ConfigManager.LastSeenVersion.Value = Current.ToString();
	}

	public static IEnumerator Check()
	{
		if (!ConfigManager.EnableUpdateCheck.Value)
		{
			yield break;
		}
		string json = null;
		yield return Get("https://api.github.com/repos/" + Repo + "/releases/latest", text => json = text);
		JObject latestRelease = null;
		try
		{
			latestRelease = json == null ? null : JObject.Parse(json);
			if (ReleaseVersion(latestRelease) is Version latest)
			{
				Latest = latest;
				FileLogger.Log($"[更新检查] 当前 {Current}，最新 {Latest}");
			}
		}
		catch (Exception ex)
		{
			FileLogger.Log("[更新检查] 读取发布信息失败: " + ex.Message);
		}
		if (!HasNewer)
		{
			yield break;
		}
		// 有新版时再取最近几次发布：中间漏装的每一版都列出来（平时不发这个请求）
		string list = null;
		yield return Get("https://api.github.com/repos/" + Repo + "/releases?per_page=20", text => list = text);
		var releases = new List<JToken>();
		try
		{
			if (list != null)
			{
				releases.AddRange(JArray.Parse(list));
			}
		}
		catch (Exception ex)
		{
			FileLogger.Log("[更新检查] 读取发布列表失败: " + ex.Message);
		}
		if (releases.Count == 0)
		{
			releases.Add(latestRelease);
		}
		var added = new List<Entry>();
		foreach (JToken release in releases)
		{
			try
			{
				Version version = ReleaseVersion(release);
				if (version != null && version > Current && (bool?)release["draft"] != true && (bool?)release["prerelease"] != true
					&& !Entries.Exists(e => e.Version == version) && !added.Exists(e => e.Version == version))
				{
					added.Add(FromRelease(release, version));
				}
			}
			catch (Exception ex)
			{
				FileLogger.Log("[更新检查] 读取一次发布失败: " + ex.Message);
			}
		}
		if (added.Count > 0)
		{
			added.AddRange(Entries);
			added.Sort((a, b) => b.Version.CompareTo(a.Version));
			Entries = added;
		}
	}

	private static Version ReleaseVersion(JToken release)
	{
		string tag = ((string)release?["tag_name"] ?? "").TrimStart('v', 'V');
		return Version.TryParse(tag, out Version v) ? Trim(v) : null;
	}

	/// <summary>内置 CHANGELOG 里还没有的版本：用那次发布的说明，去掉开头的标题和末尾安装说明那些固定的话。</summary>
	private static Entry FromRelease(JToken release, Version version)
	{
		string body = ((string)release["body"] ?? "").Replace("\r", "");
		int boilerplate = body.IndexOf("**安装 / 更新**", StringComparison.Ordinal);
		if (boilerplate >= 0)
		{
			body = body.Substring(0, boilerplate);
		}
		body = Regex.Replace(body, @"^\*\*咖啡伴侣[^\n]*\n", "");
		string date = DateTime.TryParse((string)release["published_at"], out DateTime published) ? "（" + published.ToString("yyyy-MM-dd") + "）" : "";
		List<Entry> parsed = Parse("## " + version + date + "\n" + body);
		return parsed.Count > 0 ? parsed[0] : new Entry { Version = version, Title = version + date, Body = "" };
	}

	private static IEnumerator Get(string url, Action<string> done)
	{
		UnityWebRequest request = UnityWebRequest.Get(url);
		request.timeout = 15;
		request.SetRequestHeader("Accept", "application/vnd.github+json");
		request.SetRequestHeader("User-Agent", "LOMAssistant/" + Current);
		yield return request.SendWebRequest();
		try
		{
			if (request.result == UnityWebRequest.Result.Success)
			{
				done(request.downloadHandler.text);
			}
			else
			{
				FileLogger.Log("[更新检查] 没查到（" + request.error + "）：" + url);
			}
		}
		finally
		{
			request.Dispose();
		}
	}

	private static Version Trim(Version v) => new Version(v.Major, v.Minor, Math.Max(0, v.Build));

	private static string ReadEmbedded()
	{
		try
		{
			using Stream stream = typeof(UpdateInfo).Assembly.GetManifestResourceStream("LOMAssistant.CHANGELOG.md");
			using var reader = new StreamReader(stream);
			return reader.ReadToEnd();
		}
		catch (Exception ex)
		{
			FileLogger.LogWarning("[更新内容] 读取内置 CHANGELOG 失败: " + ex.Message);
			return "";
		}
	}

	/// <summary>按「## 版本（日期）」分段；正文的 Markdown 转成富文本：**加粗**、链接只留文字、列表换成圆点。</summary>
	private static List<Entry> Parse(string text)
	{
		var entries = new List<Entry>();
		Entry entry = null;
		var body = new List<string>();
		void Flush()
		{
			if (entry != null)
			{
				entry.Body = string.Join("\n", body).Trim('\n');
				entries.Add(entry);
			}
			body.Clear();
		}
		foreach (string raw in text.Replace("\r", "").Split('\n'))
		{
			Match m = Heading.Match(raw);
			if (m.Success && Version.TryParse(m.Groups[1].Value, out Version v))
			{
				Flush();
				entry = new Entry { Version = Trim(v), Title = m.Groups[1].Value + m.Groups[2].Value.Trim() };
				continue;
			}
			if (entry == null || raw.StartsWith("#"))
			{
				continue;
			}
			string line = raw.TrimEnd();
			if (line.Length == 0)
			{
				if (body.Count > 0 && body[body.Count - 1].Length > 0)
				{
					body.Add("");
				}
				continue;
			}
			Match bullet = Regex.Match(line, @"^(\s*)[-*]\s+(.*)$");
			if (bullet.Success)
			{
				line = (bullet.Groups[1].Length >= 2 ? "　　◦ " : "• ") + bullet.Groups[2].Value;
			}
			line = Regex.Replace(line, @"\[([^\]]+)\]\([^)]+\)", "$1");
			line = Regex.Replace(line, @"\*\*(.+?)\*\*", "<b>$1</b>");
			// 空格换成不断行空格：游戏的文字排版遇到空格优先在空格处折行，中文句子里夹个「D 盘」就会在半截折断
			body.Add(line.Replace("`", "").Replace(' ', ' '));
		}
		Flush();
		return entries;
	}
}
