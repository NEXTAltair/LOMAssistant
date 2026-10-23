using System;
using System.Collections.Generic;
using HarmonyLib;
using Mortal.Core;
using UnityEngine;

namespace LOMAssistant.Features.Extras;

/// <summary>
/// 锁定心相、四种性格（性情、处世、修养、道德）、阴阳、银两：咖啡伴侣窗口「数值」页这几行各有个小锁，点一下锁住，
/// 游戏里的剧情、选项、修炼等对它的增减都不生效；这一页的加减、设定照常能改。再点一下解锁。
/// 锁定状态不保存：重启游戏、读档、开新档都会自动解锁（不然读档时旧值会留在新档里）。
/// </summary>
public static class MoodLockPatch
{
	/// <summary>能上锁的数值。</summary>
	public static readonly GameStatType[] Lockable =
	{
		GameStatType.心理衛生, GameStatType.性情, GameStatType.處世, GameStatType.修養, GameStatType.道德,
		GameStatType.陰陽內功, GameStatType.銀兩,
	};

	/// <summary>被锁住的属性。</summary>
	private static readonly HashSet<GameStat> _locked = new HashSet<GameStat>();

	private static Texture2D _closed, _open;

	/// <summary>面板里手动加减、输入数值时置真：锁只拦游戏自己的变化，不拦玩家手动改。</summary>
	private static bool _manual;

	public static bool IsLocked(GameStat stat) => _locked.Contains(stat);

	public static void Toggle(GameStat stat)
	{
		if (!_locked.Remove(stat))
		{
			_locked.Add(stat);
		}
	}

	/// <summary>执行一次手动修改，这期间不拦截。</summary>
	public static void Manual(Action change)
	{
		_manual = true;
		try
		{
			change();
		}
		finally
		{
			_manual = false;
		}
	}

	// ---------- 拦截数值变化 ----------

	private static bool Allow(GameStat stat) => _locked.Count == 0 || _manual || !_locked.Contains(stat);

	[HarmonyPrefix]
	[HarmonyPatch(typeof(GameStat), "AddValue")]
	public static bool AddValue_Prefix(GameStat __instance) => Allow(__instance);

	[HarmonyPrefix]
	[HarmonyPatch(typeof(GameStat), "SetValue")]
	public static bool SetValue_Prefix(GameStat __instance) => Allow(__instance);

	[HarmonyPrefix]
	[HarmonyPatch(typeof(GameStat), "Reset")]
	public static bool Reset_Prefix(GameStat __instance) => Allow(__instance);

	// 读档和重置前先解锁，免得旧存档的数值带进新存档
	[HarmonyPrefix]
	[HarmonyPatch(typeof(PlayerStatManagerData), "ResetData")]
	public static void ResetData_Prefix() => _locked.Clear();

	[HarmonyPrefix]
	[HarmonyPatch(typeof(PlayerStatManagerData), "Load")]
	public static void Load_Prefix() => _locked.Clear();

	// ---------- 画锁（不依赖任何游戏图片或字体） ----------

	public static Texture2D Icon(bool closed)
	{
		if (_closed == null || _open == null)
		{
			_closed = DrawLock(true);
			_open = DrawLock(false);
		}
		return closed ? _closed : _open;
	}

	private static Texture2D DrawLock(bool closed)
	{
		const int n = 48;
		var pixels = new Color32[n * n];
		var white = new Color32(255, 255, 255, 255);
		float cx = 24f;
		float ringY = closed ? 26f : 32f;
		const float outer = 11f, inner = 6.5f;

		for (int y = 0; y < n; y++)
		{
			for (int x = 0; x < n; x++)
			{
				bool on = false;
				// 锁身
				if (x >= 8 && x < 40 && y >= 4 && y < 26)
				{
					on = true;
					// 钥匙孔
					float dx = x - cx, dy = y - 16f;
					if (dx * dx + dy * dy < 9f || (Mathf.Abs(dx) < 1.8f && y >= 8 && y < 16))
					{
						on = false;
					}
				}
				else
				{
					// 锁环：上半圆 + 左腿 + （锁上时）右腿
					float dx = x + 0.5f - cx, dy = y + 0.5f - ringY;
					float d = Mathf.Sqrt(dx * dx + dy * dy);
					if (dy >= 0f && d <= outer && d >= inner)
					{
						on = true;
					}
					else if (dy < 0f && y >= 24)
					{
						bool leftLeg = dx <= -inner && dx >= -outer;
						bool rightLeg = dx >= inner && dx <= outer;
						on = leftLeg || (closed && rightLeg);
					}
				}
				if (on)
				{
					pixels[y * n + x] = white;
				}
			}
		}
		var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
		tex.SetPixels32(pixels);
		tex.Apply();
		return tex;
	}
}
