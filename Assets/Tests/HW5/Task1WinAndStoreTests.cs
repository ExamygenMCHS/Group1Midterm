using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

// HW5 Task 1: win condition in Scene1-FarmingSim and the store in Scene2-Store
public class Task1WinAndStoreTests : HW5TestBase
{
    private static bool HasWon()
    {
        return (bool)Call(FindOne("FarmWinCondition"), "HasWon");
    }

    [Test]
    public void BuildSettings_ListTheScenesInOrder()
    {
        // while tests run, the Test Runner adds its own temporary scene to the list; ignore it
        var scenes = new System.Collections.Generic.List<string>();
        for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
        {
            string path = SceneUtility.GetScenePathByBuildIndex(i);
            if (path.StartsWith("Assets/Game/Scenes/"))
                scenes.Add(System.IO.Path.GetFileNameWithoutExtension(path));
        }
        // midterm: Scene5 (the inventory level) follows Scene4
        CollectionAssert.AreEqual(new[] { Scene0, Scene1, Scene2, Scene3, Scene4, Scene5 }, scenes);
#if UNITY_EDITOR
        // the saved list must point at the scene files that exist now (a recreated scene gets a new GUID)
        string saved = System.IO.File.ReadAllText("ProjectSettings/EditorBuildSettings.asset");
        foreach (string name in new[] { Scene0, Scene1, Scene2, Scene3, Scene4, Scene5 })
        {
            string guid = UnityEditor.AssetDatabase.AssetPathToGUID("Assets/Game/Scenes/" + name + ".unity");
            StringAssert.Contains("guid: " + guid, saved, name + " has a stale GUID in the saved build list (remove and re-add it, then File > Save Project)");
        }
#endif
    }

    [UnityTest]
    public IEnumerator Win_DoesNotFire_WhileOneTileIsStillDrySoil()
    {
        yield return Load(Scene1);
        FreezeCalendar();
        Component[] tiles = Tiles();
        for (int i = 0; i < 15; i++)
            Water(tiles[i]);
        Call(tiles[15], "InteractWithFarmTile"); // tilled (dry soil), not watered
        yield return null;

        Assert.AreEqual("Tilled", Cond(tiles[15]));
        Assert.IsFalse(HasWon());
        Assert.IsFalse(FindInScene("WinPanel").activeInHierarchy, "the winning message must stay hidden");
        Assert.AreEqual(0, Fund);
        Assert.AreEqual("Fund: $ 0", TextOf("FundAmount"));
    }

    [UnityTest]
    public IEnumerator Win_Fires_WhenEveryTileIsWatered_ShowsMessageNextLevelAndNewFund()
    {
        yield return Load(Scene1);
        FreezeCalendar();
        foreach (Component tile in Tiles())
            Water(tile);
        yield return null;

        Assert.IsTrue(HasWon());
        GameObject winPanel = FindInScene("WinPanel");
        Assert.IsTrue(winPanel.activeInHierarchy, "winning message should be shown");
        StringAssert.Contains("You Win", TextOf("WinMessage"));

        var nextLevel = ButtonNamed("Next Level");
        Assert.IsTrue(nextLevel.gameObject.activeInHierarchy, "Next Level button shows together with the message");
        Assert.IsTrue(nextLevel.transform.IsChildOf(winPanel.transform));
        Assert.AreEqual("Next Level", nextLevel.GetComponentInChildren<TMPro.TMP_Text>().text);

        Assert.Greater(Fund, 0, "winning gives the player some fund");
        Assert.AreEqual("Fund: $ " + Fund, TextOf("FundAmount"), "the top right fund text shows the new amount");
    }

    [UnityTest]
    public IEnumerator Win_DoesNotFire_WhenATileWentBackToDrySoilBeforeTheLastOneWasWatered()
    {
        yield return Load(Scene1);
        FreezeCalendar();
        Component[] tiles = Tiles();
        for (int i = 0; i < 15; i++)
            Water(tiles[i]);

        PassDays(DaysUntilRevert()); // the slow player: the first 15 tiles dry up
        Assert.AreEqual("Tilled", Cond(tiles[0]));

        Water(tiles[15]);
        yield return null;
        Assert.IsFalse(HasWon(), "15 tiles are dry soil again, so watering the last tile must not win");
        Assert.AreEqual(0, Fund);

        // watering the dried tiles again does win
        foreach (Component tile in tiles)
            Water(tile);
        yield return null;
        Assert.IsTrue(HasWon());
    }

    [UnityTest]
    public IEnumerator Win_FiresOnlyOnce()
    {
        yield return Load(Scene1);
        FreezeCalendar();
        int timesWon = 0;
        ((UnityEvent)Get(FindOne("FarmWinCondition"), "playerWonEvent")).AddListener(() => timesWon++);

        foreach (Component tile in Tiles())
            Water(tile);
        int fundAfterFirstWin = Fund;

        PassDays(DaysUntilRevert()); // everything dries ...
        foreach (Component tile in Tiles())
            Water(tile);             // ... and is watered again
        PassDays(1);
        yield return null;

        Assert.AreEqual(1, timesWon);
        Assert.AreEqual(fundAfterFirstWin, Fund, "the reward is only paid once");
    }

    [UnityTest]
    public IEnumerator NextLevelButton_LoadsScene2Store()
    {
        yield return Load(Scene1);
        foreach (Component tile in Tiles())
            Water(tile);
        yield return null;
        int fund = Fund;

        ButtonNamed("Next Level").onClick.Invoke();
        yield return WaitForScene(Scene2);

        Assert.AreEqual(fund, Fund, "the fund earned in Scene1 is carried into the store");
        Assert.AreEqual("Fund: $ " + fund, TextOf("FundAmount"));
    }

    [UnityTest]
    public IEnumerator Store_HasMessage_And_StartNextLevelIsLockedUntilBuySeed()
    {
        StartSession(100, 0);
        yield return Load(Scene2);

        StringAssert.Contains("buy seeds", TextOf("StoreMessage"));
        var buySeed = ButtonNamed("Buy Seed");
        var startNextLevel = ButtonNamed("Start Next Level");
        Assert.AreEqual("Buy Seed", buySeed.GetComponentInChildren<TMPro.TMP_Text>().text);
        Assert.AreEqual("Start Next Level", startNextLevel.GetComponentInChildren<TMPro.TMP_Text>().text);
        Assert.IsTrue(buySeed.interactable);
        Assert.IsFalse(startNextLevel.interactable, "Start Next Level must be in-active before a seed is bought");

        buySeed.onClick.Invoke(); // HW6: buys the chosen number of seeds (5 to start with: the next level needs 5)
        yield return null;

        Assert.IsTrue(startNextLevel.interactable, "Start Next Level unlocks after Buy Seed");
        Assert.AreEqual(5, Seeds);
        Assert.AreEqual(50, Fund, "5 seeds at $10 each");
        Assert.AreEqual("Fund: $ " + Fund, TextOf("FundAmount"));
        Assert.AreEqual("Seeds: 5", TextOf("SeedAmount"));
    }

    [UnityTest]
    public IEnumerator Store_BuySeedWithoutEnoughFund_BuysNothingAndKeepsTheLock()
    {
        StartSession(5, 0); // less than the seeds cost
        yield return Load(Scene2);

        ButtonNamed("Buy Seed").onClick.Invoke();
        yield return null;

        Assert.AreEqual(5, Fund);
        Assert.AreEqual(0, Seeds);
        Assert.IsFalse(ButtonNamed("Start Next Level").interactable);
        StringAssert.Contains("Not enough", TextOf("StoreFeedback"));
    }

    [UnityTest]
    public IEnumerator Store_OpenedDirectly_UsesTheTestFund()
    {
        ForgetFarmFund(); // as if Play was pressed with Scene2 open: the scene's own Farm Fund supplies the test values
        yield return Load(Scene2);
        Assert.AreEqual(100, Fund);
        Assert.AreEqual("Fund: $ 100", TextOf("FundAmount"));
    }

    [UnityTest]
    public IEnumerator Store_StartNextLevel_LoadsScene3Plant()
    {
        StartSession(100, 0);
        yield return Load(Scene2);
        ButtonNamed("Buy Seed").onClick.Invoke();
        yield return null;

        ButtonNamed("Start Next Level").onClick.Invoke();
        yield return WaitForScene(Scene3);
        Assert.AreEqual("Seeds: 5", TextOf("SeedAmount"));
        Assert.AreEqual("Fund: $ 50", TextOf("FundAmount"), "the remaining balance shows in Scene3");
    }
}
