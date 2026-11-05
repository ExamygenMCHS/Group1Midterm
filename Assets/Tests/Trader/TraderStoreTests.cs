using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

//The trader's store GUI (the TraderShopCanvas prefab), opened on The Farm through the trader.
public class TraderStoreTests : HW6TestBase
{
    [UnityTest]
    public IEnumerator Shop_ShowsTheWholeCatalog()
    {
        StartSession(120, 0);
        yield return Load(Farm);
        yield return OpenShop();

        foreach (string name in new[] { "Row - Sunflower Seeds", "Row - Pumpkin Seeds", "Row - Sell Sunflowers",
            "Row - Sell Pumpkins", "Row - Speed Boots", "Row - Bag" })
            Assert.IsTrue(FindInScene(name).activeInHierarchy, name + " is always on offer");
        Assert.IsNull(FindInScene("Start Next Level"), "the shop has no levels to start");
        Assert.IsNull(FindInScene("NextLevelNeeds"));
        Assert.IsNull(FindInScene("MoreItemsLater"));
    }

    [UnityTest]
    public IEnumerator Shop_BrokePlayerWithoutSeeds_GetsStarterSeeds_Once()
    {
        StartSession(0, 0);
        yield return Load(Farm);
        yield return OpenShop();
        StoreControl store = Shop();
        Assert.AreEqual(StoreControl.StarterSeedCount, SeedsOf("Sunflower"), "the cheaper crop");
        Assert.AreEqual(0, SeedsOf("Pumpkin"));
        store.PrepareShop();
        Assert.AreEqual(StoreControl.StarterSeedCount, SeedsOf("Sunflower"), "reopening does not give more");
    }

    [UnityTest]
    public IEnumerator Shop_PlayerWithMoneySeedsOrHarvest_GetsNoStarterSeeds()
    {
        StartSession(50, 0);
        yield return Load(Farm);
        yield return OpenShop();
        Assert.AreEqual(0, SeedsOf("Sunflower"), "can afford seeds");

        StartSession(0, 0);
        yield return Load(Farm);
        global::FarmFund.Instance.Inventory.AddHarvested((PlantData)Crop("Pumpkin"), 1);
        yield return OpenShop();
        Assert.AreEqual(0, SeedsOf("Sunflower"), "has a harvest to sell");
    }

    [UnityTest]
    public IEnumerator Shop_CanSpendAllAvailableFund()
    {
        StartSession(35, 0);
        yield return Load(Farm);
        yield return OpenShop();
        StoreControl store = Shop();
        Assert.IsTrue(store.BuySpeedBoots(35));
        Assert.AreEqual(0, Fund);
        Assert.IsTrue(global::FarmFund.Instance.Inventory.speedBoots);
        StringAssert.DoesNotContain("next level", TextOf("StoreFeedback"));
        Assert.IsFalse(store.BuySpeedBoots(35), "one-time upgrades cannot charge twice");
        Assert.AreEqual(0, Fund);
    }

    [UnityTest]
    public IEnumerator Shop_ReopeningRefreshesTheExistingInventory_AndKeepsTransactions()
    {
        StartSession(100, 0);
        yield return Load(Farm);
        yield return OpenShop();
        StoreControl store = Shop();
        PlantData sunflower = (PlantData)Crop("Sunflower");
        FarmInventory inventory = global::FarmFund.Instance.Inventory;
        Assert.IsTrue(store.BuySeeds(sunflower, 2));
        inventory.AddHarvested(sunflower, 2);
        Assert.IsTrue(store.SellHarvest(sunflower, 1));
        int fundsAfterSale = Fund;
        StringAssert.DoesNotContain("next level", TextOf("StoreFeedback"));

        store.gameObject.SetActive(false);
        inventory.AddSeeds(sunflower, 3);
        store.gameObject.SetActive(true);
        yield return null;
        Assert.AreEqual(fundsAfterSale, Fund, "reopening must not reset the farm fund");
        Assert.AreEqual(5, inventory.GetSeeds(sunflower));
        Assert.AreEqual(1, inventory.GetHarvested(sunflower));
        Assert.AreEqual("You have 5", RowText("Row - Sunflower Seeds", "Owned"));

        for (int i = 0; i < 3; i++)
        {
            store.gameObject.SetActive(false);
            store.gameObject.SetActive(true);
            store.RefreshShop();
        }
        var row = FindInScene("Row - Sunflower Seeds").GetComponent<StoreItemRow>();
        row.SetQuantity(1);
        row.Apply();
        Assert.AreEqual(6, inventory.GetSeeds(sunflower));
        Assert.AreEqual(fundsAfterSale - sunflower.seedPrice, Fund, "one button action makes one purchase");
    }

    [UnityTest]
    public IEnumerator Shop_CanBePreparedOnAnInactivePanel_BeforeFirstStart()
    {
        StartSession(50, 0);
        yield return Load(Farm);
        var panel = new GameObject("Inactive trader store test");
        panel.SetActive(false);
        StoreControl store = panel.AddComponent<StoreControl>();
        store.PrepareShop();
        panel.SetActive(true);
        yield return null;
        Assert.IsTrue(store.isActiveAndEnabled);
        Object.Destroy(panel);
    }

    [UnityTest]
    public IEnumerator BootsUpgrade_ChangesCurrentFarmSpeed_WithoutReloadingTheScene()
    {
        StartSession(100, 0);
        yield return Load(Farm);
        FarmBalance balance = Object.FindAnyObjectByType<FarmBalance>();
        PlayerMove movement = Object.FindAnyObjectByType<PlayerMove>();
        EnergySettings settings = (EnergySettings)Get(balance, "settings");
        FarmInventory inventory = global::FarmFund.Instance.Inventory;
        inventory.SetSpeedBoots(false);
        Assert.AreEqual(settings.playerSpeed, movement.forwardSpeed, .0001f);
        inventory.SetSpeedBoots(true);
        Assert.AreEqual(settings.playerSpeed * settings.speedBootsMultiplier, movement.forwardSpeed, .0001f);
        Assert.AreEqual(movement.forwardSpeed, movement.sidewaySpeed);
        inventory.SetSpeedBoots(false);
        Assert.AreEqual(settings.playerSpeed, movement.forwardSpeed, .0001f);

        balance.enabled = false;
        inventory.SetSpeedBoots(true);
        Assert.AreEqual(settings.playerSpeed, movement.forwardSpeed, .0001f, "a disabled balance component no longer listens");
        balance.enabled = true;
        Assert.AreEqual(settings.playerSpeed * settings.speedBootsMultiplier, movement.forwardSpeed, .0001f);
    }
}
