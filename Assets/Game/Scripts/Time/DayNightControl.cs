using UnityEngine;
using TMPro; // Important for TextMeshPro
using UnityEngine.Events;

public class DayNightControl : MonoBehaviour
{
    public Light sunLight; // Assign your directional light in the Inspector
    public float cycleLengthInSeconds = 60f; //time of a whole day, can be adjusted in Unity editor

    [SerializeField]
    private int DaysPassedInGame = 1;
    private float currentTimeOfDay = 0f; // 0 to 1, representing percentage through the day

    public TMP_Text dayLable;

    //multiplies how fast the in-game clock runs (1 = normal). TimeFastForward raises it while the player holds F.
    public float timeSpeed = 1f;

    //this event gets fired when a day finishes and will let all the other objects that listens to this event know
    public UnityEvent dayPassedEvent=new UnityEvent();

    void Update()
    {
        
        currentTimeOfDay += Time.deltaTime * timeSpeed / cycleLengthInSeconds;// adjust a small amount of time per frame (times timeSpeed)

        if (currentTimeOfDay >= 1f)
        {
            currentTimeOfDay = 0f; // Reset to start a new day
            DaysPassedInGame++;
            if (dayLable != null)//the day is shown on the time display canvas now (TimeDisplay), this label is optional
                dayLable.text="Days:  "+DaysPassedInGame.ToString();

            dayPassedEvent.Invoke();//make announcement to all listeners
        }

        // Calculate sun's rotation based on time of day
        // 0 degrees for sunrise, 180 for sunset, 360 for next sunrise
        float sunRotationX = Mathf.Lerp(0f, 360f, currentTimeOfDay);

        // Apply rotation to the sun light
        sunLight.transform.rotation = Quaternion.Euler(sunRotationX, 0f, 0f);

        // Optional: Adjust other elements, like skybox, light source intensity, and so on
        // sunLight.intensity = 
        // RenderSettings.fogColor = 
        // RenderSettings.skybox.SetFloat
    }
    public int Get_DaysPassedInGame() {
        return DaysPassedInGame;
    }

    //how far the current day is, from 0 (sunrise, 6:00 AM) to 1 (the next sunrise); the clock on the time display uses it
    public float GetTimeOfDay()
    {
        return currentTimeOfDay;
    }

    //provide API for other objects to add themselves to the listner list
    //objects in this list can receive callback when each day passessss
    public void listenToDayPassEvent(GameObject listener)
    {
        //when the day pass event is triggered, the farm tile's ADayPassed() function will be called
        dayPassedEvent.AddListener(listener.GetComponent<FarmTileControl>().ADayPassed);
    }
}
