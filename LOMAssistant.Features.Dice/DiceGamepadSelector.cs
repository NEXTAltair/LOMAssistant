using System.Collections.Generic;
using Mortal.Story;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace LOMAssistant.Features.Dice;

/// <summary>
/// 手柄自选骰子线路：骰子界面里用十字键 / 左摇杆上下切换线路（高亮），按 A 选中高亮的那条。
/// 没有高亮任何线路时按 A，仍是游戏原本的随机掷骰。
/// A 在游戏里本来就是「开始掷骰 / 下一步」，这里只是在同一帧先把结果定好，后面的流程和鼠标点线路完全一样。
/// 挂在骰子界面上，界面关闭时不运行。
/// </summary>
public class DiceGamepadSelector : MonoBehaviour
{
	private DiceMenuDialog _menu;
	private int _highlight; // 1 起算，0 表示没有高亮

	public static void Attach(DiceMenuDialog menu)
	{
		DiceGamepadSelector selector = menu.gameObject.GetComponent<DiceGamepadSelector>() ?? menu.gameObject.AddComponent<DiceGamepadSelector>();
		selector._menu = menu;
		selector._highlight = 0;
	}

	private void Update()
	{
		Gamepad pad = Gamepad.current;
		if (pad == null || _menu == null)
		{
			return;
		}
		List<int> lines = VisibleLines();
		if (lines.Count == 0)
		{
			return;
		}
		int step = 0;
		if (pad.dpad.up.wasPressedThisFrame || pad.leftStick.up.wasPressedThisFrame)
		{
			step = -1;
		}
		else if (pad.dpad.down.wasPressedThisFrame || pad.leftStick.down.wasPressedThisFrame)
		{
			step = 1;
		}
		if (step != 0)
		{
			int pos = lines.IndexOf(_highlight);
			pos = pos < 0 ? (step > 0 ? 0 : lines.Count - 1) : Mathf.Clamp(pos + step, 0, lines.Count - 1);
			Highlight(lines[pos]);
		}
		if (_highlight > 0 && lines.Contains(_highlight) && pad.buttonSouth.wasPressedThisFrame)
		{
			FileLogger.Log("[DicePatch] 手柄选中线路 " + _highlight);
			DiceManualSelectionPatch.Choose(_highlight);
			_highlight = 0;
		}
	}

	// 当前显示出来的线路编号（与鼠标点击用的编号一致：CachedButtons 下标 + 1）
	private List<int> VisibleLines()
	{
		var lines = new List<int>();
		Button[] buttons = _menu.CachedButtons;
		if (buttons == null)
		{
			return lines;
		}
		for (int i = 0; i < buttons.Length; i++)
		{
			if (buttons[i] != null && buttons[i].gameObject.activeInHierarchy)
			{
				lines.Add(i + 1);
			}
		}
		return lines;
	}

	private void Highlight(int line)
	{
		Button[] buttons = _menu.CachedButtons;
		if (_highlight > 0 && _highlight <= buttons.Length && buttons[_highlight - 1] != null)
		{
			buttons[_highlight - 1].GetComponent<DiceOption>()?.UnSelect();
		}
		_highlight = line;
		buttons[line - 1].GetComponent<DiceOption>()?.Select();
	}
}
