using UnityEngine;
using UnityEngine.Events;

//turns the example's "days passed in game" (DayNightControl) into a day of the week and a season.
//Both are enums, like the farm tile conditions, and they change state on the example's "a day just passed" event.
//When a new season starts it makes an announcement through newSeasonEvent, set up the same way as dayPassedEvent,
//so the plants, the plant manager, the level goal, the HUD and the season visuals can all react to it.
[DefaultExecutionOrder(-50)]//Awake must run before the other scripts register with the events
public class GameCalendar : MonoBehaviour
{
    public enum WeekDay //used for the switch states, day 1 of the game is a Monday
    {
        Monday, Tuesday, Wednesday, Thursday, Friday, Saturday, Sunday
    }
    public enum Season //used for the switch states, day 1 of the game is the first day of spring
    {
        Spring, Summer, Fall, Winter
    }

    public const int DaysPerWeek = 7;
    public const int SeasonsPerYear = 4;

    //how many weeks one season lasts. Set it low while debugging, so winter comes after fewer in-game days
    public int weeksPerSeason = 1;

    public WeekDay weekDay;//current day of the week
    public Season season;//current season

    //this event gets fired when a new season starts and will let all the other objects that listen to this event know
    public UnityEvent newSeasonEvent = new UnityEvent();

    [SerializeField]
    private DayNightControl dayNightControl;

    private void Awake()
    {
        if (dayNightControl == null)
            dayNightControl = GetComponent<DayNightControl>();
        UpdateCalendar(false);//the state on the level's start day, nothing to announce yet
        //listen to the example's day passed event; this happens before the tiles and plants register theirs (in Start),
        //so they already see the new season when they hear about the new day
        dayNightControl.dayPassedEvent.AddListener(ADayPassed);
    }

    private void OnDestroy()
    {
        if (dayNightControl != null)
            dayNightControl.dayPassedEvent.RemoveListener(ADayPassed);
    }

    //called by the day night counter when a day just passed
    public void ADayPassed()
    {
        UpdateCalendar(true);
    }

    private void UpdateCalendar(bool announceNewSeason)
    {
        int day = GetDay();
        weekDay = DayToWeekDay(day);//Monday -> Tuesday -> ... -> Sunday -> Monday

        Season newSeason = DayToSeason(day);//Spring -> Summer -> Fall -> Winter -> Spring
        if (newSeason != season)
        {
            season = newSeason;
            if (announceNewSeason)
                newSeasonEvent.Invoke();//make announcement to all listeners
        }
    }

    // ---------------------------------------------------------------- conversions from DayNightControl's "days passed in game"

    public int GetDaysPerSeason()
    {
        return DaysPerWeek * Mathf.Max(1, weeksPerSeason);
    }
    public WeekDay DayToWeekDay(int day)
    {
        return (WeekDay)((day - 1) % DaysPerWeek);
    }
    public Season DayToSeason(int day)
    {
        return (Season)((day - 1) / GetDaysPerSeason() % SeasonsPerYear);
    }
    public int DayToYear(int day)
    {
        return (day - 1) / (GetDaysPerSeason() * SeasonsPerYear) + 1;
    }
    public int DayToWeekOfSeason(int day)
    {
        return (day - 1) % GetDaysPerSeason() / DaysPerWeek + 1;
    }

    // ---------------------------------------------------------------- API for the other scripts

    public int GetDay()
    {
        return dayNightControl.Get_DaysPassedInGame();
    }
    public int GetYear()
    {
        return DayToYear(GetDay());
    }
    public int GetWeekOfSeason()
    {
        return DayToWeekOfSeason(GetDay());
    }
    public bool IsWinter()
    {
        return season == Season.Winter;
    }

    //how many in-game days until the given season starts next (counting from today)
    public int DaysUntil(Season target)
    {
        int seasonLength = GetDaysPerSeason();
        int yearLength = seasonLength * SeasonsPerYear;
        int dayInYear = (GetDay() - 1) % yearLength;
        int days = (int)target * seasonLength - dayInYear;
        if (days <= 0)
            days += yearLength;
        return days;
    }

    //time of day from the sun: 0 = sunrise at 6:00 AM, 0.25 = noon (sun straight above), 0.5 = 6:00 PM, 0.75 = midnight
    public int GetMinutesSinceMidnight()
    {
        float minutes = 6f * 60f + dayNightControl.GetTimeOfDay() * 24f * 60f;
        return Mathf.FloorToInt(minutes) % (24 * 60);
    }

    //for example "10:30 AM"; the clock moves in steps of ten minutes, like most farming games
    public string GetClockText()
    {
        int minutes = GetMinutesSinceMidnight() / 10 * 10;
        int hour = minutes / 60;
        int hour12 = hour % 12 == 0 ? 12 : hour % 12;
        return hour12.ToString() + ":" + (minutes % 60).ToString("00") + (hour < 12 ? " AM" : " PM");
    }

    public DayNightControl GetDayNightControl()
    {
        return dayNightControl;
    }
}
