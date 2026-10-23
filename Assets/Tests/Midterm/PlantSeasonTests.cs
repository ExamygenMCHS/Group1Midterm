using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// Midterm Tasks 2 and 4: when a plant can be planted and when it disappears comes from its PlantData asset,
// so a new plant needs a new asset and no code. The game types are used directly (the test assembly references FarmGame).
// Scene5 starts on day 36, the first day of summer (fall starts on day 43, winter on day 50, spring again on day 57).
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
        yield return LoadScene5();
        AdvanceDays(21);//through winter to the next spring (day 57)
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
        yield return LoadScene5();
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
        yield return LoadScene5();
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

    // the levels start on different days (Scene3 day 8, Scene4-6 day 36, both the first day of summer), so a change to a
    // crop's seasons or growth days must still leave the player time to plant what the level asks for
    [UnityTest]
    public IEnumerator EveryPlantingLevel_GivesEachCropAtLeastFiveDaysToBePlanted()
    {
        foreach (string scene in new[] { Scene3, Scene4, Scene5, Scene6 })
        {
            int cropCount = 0;
            for (int i = 0; i == 0 || i < cropCount; i++)
            {
                if (scene == Scene3 || scene == Scene4)
                    StartSession(50, 0, scene == Scene3 ? 2 : 3);
                else
                    StartBagSession(60, scene == Scene5 ? 3 : 4);
                yield return Load(scene);
                FreezeCalendar();
                var crops = (PlantData[])Get(PlantManagerScript(), "crops");
                cropCount = crops.Length;
                Assert.Greater(cropCount, 0, scene + " has no crops to plant");
                PlantData crop = crops[i];
                var calendar = (GameCalendar)Calendar();

                int window = 0;
                int growingDays = crop.daysToGrowing + crop.daysToMature;
                while (window < 40 && crop.CanPlantIn(calendar.season)
                       && !(crop.disappears && calendar.DaysUntil(crop.disappearSeason) <= growingDays))
                {
                    window++;
                    AdvanceDays(1);
                }
                Assert.GreaterOrEqual(window, 5, scene + ": " + crop.cropName + " can only be planted on " + window + " day(s) from the start");
            }
        }
    }

    [UnityTest]
    public IEnumerator Plant_WithersInTheSeasonItsOwnDataNames()
    {
        yield return LoadScene5();
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
    public IEnumerator Scene6_LoadsFromItsBuildName_WithBothCropsReadyToPlant()
    {
        StartBagSession(60, 4);
        AddSeeds("Pumpkin", 3);
        yield return Load(Scene6);
        FreezeCalendar();

        Assert.AreEqual(2, (int)Call(PlantManagerScript(), "GetCropCount"));
        var tile = Tiles()[0];
        Hold("SeedBag", Crop("Pumpkin"));
        yield return PlantOn(tile);
        Assert.AreEqual("Pumpkin", ((Plant)PlantOnTile(tile)).crop.cropName);
        Assert.AreEqual(2, SeedsOf("Pumpkin"));
    }

    [UnityTest]
    public IEnumerator EveryStoreVisit_SendsThePlayerToASceneInTheBuild()
    {
        for (int levelsWon = 1; levelsWon <= 4; levelsWon++)
        {
            StartSession(100, 0, levelsWon);
            yield return Load(Scene2);
            string next = Object.FindAnyObjectByType<StoreControl>().GetNextLevelScene();
            Assert.IsTrue(Application.CanStreamedLevelBeLoaded(next), "after " + levelsWon + " won level(s) the store loads " + next + ", which is not in the build");
        }
    }
}
