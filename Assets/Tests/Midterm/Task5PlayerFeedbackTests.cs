using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class Task5PlayerFeedbackTests : MidtermTestBase
{
    [UnityTest]
    public IEnumerator Planting_GivesANextAction_AndOnlyConsumesOneSeed()
    {
        yield return LoadScene5();
        var manager = (PlantManager)PlantManagerScript();
        var tile = Tiles()[0];
        Water(tile);
        PointAt(tile);
        Hold("SeedBag", Crop("Sunflower"));
        Assert.IsTrue(manager.TryPlantOnCurTile());
        Assert.IsFalse(manager.TryPlantOnCurTile(), "a second press during the action cannot use another seed");
        yield return WaitForPlayerIdle();
        StringAssert.Contains("Planted a sunflower", HudMessage());
        StringAssert.Contains("other tiles", HudMessage());
        Assert.AreEqual(3, SeedsOf("Sunflower"));
        Assert.AreEqual(3, InBag("Sunflower Seeds"));
        Assert.IsNotNull(PlantOnTile(tile));
    }

    [UnityTest]
    public IEnumerator Planting_TooLateForWinter_KeepsSeedsAndEnergy()
    {
        yield return LoadScene5();
        AdvanceDays(((GameCalendar)Calendar()).DaysUntil(GameCalendar.Season.Winter) - 4);
        var manager = (PlantManager)PlantManagerScript();
        var tile = Tiles()[0];
        Water(tile);
        PointAt(tile);
        Hold("SeedBag", Crop("Sunflower"));
        float energy = ((PlayerEnergy)Energy()).GetCurEnergy();
        Assert.IsFalse(manager.TryPlantOnCurTile());
        Assert.AreEqual(4, SeedsOf("Sunflower"));
        Assert.AreEqual(energy, ((PlayerEnergy)Energy()).GetCurEnergy());
        Assert.IsNull(PlantOnTile(tile));
        StringAssert.Contains("4 growing days", HudMessage());
    }

    [UnityTest]
    public IEnumerator Planting_WithEnoughTime_StillAllowsTheLastSafeDay()
    {
        yield return LoadScene5();
        AdvanceDays(((GameCalendar)Calendar()).DaysUntil(GameCalendar.Season.Winter) - 5);
        var tile = Tiles()[0];
        yield return PlantOn(tile);
        AdvanceDays(4);
        Assert.IsTrue(((Plant)PlantOnTile(tile)).IsMature());
        yield return EOn(tile);
        Assert.AreEqual(1, HarvestedOf("Sunflower"));
        Assert.AreEqual(1, InBag("Sunflower"));
    }

    [UnityTest]
    public IEnumerator WinterFeedback_NamesTheCrop_WithoutUsingASeed()
    {
        yield return LoadScene5();
        AdvanceDays(((GameCalendar)Calendar()).DaysUntil(GameCalendar.Season.Winter));
        var tile = Tiles()[0];
        Water(tile);
        PointAt(tile);
        Hold("SeedBag", Crop("Sunflower"));
        Assert.IsFalse(((PlantManager)PlantManagerScript()).TryPlantOnCurTile());
        StringAssert.Contains("Sunflower cannot be planted in winter", HudMessage());
        Assert.AreEqual(4, SeedsOf("Sunflower"));
    }

    [UnityTest]
    public IEnumerator LongFeedback_HasReadingTime_AndClearsWhilePaused()
    {
        yield return LoadScene5();
        var message = (TimedMessage)Get(PlantManagerScript(), "hudMessage");
        string text = "Planted a sunflower! Work on other tiles or refill your can while it grows. Hold F to speed up days.";
        message.Show(text);
        Time.timeScale = 0f;
        yield return new WaitForSecondsRealtime(2.3f);
        Assert.AreEqual(text, HudMessage(), "the message must last long enough to read");
        yield return new WaitForSecondsRealtime(4f);
        Assert.AreEqual("", HudMessage(), "pause does not leave an old message stuck on screen");
    }
}
