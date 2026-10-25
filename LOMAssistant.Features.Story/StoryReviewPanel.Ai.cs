using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using LOMAssistant.Config;
using LOMAssistant.Features.Ai;
using Mortal.Core;
using UnityEngine;

namespace LOMAssistant.Features.Story;

/// <summary>
/// 「AI」页：AI 攻略问答。没配好服务商时先显示设置（服务商、地址、模型、Key、剧透程度、测试连接），配好后显示对话。
/// AI 的回答本来就是界面语言写的，画的时候加 Verbatim 记号，不再查表翻译。
/// </summary>
public partial class StoryReviewPanel
{
	private const int AiTab = 7;

	private string _aiInput = "";
	private Vector2 _aiScroll, _aiSetupScroll;
	private bool _aiSetup;
	private string _aiTestStatus = "";
	private bool _aiTesting;
	private int _aiShownCount;

	private static readonly string[] AiExamples =
	{
		"我现在该做什么？",
		"怎么和小师妹结缘？",
		"有哪些结局？怎么达成？",
	};

	private void DrawAi(Rect r)
	{
		// AI 查资料要用整理好的剧情数据：打开这一页就开始整理（和路线图一样）
		if (!StoryAtlas.Ready && !StoryAtlas.Building && UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "Title")
		{
			StoryAtlas.Start();
		}
		if (_aiSetup || !AiChat.Configured)
		{
			DrawAiSetup(r);
		}
		else
		{
			DrawAiChat(r);
		}
	}

	private void DrawAiSetup(Rect r)
	{
		GUILayout.BeginArea(r);
		_aiSetupScroll = GUILayout.BeginScrollView(_aiSetupScroll);
		GUILayout.Label("AI 攻略设置", _heading);
		GUILayout.Label("需要自己的 API Key。Key 只存在这台电脑的配置文件里，只发给下面选的服务商；导出诊断时会隐藏。每次提问，问题和 AI 查到的游戏剧情会发给服务商，按服务商的价格计费。", _muted);
		GUILayout.Space(S(8));

		GUILayout.Label("服务商", _label);
		int current = ConfigManager.AiProvider.Value;
		string[] names = LlmProvider.All.Select(p => p.Name).ToArray();
		int picked = Pick(names, current, 3);
		if (picked != current)
		{
			LlmProvider p = LlmProvider.At(picked);
			ConfigManager.AiProvider.Value = picked;
			ConfigManager.AiBaseUrl.Value = p.BaseUrl;
			ConfigManager.AiModel.Value = p.Model;
			_aiTestStatus = "";
		}
		LlmProvider provider = AiChat.Provider;
		GUILayout.Space(S(8));

		GUILayout.BeginHorizontal();
		GUILayout.Label("API 地址", _label, GUILayout.Width(S(120)), Ctl());
		Assign(ConfigManager.AiBaseUrl, GUILayout.TextField(ConfigManager.AiBaseUrl.Value, 200, _field, Ctl()));
		GUILayout.EndHorizontal();
		GUILayout.BeginHorizontal();
		GUILayout.Label("模型", _label, GUILayout.Width(S(120)), Ctl());
		Assign(ConfigManager.AiModel, GUILayout.TextField(ConfigManager.AiModel.Value, 100, _field, Ctl()));
		GUILayout.EndHorizontal();
		if (!provider.NoKey)
		{
			GUILayout.BeginHorizontal();
			GUILayout.Label("API Key", _label, GUILayout.Width(S(120)), Ctl());
			Assign(ConfigManager.AiApiKey, GUILayout.PasswordField(ConfigManager.AiApiKey.Value, '•', 300, _field, Ctl()));
			if (provider.KeysPage.Length > 0 && GUILayout.Button("去申请 Key", _button, GUILayout.Width(TextWidth("去申请 Key", _button, S(120))), Ctl()))
			{
				Application.OpenURL(provider.KeysPage);
			}
			GUILayout.EndHorizontal();
		}
		GUILayout.Label("模型名是建议值，服务商出了新模型可以自己改。要选支持工具调用（function calling）的模型。", _muted);
		GUILayout.Space(S(8));

		GUILayout.Label("剧透程度", _label);
		int spoiler = Pick(new[] { "想知道什么说什么", "只说当前和走过的", "只给方向提示" }, Mathf.Clamp(ConfigManager.AiSpoiler.Value, 0, 2), 3);
		if (spoiler != ConfigManager.AiSpoiler.Value)
		{
			ConfigManager.AiSpoiler.Value = spoiler;
		}
		GUILayout.Space(S(12));

		GUILayout.BeginHorizontal();
		GUI.enabled = !_aiTesting && AiChat.Configured;
		if (GUILayout.Button(_aiTesting ? "正在测试…" : "测试连接", _button, GUILayout.Width(S(160)), Ctl()))
		{
			_aiTesting = true;
			_aiTestStatus = "";
			StartCoroutine(AiChat.Test(error =>
			{
				_aiTesting = false;
				_aiTestStatus = error.Length == 0 ? "<color=#2F6B3A>连上了</color>" : "<color=#A3302A>" + error + "</color>";
			}));
		}
		if (GUILayout.Button("开始提问", _primary, GUILayout.Width(S(160)), Ctl()))
		{
			_aiSetup = false;
		}
		GUI.enabled = true;
		GUILayout.Label(_aiTestStatus, MiddleRich(), Ctl());
		GUILayout.EndHorizontal();
		GUILayout.EndScrollView();
		GUILayout.EndArea();
	}

	// 一组互斥的按钮，选中的用朱砂底（GUILayout 的 SelectionGrid、Toolbar 用普通按钮样式时看不出选的是哪个）
	private int Pick(string[] labels, int current, int columns)
	{
		int picked = current;
		for (int i = 0; i < labels.Length; i++)
		{
			if (i % columns == 0)
			{
				GUILayout.BeginHorizontal();
			}
			if (GUILayout.Button(labels[i], i == current ? _primary : _button, Ctl()))
			{
				picked = i;
			}
			if (i % columns == columns - 1 || i == labels.Length - 1)
			{
				GUILayout.EndHorizontal();
			}
		}
		return picked;
	}

	private GUIStyle _middleRich;
	private float _middleRichScale;

	// 竖直居中、能显示颜色的单行字（Middle 出来的样式关了富文本）
	private GUIStyle MiddleRich()
	{
		if (_middleRich == null || _middleRichScale != _scale)
		{
			_middleRich = new GUIStyle(Middle(_rich)) { richText = true };
			_middleRichScale = _scale;
		}
		return _middleRich;
	}

	private static void Assign(ConfigElement<string> element, string value)
	{
		if (value != element.Value)
		{
			element.Value = value;
		}
	}

	private void DrawAiChat(Rect r)
	{
		float h = S(36);
		LlmProvider provider = AiChat.Provider;
		GUI.Label(new Rect(r.x, r.y, r.width - S(300), h), provider.Name + " · " + ConfigManager.AiModel.Value + (AiChat.TokensUsed > 0 ? "　" + string.Format("这次开游戏用了 {0} token", AiChat.TokensUsed) : ""), Middle(_muted));
		if (GUI.Button(new Rect(r.xMax - S(290), r.y, S(140), h), "AI 设置", _button))
		{
			_aiSetup = true;
		}
		GUI.enabled = !AiChat.Busy && AiChat.Shown.Count > 0;
		if (GUI.Button(new Rect(r.xMax - S(140), r.y, S(140), h), "清空对话", _button))
		{
			AiChat.Clear();
		}
		GUI.enabled = true;

		float inputHeight = S(76);
		var area = new Rect(r.x, r.y + h + S(8), r.width, r.height - h - S(8) - inputHeight - S(10));
		// 新消息来了滚到最下面
		if (AiChat.Shown.Count != _aiShownCount)
		{
			_aiShownCount = AiChat.Shown.Count;
			_aiScroll.y = float.MaxValue;
		}
		DragScroll("ai", area, ref _aiScroll);
		GUILayout.BeginArea(area);
		_aiScroll = GUILayout.BeginScrollView(_aiScroll);
		if (!StoryAtlas.Ready)
		{
			GUILayout.Label(StoryAtlas.Status, _muted);
		}
		if (AiChat.Shown.Count == 0)
		{
			GUILayout.Label("问剧情、攻略、条件、结局都可以，AI 会自己查游戏里的剧情数据再回答。比如：", _muted);
			foreach (string example in AiExamples)
			{
				if (GUILayout.Button(example, _button, GUILayout.Width(TextWidth(example, _button, S(200)))))
				{
					// 按界面语言发问，AI 也用这种语言答
					AiChat.Ask(TextConverter.ForDisplay(example));
				}
			}
		}
		foreach (AiChat.Entry entry in AiChat.Shown)
		{
			switch (entry.Kind)
			{
				case AiChat.Kind.User:
					GUILayout.BeginVertical(_card);
					GUILayout.Label("<b>你：</b>", _rich);
					GUILayout.Label(TextConverter.Verbatim + Escape(entry.Text), _rich);
					GUILayout.EndVertical();
					break;
				case AiChat.Kind.Assistant:
					GUILayout.BeginVertical(_card);
					GUILayout.Label(TextConverter.Verbatim + RichFromMarkdown(entry.Text), _rich);
					GUILayout.EndVertical();
					break;
				case AiChat.Kind.Activity:
					GUILayout.Label("　· " + entry.Text, _muted);
					break;
				default:
					GUILayout.Label("<color=#A3302A>" + Escape(entry.Text) + "</color>", _rich);
					break;
			}
		}
		if (AiChat.Busy)
		{
			GUILayout.Label("　AI 正在思考……", _muted);
		}
		GUILayout.EndScrollView();
		GUILayout.EndArea();

		// 输入框：回车发送，Shift+回车换行；输入法还在选字时回车交给输入法
		var box = new Rect(r.x, r.yMax - inputHeight, r.width - S(150), inputHeight);
		Event e = Event.current;
		bool send = false;
		GUI.SetNextControlName("AiInput");
		if (e.type == EventType.KeyDown && (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) && !e.shift
			&& GUI.GetNameOfFocusedControl() == "AiInput" && string.IsNullOrEmpty(Input.compositionString))
		{
			send = true;
			e.Use();
		}
		_aiInput = GUI.TextArea(box, _aiInput, 500, _field);
		var button = new Rect(r.xMax - S(140), r.yMax - inputHeight, S(140), inputHeight);
		if (AiChat.Busy)
		{
			if (GUI.Button(button, "停止", _button))
			{
				AiChat.Stop();
			}
		}
		else if (GUI.Button(button, "发送", _primary) || send)
		{
			string question = _aiInput.Trim();
			if (question.Length > 0)
			{
				_aiInput = "";
				AiChat.Ask(question);
			}
		}
	}

	private static string Escape(string text) => (text ?? "").Replace("<", "‹").Replace(">", "›");

	private static readonly Regex Bold = new Regex(@"\*\*(.+?)\*\*");
	private static readonly Regex Heading = new Regex(@"^#{1,6}\s*(.+)$", RegexOptions.Multiline);

	// 模型常用 Markdown：粗体、标题换成富文本粗体，列表符号照留，其余尖括号换掉免得被当成标签
	private static string RichFromMarkdown(string text)
	{
		string t = Escape(text).Replace("\r", "");
		t = Heading.Replace(t, "<b>$1</b>");
		t = Bold.Replace(t, "<b>$1</b>");
		t = Regex.Replace(t, @"^\s*[-*]\s+", "· ", RegexOptions.Multiline);
		return t.Replace("`", "");
	}

	// 一个界面物体下面所有显示着的字（普通文字和 TextMeshPro），去掉富文本、转简体，连成一句
	private static string TextsUnder(Component root)
	{
		var parts = root.GetComponentsInChildren<UnityEngine.UI.Text>().Select(t => t.text)
			.Concat(root.GetComponentsInChildren<TMPro.TMP_Text>().Select(t => t.text))
			.Select(t => TextConverter.ToSimplified(RichText.Strip(t ?? "")).Replace("\n", " ").Trim())
			.Where(t => t.Length > 0).Distinct().ToArray();
		return string.Join(" ", parts);
	}

	/// <summary>屏幕上正显示的对白、选项、掷骰：直接读游戏界面上的字。</summary>
	private static string OnScreen()
	{
		var sb = new StringBuilder();
		try
		{
			foreach (Fungus.SayDialog say in Object.FindObjectsOfType<Fungus.SayDialog>())
			{
				if (!say.isActiveAndEnabled)
				{
					continue;
				}
				string line = string.Join("：", say.GetComponentsInChildren<UnityEngine.UI.Text>()
					.Where(t => t.name == "NameText" || t.name == "StoryText").OrderBy(t => t.name == "StoryText")
					.Select(t => TextConverter.ToSimplified(RichText.Strip(t.text ?? "")).Replace("\n", " ").Trim()).Where(t => t.Length > 0).ToArray());
				if (line.Length > 0)
				{
					sb.Append("对白：").Append(line).Append('\n');
				}
			}
			foreach (Fungus.MenuDialog menu in Object.FindObjectsOfType<Fungus.MenuDialog>())
			{
				if (!menu.isActiveAndEnabled)
				{
					continue;
				}
				var options = menu.GetComponentsInChildren<UnityEngine.UI.Button>().Select(TextsUnder).Where(t => t.Length > 0).ToList();
				if (options.Count > 0)
				{
					sb.Append("正在选的选项：").Append(string.Join(" / ", options.Select(o => "「" + o + "」").ToArray())).Append('\n');
				}
			}
			foreach (Mortal.Story.DiceMenuDialog dice in Object.FindObjectsOfType<Mortal.Story.DiceMenuDialog>())
			{
				if (!dice.isActiveAndEnabled)
				{
					continue;
				}
				string header = TextConverter.ToSimplified(RichText.Strip(HarmonyLib.Traverse.Create(dice).Field("_header").GetValue<string>() ?? "")).Trim();
				var lines = dice.GetComponentsInChildren<Mortal.Story.DiceOption>().Select(TextsUnder).Where(t => t.Length > 0).ToList();
				sb.Append("正在掷骰").Append(header.Length > 0 ? "「" + header + "」" : "").Append("，各档：").Append(string.Join(" / ", lines.Select(o => "「" + o + "」").ToArray())).Append('\n');
			}
		}
		catch (System.Exception ex)
		{
			FileLogger.LogWarning("[AI] 读取屏幕内容失败: " + ex.Message);
		}
		if (sb.Length > 0 && (OptionInfoPatch.LastScriptKey ?? "").Length > 0)
		{
			sb.Append("这段剧情的剧本：").Append(OptionInfoPatch.LastScriptKey).Append("（用 script 查它，按选项文字对上分岔）\n");
		}
		return sb.ToString();
	}

	/// <summary>给 AI 的 player_state 工具：玩家现在的进度、数值、好感（旗标、书籍、物品太多，不放）。</summary>
	internal static string AiPlayerState()
	{
		PlayerStatManagerData player = PlayerStatManagerData.Instance;
		string scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
		if (player == null || scene == "Title")
		{
			return "玩家在标题画面，还没读档进游戏。";
		}
		var sb = new StringBuilder();
		sb.Append("游戏日期：").Append(ReferenceEquals(player.GameTime, null) ? "不明" : StoryAtlas.DateText(player.GameTime)).Append('\n');
		sb.Append("现在在：").Append(scene == "Story" ? "剧情里" : scene == "Free" ? "养成地图上" : scene == "Combat" || scene == "Battle" ? "战斗中" : scene).Append('\n');
		string story = player.CurrentStoryScript ?? "";
		if (story.Length > 0)
		{
			sb.Append("最近的剧本：").Append(story);
			if (StoryAtlas.Owner.TryGetValue(story, out StoryMission owner))
			{
				sb.Append("（任务 ").Append(owner.Key).Append("「").Append(AiKnowledge.Clean(owner.Title)).Append("」）");
			}
			sb.Append('\n');
		}
		if (DataHelper.GetMainMission(out MissionCheckData main, out string mainTitle, out string mainDesc))
		{
			MissionManagerData missions = MissionManagerData.Instance;
			bool ready = missions != null && missions.MainMissionStart && missions.MainCheckData == main && !string.IsNullOrEmpty(main.StartScript);
			sb.Append("主线：").Append(AiKnowledge.Clean(mainTitle).Replace("\n", " ")).Append(ready ? "（条件已满足，去 " + TextConverter.ToSimplified(main.Position.ToString()) + " 就会触发）" : "").Append("：").Append(AiKnowledge.Clean(mainDesc).Replace("\n", " ")).Append('\n');
		}
		var active = MissionManagerData.Instance?.ActiveSubMissions;
		var subs = DataHelper.GetMissions();
		if (subs.Count > 0)
		{
			sb.Append("进行中的支线：\n");
			foreach (var pair in subs)
			{
				bool ready = active != null && active.Values.Contains(pair.Key) && !string.IsNullOrEmpty(pair.Key.StartScript);
				sb.Append("- ").Append(AiKnowledge.Clean(pair.Value.Item1).Replace("\n", " ")).Append(ready ? "（可以触发）" : "").Append("：").Append(AiKnowledge.Clean(pair.Value.Item2).Replace("\n", " ")).Append('\n');
			}
		}
		string onScreen = OnScreen();
		if (onScreen.Length > 0)
		{
			sb.Append("【眼前】（屏幕上正显示的，玩家问「这里」「这个选项」「这次掷骰」指的就是它）\n").Append(onScreen);
		}
		var lover = StoryRomance.All.FirstOrDefault(StoryRomance.IsLover);
		sb.Append("结缘对象：").Append(lover != null ? StoryRomance.Name(lover) : "还没有").Append('\n');
		var skip = new HashSet<string> { "旗标", "书籍", "物品", "称号", "功法修炼" };
		foreach (var (title, rows) in BuildStatSections(player))
		{
			if (skip.Contains(title))
			{
				continue;
			}
			var parts = new List<string>();
			foreach (StatRow row in rows)
			{
				if (row.NoValue || row.Get == null)
				{
					continue;
				}
				int value = row.Get();
				// 好感、天赋只列有数的
				if ((title == "好感" || title == "天赋") && value == 0)
				{
					continue;
				}
				parts.Add(AiKnowledge.Clean(row.Name) + " " + value + (row.Of > 0 ? "/" + row.Of : ""));
			}
			if (parts.Count > 0)
			{
				sb.Append(title).Append("：").Append(string.Join("，", parts.ToArray())).Append('\n');
			}
		}
		return sb.ToString();
	}
}
