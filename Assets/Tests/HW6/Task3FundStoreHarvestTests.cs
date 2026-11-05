using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// HW6 Task 3: the Farm Fund object, the trader's store with prices and quantities, and harvesting (with its seed drops)
public class Task3FundStoreHarvestTests : HW6TestBase
{
    [UnityTest]
    public IEnumerator FarmFund_IsAnEmptyObjectFromTheFarm_ThatSurvivesSceneLoads()
    {
        yield return Load(Farm);
        Component fund = FarmFund();
        GameObject go = fund.gameObject;
        Assert.AreEqual("Farm Fund", go.name);
        Assert.AreEqual("DontDestroyOnLoad", go.scene.name, "the Farm Fund is set to DontDestroyOnLoad");
        Assert.IsNull(go.GetComponent<Renderer>(), "an empty object with the script");
        Assert.AreEqual(0, Fund, "a new game starts with no money");

        yield return Load(Scene0);
        Assert.AreSame(go, FarmFund().gameObject, "the same Farm Fund object is still alive in the next scene");
        Assert.AreEqual(1, Object.FindObjectsByType(GameType("FarmFund"), FindObjectsInactive.Include).Length);
    }

    [UnityTest]
    public IEnumerator TheFarm_StartsANewGame_WhenNoFarmFundIsAlive()
    {
        yield return Load(Farm, withTools: false);
        Assert.AreEqual(0, Fund);
        Assert.AreEqual(4, SeedsOf("Sunflower"));
        Assert.AreEqual(4, SeedsOf("Pumpkin"));
        Assert.IsFalse((bool)Get(Inventory(), "speedBoots"), "the speed boots are still to be bought");
    }

    [UnityTest]
    public IEnumerator TheFarm_KeepsThePlayersFarmFund_WhenTheyComeBack()
    {
        StartSession(500, 9);
        yield return Load(Farm);
        Assert.AreEqual(1, Object.FindObjectsByType(GameType("FarmFund"), FindObjectsInactive.Include).Length,
            "the farm's own Farm Fund copy removed itself");
        Assert.AreEqual(500, Fund, "the farm is the hub: coming back keeps the money");
        Assert.AreEqual(9, Seeds);
    }

    [UnityTest]
    public IEnumerator Store_ShowsThePriceOfOneItem_AndBuysTheChosenQuantity()
    {
        StartSession(100, 0);
        yield return Load(Farm);
        yield return OpenShop();
        const string row = "Row - Sunflower Seeds";
        Assert.AreEqual("$ 10 each", RowText(row, "Price"), "the price of a single seed");

        Call(Row(row), "SetQuantity", 5);
        Assert.AreEqual("5", RowText(row, "Quantity"));
        Assert.AreEqual("Total $ 50", RowText(row, "Total"));

        RowButton(row, "Minus").onClick.Invoke();
        RowButton(row, "Minus").onClick.Invoke();
        yield return null;
        Assert.AreEqual("3", RowText(row, "Quantity"));
        Assert.AreEqual("Total $ 30", RowText(row, "Total"));

        ButtonNamed("Buy Seed").onClick.Invoke();
        yield return null;
        Assert.AreEqual(3, SeedsOf("Sunflower"));
        Assert.AreEqual(70, Fund);
        Assert.AreEqual("You have 3", RowText(row, "Owned"));
    }

    [UnityTest]
    public IEnumerator Harvesting_StoresTheCrop_AndGivesBackOneOrTwoSeeds()
    {
        StartSession(50, 5);
        yield return Load(Farm);
        FreezeCalendar();

        Component[] tiles = Tiles();
        Time.timeScale = 3f;
        for (int i = 0; i < 5; i++)
            yield return PlantOn(tiles[i]);
        Assert.AreEqual(0, SeedsOf("Sunflower"), "every seed is in the ground");
        PassDays(4);
        for (int i = 0; i < 5; i++)
        {
            int seedsBefore = SeedsOf("Sunflower");
            yield return EOn(tiles[i]);
            int dropped = SeedsOf("Sunflower") - seedsBefore;
            Assert.That(dropped, Is.InRange(1, 2), "a harvest gives back 1 or 2 seeds");
            StringAssert.Contains("Harvested", HudMessage());
            StringAssert.Contains("+" + dropped + " sunflower seed", HudMessage(), "the HUD says how many seeds came back");
        }
        Time.timeScale = 1f;
        yield return null;

        Assert.AreEqual(5, HarvestedOf("Sunflower"), "the harvest goes into storage, to sell later");
        Assert.AreEqual(50, Fund, "free play: no harvest bonus");
    }
}
