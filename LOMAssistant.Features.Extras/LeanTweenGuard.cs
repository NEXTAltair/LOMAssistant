using HarmonyLib;

namespace LOMAssistant.Features.Extras;

/// <summary>
/// LeanTween（立绘移动、淡入淡出用的补间库）默认只有 400 个格子，游戏没有调大。随时存档恢复时 30 倍速快进，
/// 每句台词都给立绘做变暗、移动，补间还没播完就又加新的，长的存档会把 400 格用完：之后新建补间返回 null，
/// 剧本里显示立绘时报 NullReferenceException，Lua 协程停掉，恢复卡住。Install 在游戏第一次用到之前先建 4000 格。
///
/// 另外修它的越界：分到最后一个格子（第 400 个）时 options() 把 tweenMaxSearch 设成 400，
/// cancel、pause 等按「i &lt;= tweenMaxSearch」遍历，就读到 tweens[400] 报 IndexOutOfRangeException。
/// 每帧的 update() 会把它改回来，所以只有同一帧里先分到最后一格、再 cancel 才出错；随时存档恢复时 30 倍速快进、
/// 一帧里换很多次立绘，容易撞上。报错发生在剧本里显示立绘时，那段剧本的 Lua 协程直接停掉，剧情就卡住不动。
/// </summary>
public static class LeanTweenGuard
{
	private static readonly AccessTools.FieldRef<int> MaxSearch = AccessTools.StaticFieldRefAccess<int>(AccessTools.Field(typeof(LeanTween), "tweenMaxSearch"));
	private static readonly AccessTools.FieldRef<int> MaxTweens = AccessTools.StaticFieldRefAccess<int>(AccessTools.Field(typeof(LeanTween), "maxTweens"));

	private const int Capacity = 4000;

	public static void Install()
	{
		// 已经建好（别的东西先用过 LeanTween）就改不了大小，只记一笔
		if (AccessTools.Field(typeof(LeanTween), "tweens").GetValue(null) != null)
		{
			FileLogger.LogWarning("[补间] LeanTween 已经初始化，格子数 " + LeanTween.maxSimulataneousTweens + "，没能调大");
			return;
		}
		try
		{
			LeanTween.init(Capacity);
			FileLogger.Log("[补间] LeanTween 格子数调到 " + LeanTween.maxSimulataneousTweens);
		}
		catch (System.Exception ex)
		{
			FileLogger.LogWarning("[补间] 调大 LeanTween 格子数失败: " + ex.Message);
		}
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(LeanTween), nameof(LeanTween.options), new System.Type[0])]
	public static void Options_Postfix()
	{
		if (MaxSearch() >= MaxTweens())
		{
			MaxSearch() = MaxTweens() - 1;
		}
	}
}
