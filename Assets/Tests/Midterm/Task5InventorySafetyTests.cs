using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class Task5InventorySafetyTests : MidtermTestBase
{
    [UnityTest]
    public IEnumerator InvalidInventoryChanges_DoNotRemoveOrCreateItems()
    {
        yield return LoadFarmInSummer();
        var crop = (PlantData)Crop("Sunflower");
        var inventory = FarmItems();
        inventory.AddSeeds(crop, -2);
        inventory.AddSeeds(crop, 0);
        inventory.AddSeeds(null, 2);
        inventory.AddHarvested(crop, -2);
        inventory.AddHarvested(null, 2);
        Assert.IsFalse(inventory.TryUseSeed(null));
        Assert.IsFalse(inventory.TryRemoveHarvested(null, 1));
        Assert.AreEqual(4, inventory.GetSeeds(crop));
        Assert.AreEqual(0, inventory.GetHarvested(crop));
        Assert.AreEqual(4, InBag("Sunflower Seeds"));

    }

    [Test]
    public void LargeCounts_DoNotWrapBelowZero()
    {
        var go = new GameObject("Inventory Test");
        var crop = ScriptableObject.CreateInstance<PlantData>();
        try
        {
            var inventory = go.AddComponent<FarmInventory>();
            inventory.AddSeeds(crop, int.MaxValue);
            inventory.AddHarvested(crop, int.MaxValue);
            inventory.AddSeeds(crop, 1);
            inventory.AddHarvested(crop, 1);
            Assert.AreEqual(int.MaxValue, inventory.GetSeeds(crop));
            Assert.AreEqual(int.MaxValue, inventory.GetHarvested(crop));
            Assert.IsTrue(inventory.TryUseSeed(crop));
            Assert.AreEqual(int.MaxValue - 1, inventory.GetSeeds(crop));
            Assert.IsTrue(inventory.TryRemoveHarvested(crop, int.MaxValue));
            Assert.AreEqual(0, inventory.GetHarvested(crop));
        }
        finally
        {
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(crop);
        }
    }

    [UnityTest]
    public IEnumerator InvalidShopOrders_KeepMoneyAndStock()
    {
        StartSession(200, 5);
        AddHarvest("Sunflower", 2);
        yield return Load(Farm);
        bool bagBefore = FarmItems().bag, bootsBefore = FarmItems().speedBoots;
        yield return OpenShop();
        var crop = (PlantData)Crop("Sunflower");
        var store = Shop();
        foreach (int amount in new[] { -1, 0, int.MaxValue })
        {
            Assert.IsFalse(store.BuySeeds(crop, amount));
            Assert.IsFalse(store.SellHarvest(crop, amount));
        }
        Assert.IsFalse(store.BuySeeds(null, 1));
        Assert.IsFalse(store.SellHarvest(null, 1));
        Assert.IsFalse(store.BuyBag(-1));
        Assert.IsFalse(store.BuySpeedBoots(-1));
        Assert.AreEqual(200, Fund);
        Assert.AreEqual(5, SeedsOf("Sunflower"));
        Assert.AreEqual(2, HarvestedOf("Sunflower"));
        Assert.AreEqual(bagBefore, FarmItems().bag);
        Assert.AreEqual(bootsBefore, FarmItems().speedBoots);
    }

    [UnityTest]
    public IEnumerator MoneyOverflow_RejectsTheSaleWithoutTakingHarvest()
    {
        StartSession(int.MaxValue - 5, 0);
        AddHarvest("Sunflower", 1);
        yield return Load(Farm);
        yield return OpenShop();
        var store = Shop();
        Assert.IsFalse(store.SellHarvest((PlantData)Crop("Sunflower"), 1));
        global::FarmFund.Instance.AddFund(100);
        global::FarmFund.Instance.AddFund(-10);
        Assert.AreEqual(int.MaxValue - 5, Fund);
        Assert.AreEqual(1, HarvestedOf("Sunflower"));
    }

    [UnityTest]
    public IEnumerator OnePlant_GivesOneHarvest_AndCanOnlyBeSoldOnce()
    {
        yield return LoadFarmInSummer();
        var tile = Tiles()[0];
        Hold("SeedBag", Crop("Sunflower"));
        yield return PlantOn(tile);
        PassDays(4);
        yield return EOn(tile);
        var manager = (PlantManager)PlantManagerScript();
        Assert.IsFalse(manager.TryHarvestOnCurTile());
        int seeds = SeedsOf("Sunflower");
        Assert.That(seeds, Is.InRange(4, 5), "4 - 1 planted + the 1 or 2 seeds the harvest gave back");
        Assert.AreEqual(1, HarvestedOf("Sunflower"));
        Assert.AreEqual(1, InBag("Sunflower"));

        yield return OpenShop();
        var store = Shop();
        Assert.IsTrue(store.SellHarvest((PlantData)Crop("Sunflower"), 1));
        Assert.IsFalse(store.SellHarvest((PlantData)Crop("Sunflower"), 1));
        Assert.AreEqual(75, Fund);
        Assert.AreEqual(seeds, InBag("Sunflower Seeds"), "selling the harvest leaves the seeds alone");
        Assert.AreEqual(0, InBag("Sunflower"));
    }
}
