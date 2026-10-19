using System.Collections;
using NUnit.Framework;
using UnityEngine;

// Shared helpers for the midterm tests (the inventory bag in the store and in Scene5-InventoryMerge).
// The game types are used directly here (the test assembly references FarmGame).
public abstract class MidtermTestBase : HW6TestBase
{
    protected static FarmBag Bag()
    {
        FarmBag bag = Object.FindAnyObjectByType<FarmBag>(FindObjectsInactive.Include);
        Assert.IsNotNull(bag, "the scene has no bag");
        return bag;
    }

    protected static FarmInventory FarmItems()
    {
        return global::FarmFund.Instance.Inventory;
    }

    // how many of an item the bag shows (its stack's counter), 0 when the bag has no stack of it
    protected static int InBag(string itemName)
    {
        return Bag().GetInventoryManager().GetAmount(itemName);
    }

    // the bag slots in fill order: the 7 slots of the inventory bar first, then the main bag
    protected static GameObject BagSlot(int index)
    {
        return Bag().GetInventoryManager().inventorySlots[index];
    }

    // the item lying in a bag slot (null when the slot is empty or holds the faded copy of an item in the hand)
    protected static InventoryItem ItemInSlot(int index)
    {
        return BagSlot(index).GetComponentInChildren<InventoryItem>();
    }

    // the name of the bag item in the hand slot at the bottom left ("" when the hand is empty)
    protected static string InHand()
    {
        GameObject item = Bag().GetItemInHand();
        return item != null ? item.name : "";
    }

    // the name of the bag item in a bar slot as the hotbar sees it (also the item in the hand: its slot keeps a faded copy)
    protected static string BarItem(int index)
    {
        InventoryItemData item = Bag().GetBarSlot(index).item;
        return item != null ? item.itemName : "";
    }

    // a player who comes from the store with the bag (and the given levels won)
    protected static void StartBagSession(int fund, int levelsWon)
    {
        StartSession(fund, 0, levelsWon);
        FarmItems().SetBag(true);
    }

    // Scene5 with the bag, the tools and a full can, 4 + 4 seeds (as the third store visit requires)
    protected static IEnumerator LoadScene5()
    {
        StartBagSession(60, 3);
        AddSeeds("Pumpkin", 4);
        AddSeeds("Sunflower", 4);
        FarmItems().SetSpeedBoots(true);
        yield return Load(Scene5);
        FreezeCalendar();
    }
}
