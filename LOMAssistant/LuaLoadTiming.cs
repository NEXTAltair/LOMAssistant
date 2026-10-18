using System.Diagnostics;
using Fungus;
using HarmonyLib;

namespace LOMAssistant;

/// <summary>
/// 剧本加载计时：每段剧情开始时游戏会编译整段剧本，本 Mod 也会解析剧本（选项提示、信息窗口）、
/// 给剧本套随时存档的包装。总用时超过 200 毫秒就记一条，分开列出各部分，排查「剧情节点之间卡几秒」。
/// </summary>
public static class LuaLoadTiming
{
	private const long LogThresholdMs = 200;

	internal static long ParseMs;

	internal static long AnytimeMs;

	private static readonly Stopwatch Total = new Stopwatch();

	[HarmonyPrefix]
	[HarmonyPriority(Priority.First + 1)]
	[HarmonyPatch(typeof(LuaEnvironment), "LoadLuaFunction")]
	public static void Prefix()
	{
		ParseMs = 0;
		AnytimeMs = 0;
		Total.Restart();
	}

	[HarmonyPostfix]
	[HarmonyPriority(Priority.Last - 1)]
	[HarmonyPatch(typeof(LuaEnvironment), "LoadLuaFunction")]
	public static void Postfix(string luaString, string friendlyName)
	{
		Total.Stop();
		long total = Total.ElapsedMilliseconds;
		if (total < LogThresholdMs)
		{
			return;
		}
		long game = total - ParseMs - AnytimeMs;
		FileLogger.Log($"[性能] 加载剧本 {friendlyName}（{luaString?.Length ?? 0} 字）用时 {total} 毫秒：游戏编译 {game}，本 Mod 解析 {ParseMs}，随时存档包装 {AnytimeMs}");
	}
}
