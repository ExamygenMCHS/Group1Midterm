using System.Collections;
using NUnit.Framework;
using UnityEngine;

// Shared helpers for the midterm tests (the inventory bag in the store and on The Farm).
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

    // a player who owns the bag
    protected static void StartBagSession(int fund)
    {
        StartSession(fund, 0);
        FarmItems().SetBag(true);
    }

    // The Farm as a new game starts it (the bag, 4 + 4 seeds), with the tools picked up and a full can
    protected static IEnumerator LoadFarm()
    {
        yield return Load(Farm);
        FreezeCalendar();
    }

    // the three tools in the inventory before the farm loads: the bag then fills in the order of the old levels
    // (hoe, watering can, the seeds, sickle) instead of putting tools picked up later into the next free slots
    protected static void OwnTools()
    {
        foreach (string tool in new[] { "Hoe", "WateringCan", "Sickle" })
            Call(Inventory(), "AddTool", ToolType(tool));
    }

    // a player who comes back to The Farm with the bag, the tools and 4 + 4 seeds (as the old Scene5 began), on day 8,
    // the first day of summer, when both crops can be planted
    protected static IEnumerator LoadFarmInSummer()
    {
        StartBagSession(60);
        OwnTools();
        AddSeeds("Sunflower", 4);
        AddSeeds("Pumpkin", 4);
        yield return LoadFarm();
        AdvanceDays(7);
        Assert.AreEqual("Summer", Season());
    }
}
