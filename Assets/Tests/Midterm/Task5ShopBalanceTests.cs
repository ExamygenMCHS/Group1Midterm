using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class Task5ShopBalanceTests : MidtermTestBase
{
    [UnityTest]
    public IEnumerator OptionalBoots_CannotUseTheMoneyNeededForSeeds()
    {
        StartSession(95, 0, 2);
        yield return Load(Scene2);
        var store = Object.FindAnyObjectByType<StoreControl>();
        Assert.IsTrue(store.BuySeeds((PlantData)Crop("Pumpkin"), 3));
        Assert.IsFalse(store.BuySpeedBoots(35));
        Assert.AreEqual(35, Fund);
        Assert.IsFalse(FarmItems().speedBoots);
        StringAssert.Contains("Keep $ 30", TextOf("StoreFeedback"));
        Assert.IsTrue(store.BuySeeds((PlantData)Crop("Sunflower"), 3));
        Assert.AreEqual(5, Fund);
        Assert.IsTrue(store.IsNextLevelUnlocked());
    }

    [UnityTest]
    public IEnumerator ExtraSeeds_LeaveEnoughForTheBagAndOtherCrop()
    {
        StartSession(200, 0, 3);
        yield return Load(Scene2);
        var store = Object.FindAnyObjectByType<StoreControl>();
        var sunflower = (PlantData)Crop("Sunflower");
        Assert.IsFalse(store.BuySeeds(sunflower, 10));
        Assert.AreEqual(200, Fund);
        Assert.AreEqual(0, SeedsOf("Sunflower"));
        StringAssert.Contains("Keep $ 140", TextOf("StoreFeedback"));

        Assert.IsTrue(store.BuySeeds(sunflower, 4));
        Assert.IsTrue(store.BuyBag(60));
        Assert.IsTrue(store.BuySeeds((PlantData)Crop("Pumpkin"), 4));
        Assert.AreEqual(20, Fund);
        Assert.IsTrue(store.IsNextLevelUnlocked());
        Assert.AreEqual(4, InBag("Sunflower Seeds"));
        Assert.AreEqual(4, InBag("Pumpkin Seeds"));
    }

    [UnityTest]
    public IEnumerator UnsoldHarvest_CanPayForTheRemainingRequirements()
    {
        StartSession(50, 0, 2);
        AddHarvest("Sunflower", 5);
        yield return Load(Scene2);
        var store = Object.FindAnyObjectByType<StoreControl>();
        var sunflower = (PlantData)Crop("Sunflower");
        Assert.IsTrue(store.BuySpeedBoots(35));
        Assert.AreEqual(15, Fund);
        Assert.IsTrue(store.SellHarvest(sunflower, 5));
        Assert.IsTrue(store.BuySeeds((PlantData)Crop("Pumpkin"), 3));
        Assert.IsTrue(store.BuySeeds(sunflower, 3));
        Assert.AreEqual(0, Fund);
        Assert.IsTrue(store.IsNextLevelUnlocked());
    }

    [UnityTest]
    public IEnumerator ReadyForTheLevel_StillAllowsAffordableExtraSeeds()
    {
        StartSession(100, 0);
        yield return Load(Scene2);
        var store = Object.FindAnyObjectByType<StoreControl>();
        var sunflower = (PlantData)Crop("Sunflower");
        Assert.IsTrue(store.BuySeeds(sunflower, 5));
        Assert.IsTrue(store.BuySeeds(sunflower, 5));
        Assert.AreEqual(10, SeedsOf("Sunflower"));
        Assert.AreEqual(0, Fund);
        Assert.IsTrue(store.IsNextLevelUnlocked());
    }
}
