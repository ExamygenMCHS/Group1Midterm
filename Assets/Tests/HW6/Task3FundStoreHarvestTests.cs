using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// HW6 Task 3: the Farm Fund object, the store with prices and quantities, harvesting, the Scene3 win condition,
// and the second store visit (sell the harvest, new items)
public class Task3FundStoreHarvestTests : HW6TestBase
{
    [UnityTest]
    public IEnumerator FarmFund_IsAnEmptyObjectFromScene1_ThatSurvivesSceneLoads()
    {
        yield return Load(Scene1);
        Component fund = FarmFund();
        GameObject go = fund.gameObject;
        Assert.AreEqual("Farm Fund", go.name);
        Assert.AreEqual("DontDestroyOnLoad", go.scene.name, "the Farm Fund is set to DontDestroyOnLoad");
        Assert.IsNull(go.GetComponent<Renderer>(), "an empty object with the script");
        Assert.AreEqual(0, Fund, "a new game starts with no money");

        foreach (Component tile in Tiles())
            Water(tile);
        yield return null;
        Assert.AreEqual(100, Fund, "winning Scene1 gives the first fund");

        ButtonNamed("Next Level").onClick.Invoke();
        yield return WaitForScene(Scene2);
        Assert.AreSame(go, FarmFund().gameObject, "the same Farm Fund object is still alive in the store");
        Assert.AreEqual(1, Object.FindObjectsByType(GameType("FarmFund"), FindObjectsInactive.Include).Length,
            "the store's own Farm Fund copy (for playing the store on its own) removed itself");
        Assert.AreEqual("Fund: $ 100", TextOf("FundAmount"));
    }

    [UnityTest]
    public IEnumerator Scene1_StartsANewGame_EvenWhenAnOldFarmFundIsStillAlive()
    {
        StartSession(500, 9, 2);
        yield return Load(Scene1);
        Assert.AreEqual(1, Object.FindObjectsByType(GameType("FarmFund"), FindObjectsInactive.Include).Length);
        Assert.AreEqual(0, Fund);
        Assert.AreEqual(0, Seeds);
        Assert.AreEqual(0, (int)Get(FarmFund(), "levelsWon"));
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
        yield return WaitForScene(Scene3);
        Assert.AreEqual("Fund: $ 50", TextOf("FundAmount"), "the remaining balance shows in Scene3");
        Assert.AreEqual("Seeds: 5", TextOf("SeedAmount"));
    }

    [UnityTest]
    public IEnumerator Harvesting_IsCountedOnTheHud_AndFiveHarvestsWinAndOpenTheStoreAgain()
    {
        StartSession(50, 5);
        yield return Load(Scene3);
        FreezeCalendar();
        StringAssert.Contains("Harvest 5 sunflowers before winter", TextOf("Instruction"), "the GUI tells the player how to win");
        StringAssert.Contains("0 / 5", HudText("GoalPanel/HarvestCount"));

        Component[] tiles = Tiles();
        Time.timeScale = 3f;
        for (int i = 0; i < 5; i++)
            yield return PlantOn(tiles[i]);
        PassDays(4);
        for (int i = 0; i < 5; i++)
        {
            yield return EOn(tiles[i]);
            if (i < 4)
            {
                StringAssert.Contains((i + 1) + " / 5", HudText("GoalPanel/HarvestCount"), "the GUI shows how many plants are harvested");
                StringAssert.Contains("Harvested", HudMessage());
            }
        }
        Time.timeScale = 1f;
        yield return null;

        Assert.AreEqual("Won", GoalState());
        Assert.IsTrue(FindInScene("WinPanel").activeInHierarchy);
        StringAssert.Contains("You Win", TextOf("WinMessage"));
        Assert.AreEqual(100, Fund, "$50 left + $50 bonus");
        Assert.AreEqual(5, HarvestedOf("Sunflower"), "the harvest goes into storage, to sell in the store");
        Assert.AreEqual(2, (int)Get(FarmFund(), "levelsWon"));

        ButtonNamed("Next Level").onClick.Invoke();
        yield return WaitForScene(Scene2);
        foreach (string row in new[] { "Row - Sell Sunflowers", "Row - Sunflower Seeds", "Row - Pumpkin Seeds", "Row - Speed Boots" })
            Assert.IsTrue(FindInScene(row).activeInHierarchy, row + " is offered on the second visit");
        Assert.IsTrue(FindInScene("Row - Pumpkin Seeds").transform.Find("NewTag").gameObject.activeSelf, "new items are tagged");
        Assert.AreEqual("Earn $ 75", RowText("Row - Sell Sunflowers", "Total"), "selling all 5 sunflowers at $15");
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
    public IEnumerator Store_SecondVisit_SellsTheHarvest_SellsNewItems_AndLeadsToScene4()
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
        Assert.IsFalse(ButtonNamed("Start Next Level").interactable, "Scene4 also needs 3 sunflower seeds");
        Call(Row("Row - Sunflower Seeds"), "SetQuantity", 3);
        ButtonNamed("Buy Seed").onClick.Invoke();
        yield return null;
        Assert.AreEqual(3, SeedsOf("Sunflower"));
        Assert.IsTrue(ButtonNamed("Start Next Level").interactable);

        ButtonNamed("Start Next Level").onClick.Invoke();
        yield return WaitForScene(Scene4);
    }

    [UnityTest]
    public IEnumerator Scene3_IsLost_WhenWinterComesFirst_AndTryAgainRestartsWithTheStartingFund()
    {
        StartSession(50, 5);
        yield return Load(Scene3);
        FreezeCalendar();
        yield return PlantOn(Tiles()[0]);
        Assert.AreEqual(4, SeedsOf("Sunflower"));

        for (int i = 0; i < 20 && Season() != "Winter"; i++)
            AdvanceDays(1);
        yield return null;
        Assert.AreEqual("Lost", GoalState(), "winter came before 5 sunflowers were harvested");
        Assert.IsTrue(FindInScene("LosePanel").activeInHierarchy);
        Assert.IsFalse(FindInScene("WinPanel").activeInHierarchy);

        ButtonNamed("Try Again").onClick.Invoke();
        yield return WaitForScene(Scene3);
        yield return null;
        Assert.AreEqual(5, SeedsOf("Sunflower"), "the level restarts with the seeds it started with");
        Assert.AreEqual(50, Fund);
        Assert.AreEqual("Playing", GoalState());
        Assert.IsFalse(FindInScene("LosePanel").activeInHierarchy);
    }
}
