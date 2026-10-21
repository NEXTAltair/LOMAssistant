using System;
using BepInEx;

namespace LOMAssistant;

/// <summary>
/// 关掉 BepInEx 的日志窗口（黑色控制台）。BepInEx 默认开着它，新装的玩家一进游戏就会弹出来，以为出了错。
/// 启动时把 BepInEx 的这一项关掉（下次不再弹出），已经弹出来的窗口当场关掉。日志照常写进文件。
/// </summary>
internal static class BepInExConsole
{
	public static void Hide()
	{
		try
		{
			if (ConsoleManager.ConfigConsoleEnabled.Value)
			{
				ConsoleManager.ConfigConsoleEnabled.Value = false;
				FileLogger.Log("已关掉 BepInEx 日志窗口（BepInEx.cfg 里的 Logging.Console）");
			}
			ConsoleManager.DetachConsole();
		}
		catch (Exception e)
		{
			FileLogger.LogWarning("关闭 BepInEx 日志窗口失败: " + e.Message);
		}
	}
}
