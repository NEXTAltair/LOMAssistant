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
