using System.Collections.Generic;
using HarmonyLib;
using LOMAssistant.Config;
using Mortal.Core;

namespace LOMAssistant.Features.Extras;

/// <summary>
/// 四师兄售卖全部物品（来自 Jim97 多功能 Mod，活侠传助手整合时去掉了）。
/// 读档、换月刷新商店后，把所有没拥有的书籍、杂物、特殊物品加进商店。
/// 《证类本草》（9001）不卖：提前买了六月后山事件会出 bug；剧情里拿不到的三本书也不卖。
///
/// 和原版不同的是：加进去的商品不写进存档（存档前先拿掉、存完放回），
/// 所以关掉这个功能后，存档里的商店就是原样，不需要重置商店。
/// </summary>
public static class ShopAllItemsPatch
{
	// 不卖的书：《证类本草》提前买了六月后山事件会出 bug；
	// 松风剑法、天师剑法、四平枪谱剧情里从来不给、原版商店也不卖，像是没做完的书
	private static readonly HashSet<string> ExcludedBooks = new HashSet<string> { "9001", "2010", "2200", "4000" };

	// 本次加进商店的商品，存档时要排除
	private static readonly HashSet<ShopItem> Added = new HashSet<ShopItem>();

	[HarmonyPostfix]
	[HarmonyPatch(typeof(ShopDatabase), "Load")]
	public static void Load_Postfix(ShopDatabase __instance)
	{
		Added.Clear(); // Load 会先 Reset，旧列表已经不用了
		AddAll(__instance);
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(ShopDatabase), "UpdateItemFromMission")]
	public static void UpdateItemFromMission_Postfix(ShopDatabase __instance)
	{
		AddAll(__instance);
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(ShopDatabase), "Save")]
	public static void Save_Prefix(ShopDatabase __instance, out List<ShopItem>[] __state)
	{
		__state = null;
		if (Added.Count == 0)
		{
			return;
		}
		__state = new[] { new List<ShopItem>(__instance.Books), new List<ShopItem>(__instance.Miscs), new List<ShopItem>(__instance.Specials) };
		__instance.Books.RemoveAll(Added.Contains);
		__instance.Miscs.RemoveAll(Added.Contains);
		__instance.Specials.RemoveAll(Added.Contains);
	}

	[HarmonyFinalizer]
	[HarmonyPatch(typeof(ShopDatabase), "Save")]
	public static void Save_Finalizer(ShopDatabase __instance, List<ShopItem>[] __state)
	{
		if (__state == null)
		{
			return;
		}
		Restore(__instance.Books, __state[0]);
		Restore(__instance.Miscs, __state[1]);
		Restore(__instance.Specials, __state[2]);
	}

	private static void Restore(List<ShopItem> list, List<ShopItem> before)
	{
		list.Clear();
		list.AddRange(before);
	}

	private static void AddAll(ShopDatabase shop)
	{
		if (!ConfigManager.EnableShopAllItems.Value || shop.Books == null)
		{
			return;
		}
		ItemDatabase items = ItemDatabase.Instance;
		int count = 0;
		// 剧本里没有按「有没有某本书」判断的条件，提前买到不影响剧情触发（只有《证类本草》例外）
		foreach (ItemData book in items.Books.List)
		{
			if (ExcludedBooks.Contains(book.Id))
			{
				continue;
			}
			if (book.CurrentCount == 0 && !shop.Books.Exists(x => x.Id == book.Id))
			{
				if (book.Cost == 0)
				{
					Traverse.Create(book).Field("_cost").SetValue(999);
				}
				Add(shop.Books, new ShopItem(book, 1));
				count++;
			}
		}
		foreach (ItemData misc in items.Miscs.List)
		{
			if (misc.Cost != 0 && !shop.Miscs.Exists(x => x.Id == misc.Id))
			{
				Add(shop.Miscs, new ShopItem(misc, 99));
				count++;
			}
		}
		foreach (ItemData special in items.Special.List)
		{
			if (special.CurrentCount == 0 && special.Cost != 0 && !shop.Specials.Exists(x => x.Id == special.Id))
			{
				Add(shop.Specials, new ShopItem(special, 1));
				count++;
			}
		}
		Log(count);
	}

	private static void Log(int count)
	{
		if (count > 0)
		{
			FileLogger.Log($"四师兄商店：新增 {count} 件商品");
		}
	}

	private static void Add(List<ShopItem> list, ShopItem item)
	{
		list.Add(item);
		Added.Add(item);
	}
}
