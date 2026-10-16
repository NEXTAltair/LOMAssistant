using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using Mortal.Core;
using UnityEngine;
using UnityEngine.UI;

namespace LOMAssistant.Features.Extras;

/// <summary>
/// 读档界面每张自动存档卡片右侧的 ▲/▼ 按钮：▼ 切到更早的旧档，▲ 切回较新的（标题显示“｜旧档N”）。
/// 不能再切的方向会变暗、不可点，一眼就能看出有没有旧档。卡片原有的点击读取、删除都作用于当前显示的那一档。
/// </summary>
public class AutoSaveCardHistory : MonoBehaviour
{
	private const float ButtonScale = 0.45f;
	private const float DimAlpha = 0.3f;

	private AutoSaveSlotPanel _card;
	private string _category;
	private Button _newer;
	private Button _older;

	public static AutoSaveCardHistory Attach(AutoSaveSlotPanel card, string category)
	{
		AutoSaveCardHistory history = card.gameObject.GetComponent<AutoSaveCardHistory>() ?? card.gameObject.AddComponent<AutoSaveCardHistory>();
		history._card = card;
		history._category = category;
		history.CreateButtons();
		history.Refresh();
		return history;
	}

	private int CurrentIndex
	{
		get
		{
			string slot = Traverse.Create(_card).Field("_slot").GetValue<string>();
			return slot != null && slot.StartsWith(_category + "_") && int.TryParse(slot.Substring(_category.Length + 1), out int i) ? i : 0;
		}
	}

	private void CreateButtons()
	{
		if (_newer != null)
		{
			return;
		}
		Button delete = Traverse.Create(_card).Field("_deleteButton").GetValue<Button>();
		if (delete == null)
		{
			LOMAssistantPlugin.LogWarning("自动存档历史：卡片上找不到删除按钮。");
			return;
		}
		bool deleteWasActive = delete.gameObject.activeSelf;
		delete.gameObject.SetActive(true); // measured below; hidden buttons report no bounds
		_newer = CreateArrow(delete, "▲", "LOMAssistant_NewerButton", up: true);
		_older = CreateArrow(delete, "▼", "LOMAssistant_OlderButton", up: false);
		_newer.onClick.AddListener(() => Step(older: false));
		_older.onClick.AddListener(() => Step(older: true));
		delete.gameObject.SetActive(deleteWasActive);
		FileLogger.Log($"自动存档历史：{_category} 卡片已添加 ▲/▼ 按钮 {MenuButtonHelper.DescribeHierarchy(_older.transform)}");
	}

	/// <summary>A small copy of the delete button, stacked to its right (▲ on top, ▼ below).</summary>
	private static Button CreateArrow(Button delete, string label, string name, bool up)
	{
		Button arrow = MenuButtonHelper.CloneButton(delete, label, name);
		arrow.transform.localScale = delete.transform.localScale * ButtonScale;

		// Right of the delete frame, stacked around its middle (world space, so pivots don't matter).
		Rect frame = VisualBounds(delete.transform);
		Rect own = VisualBounds(arrow.transform);
		// The frame sprite (Sliced, button_menu_3) has a wide transparent margin: only about this share of its
		// box is visible frame (measured in-game). Place by the visible parts, not the boxes.
		const float visibleX = 0.54f;
		const float visibleY = 0.4f;
		float gap = frame.width * 0.03f;
		float x = frame.center.x + frame.width * visibleX / 2f + gap + own.width * visibleX / 2f;
		float y = frame.center.y + (own.height * visibleY / 2f + frame.height * 0.02f) * (up ? 1f : -1f);
		var target = new Vector2(x, y);
		arrow.transform.position += (Vector3)(target - own.center);
		if (up)
		{
			var sb = new System.Text.StringBuilder();
			var c = new Vector3[4];
			foreach (Image img in delete.GetComponentsInChildren<Image>(includeInactive: false))
			{
				img.rectTransform.GetWorldCorners(c);
				sb.Append($" [{img.name} sprite={(img.sprite != null ? img.sprite.name : "null")} a={img.color.a:0.##} x={c[0].x:0.000}-{c[2].x:0.000} pa={img.preserveAspect} type={img.type}]");
			}
			FileLogger.Log($"自动存档历史：删除按钮边框 x={frame.xMin:0.000}-{frame.xMax:0.000} y={frame.yMin:0.000}-{frame.yMax:0.000}，箭头 {own.width:0.000}x{own.height:0.000}；图片:{sb}");
		}
		if (arrow.GetComponent<CanvasGroup>() == null)
		{
			arrow.gameObject.AddComponent<CanvasGroup>();
		}
		return arrow;
	}

	/// <summary>World-space box of the sprite images under <paramref name="root"/> (text is ignored: its box is wider).</summary>
	private static Rect VisualBounds(Transform root)
	{
		float xMin = float.MaxValue, yMin = float.MaxValue, xMax = float.MinValue, yMax = float.MinValue;
		foreach (Image g in root.GetComponentsInChildren<Image>(includeInactive: false))
		{
			if (!g.enabled || g.sprite == null || g.color.a <= 0f)
			{
				continue;
			}
			Rect local = g.rectTransform.rect;
			foreach (Vector2 p in new[] { local.min, local.max, new Vector2(local.xMin, local.yMax), new Vector2(local.xMax, local.yMin) })
			{
				Vector3 c = g.rectTransform.TransformPoint(p);
				xMin = Mathf.Min(xMin, c.x);
				yMin = Mathf.Min(yMin, c.y);
				xMax = Mathf.Max(xMax, c.x);
				yMax = Mathf.Max(yMax, c.y);
			}
		}
		var corners = new Vector3[4];
		if (xMin > xMax)
		{
			((RectTransform)root).GetWorldCorners(corners);
			return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
		}
		return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
	}

	/// <summary>Indices of the history files that exist for this category (1 = most recent).</summary>
	private List<int> ExistingHistory()
	{
		var result = new List<int>();
		for (int i = 1; i <= AutoSaveHistoryPatch.MaxHistory; i++)
		{
			string file = Traverse.Create(SaveSystem.Instance).Method("GetSaveDataFile", new[] { typeof(string) })
				.GetValue<string>(AutoSaveHistoryPatch.HistorySlot(_category, i));
			if (File.Exists(file))
			{
				result.Add(i);
			}
		}
		return result;
	}

	public void Refresh()
	{
		if (_newer == null)
		{
			return;
		}
		int current = CurrentIndex;
		List<int> history = ExistingHistory();
		SetEnabled(_newer, current > 0);
		SetEnabled(_older, history.Exists(i => i > current));
	}

	private static void SetEnabled(Button button, bool enabled)
	{
		button.gameObject.SetActive(true);
		button.interactable = enabled;
		button.GetComponent<CanvasGroup>().alpha = enabled ? 1f : DimAlpha;
	}

	public void ResetToLatest()
	{
		Traverse.Create(_card).Field("_slot").SetValue(_category);
	}

	private void Step(bool older)
	{
		List<int> history = ExistingHistory();
		int current = CurrentIndex;
		int target = current;
		if (older)
		{
			int next = history.Find(i => i > current);
			if (next == 0)
			{
				return;
			}
			target = next;
		}
		else
		{
			if (current == 0)
			{
				return;
			}
			int prev = history.FindLast(i => i < current);
			target = prev; // 0 (none newer in history) means back to the newest autosave
		}
		Traverse.Create(_card).Field("_slot").SetValue(target == 0 ? _category : AutoSaveHistoryPatch.HistorySlot(_category, target));
		_card.Setup();
	}
}
