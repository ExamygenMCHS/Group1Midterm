using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

//Run later in the Unity Play Mode test runner. These tests use the existing store GUI and farm scenes.
//They are staged without opening Unity while the freeplay scene is being merged.
public class TraderStoreTests : HW6TestBase
{
    [UnityTest]
    public IEnumerator Freeplay_ShowsTheWholeCatalog_AndCannotLoadTheNextLevel()
    {
        StartSession(120, 0, 0);
        yield return Load(Scene2);
        StoreControl store = Object.FindAnyObjectByType<StoreControl>();
        store.ConfigureFreeplay();

        foreach (string name in new[] { "Row - Sunflower Seeds", "Row - Pumpkin Seeds", "Row - Sell Sunflowers",
            "Row - Sell Pumpkins", "Row - Speed Boots", "Row - Bag" })
            Assert.IsTrue(FindInScene(name).activeInHierarchy, name + " is available regardless of levels won");
        Assert.IsFalse(ButtonNamed("Start Next Level").gameObject.activeSelf);
        Assert.IsFalse(FindInScene("NextLevelNeeds").activeSelf);
        Assert.IsFalse(store.IsNextLevelUnlocked());
        Assert.AreEqual(string.Empty, store.GetNextLevelScene());
        string scene = SceneManager.GetActiveScene().path;
        store.StartNextLevel();
        yield return null;
        Assert.AreEqual(scene, SceneManager.GetActiveScene().path);
        Assert.AreEqual(0, global::FarmFund.Instance.levelsWon);
    }

    [UnityTest]
    public IEnumerator Freeplay_BrokePlayerWithoutSeeds_GetsStarterSeeds_Once()
    {
        StartSession(0, 0, 0);
        yield return Load(Scene2);
        StoreControl store = Object.FindAnyObjectByType<StoreControl>();
        store.ConfigureFreeplay();
        Assert.AreEqual(StoreControl.StarterSeedCount, SeedsOf("Sunflower"), "the cheaper crop");
        Assert.AreEqual(0, SeedsOf("Pumpkin"));
        store.ConfigureFreeplay();
        Assert.AreEqual(StoreControl.StarterSeedCount, SeedsOf("Sunflower"), "reopening does not give more");
    }

    [UnityTest]
    public IEnumerator Freeplay_PlayerWithMoneySeedsOrHarvest_GetsNoStarterSeeds()
    {
        StartSession(50, 0, 0);
        yield return Load(Scene2);
        StoreControl store = Object.FindAnyObjectByType<StoreControl>();
        store.ConfigureFreeplay();
        Assert.AreEqual(0, SeedsOf("Sunflower"), "can afford seeds");

        StartSession(0, 0, 0);
        yield return Load(Scene2);
        global::FarmFund.Instance.Inventory.AddHarvested((PlantData)Crop("Pumpkin"), 1);
        Object.FindAnyObjectByType<StoreControl>().ConfigureFreeplay();
        Assert.AreEqual(0, SeedsOf("Sunflower"), "has a harvest to sell");
    }

    [UnityTest]
    public IEnumerator Freeplay_CanSpendAllAvailableFund_WithoutReservingLevelRequirements()
    {
        StartSession(35, 0, 2);
        yield return Load(Scene2);
        StoreControl store = Object.FindAnyObjectByType<StoreControl>();
        store.ConfigureFreeplay();
        Assert.IsTrue(store.BuySpeedBoots(35));
        Assert.AreEqual(0, Fund);
        Assert.IsTrue(global::FarmFund.Instance.Inventory.speedBoots);
        StringAssert.DoesNotContain("next level", TextOf("StoreFeedback"));
        Assert.IsFalse(store.BuySpeedBoots(35), "one-time upgrades cannot charge twice");
        Assert.AreEqual(0, Fund);
    }

    [UnityTest]
    public IEnumerator Freeplay_ReopeningRefreshesTheExistingInventory_AndKeepsTransactions()
    {
        StartSession(100, 0, 1);
        yield return Load(Scene2);
        StoreControl store = Object.FindAnyObjectByType<StoreControl>();
        PlantData sunflower = (PlantData)Crop("Sunflower");
        FarmInventory inventory = global::FarmFund.Instance.Inventory;
        store.ConfigureFreeplay();
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
    public IEnumerator LevelProgression_RemainsTheDefault_AndStillReservesSeedMoney()
    {
        StartSession(35, 0, 2);
        yield return Load(Scene2);
        StoreControl store = Object.FindAnyObjectByType<StoreControl>();
        Assert.IsFalse(store.IsFreeplay);
        Assert.IsFalse(store.BuySpeedBoots(35));
        Assert.AreEqual(35, Fund);
        Assert.IsTrue(ButtonNamed("Start Next Level").gameObject.activeSelf);
        Assert.AreEqual(Farm, store.GetNextLevelScene(), "every store visit leads back to the farm");
    }

    [UnityTest]
    public IEnumerator Freeplay_CanBeConfiguredOnAnInactivePanel_BeforeFirstStart()
    {
        StartSession(50, 0, 0);
        yield return Load(Scene2);
        var panel = new GameObject("Inactive trader store test");
        panel.SetActive(false);
        StoreControl store = panel.AddComponent<StoreControl>();
        store.ConfigureFreeplay();
        panel.SetActive(true);
        yield return null;
        Assert.IsTrue(store.IsFreeplay);
        Assert.AreEqual(string.Empty, store.GetNextLevelScene());
        Object.Destroy(panel);
    }

    [UnityTest]
    public IEnumerator BootsUpgrade_ChangesCurrentFarmSpeed_WithoutReloadingTheScene()
    {
        StartSession(100, 0, 2);
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
