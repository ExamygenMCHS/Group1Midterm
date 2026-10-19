using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// HW6 Task 4: Scene4-Harvest, the 3rd farming level, uses what was bought in the second store visit
public class Task4Scene4Tests : HW6TestBase
{
    private static IEnumerator LoadScene4(bool boots, int pumpkinSeeds = 3, int sunflowerSeeds = 3)
    {
        StartSession(0, 0, 2);
        AddSeeds("Pumpkin", pumpkinSeeds);
        AddSeeds("Sunflower", sunflowerSeeds);
        Call(Inventory(), "SetSpeedBoots", boots);
        yield return Load(Scene4);
        FreezeCalendar();
    }

    private static float PlayerSpeed()
    {
        return (float)Get(FindOne("PlayerMove"), "forwardSpeed");
    }

    private static float BaseSpeed()
    {
        return (float)Get(Get(FindOne("FarmBalance"), "settings"), "playerSpeed");
    }

    [UnityTest]
    public IEnumerator SpeedBoots_FromTheStore_MakeThePlayerFaster_AndAreWornByTheFarmer()
    {
        yield return LoadScene4(true);
        float multiplier = (float)Get(Get(FindOne("FarmBalance"), "settings"), "speedBootsMultiplier");
        Assert.Greater(multiplier, 1f);
        Assert.AreEqual(BaseSpeed() * multiplier, PlayerSpeed(), 0.001f, "the boots raise the walking speed");
        int shown = 0;
        foreach (Transform t in GameObject.Find("Player").GetComponentsInChildren<Transform>(true))
            if (t.name == "SpeedBoot" && t.gameObject.activeInHierarchy) shown++;
        Assert.AreEqual(2, shown, "the golden boots are on both feet");
    }

    [UnityTest]
    public IEnumerator WithoutSpeedBoots_ThePlayerWalksAtTheNormalSpeed()
    {
        yield return LoadScene4(false);
        Assert.AreEqual(BaseSpeed(), PlayerSpeed(), 0.001f);
        foreach (Transform t in GameObject.Find("Player").GetComponentsInChildren<Transform>(true))
            if (t.name == "SpeedBoot") Assert.IsFalse(t.gameObject.activeInHierarchy);
    }

    [UnityTest]
    public IEnumerator Scene4_OffersBothCrops_InTheirOwnHotbarSlots_AndPumpkinsGrowSlower()
    {
        yield return LoadScene4(false);
        Assert.AreEqual(2, (int)Call(PlantManagerScript(), "GetCropCount"));
        StringAssert.Contains("Sunflower seeds: 3", TextOf("SeedAmount"));
        Hold("SeedBag", Crop("Pumpkin")); // the pumpkin seed slot
        yield return null;
        StringAssert.Contains("Pumpkin seeds: 3", TextOf("SeedAmount"), "the pumpkin seeds are in the hand");

        Component[] tiles = Tiles();
        yield return PlantOn(tiles[0]); // pumpkin
        Hold("SeedBag", Crop("Sunflower"));
        yield return PlantOn(tiles[1]); // sunflower
        Component pumpkin = PlantOnTile(tiles[0]);
        Component sunflower = PlantOnTile(tiles[1]);
        Assert.AreEqual("Pumpkin", Get(Get(pumpkin, "crop"), "cropName"));
        Assert.AreEqual("Sunflower", Get(Get(sunflower, "crop"), "cropName"));

        PassDays(4);
        Assert.AreEqual("Mature", Get(sunflower, "plantStage").ToString());
        Assert.AreEqual("Growing", Get(pumpkin, "plantStage").ToString(), "pumpkins take longer");
        PassDays(2);
        Assert.AreEqual("Mature", Get(pumpkin, "plantStage").ToString());
    }

    [UnityTest]
    public IEnumerator Scene4_IsWonByHarvestingThreePumpkinsAndThreeSunflowers_ThenNextLevelOpensTheStore()
    {
        yield return LoadScene4(true);
        StringAssert.Contains("3 pumpkins and 3 sunflowers", TextOf("Instruction"));
        Component[] tiles = Tiles();
        Time.timeScale = 3f;
        for (int i = 0; i < 3; i++)
            yield return PlantOn(tiles[i]); // sunflowers (the first seed slot)
        Call(PlantManagerScript(), "SwitchSeed"); // Q: the next seed slot, the pumpkins
        for (int i = 3; i < 6; i++)
            yield return PlantOn(tiles[i]); // pumpkins
        PassDays(6);
        for (int i = 0; i < 6; i++)
        {
            Assert.AreEqual("Playing", GoalState(), "not won before the last plant");
            yield return EOn(tiles[i]);
        }
        Time.timeScale = 1f;
        yield return null;

        Assert.AreEqual("Won", GoalState());
        Assert.AreEqual(3, (int)Get(FarmFund(), "levelsWon"));
        // midterm: Scene4 is no longer the last level; the store sells the bag next (third visit)
        StringAssert.Contains("Sell your crops in the store", TextOf("WinMessage"));
        var nextLevel = ButtonNamed("Next Level");
        Assert.IsTrue(nextLevel.gameObject.activeInHierarchy);
        nextLevel.onClick.Invoke();
        yield return WaitForScene(Scene2);
        Assert.IsTrue(FindInScene("Row - Bag").activeInHierarchy, "the third store visit sells the bag");
    }

    // the whole game through its buttons: water level, store, plant level, store again, Scene4
    [UnityTest]
    [Timeout(120000)]
    public IEnumerator WholeGame_Scene0_To_Scene4_ThroughTheButtons()
    {
        yield return Load(Scene0);
        ButtonNamed("Start").onClick.Invoke();
        yield return WaitForScene(Scene1);
        FreezeCalendar();
        // Hotbar: a new game starts with the tools on the ground
        Assert.AreEqual("None", HeldItem());
        yield return PickUp("Hoe");
        yield return PickUp("WateringCan");
        Assert.AreEqual("WateringCan", HeldItem());
        foreach (Component tile in Tiles())
            Water(tile);
        yield return null;
        ButtonNamed("Next Level").onClick.Invoke();
        yield return WaitForScene(Scene2);
        Assert.AreEqual(100, Fund);

        ButtonNamed("Buy Seed").onClick.Invoke(); // 5 sunflower seeds
        yield return null;
        ButtonNamed("Start Next Level").onClick.Invoke();
        yield return WaitForScene(Scene3);
        FreezeCalendar();
        Assert.AreEqual("Fund: $ 50", TextOf("FundAmount"));
        // the hoe and the can came along from Scene1; the sickle waits by the fence
        Assert.IsNull(PickupOf("Hoe"));
        Assert.IsNull(PickupOf("WateringCan"));
        Assert.IsTrue((bool)Call(Hotbar(), "HasItem", ToolType("Hoe")));
        Assert.IsTrue((bool)Call(Hotbar(), "HasItem", ToolType("WateringCan")));
        yield return PickUp("Sickle");

        Component[] tiles = Tiles();
        Time.timeScale = 3f;
        for (int i = 0; i < 5; i++)
            yield return PlantOn(tiles[i]);
        PassDays(4);
        for (int i = 0; i < 5; i++)
            yield return EOn(tiles[i]);
        Time.timeScale = 1f;
        Assert.AreEqual("Won", GoalState());
        ButtonNamed("Next Level").onClick.Invoke();
        yield return WaitForScene(Scene2);
        Assert.AreEqual(100, Fund, "$50 + $50 bonus");

        ButtonNamed("Sell Sunflowers").onClick.Invoke(); // +75
        ButtonNamed("Buy Pumpkin Seed").onClick.Invoke(); // -60
        ButtonNamed("Buy Speed Boots").onClick.Invoke(); // -35
        Call(Row("Row - Sunflower Seeds"), "SetQuantity", 3);
        ButtonNamed("Buy Seed").onClick.Invoke(); // -30
        yield return null;
        Assert.AreEqual(50, Fund);
        Assert.IsTrue(ButtonNamed("Start Next Level").interactable);
        ButtonNamed("Start Next Level").onClick.Invoke();
        yield return WaitForScene(Scene4);

        Assert.AreEqual("Fund: $ 50", TextOf("FundAmount"), "the fund carried through every scene");
        Assert.AreEqual(3, SeedsOf("Pumpkin"));
        Assert.AreEqual(3, SeedsOf("Sunflower"));
        Assert.Greater(PlayerSpeed(), BaseSpeed(), "the boots bought in the store work in Scene4");
        Assert.IsTrue((bool)Call(Hotbar(), "HasItem", ToolType("Sickle")), "the sickle came along from Scene3");
        Assert.IsNull(PickupOf("Sickle"));
    }
}
