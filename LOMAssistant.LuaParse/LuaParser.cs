using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace LOMAssistant.LuaParse;

public class LuaParser
{
	private static readonly Lazy<LuaParser> instance = new Lazy<LuaParser>(() => new LuaParser());

	private static readonly Regex indexMarkerRegex = new Regex("^\\s*--index:(O(_\\w+)+)", RegexOptions.Multiline);

	private static readonly Regex endBlockRegex = new Regex("\\b(elseif|end)");

	private static readonly Regex switchRegex = new Regex("checkpointmanager\\.Switch\\(([^)]*)\\)");

	private static readonly Regex positionRegex = new Regex("checkpointmanager\\.Position\\(([^)]*)\\)");

	private static readonly Regex modifyRegex = new Regex("\\s*statmodifymanager\\.(?!GetDisplayTime)\\w+\\(.*?\\)");

	private static readonly Regex battleScenesRegex = new Regex("luamanager\\.ChangeScene\\(\"(Combat|Battle)\"");

	private static readonly Regex scriptActionsRegex = new Regex("luamanager\\.SetNextScript");

	public Dictionary<string, List<Action>> OptionResult;

	// 被锁的选项：脚本里写成「条件 and "正常选项" or "~锁住时显示的文字"」
	private static readonly Regex lockedOptionRegex = new Regex("(\\w+)\\s+and\\s+\"([^\"]*)\"\\s+or\\s+\"~(O_\\w+)\"");

	private static readonly Regex optionKeyRegex = new Regex("O_\\w+");

	/// <summary>锁住时显示的文字的 key → (解锁条件名, 解锁后的选项 key)。每次载入剧本时重建。</summary>
	public readonly Dictionary<string, (string Condition, string Unlocked)> LockedOptions = new Dictionary<string, (string, string)>();

	/// <summary>选项 key → 这个选项里「满足某个分支判断才跳到另一段剧情」的位置（分支判断名, 第几支，0 表示 else）。</summary>
	public readonly Dictionary<string, List<(string Switch, int Branch)>> OptionJumps = new Dictionary<string, List<(string, int)>>();

	private static readonly Regex switchAssignRegex = new Regex("^local\\s+(\\w+)\\s*=\\s*checkpointmanager\\.Switch\\(\"([^\"]+)\"\\)");

	private static readonly Regex branchRegex = new Regex("^(if|elseif)\\s+(\\w+)\\s*==\\s*(\\d+)\\s+then");

	// 逐行跟踪 if/elseif/else/end，找出写在某个分支判断里的「跳到下一段剧本」
	private static List<(string, int)> FindConditionalJumps(string block)
	{
		var result = new List<(string, int)>();
		var switches = new Dictionary<string, string>();
		var stack = new List<(string Var, int Branch)>(); // Var 为 null 表示不是分支判断的块（普通 if / for 等）
		foreach (string raw in block.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None))
		{
			string line = raw.Trim();
			if (line.StartsWith("--"))
			{
				continue;
			}
			Match assign = switchAssignRegex.Match(line);
			if (assign.Success)
			{
				switches[assign.Groups[1].Value] = assign.Groups[2].Value;
				continue;
			}
			Match branch = branchRegex.Match(line);
			if (branch.Success && switches.ContainsKey(branch.Groups[2].Value))
			{
				int n = int.Parse(branch.Groups[3].Value);
				if (branch.Groups[1].Value == "if")
				{
					stack.Add((branch.Groups[2].Value, n));
				}
				else if (stack.Count > 0)
				{
					stack[stack.Count - 1] = (stack[stack.Count - 1].Var, n);
				}
				continue;
			}
			if (line.StartsWith("if ") || line.StartsWith("for ") || line.StartsWith("while ") || line.Contains("function("))
			{
				stack.Add((null, 0));
			}
			else if (line == "else" && stack.Count > 0 && stack[stack.Count - 1].Var != null)
			{
				stack[stack.Count - 1] = (stack[stack.Count - 1].Var, 0);
			}
			else if (line == "end" || line.StartsWith("end)") || line.StartsWith("end "))
			{
				if (stack.Count > 0)
				{
					stack.RemoveAt(stack.Count - 1);
				}
			}
			else if (line.Contains("luamanager.SetNextScript"))
			{
				for (int i = stack.Count - 1; i >= 0; i--)
				{
					if (stack[i].Var != null)
					{
						var hit = (switches[stack[i].Var], stack[i].Branch);
						if (!result.Contains(hit))
						{
							result.Add(hit);
						}
						break;
					}
				}
			}
		}
		return result;
	}

	public void ExtractLockedOptions(string luaScript)
	{
		// 对话里也会零碎执行一些小段 Lua，那些不含选项菜单，不能把当前剧本的记录清掉
		if (luaScript == null || !luaScript.Contains("setmenudialog"))
		{
			return;
		}
		LockedOptions.Clear();
		foreach (Match m in lockedOptionRegex.Matches(luaScript ?? ""))
		{
			Match unlocked = optionKeyRegex.Match(m.Groups[2].Value);
			LockedOptions[m.Groups[3].Value] = (m.Groups[1].Value, unlocked.Success ? unlocked.Value : null);
		}
	}

	private Dictionary<string, List<Action>> luaModification;

	private Dictionary<string, List<string>> luaOptions;

	private Dictionary<string, List<string>> luaSwitchCheck;

	private Dictionary<string, List<string>> luaPositionWork;

	public static LuaParser Instance => instance.Value;

	private LuaParser()
	{
		OptionResult = new Dictionary<string, List<Action>>();
		luaModification = new Dictionary<string, List<Action>>();
		luaOptions = new Dictionary<string, List<string>>();
		luaSwitchCheck = new Dictionary<string, List<string>>();
		luaPositionWork = new Dictionary<string, List<string>>();
	}

	public void ClearCache()
	{
		OptionResult.Clear();
		OptionJumps.Clear();
		luaOptions.Clear();
		luaModification.Clear();
		luaSwitchCheck.Clear();
		luaPositionWork.Clear();
	}

	public Dictionary<string, List<Action>> ExtractOptionActionsFromLua(string friendlyName, string luaScript = "")
	{
		Dictionary<string, List<Action>> dictionary = new Dictionary<string, List<Action>>();
		if (luaOptions.TryGetValue(friendlyName, out var value))
		{
			foreach (string item in value)
			{
				dictionary.Add(item, OptionResult[item]);
			}
			return dictionary;
		}
		luaOptions[friendlyName] = new List<string>();
		MatchCollection matchCollection = indexMarkerRegex.Matches(luaScript);
		for (int i = 0; i < matchCollection.Count; i++)
		{
			string value2 = matchCollection[i].Groups[1].Value;
			luaOptions[friendlyName].Add(value2);
			if (OptionResult.ContainsKey(value2))
			{
				dictionary[value2] = OptionResult[value2];
				continue;
			}
			int num = matchCollection[i].Index + matchCollection[i].Length;
			int num2 = luaScript.Length;
			if (i + 1 < matchCollection.Count)
			{
				num2 = Math.Min(num2, matchCollection[i + 1].Index);
			}
			else
			{
				Match match = endBlockRegex.Match(luaScript.Substring(num));
				if (match.Success)
				{
					num2 = num + match.Index;
				}
			}
			string blockContent = luaScript.Substring(num, num2 - num);
			List<Action> list = new List<Action>();
			AddActionsToList(blockContent, modifyRegex, ActionType.ModifyAction, list);
			AddActionsToList(blockContent, battleScenesRegex, ActionType.BattleScene, list);
			dictionary[value2] = list;
			OptionResult[value2] = list;
			OptionJumps[value2] = FindConditionalJumps(blockContent);
		}
		return dictionary;
	}

	private void AddActionsToList(string blockContent, Regex regex, ActionType actionType, List<Action> actionList)
	{
		string[] array = blockContent.Split(new string[3] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
		bool isConditional = false;
		string[] array2 = array;
		foreach (string text in array2)
		{
			string text2 = text.Trim();
			if (text2.StartsWith("if") || text2.StartsWith("elseif"))
			{
				isConditional = true;
			}
			else if (text2.StartsWith("end"))
			{
				isConditional = false;
			}
			foreach (Match item in regex.Matches(text))
			{
				actionList.Add(new Action(actionType, item.Value.Trim(), isConditional));
			}
		}
	}

	public List<string> ExtractSwitchCheckFromLua(string friendlyName, string luaScript = "")
	{
		if (luaSwitchCheck.ContainsKey(friendlyName))
		{
			return luaSwitchCheck[friendlyName];
		}
		List<string> list = ExtractMatches(luaScript, switchRegex);
		luaSwitchCheck[friendlyName] = list;
		return list;
	}

	public List<string> ExtractPositionWorkFromLua(string friendlyName, string luaScript = "")
	{
		if (luaPositionWork.ContainsKey(friendlyName))
		{
			return luaPositionWork[friendlyName];
		}
		List<string> list = ExtractMatches(luaScript, positionRegex);
		luaPositionWork[friendlyName] = list;
		return list;
	}

	private List<string> ExtractMatches(string luaScript, Regex regex)
	{
		List<string> list = new List<string>();
		foreach (Match item2 in regex.Matches(luaScript))
		{
			string item = item2.Groups[1].Value.Trim().Trim('"');
			if (!list.Contains(item))
			{
				list.Add(item);
			}
		}
		return list;
	}

	public List<Action> ExtractActionsFromLua(string luaScriptName)
	{
		if (luaModification.ContainsKey(luaScriptName))
		{
			return luaModification[luaScriptName];
		}
		string[] array = DataHelper.LoadLuaScript(luaScriptName).Split(new string[3] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
		List<Action> list = new List<Action>();
		bool isConditional = false;
		string[] array2 = array;
		for (int i = 0; i < array2.Length; i++)
		{
			string text = array2[i].Trim();
			if (text.StartsWith("if") || text.StartsWith("elseif"))
			{
				isConditional = true;
			}
			else if (text.StartsWith("end"))
			{
				isConditional = false;
			}
			foreach (Match item in modifyRegex.Matches(text))
			{
				list.Add(new Action(ActionType.ModifyAction, item.Value, isConditional));
			}
			foreach (Match item2 in battleScenesRegex.Matches(text))
			{
				list.Add(new Action(ActionType.BattleScene, item2.Value, isConditional));
			}
		}
		luaModification[luaScriptName] = list;
		return list;
	}

	public static string Description(List<Action> actions)
	{
		List<string> list = (from action in actions
			where !action.IsConditional
			select action.Description into description
			where !string.IsNullOrEmpty(description)
			select description).Distinct().ToList();
		List<string> list2 = (from action in actions
			where action.IsConditional
			select action.Description into description
			where !string.IsNullOrEmpty(description)
			select description).Distinct().ToList();
		List<string> list3 = new List<string>();
		if (list.Count > 0)
		{
			list3.Add(string.Join("|", list));
		}
		if (list2.Count > 0)
		{
			list3.Add("后续: " + string.Join(" 或 ", list2));
		}
		return string.Join(" | ", list3);
	}
}
