using System;
using System.Globalization;
using LOMAssistant.Config;

namespace LOMAssistant;

/// <summary>日志：写 BepInEx 控制台，打开写文件时也写进 Mod 日志。（原来的 Tab 信息窗口已经去掉，只留这两个日志方法。）</summary>
public static class DicePlugin
{
	public static void LogInfo(object o)
	{
		LOMAssistantPlugin.LogInfo("[" + DateTime.Now.ToString(DateTimeFormatInfo.InvariantInfo) + "] " + o);
		if (ConfigManager.LogToFile.Value)
		{
			FileLogger.Log(o);
		}
	}

	public static void LogError(object o)
	{
		LOMAssistantPlugin.LogError("[" + DateTime.Now.ToString(DateTimeFormatInfo.InvariantInfo) + "] " + o);
		if (ConfigManager.LogToFile.Value)
		{
			FileLogger.LogError(o);
		}
	}
}
