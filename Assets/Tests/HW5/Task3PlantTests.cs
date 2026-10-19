using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

// HW5 Task 3: plants in Scene3-Plant
public class Task3PlantTests : HW5TestBase
{
    private static Transform PlantManager()
    {
        GameObject manager = GameObject.Find("PlantManager");
        Assert.IsNotNull(manager, "Scene3 needs an object called PlantManager");
        return manager.transform;
    }

    // E with the seeds in the hand (Hotbar: slot 3)
    private static bool TryPlant()
    {
        Hold("SeedBag");
        return (bool)Call(PlantManager().GetComponent(GameType("PlantManager")), "TryPlantOnCurTile");
    }

    // HW6: planting is an action of the player's state machine; the plant appears when the seed goes into the soil
    private static IEnumerator Plant(bool expected, string message = "")
    {
        Assert.AreEqual(expected, TryPlant(), message);
        yield return WaitForPlayerIdle();
    }

    private static string Stage(Transform plant)
    {
        return Get(plant.GetComponent(GameType("Plant")), "plantStage").ToString();
    }

    // names of the active child models of a plant, e.g. "PlantedModel (small)"
    private static string ActiveModels(Transform plant)
    {
        string names = "";
        foreach (Transform model in plant)
            if (model.gameObject.activeSelf) names += model.name + ";";
        return names;
    }

    private IEnumerator LoadPlantScene(int seeds = 5)
    {
        StartSession(50, seeds);
        yield return Load(Scene3);
        FreezeCalendar();
    }

    [UnityTest]
    public IEnumerator Scene3_IsTheFarmWithoutTheWaterToWinLogic()
    {
        yield return LoadPlantScene();
        Assert.IsNull(FindOne("FarmWinCondition"), "water-to-win stays in Scene1");
        foreach (Component tile in Tiles())
            Water(tile);
        yield return null;
        Assert.IsFalse(FindInScene("WinPanel").activeInHierarchy, "watering every tile does not win Scene3 (HW6: harvesting does)");
        Assert.AreEqual(50, Fund, "no reward for watering in Scene3");

        // the Task 2 features carry over
        Assert.IsNotNull(FindInScene("EnergyBar"));
        Assert.IsNotNull(FindInScene("Minimap"));
        Assert.IsNotNull(GameObject.Find("SleepArea"));
        Assert.AreEqual("Seeds: 5", TextOf("SeedAmount"));
    }

    [UnityTest]
    public IEnumerator PlantPrefab_HasThreeModels_OnlyTheSmallOneVisible_AllSmallerThanATile()
    {
        yield return LoadPlantScene();
        Component tile = Tiles()[0];
        Water(tile);
        PointAt(tile);
        yield return Plant(true);
        Transform plant = PlantManager().GetChild(0);

        Assert.AreEqual(3, plant.childCount, "Plant = empty parent with the 3 stage models as children");
        Assert.AreEqual("PlantedModel (small);", ActiveModels(plant), "only the small model is visible when planted");

        float tileSize = tile.transform.localScale.x;
        foreach (Transform model in plant)
        {
            model.gameObject.SetActive(true);
            Bounds bounds = new Bounds(model.position, Vector3.zero);
            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>())
                bounds.Encapsulate(renderer.bounds);
            Assert.Less(bounds.size.x, tileSize, model.name + " is wider than a tile");
            Assert.Less(bounds.size.z, tileSize, model.name + " is deeper than a tile");
        }
        yield return null;
    }

    [UnityTest]
    public IEnumerator Planting_WorksOnlyOnAWateredTile()
    {
        yield return LoadPlantScene();
        Component tile = Tiles()[6];
        PointAt(tile);

        Assert.AreEqual("Grass", Cond(tile));
        yield return Plant(false, "no plant on grass");
        Assert.AreEqual(0, PlantManager().childCount);

        Call(tile, "InteractWithFarmTile");
        Assert.AreEqual("Tilled", Cond(tile));
        yield return Plant(false, "no plant on dry soil");
        Assert.AreEqual(0, PlantManager().childCount);

        Call(tile, "InteractWithFarmTile");
        Assert.AreEqual("Watered", Cond(tile));
        yield return Plant(true, "a plant spawns on a watered tile");
        Assert.AreEqual(1, PlantManager().childCount);

        Transform plant = PlantManager().GetChild(0);
        Assert.AreSame(PlantManager(), plant.parent, "spawned plants are children of PlantManager");
        StringAssert.StartsWith("Plant", plant.name);
        Assert.AreEqual(tile.transform.position.x, plant.position.x, 0.001f, "centred on the tile");
        Assert.AreEqual(tile.transform.position.z, plant.position.z, 0.001f);
        Assert.AreEqual(tile.transform.position.y + tile.transform.localScale.y / 2, plant.position.y, 0.001f, "on top of the tile");
        yield return null;
    }

    [UnityTest]
    public IEnumerator Planting_NothingHappens_WhenThePlayerIsNotOnATile()
    {
        yield return LoadPlantScene();
        PointAt(null);
        Assert.IsFalse(TryPlant());
        Assert.AreEqual(0, PlantManager().childCount);
    }

    [UnityTest]
    public IEnumerator Planting_TwiceOnTheSameTile_SpawnsOnlyOnePlant()
    {
        yield return LoadPlantScene();
        Component tile = Tiles()[3];
        Water(tile);
        PointAt(tile);
        Assert.IsTrue(TryPlant());
        Assert.IsFalse(TryPlant(), "a second press while the seed goes in is ignored");
        yield return WaitForPlayerIdle();
        yield return Plant(false, "the tile already has a plant");
        Assert.AreEqual(1, PlantManager().childCount);
        Assert.AreEqual(4, Seeds, "the refused attempts do not use a seed");
    }

    [UnityTest]
    public IEnumerator Planting_UsesASeedAndEnergy_AndIsBlockedWithoutEither()
    {
        yield return LoadPlantScene(seeds: 1);
        Component[] tiles = Tiles();
        Water(tiles[0]);
        Water(tiles[1]);
        Water(tiles[2]);

        float before = CurEnergy();
        PointAt(tiles[0]);
        Assert.IsTrue(TryPlant());
        Assert.AreEqual(0, Seeds);
        Assert.Less(CurEnergy(), before, "planting costs energy");
        yield return WaitForPlayerIdle();
        Assert.AreEqual("Seeds: 0", TextOf("SeedAmount"));

        PointAt(tiles[1]);
        yield return Plant(false, "no seeds left");
        Assert.AreEqual(1, PlantManager().childCount);

        Call(Inventory(), "AddSeeds", Crop("Sunflower"), 3);
        DrainEnergy();
        PointAt(tiles[2]);
        yield return Plant(false, "out of energy");
        Assert.AreEqual(1, PlantManager().childCount);
        Assert.AreEqual(3, Seeds, "a blocked attempt does not use a seed");
    }

    [UnityTest]
    public IEnumerator PlantKey_E_PlantsOnTheWateredTile()
    {
        yield return LoadPlantScene();
        SendInputToGameView();
        Keyboard keyboard = Keyboard.current ?? InputSystem.AddDevice<Keyboard>();
        Component[] tiles = Tiles();

        PointAt(tiles[9]); // grass
        yield return Task2MinimapAndEnergyTests.PressKey(keyboard, Key.Digit3); // Hotbar: the seeds into the hand
        yield return Task2MinimapAndEnergyTests.PressKey(keyboard, Key.E);
        Assert.AreEqual(0, PlantManager().childCount, "E on grass does nothing");

        Water(tiles[9]);
        yield return Task2MinimapAndEnergyTests.PressKey(keyboard, Key.E);
        yield return WaitForPlayerIdle();
        Assert.AreEqual(1, PlantManager().childCount, "E on a watered tile plants");
    }

    [UnityTest]
    public IEnumerator Plant_GrowsWithTheInGameDays_SmallToMidToLarge_ThenStops()
    {
        yield return LoadPlantScene();
        Component tile = Tiles()[10];
        Water(tile);
        PointAt(tile);
        yield return Plant(true); // Plant.Start subscribes to the day passed event

        Transform plant = PlantManager().GetChild(0);
        Component plantScript = plant.GetComponent(GameType("Plant"));
        int daysToGrowing = (int)Get(plantScript, "daysToGrowing");
        int daysToMature = (int)Get(plantScript, "daysToMature");
        Assert.Greater(daysToGrowing, 1, "growing takes a few days");
        Assert.Greater(daysToMature, 1);

        Assert.AreEqual("Planted", Stage(plant));
        PassDays(daysToGrowing - 1);
        Assert.AreEqual("Planted", Stage(plant), "not yet");
        PassDays(1);
        Assert.AreEqual("Growing", Stage(plant));
        Assert.AreEqual("GrowingModel (mid);", ActiveModels(plant), "small hidden, mid shown");

        PassDays(daysToMature - 1);
        Assert.AreEqual("Growing", Stage(plant), "not yet");
        PassDays(1);
        Assert.AreEqual("Mature", Stage(plant));
        Assert.AreEqual("MatureModel (large);", ActiveModels(plant), "mid hidden, large shown");

        PassDays(20);
        Assert.AreEqual("Mature", Stage(plant), "a mature plant no longer changes");
        Assert.AreEqual("MatureModel (large);", ActiveModels(plant));
    }

    [UnityTest]
    public IEnumerator Tile_StillDriesAndTurnsToGrass_IndependentlyOfThePlantOnIt()
    {
        yield return LoadPlantScene();
        Component tile = Tiles()[12];
        Water(tile);
        PointAt(tile);
        yield return Plant(true);
        Transform plant = PlantManager().GetChild(0);

        PassDays(DaysUntilRevert());
        Assert.AreEqual("Tilled", Cond(tile), "the tile dries although a plant stands on it");
        PassDays(DaysUntilRevert());
        Assert.AreEqual("Grass", Cond(tile));

        Assert.IsTrue(plant != null, "the plant stays");
        Assert.AreEqual("Mature", Stage(plant), "and kept growing");
    }

    [UnityTest]
    public IEnumerator Plants_StopListeningToTheDayEvent_WhenTheSceneIsLeft()
    {
        yield return LoadPlantScene();
        Component tile = Tiles()[0];
        Water(tile);
        PointAt(tile);
        yield return Plant(true);

        yield return Load(Scene3); // reload: the old plant is destroyed
        FreezeCalendar();
        Assert.AreEqual(0, PlantManager().childCount);
        PassDays(3); // would throw if a destroyed plant was still subscribed
        yield return null;
    }
}
