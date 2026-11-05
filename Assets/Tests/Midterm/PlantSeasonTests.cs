using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// Midterm Tasks 2 and 4: when a plant can be planted and when it disappears comes from its PlantData asset,
// so a new plant needs a new asset and no code. The game types are used directly (the test assembly references FarmGame).
// The Farm starts on day 1, the first day of spring (summer starts on day 8, fall on day 15, winter on day 22, spring again on day 29).
public class PlantSeasonTests : MidtermTestBase
{
    private static PlantData Data(string cropName)
    {
        return (PlantData)Crop(cropName);
    }

    [Test]
    public void SunflowerAndPumpkin_AssetsDifferInSeasons_NotOnlyInSpeed()
    {
        PlantData sunflower = Data("Sunflower");
        PlantData pumpkin = Data("Pumpkin");

        CollectionAssert.AreEquivalent(new[] { GameCalendar.Season.Spring, GameCalendar.Season.Summer }, sunflower.plantableSeasons);
        CollectionAssert.AreEquivalent(new[] { GameCalendar.Season.Summer, GameCalendar.Season.Fall }, pumpkin.plantableSeasons);
        foreach (PlantData crop in new[] { sunflower, pumpkin })
        {
            Assert.IsTrue(crop.DiesIn(GameCalendar.Season.Winter), crop.cropName + " withers when winter starts");
            Assert.IsFalse(crop.DiesIn(GameCalendar.Season.Fall), crop.cropName + " is fine in fall");
            Assert.IsFalse(crop.CanPlantIn(GameCalendar.Season.Winter), "nothing is planted in winter");
        }
        Assert.Less(sunflower.daysToGrowing + sunflower.daysToMature, pumpkin.daysToGrowing + pumpkin.daysToMature,
            "pumpkins also grow slower");
    }

    [Test]
    public void PlantData_AnswersFromItsOwnLists_WithoutKnowingAnyCrop()
    {
        PlantData crop = ScriptableObject.CreateInstance<PlantData>();
        crop.plantableSeasons = new[] { GameCalendar.Season.Summer, GameCalendar.Season.Fall };
        crop.disappears = true;
        crop.disappearSeason = GameCalendar.Season.Fall;

        Assert.IsFalse(crop.CanPlantIn(GameCalendar.Season.Spring));
        Assert.IsTrue(crop.CanPlantIn(GameCalendar.Season.Summer));
        Assert.IsTrue(crop.CanPlantIn(GameCalendar.Season.Fall));
        Assert.IsFalse(crop.CanPlantIn(GameCalendar.Season.Winter));
        Assert.IsTrue(crop.DiesIn(GameCalendar.Season.Fall), "the disappear season is read from the data, it is not always winter");
        Assert.IsFalse(crop.DiesIn(GameCalendar.Season.Winter));

        crop.disappears = false;
        Assert.IsFalse(crop.DiesIn(GameCalendar.Season.Fall), "a plant that does not disappear survives every season");

        Assert.IsTrue(crop.TryGetNextPlantableSeason(GameCalendar.Season.Spring, out GameCalendar.Season next));
        Assert.AreEqual(GameCalendar.Season.Summer, next);
        Assert.IsTrue(crop.TryGetNextPlantableSeason(GameCalendar.Season.Summer, out next));
        Assert.AreEqual(GameCalendar.Season.Fall, next);
        Assert.IsFalse(crop.TryGetNextPlantableSeason(GameCalendar.Season.Fall, out next), "no later season this year");

        crop.plantableSeasons = null;
        Assert.IsFalse(crop.CanPlantIn(GameCalendar.Season.Summer), "a crop with no list can't be planted, and doesn't throw");
        Object.DestroyImmediate(crop);
    }

    [UnityTest]
    public IEnumerator PumpkinInSpring_IsRefusedWithAHint_AndCostsNothing()
    {
        yield return LoadFarm(); // a new game starts in spring
        Assert.AreEqual("Spring", Season());
        var manager = (PlantManager)PlantManagerScript();
        var tile = Tiles()[0];
        Water(tile);
        PointAt(tile);
        Hold("SeedBag", Crop("Pumpkin"));
        float energy = ((PlayerEnergy)Energy()).GetCurEnergy();

        Assert.IsFalse(manager.TryPlantOnCurTile());
        StringAssert.Contains("Pumpkin cannot be planted in spring", HudMessage());
        StringAssert.Contains("Wait for summer", HudMessage());
        Assert.AreEqual(4, SeedsOf("Pumpkin"));
        Assert.AreEqual(energy, ((PlayerEnergy)Energy()).GetCurEnergy());
        Assert.IsNull(PlantOnTile(tile));
    }

    [UnityTest]
    public IEnumerator InSummer_BothCropsCanBePlanted_EachWithItsOwnData()
    {
        yield return LoadFarmInSummer();
        Assert.AreEqual("Summer", Season());
        var sunflowerTile = Tiles()[0];
        var pumpkinTile = Tiles()[1];

        Hold("SeedBag", Crop("Sunflower"));
        yield return PlantOn(sunflowerTile);
        Hold("SeedBag", Crop("Pumpkin"));
        yield return PlantOn(pumpkinTile);

        var sunflower = (Plant)PlantOnTile(sunflowerTile);
        var pumpkin = (Plant)PlantOnTile(pumpkinTile);
        Assert.AreEqual("Sunflower", sunflower.crop.cropName);
        Assert.AreEqual("Pumpkin", pumpkin.crop.cropName);
        Assert.AreEqual(sunflower.crop.daysToGrowing, sunflower.daysToGrowing, "growth days come from the sunflower's data");
        Assert.AreEqual(pumpkin.crop.daysToGrowing, pumpkin.daysToGrowing, "growth days come from the pumpkin's data");
        Assert.AreNotEqual(sunflower.daysToGrowing, pumpkin.daysToGrowing);
    }

    [UnityTest]
    public IEnumerator SunflowerInFall_IsRefused_ButThePumpkinStillGrows()
    {
        yield return LoadFarmInSummer();
        AdvanceDays(7);
        Assert.AreEqual("Fall", Season());
        var manager = (PlantManager)PlantManagerScript();
        var sunflowerTile = Tiles()[0];
        var pumpkinTile = Tiles()[1];

        Water(sunflowerTile);
        PointAt(sunflowerTile);
        Hold("SeedBag", Crop("Sunflower"));
        float energy = ((PlayerEnergy)Energy()).GetCurEnergy();
        Assert.IsFalse(manager.TryPlantOnCurTile());
        StringAssert.Contains("Sunflower cannot be planted in fall", HudMessage());
        Assert.AreEqual(4, SeedsOf("Sunflower"));
        Assert.AreEqual(energy, ((PlayerEnergy)Energy()).GetCurEnergy());

        Hold("SeedBag", Crop("Pumpkin"));
        yield return PlantOn(pumpkinTile);
        Assert.IsNotNull(PlantOnTile(pumpkinTile));
    }

    // free play starts a new game on the first day of spring: within the first year every crop on the farm must get
    // at least five days on which it can be planted and still has time to grow before its disappear season
    [UnityTest]
    public IEnumerator TheFarm_GivesEachCropAtLeastFiveDaysToBePlanted_InTheFirstYear()
    {
        yield return LoadFarm();
        var crops = (PlantData[])Get(PlantManagerScript(), "crops");
        Assert.Greater(crops.Length, 0, "The Farm has no crops to plant");
        var calendar = (GameCalendar)Calendar();
        int[] window = new int[crops.Length];
        int daysPerYear = calendar.GetDaysPerSeason() * GameCalendar.SeasonsPerYear;
        for (int day = 0; day < daysPerYear; day++)
        {
            for (int c = 0; c < crops.Length; c++)
            {
                int growingDays = crops[c].daysToGrowing + crops[c].daysToMature;
                if (crops[c].CanPlantIn(calendar.season)
                    && !(crops[c].disappears && calendar.DaysUntil(crops[c].disappearSeason) <= growingDays))
                    window[c]++;
            }
            AdvanceDays(1);
        }
        for (int c = 0; c < crops.Length; c++)
            Assert.GreaterOrEqual(window[c], 5, crops[c].cropName + " can only be planted on " + window[c] + " day(s) of the year");
    }

    [UnityTest]
    public IEnumerator Plant_WithersInTheSeasonItsOwnDataNames()
    {
        yield return LoadFarmInSummer();
        var normalTile = Tiles()[0];
        var shortLivedTile = Tiles()[1];
        Hold("SeedBag", Crop("Sunflower"));
        yield return PlantOn(normalTile);
        yield return PlantOn(shortLivedTile);
        var normal = (Plant)PlantOnTile(normalTile);
        var shortLived = (Plant)PlantOnTile(shortLivedTile);

        //the same plant with other data: it disappears when fall starts instead of winter
        PlantData fallKiller = Object.Instantiate(Data("Sunflower"));
        fallKiller.disappearSeason = GameCalendar.Season.Fall;
        shortLived.crop = fallKiller;

        AdvanceDays(7);
        Assert.AreEqual("Fall", Season());
        Assert.AreEqual(Plant.PlantStage.Withered, shortLived.plantStage, "fall is its disappear season");
        Assert.AreNotEqual(Plant.PlantStage.Withered, normal.plantStage, "the real sunflower only withers in winter");
        Object.DestroyImmediate(fallKiller);
    }

    [UnityTest]
    public IEnumerator TheFarm_LoadsFromItsBuildName_WithBothCropsReadyToPlant()
    {
        yield return LoadFarmInSummer(); // Load uses the build name; a new game brings 4 pumpkin seeds

        Assert.AreEqual(2, (int)Call(PlantManagerScript(), "GetCropCount"));
        var tile = Tiles()[0];
        Hold("SeedBag", Crop("Pumpkin"));
        yield return PlantOn(tile);
        Assert.AreEqual("Pumpkin", ((Plant)PlantOnTile(tile)).crop.cropName);
        Assert.AreEqual(3, SeedsOf("Pumpkin"));
    }
}
