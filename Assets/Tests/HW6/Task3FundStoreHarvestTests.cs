using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// HW6 Task 3: the Farm Fund object, the store with prices and quantities, harvesting (with its seed drops),
// and the second store visit (sell the harvest, new items)
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

        yield return Load(Scene2);
        Assert.AreSame(go, FarmFund().gameObject, "the same Farm Fund object is still alive in the store");
        Assert.AreEqual(1, Object.FindObjectsByType(GameType("FarmFund"), FindObjectsInactive.Include).Length,
            "the store's own Farm Fund copy (for playing the store on its own) removed itself");
        Assert.AreEqual("Fund: $ 0", TextOf("FundAmount"));
    }

    [UnityTest]
    public IEnumerator TheFarm_StartsANewGame_WhenNoFarmFundIsAlive()
    {
        yield return Load(Farm, withTools: false);
        Assert.AreEqual(0, Fund);
        Assert.AreEqual(4, SeedsOf("Sunflower"));
        Assert.AreEqual(4, SeedsOf("Pumpkin"));
        Assert.AreEqual(0, (int)Get(FarmFund(), "levelsWon"));
        Assert.IsFalse((bool)Get(Inventory(), "speedBoots"), "the speed boots are still to be bought");
    }

    [UnityTest]
    public IEnumerator TheFarm_KeepsThePlayersFarmFund_WhenTheyComeBack()
    {
        StartSession(500, 9, 2);
        yield return Load(Farm);
        Assert.AreEqual(1, Object.FindObjectsByType(GameType("FarmFund"), FindObjectsInactive.Include).Length,
            "the farm's own Farm Fund copy removed itself");
        Assert.AreEqual(500, Fund, "the farm is the hub: coming back keeps the money");
        Assert.AreEqual(9, Seeds);
        Assert.AreEqual(2, (int)Get(FarmFund(), "levelsWon"));
    }

    [UnityTest]
    public IEnumerator Store_ShowsThePriceOfOneItem_AndBuysTheChosenQuantity()
    {
        StartSession(100, 0);
        yield return Load(Scene2);
        const string row = "Row - Sunflower Seeds";
        Assert.AreEqual("$ 10 each", RowText(row, "Price"), "the price of a single seed");
        Assert.AreEqual("5", RowText(row, "Quantity"), "starts at the number the next level needs");
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
        Assert.AreEqual("Fund: $ 70", TextOf("FundAmount"));
        Assert.IsFalse(ButtonNamed("Start Next Level").interactable, "3 seeds are not enough for the 5 harvests the next level asks for");
        StringAssert.Contains("you have 3", TextOf("NextLevelNeeds"));

        RowButton(row, "Minus").onClick.Invoke();
        ButtonNamed("Buy Seed").onClick.Invoke();
        yield return null;
        Assert.AreEqual(5, SeedsOf("Sunflower"));
        Assert.AreEqual(50, Fund);
        Assert.IsTrue(ButtonNamed("Start Next Level").interactable);

        ButtonNamed("Start Next Level").onClick.Invoke();
        yield return WaitForScene(Farm);
        Assert.AreEqual("Fund: $ 50", TextOf("FundAmount"), "the remaining balance shows on the farm");
        Assert.AreEqual(5, SeedsOf("Sunflower"));
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
        Assert.IsNull(FindInScene("WinPanel"), "free play: no win panel");
    }

    [UnityTest]
    public IEnumerator Store_FirstVisit_HidesTheSecondVisitsItems()
    {
        StartSession(100, 0, 1);
        yield return Load(Scene2);
        Assert.IsFalse(FindInScene("Row - Sell Sunflowers").activeInHierarchy);
        Assert.IsFalse(FindInScene("Row - Pumpkin Seeds").activeInHierarchy);
        Assert.IsFalse(FindInScene("Row - Speed Boots").activeInHierarchy);
        Assert.IsTrue(FindInScene("MoreItemsLater").activeInHierarchy);
    }

    [UnityTest]
    public IEnumerator Store_SecondVisit_SellsTheHarvest_SellsNewItems_AndLeadsBackToTheFarm()
    {
        StartSession(20, 0, 2);
        AddHarvest("Sunflower", 5);
        yield return Load(Scene2);

        ButtonNamed("Sell Sunflowers").onClick.Invoke();
        yield return null;
        Assert.AreEqual(95, Fund, "5 sunflowers sold at $15");
        Assert.AreEqual(0, HarvestedOf("Sunflower"));

        Assert.AreEqual("$ 20 each", RowText("Row - Pumpkin Seeds", "Price"));
        ButtonNamed("Buy Pumpkin Seed").onClick.Invoke(); // 3 by default
        yield return null;
        Assert.AreEqual(3, SeedsOf("Pumpkin"));
        Assert.AreEqual(35, Fund);

        ButtonNamed("Buy Speed Boots").onClick.Invoke();
        yield return null;
        //keep the money for the seeds instead of getting stuck in the shop
        Assert.IsFalse((bool)Get(Inventory(), "speedBoots"));
        Assert.AreEqual(35, Fund);
        StringAssert.Contains("Keep $ 30", TextOf("StoreFeedback"));
        Assert.IsFalse(ButtonNamed("Start Next Level").interactable, "this visit also asks for 3 sunflower seeds");
        Call(Row("Row - Sunflower Seeds"), "SetQuantity", 3);
        ButtonNamed("Buy Seed").onClick.Invoke();
        yield return null;
        Assert.AreEqual(3, SeedsOf("Sunflower"));
        Assert.IsTrue(ButtonNamed("Start Next Level").interactable);

        ButtonNamed("Start Next Level").onClick.Invoke();
        yield return WaitForScene(Farm);
    }
}
