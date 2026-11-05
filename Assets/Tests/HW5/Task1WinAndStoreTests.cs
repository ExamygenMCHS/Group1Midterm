using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

// HW5 Task 1: the build list and the store in Scene2-Store (the water-to-win level is now part of The Farm, without the win)
public class Task1WinAndStoreTests : HW5TestBase
{
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
        // the farm levels are one scene now: the intro, The Farm, and the store (opened later by the wandering trader)
        CollectionAssert.AreEqual(new[] { Scene0, Farm, Scene2 }, scenes);
#if UNITY_EDITOR
        // the saved list must point at the scene files that exist now (a recreated scene gets a new GUID)
        string saved = System.IO.File.ReadAllText("ProjectSettings/EditorBuildSettings.asset");
        foreach (string name in new[] { Scene0, Farm, Scene2 })
        {
            string guid = UnityEditor.AssetDatabase.AssetPathToGUID("Assets/Game/Scenes/" + name + ".unity");
            StringAssert.Contains("guid: " + guid, saved, name + " has a stale GUID in the saved build list (remove and re-add it, then File > Save Project)");
        }
#endif
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
    public IEnumerator Store_StartNextLevel_GoesBackToTheFarm_WithTheBalanceAndSeeds()
    {
        StartSession(100, 0);
        yield return Load(Scene2);
        ButtonNamed("Buy Seed").onClick.Invoke();
        yield return null;

        ButtonNamed("Start Next Level").onClick.Invoke();
        yield return WaitForScene(Farm);
        Assert.AreEqual(5, SeedsOf("Sunflower"), "the seeds bought in the store come back to the farm");
        Assert.AreEqual(50, Fund, "the farm keeps the player's fund (it is the hub, not a new game)");
        Assert.AreEqual("Fund: $ 50", TextOf("FundAmount"), "the remaining balance shows on the farm");
    }
}
