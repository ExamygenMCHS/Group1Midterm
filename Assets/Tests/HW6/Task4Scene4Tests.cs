using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// HW6 Task 4 (was Scene4-Harvest): the farm uses what was bought in the store - the speed boots and both crops -
// and the whole loop from the intro to a harvest
public class Task4Scene4Tests : HW6TestBase
{
    // a player who comes back from the store with the boots (or not) and both crops' seeds
    private static IEnumerator LoadFarmWith(bool boots, int pumpkinSeeds = 3, int sunflowerSeeds = 3)
    {
        StartSession(0, 0);
        AddSeeds("Pumpkin", pumpkinSeeds);
        AddSeeds("Sunflower", sunflowerSeeds);
        Call(Inventory(), "SetSpeedBoots", boots);
        yield return Load(Farm);
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
        yield return LoadFarmWith(true);
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
        yield return LoadFarmWith(false);
        Assert.AreEqual(BaseSpeed(), PlayerSpeed(), 0.001f);
        foreach (Transform t in GameObject.Find("Player").GetComponentsInChildren<Transform>(true))
            if (t.name == "SpeedBoot") Assert.IsFalse(t.gameObject.activeInHierarchy);
    }

    [UnityTest]
    public IEnumerator TheFarm_OffersBothCrops_InTheirOwnHotbarSlots_AndPumpkinsGrowSlower()
    {
        yield return LoadFarmWith(false);
        AdvanceDays(7); // a new game starts in spring; pumpkins are planted from summer on
        Assert.AreEqual("Summer", Season());
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

    // the whole loop through the intro: a new game, the tools picked up from the ground, a crop grown and harvested
    [UnityTest]
    [Timeout(120000)]
    public IEnumerator WholeLoop_FromTheIntro_ToAHarvestThatGivesSeedsBack()
    {
        yield return Load(Scene0);
        ButtonNamed("Start").onClick.Invoke();
        yield return WaitForScene(Farm);
        FreezeCalendar();
        // Hotbar: a new game starts with the tools on the ground and the starting seeds in the hand
        Assert.AreEqual("SeedBag", HeldItem());
        yield return PickUp("Hoe");
        yield return PickUp("WateringCan");
        Assert.AreEqual("WateringCan", HeldItem());
        FillCan();
        yield return PickUp("Sickle");
        Assert.IsNull(PickupOf("Hoe"));
        Assert.IsNull(PickupOf("WateringCan"));
        Assert.IsNull(PickupOf("Sickle"));

        Component[] tiles = Tiles();
        Time.timeScale = 3f;
        Hold("SeedBag", Crop("Sunflower")); // spring: sunflowers
        for (int i = 0; i < 4; i++)
            yield return PlantOn(tiles[i]);
        Assert.AreEqual(0, SeedsOf("Sunflower"), "the 4 starting sunflower seeds are planted");
        PassDays(4);
        for (int i = 0; i < 4; i++)
            yield return EOn(tiles[i]);
        Time.timeScale = 1f;

        Assert.AreEqual(4, HarvestedOf("Sunflower"));
        Assert.That(SeedsOf("Sunflower"), Is.InRange(4, 8), "every harvest gave back 1 or 2 seeds, so the farm keeps going");
        Assert.AreEqual(4, SeedsOf("Pumpkin"), "the pumpkin seeds wait for summer");
        Assert.AreEqual("Fund: $ 0", TextOf("FundAmount"));
    }
}
