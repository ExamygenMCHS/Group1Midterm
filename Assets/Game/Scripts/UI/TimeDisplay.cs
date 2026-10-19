using UnityEngine;
using UnityEngine.UI;
using TMPro;

//the time display canvas that replaces the example's "Days: 1" label.
//It shows the day, the day of the week, the season (with its icon) and the clock, and it only rebuilds the date texts
//when the calendar announces a new day or a new season.
public class TimeDisplay : MonoBehaviour
{
    [SerializeField]
    private GameCalendar calendar;
    [SerializeField]
    private TMP_Text dayLabel;//"Day 13"
    [SerializeField]
    private TMP_Text weekDayLabel;//"Saturday"
    [SerializeField]
    private TMP_Text seasonLabel;//"Summer · Week 1"
    [SerializeField]
    private TMP_Text clockLabel;//"10:30 AM"
    [SerializeField]
    private TMP_Text fastForwardLabel;//shown while the player holds the fast-forward key
    [SerializeField]
    private TimeFastForward fastForward;
    [SerializeField]
    private Image seasonIcon;
    [SerializeField]
    private Sprite springIcon, summerIcon, fallIcon, winterIcon;
    [SerializeField]
    private Color springColor = new Color(0.62f, 0.92f, 0.45f);
    [SerializeField]
    private Color summerColor = new Color(1f, 0.82f, 0.3f);
    [SerializeField]
    private Color fallColor = new Color(1f, 0.6f, 0.25f);
    [SerializeField]
    private Color winterColor = new Color(0.68f, 0.86f, 1f);
    [SerializeField]
    private float seasonPulseSeconds = 1.2f;//the season part grows for a moment when a new season starts

    private int shownMinutes = -1;
    private float pulseLeft = 0f;

    private void Start()
    {
        calendar.GetDayNightControl().dayPassedEvent.AddListener(UpdateDate);
        calendar.newSeasonEvent.AddListener(OnNewSeason);
        UpdateDate();
    }

    private void OnDestroy()
    {
        if (calendar == null)
            return;
        calendar.newSeasonEvent.RemoveListener(OnNewSeason);
        if (calendar.GetDayNightControl() != null)
            calendar.GetDayNightControl().dayPassedEvent.RemoveListener(UpdateDate);
    }

    private void OnNewSeason()
    {
        UpdateDate();
        pulseLeft = seasonPulseSeconds;
    }

    public void UpdateDate()
    {
        dayLabel.text = "Day " + calendar.GetDay().ToString();
        weekDayLabel.text = calendar.weekDay.ToString();
        string season = calendar.season.ToString() + "  ·  Week " + calendar.GetWeekOfSeason().ToString();
        if (calendar.GetYear() > 1)
            season += "  ·  Year " + calendar.GetYear().ToString();
        seasonLabel.text = season;

        Color color = springColor;
        Sprite icon = springIcon;
        switch (calendar.season)
        {
            case GameCalendar.Season.Spring:
                color = springColor;
                icon = springIcon;
                break;
            case GameCalendar.Season.Summer:
                color = summerColor;
                icon = summerIcon;
                break;
            case GameCalendar.Season.Fall:
                color = fallColor;
                icon = fallIcon;
                break;
            case GameCalendar.Season.Winter:
                color = winterColor;
                icon = winterIcon;
                break;
        }
        seasonLabel.color = color;
        if (seasonIcon != null && icon != null)
            seasonIcon.sprite = icon;
    }

    private void Update()
    {
        //only rebuild the clock text when the shown time changes (every ten in-game minutes)
        int minutes = calendar.GetMinutesSinceMidnight() / 10;
        if (minutes != shownMinutes)
        {
            shownMinutes = minutes;
            clockLabel.text = calendar.GetClockText();
        }

        if (fastForwardLabel != null)
        {
            bool fast = fastForward != null && fastForward.IsFastForwarding();
            if (fastForwardLabel.gameObject.activeSelf != fast)
                fastForwardLabel.gameObject.SetActive(fast);
        }

        if (seasonIcon != null)
        {
            float scale = 1f;
            if (pulseLeft > 0f)
            {
                pulseLeft -= Time.deltaTime;
                scale = 1f + 0.35f * Mathf.Sin(Mathf.Clamp01(pulseLeft / seasonPulseSeconds) * Mathf.PI);
            }
            seasonIcon.rectTransform.localScale = Vector3.one * scale;
        }
    }
}
