using System.Collections.Generic;
using LOMAssistant.Config;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace LOMAssistant;

/// <summary>
/// 隐藏界面看 CG（类似 galgame 的右键隐藏）：剧情里按鼠标右键（或配置的热键）隐藏对话框、标题等文字界面和本 Mod 的窗口，
/// 只留背景、CG 和立绘，方便截图；再按右键（或热键）恢复，其它按键（如 F12 截图）不会把界面叫回来，隐藏期间的点击也不会推进对话。
/// 判断哪些界面要藏：画布里有文字或按钮的藏，只有图片的（背景、CG、立绘）留着。
/// </summary>
public class UiHider : MonoBehaviour
{
	public static bool Active { get; private set; }

	/// <summary>恢复之后，触发恢复的那次按键松开之前都不交给游戏。</summary>
	public static bool Swallowing => Active || _swallowUntilRelease;

	private static bool _swallowUntilRelease;
	private static UiHider _instance;
	private readonly List<Canvas> _hidden = new List<Canvas>();
	private int _hiddenFrame;

	public static void Ensure(GameObject host)
	{
		if (_instance == null)
		{
			_instance = host.GetComponent<UiHider>() ?? host.AddComponent<UiHider>();
			InputGuard.Ensure();
		}
	}

	/// <summary>回想（对话记录）开着的时候右键不隐藏界面。</summary>
	private static bool LogOpen()
	{
		Mortal.Story.StoryManager story = Mortal.Story.StoryManager.Instance;
		return story != null && story.GetFieldValue<bool>("_logOpen");
	}

	private static bool InStory()
	{
		return SceneManager.GetActiveScene().name == "Story";
	}

	/// <summary>这次右键是拿来隐藏界面的：游戏的输入回调不要再处理它（右键先于本组件的 Update 送到游戏）。</summary>
	public static bool ClaimsRightClick(InputControl control)
	{
		return _instance != null && !Active && ConfigManager.EnableRightClickHideUI.Value && InStory()
			&& control != null && Mouse.current != null && control == Mouse.current.rightButton && !EventPicker.IsWaiting && !LogOpen() && !InputGuard.ModWindowOpen;
	}

	private void Update()
	{
		if (_swallowUntilRelease && !AnyHeld())
		{
			_swallowUntilRelease = false;
		}
		if (Active)
		{
			if (!InStory())
			{
				Restore();
			}
			// 只有右键（或热键）恢复：F12 截图等其它按键不能把界面叫回来
			else if (Time.frameCount > _hiddenFrame && TogglePressed())
			{
				Restore();
				_swallowUntilRelease = true;
			}
			return;
		}
		// 开着 Mod 的窗口（比如 F9、剧情回顾）时右键不隐藏界面，免得想关窗口却把界面全藏了
		if (!InStory() || EventPicker.IsWaiting || TextInputFocused() || LogOpen() || InputGuard.ModWindowOpen)
		{
			return;
		}
		if (TogglePressed() && !InputGuard.PointerOverModWindow)
		{
			Hide();
		}
	}

	private static bool TogglePressed()
	{
		Mouse mouse = Mouse.current;
		bool rightClick = ConfigManager.EnableRightClickHideUI.Value && mouse != null && mouse.rightButton.wasPressedThisFrame;
		bool key = InputGuard.HotkeyDown(ConfigManager.HideUIKey.Value);
		return rightClick || key;
	}

	private static bool TextInputFocused()
	{
		return GUIUtility.keyboardControl != 0;
	}

	private void Hide()
	{
		_hidden.Clear();
		foreach (Canvas canvas in FindObjectsOfType<Canvas>())
		{
			if (canvas == null || !canvas.enabled || !canvas.gameObject.activeInHierarchy || canvas.name.StartsWith("LOMAssistant"))
			{
				continue;
			}
			// 只管最外层画布下面、自己这一层直接显示的内容：里面有文字或按钮就藏
			if (HasTextOrControls(canvas))
			{
				canvas.enabled = false;
				_hidden.Add(canvas);
			}
		}
		Active = true;
		_hiddenFrame = Time.frameCount;
		FileLogger.Log("[隐藏界面] 隐藏 " + _hidden.Count + " 个画布：" + string.Join("、", _hidden.ConvertAll(c => c.name).ToArray()));
	}

	private static bool HasTextOrControls(Canvas canvas)
	{
		foreach (Graphic g in canvas.GetComponentsInChildren<Graphic>(false))
		{
			if (!g.enabled || g.canvas != canvas)
			{
				continue;
			}
			if ((g is Text t && !string.IsNullOrEmpty(t.text)) || (g is TMP_Text tmp && !string.IsNullOrEmpty(tmp.text)))
			{
				return true;
			}
		}
		foreach (Selectable s in canvas.GetComponentsInChildren<Selectable>(false))
		{
			if (s.enabled && s.GetComponentInParent<Canvas>() == canvas)
			{
				return true;
			}
		}
		return false;
	}

	private void Restore()
	{
		foreach (Canvas canvas in _hidden)
		{
			if (canvas != null)
			{
				canvas.enabled = true;
			}
		}
		_hidden.Clear();
		Active = false;
	}

	private static bool AnyHeld()
	{
		if (Keyboard.current != null && Keyboard.current.anyKey.isPressed) return true;
		Mouse m = Mouse.current;
		if (m != null && (m.leftButton.isPressed || m.rightButton.isPressed || m.middleButton.isPressed)) return true;
		Gamepad pad = Gamepad.current;
		if (pad != null)
		{
			foreach (var b in new[] { pad.buttonSouth, pad.buttonEast, pad.buttonWest, pad.buttonNorth, pad.startButton, pad.selectButton, pad.leftShoulder, pad.rightShoulder, pad.dpad.up, pad.dpad.down, pad.dpad.left, pad.dpad.right })
			{
				if (b.isPressed) return true;
			}
		}
		return false;
	}
}
