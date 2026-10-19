using System.Collections;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// Shared helpers for the HW6 Play Mode tests (on top of the HW5 ones: reflection, scenes, tiles, Farm Fund).
public abstract class HW6TestBase : HW5TestBase
{
    protected static Component PlantManagerScript()
    {
        GameObject manager = GameObject.Find("PlantManager");
        Assert.IsNotNull(manager, "the level needs a PlantManager");
        return manager.GetComponent(GameType("PlantManager"));
    }

    protected static Transform PlantsParent()
    {
        return PlantManagerScript().transform;
    }

    protected static Component Goal()
    {
        return FindOne("HarvestGoal");
    }

    protected static string GoalState()
    {
        return Get(Goal(), "goalState").ToString();
    }

    // Space on the tile: till grass (hoe in the hand) or water soil (watering can), and wait for the action to finish
    protected static IEnumerator SpaceOn(Component tile)
    {
        PointAt(tile);
        Assert.IsTrue(Space(), "Space on " + tile.name);
        yield return WaitForPlayerIdle();
    }

    // E on the tile: plant on a watered empty tile (seeds in the hand), harvest a mature plant (sickle in the hand);
    // waits for the action to finish
    protected static IEnumerator EOn(Component tile, bool expected = true)
    {
        PointAt(tile);
        HoldForE(tile);
        Assert.AreEqual(expected, (bool)Call(PlantManagerScript(), "InteractWithCurTile"), "E on " + tile.name);
        yield return WaitForPlayerIdle();
    }

    // Hotbar: the item E needs on this tile: the sickle for a plant, otherwise seeds (seeds already in the hand stay there)
    protected static void HoldForE(Component tile)
    {
        if (PlantOnTile(tile) != null)
            Hold("Sickle");
        else if (HeldItem() != "SeedBag")
            Hold("SeedBag");
    }

    // straight to a watered tile with a plant on it (tile API for the soil, the player's action for the seed)
    protected static IEnumerator PlantOn(Component tile)
    {
        Water(tile);
        yield return EOn(tile);
    }

    protected static Component PlantOnTile(Component tile)
    {
        return (Component)Call(PlantManagerScript(), "GetPlantOn", tile.gameObject);
    }

    protected static string HudMessage()
    {
        return HudCanvas().Find("HudMessage/MessageLabel").GetComponent<TMP_Text>().text;
    }

    protected static string HudText(string path)
    {
        Transform t = HudCanvas().Find(path);
        Assert.IsNotNull(t, "HUD has no " + path);
        return t.GetComponent<TMP_Text>().text;
    }

    // ---- store rows

    protected static Component Row(string name)
    {
        GameObject row = FindInScene(name);
        Assert.IsNotNull(row, "the store has no row " + name);
        return row.GetComponent(GameType("StoreItemRow"));
    }

    protected static string RowText(string row, string child)
    {
        return FindInScene(row).transform.Find(child).GetComponent<TMP_Text>().text;
    }

    protected static Button RowButton(string row, string child)
    {
        return FindInScene(row).transform.Find(child).GetComponent<Button>();
    }

    protected static void AddHarvest(string crop, int amount)
    {
        Call(Inventory(), "AddHarvested", Crop(crop), amount);
    }

    protected static void AddSeeds(string crop, int amount)
    {
        Call(Inventory(), "AddSeeds", Crop(crop), amount);
    }
}
