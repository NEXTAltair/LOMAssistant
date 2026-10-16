using LOMAssistant.Config;
using UnityEngine;

namespace LOMAssistant.Features.Extras;

/// <summary>
/// 游戏倍速：在游戏自己设定的 Time.timeScale 上再乘一个倍率。
/// 游戏会自己改 timeScale（暂停=0、剧情快进=10、战斗加速等），所以不能直接写死，
/// 而是每帧检查：值跟我们上次写的不一样，说明是游戏改的，记下来当作基准再乘倍率。
/// 暂停（0）乘完还是 0，不受影响。
/// </summary>
public static class GameSpeed
{
	public static readonly float[] Options = { 1f, 2f, 3f, 5f };

	private static float _base = 1f;
	private static float _applied = -1f;

	public static void Tick()
	{
		float mult = Mathf.Clamp(ConfigManager.GameSpeed.Value, 0.1f, 10f);
		float current = Time.timeScale;
		if (!Mathf.Approximately(current, _applied))
		{
			_base = current; // 游戏自己改的
		}
		float target = _base * mult;
		if (!Mathf.Approximately(current, target))
		{
			Time.timeScale = target;
		}
		_applied = target;
	}

	public static void Set(float mult)
	{
		ConfigManager.GameSpeed.Value = mult;
		ConfigManager.SetConfigValue(ConfigManager.GameSpeed, mult);
		FileLogger.Log($"游戏倍速 -> {mult}x");
	}
}
