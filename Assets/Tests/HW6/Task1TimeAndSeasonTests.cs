using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

// HW6 Task 1: time display canvas, day of the week and season enums, the new season event, winter and the plants
public class Task1TimeAndSeasonTests : HW6TestBase
{
    private static string TimeText(string name)
    {
        Transform panel = GameObject.Find("TimeDisplayCanvas").transform.Find("TimePanel");
        return panel.Find(name).GetComponent<TMP_Text>().text;
    }

    [UnityTest]
    public IEnumerator TimeDisplay_ReplacesTheDaysLabel_WithDayWeekDaySeasonAndClock()
    {
        yield return Load(Scene1);
        FreezeCalendar();
        yield return null;

        Assert.IsNull(FindInScene("DayLable"), "the example's \"Days: 1\" label is replaced");
        GameObject timeCanvas = GameObject.Find("TimeDisplayCanvas");
        Assert.IsNotNull(timeCanvas, "the time display is its own canvas");
        Assert.IsNotNull(timeCanvas.GetComponent<Canvas>());
        Assert.AreEqual("Day 1", TimeText("DayText"));
        Assert.AreEqual("Monday", TimeText("WeekDayText"));
        StringAssert.StartsWith("Spring", TimeText("SeasonText"));
        StringAssert.EndsWith("AM", TimeText("ClockText"));
    }

    [UnityTest]
    public IEnumerator WeekDaysAndSeasons_AreEnums_ThatChangeOnTheDayPassedEvent()
    {
        yield return Load(Scene1);
        FreezeCalendar();
        Component calendar = Calendar();
        Type weekDayType = Get(calendar, "weekDay").GetType();
        Type seasonType = Get(calendar, "season").GetType();
        Assert.IsTrue(weekDayType.IsEnum && seasonType.IsEnum, "day of the week and season are enums, like the tile conditions");
        CollectionAssert.AreEqual(new[] { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday" }, Enum.GetNames(weekDayType));
        CollectionAssert.AreEqual(new[] { "Spring", "Summer", "Fall", "Winter" }, Enum.GetNames(seasonType));

        int weeksPerSeason = (int)Get(calendar, "weeksPerSeason");
        int seasonDays = 7 * weeksPerSeason;
        var seasonsSeen = new List<string>();
        var newSeasonEvent = (UnityEvent)Get(calendar, "newSeasonEvent");
        newSeasonEvent.AddListener(() => seasonsSeen.Add(Season()));

        var weekDays = new List<string> { Get(calendar, "weekDay").ToString() };
        for (int day = 2; day <= 4 * seasonDays + 1; day++)
        {
            AdvanceDays(1);
            weekDays.Add(Get(calendar, "weekDay").ToString());
        }
        yield return null;

        CollectionAssert.AreEqual(new[] { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday", "Monday" }, weekDays.GetRange(0, 8),
            "Monday, Tuesday ... Sunday, then Monday again");
        CollectionAssert.AreEqual(new[] { "Summer", "Fall", "Winter", "Spring" }, seasonsSeen,
            "the new season event fires once per season, every " + seasonDays + " days");
        Assert.AreEqual("Day " + (4 * seasonDays + 1), TimeText("DayText"), "the time display follows the day event");
        StringAssert.StartsWith("Spring", TimeText("SeasonText"));
        StringAssert.Contains("Year 2", TimeText("SeasonText"));
    }

    [UnityTest]
    public IEnumerator WeeksPerSeason_IsAPublicNumber_ThatSetsHowLongASeasonIs()
    {
        yield return Load(Scene1);
        Component calendar = Calendar();
        var field = calendar.GetType().GetField("weeksPerSeason");
        Assert.IsNotNull(field, "weeksPerSeason is a public variable");
        Assert.AreEqual(typeof(int), field.FieldType);

        Set(calendar, "weeksPerSeason", 1);
        Assert.AreEqual("Summer", Call(calendar, "DayToSeason", 8).ToString(), "1 week per season: day 8 is summer");
        Set(calendar, "weeksPerSeason", 2);
        Assert.AreEqual("Spring", Call(calendar, "DayToSeason", 8).ToString(), "2 weeks per season: day 8 is still spring");
        Assert.AreEqual("Summer", Call(calendar, "DayToSeason", 15).ToString());
        Assert.AreEqual("Winter", Call(calendar, "DayToSeason", 43).ToString());
    }

    [UnityTest]
    public IEnumerator Clock_ShowsTheTimeOfDay_FromTheSunRotation()
    {
        yield return Load(Scene1);
        FreezeCalendar();
        Component dayNight = DayNight();
        Light sun = (Light)Get(dayNight, "sunLight");
        var cases = new[] { (0f, "6:00 AM", 0f), (0.25f, "12:00 PM", 90f), (0.1875f, "10:30 AM", 67.5f), (0.5f, "6:00 PM", 180f), (0.75f, "12:00 AM", 270f) };
        foreach (var c in cases)
        {
            Set(dayNight, "currentTimeOfDay", c.Item1);
            yield return null;
            yield return null;
            Assert.AreEqual(c.Item2, TimeText("ClockText"), "time of day " + c.Item1);
            Vector3 expected = Quaternion.Euler(c.Item3, 0f, 0f) * Vector3.forward;
            Assert.Less(Vector3.Angle(expected, sun.transform.forward), 1f, "the sun's rotation matches the clock at " + c.Item2);
        }
    }

    [UnityTest]
    public IEnumerator Clock_NonPositiveDayLength_DoesNotAdvanceOrAnnounceADay()
    {
        yield return Load(Scene1);
        var dayNight = (DayNightControl)DayNight();
        int startDay = dayNight.Get_DaysPassedInGame();
        int announced = 0;
        UnityAction recordDay = () => announced++;
        dayNight.dayPassedEvent.AddListener(recordDay);
        try
        {
            Set(dayNight, "currentTimeOfDay", 0.75f);
            foreach (float length in new[] { 0f, -1f })
            {
                dayNight.cycleLengthInSeconds = length;
                Call(dayNight, "Update");
                Assert.AreEqual(0.75f, dayNight.GetTimeOfDay());
                Assert.AreEqual(startDay, dayNight.Get_DaysPassedInGame());
                Assert.AreEqual(0, announced);
            }
        }
        finally
        {
            dayNight.dayPassedEvent.RemoveListener(recordDay);
            FreezeCalendar();
        }
    }

    [UnityTest]
    public IEnumerator Clock_Rollover_PreservesTheFractionOfTheNextDay()
    {
        yield return Load(Scene1);
        var dayNight = (DayNightControl)DayNight();
        int startDay = dayNight.Get_DaysPassedInGame();
        var daysSeen = new List<int>();
        UnityAction recordDay = () => daysSeen.Add(dayNight.Get_DaysPassedInGame());
        dayNight.dayPassedEvent.AddListener(recordDay);
        try
        {
            Set(dayNight, "currentTimeOfDay", 0.9f);
            dayNight.timeSpeed = 1f;
            // Invoke this frame's Update directly so the elapsed fraction is independent of the frame rate.
            Assert.Greater(Time.deltaTime, 0f);
            dayNight.cycleLengthInSeconds = Time.deltaTime / 0.35f;
            Call(dayNight, "Update");

            Assert.AreEqual(startDay + 1, dayNight.Get_DaysPassedInGame());
            Assert.AreEqual(0.25f, dayNight.GetTimeOfDay(), 0.00001f, "the excess time belongs to the next day");
            CollectionAssert.AreEqual(new[] { startDay + 1 }, daysSeen);
            Vector3 noon = Quaternion.Euler(90f, 0f, 0f) * Vector3.forward;
            Assert.Less(Vector3.Angle(noon, dayNight.sunLight.transform.forward), 0.1f, "the sun uses the retained time");

            Call(dayNight, "Update");
            Assert.AreEqual(0.6f, dayNight.GetTimeOfDay(), 0.00001f);
            CollectionAssert.AreEqual(new[] { startDay + 1 }, daysSeen, "a partial day does not announce another day");
        }
        finally
        {
            dayNight.dayPassedEvent.RemoveListener(recordDay);
            FreezeCalendar();
        }
    }

    [UnityTest]
    public IEnumerator Clock_MultipleDaysInOneUpdate_AnnouncesEachDayAndSeasonInOrder()
    {
        yield return Load(Scene1);
        var dayNight = (DayNightControl)DayNight();
        var calendar = (GameCalendar)Calendar();
        Set(dayNight, "DaysPassedInGame", 7);//last day of spring
        Set(dayNight, "currentTimeOfDay", 0.75f);
        calendar.ADayPassed();
        var events = new List<string>();
        UnityAction recordSeason = () => events.Add("season:" + calendar.GetDay() + ":" + calendar.season);
        UnityAction recordDay = () => events.Add("day:" + calendar.GetDay() + ":" + calendar.weekDay + ":" + calendar.season);
        calendar.newSeasonEvent.AddListener(recordSeason);
        dayNight.dayPassedEvent.AddListener(recordDay);
        try
        {
            dayNight.timeSpeed = 1f;
            Assert.Greater(Time.deltaTime, 0f);
            dayNight.cycleLengthInSeconds = Time.deltaTime / 2.5f;
            Call(dayNight, "Update");

            Assert.AreEqual(10, dayNight.Get_DaysPassedInGame());
            Assert.AreEqual(0.25f, dayNight.GetTimeOfDay(), 0.00001f);
            CollectionAssert.AreEqual(new[]
            {
                "season:8:Summer",
                "day:8:Monday:Summer",
                "day:9:Tuesday:Summer",
                "day:10:Wednesday:Summer"
            }, events, "the calendar updates before each day's listeners, without skipping or repeating a day");
        }
        finally
        {
            calendar.newSeasonEvent.RemoveListener(recordSeason);
            dayNight.dayPassedEvent.RemoveListener(recordDay);
            FreezeCalendar();
        }
    }

    [UnityTest]
    public IEnumerator HoldingF_FastForwardsOnlyTheClock()
    {
        yield return Load(Scene1);
        SendInputToGameView();
        Keyboard keyboard = Keyboard.current ?? InputSystem.AddDevice<Keyboard>();
        Component dayNight = DayNight();
        Assert.AreEqual(1f, (float)Get(dayNight, "timeSpeed"));

        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F));
        yield return null;
        yield return null;
        Assert.Greater((float)Get(dayNight, "timeSpeed"), 1f, "holding F makes the in-game clock run faster");
        Assert.IsTrue(GameObject.Find("TimeDisplayCanvas").transform.Find("TimePanel/FastForwardText").gameObject.activeInHierarchy, "and says so on the time display");

        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        yield return null;
        yield return null;
        Assert.AreEqual(1f, (float)Get(dayNight, "timeSpeed"));
    }

    [UnityTest]
    public IEnumerator Winter_RemovesAllPlants_AndNothingCanBePlantedUntilSpring()
    {
        StartSession(50, 8);
        yield return Load(Scene3);
        FreezeCalendar();
        Component[] tiles = Tiles();
        yield return PlantOn(tiles[0]);
        yield return PlantOn(tiles[1]);
        PassDays(4); // they grow to mature (the season does not change with PassDays)
        Assert.AreEqual(2, PlantsParent().childCount);

        for (int i = 0; i < 40 && Season() != "Winter"; i++)
            AdvanceDays(1);
        Assert.AreEqual("Winter", Season());
        foreach (Transform plant in PlantsParent())
            Assert.AreEqual("Withered", Get(plant.GetComponent(GameType("Plant")), "plantStage").ToString(), "winter kills every plant");
        StringAssert.Contains("Winter", HudMessage());
        yield return new WaitForSeconds(1.5f);
        Assert.AreEqual(0, PlantsParent().childCount, "and they are removed from the farm");

        Water(tiles[2]);
        int seeds = SeedsOf("Sunflower");
        yield return EOn(tiles[2], false);
        Assert.AreEqual(0, PlantsParent().childCount, "no planting in winter");
        Assert.AreEqual(seeds, SeedsOf("Sunflower"), "the refused planting keeps the seed");
        StringAssert.Contains("winter", HudMessage().ToLower());

        for (int i = 0; i < 40 && Season() != "Spring"; i++)
            AdvanceDays(1);
        Water(tiles[2]);
        yield return EOn(tiles[2]);
        Assert.AreEqual(1, PlantsParent().childCount, "in spring planting works again");
    }

    [UnityTest]
    public IEnumerator EveryPlant_ListensToTheNewSeasonEvent()
    {
        StartSession(50, 8);
        yield return Load(Scene3);
        FreezeCalendar();
        yield return PlantOn(Tiles()[5]);
        Component plant = PlantsParent().GetChild(0).GetComponent(GameType("Plant"));
        Call(plant, "ANewSeasonStarted"); // a new season that is not winter: nothing happens
        Assert.AreEqual("Planted", Get(plant, "plantStage").ToString());
        var newSeasonEvent = (UnityEvent)Get(Calendar(), "newSeasonEvent");
        Set(Calendar(), "season", Enum.Parse(Get(Calendar(), "season").GetType(), "Winter"));
        newSeasonEvent.Invoke(); // what the calendar does when winter starts
        Assert.AreEqual("Withered", Get(plant, "plantStage").ToString(), "the plant heard the event");
    }
}
