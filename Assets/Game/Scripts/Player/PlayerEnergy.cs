using UnityEngine;
using UnityEngine.Events;

//the player's energy. Interacting with tiles uses energy,
//standing in the sleep area (or standing still) gives it back.
public class PlayerEnergy : MonoBehaviour
{
    [SerializeField]
    private EnergySettings settings;
    //in bed the energy comes back as fast as the clock runs, so holding F (fast-forward) while sleeping
    //also speeds up the sleep. Found by name like the farm tiles do when it is left empty.
    [SerializeField]
    private DayNightControl dayNightControl;

    //this event gets fired every time the energy amount changes
    public UnityEvent energyChangedEvent = new UnityEvent();
    //this event gets fired when the player tries to do something without enough energy
    public UnityEvent notEnoughEnergyEvent = new UnityEvent();
    //this event gets fired when the player gets into bed or out of it (the player state machine listens to it)
    public UnityEvent restingChangedEvent = new UnityEvent();

    private float curEnergy;
    private bool isResting = false;//true while the player is inside the sleep area
    private Vector3 lastPosition;
    private float secondsStandingStill = 0f;

    private void Awake()
    {
        curEnergy = settings.maxEnergy;
        lastPosition = transform.position;
        if (dayNightControl == null)
        {
            GameObject controller = GameObject.Find("DayNightController");
            if (controller != null)
                dayNightControl = controller.GetComponent<DayNightControl>();
        }
    }

    private void Update()
    {
        //the player is standing still when the position on the ground did not change since the last frame
        Vector3 movement = transform.position - lastPosition;
        movement.y = 0f;
        lastPosition = transform.position;
        if (movement.magnitude > 0.05f * Time.deltaTime)
            secondsStandingStill = 0f;
        else
            secondsStandingStill += Time.deltaTime;

        float regenPerSecond = 0f;
        if (isResting)
            regenPerSecond = settings.sleepRegenPerSecond * GetTimeSpeed();//faster while the clock is fast-forwarded
        else if (secondsStandingStill >= settings.idleDelayInSeconds)
            regenPerSecond = settings.idleRegenPerSecond;

        if (regenPerSecond > 0f && curEnergy < settings.maxEnergy)
        {
            curEnergy = Mathf.Min(settings.maxEnergy, curEnergy + regenPerSecond * Time.deltaTime);
            energyChangedEvent.Invoke();
        }
    }

    //API prepared for anything that costs energy (tile interaction, planting).
    //Returns false and uses no energy when the player does not have enough, the caller must then cancel the action.
    public bool TrySpendEnergy(float cost)
    {
        if (curEnergy < cost)
        {
            notEnoughEnergyEvent.Invoke();
            return false;
        }
        curEnergy -= cost;
        secondsStandingStill = 0f;//working is not resting
        energyChangedEvent.Invoke();
        return true;
    }

    //API prepared for the sleep area to tell the player to start/stop resting
    public void SetResting(bool resting)
    {
        if (resting == isResting)
            return;
        isResting = resting;
        restingChangedEvent.Invoke();
    }

    public bool IsResting()
    {
        return isResting;
    }
    //how fast the in-game clock runs (1 normally, 6 while F is held)
    private float GetTimeSpeed()
    {
        return dayNightControl != null ? Mathf.Max(1f, dayNightControl.timeSpeed) : 1f;
    }
    public float GetCurEnergy()
    {
        return curEnergy;
    }
    public float GetMaxEnergy()
    {
        return settings.maxEnergy;
    }
    public float GetTileInteractionCost()
    {
        return settings.tileInteractionCost;
    }
    public float GetPlantingCost()
    {
        return settings.plantingCost;
    }
    public float GetHarvestCost()
    {
        return settings.harvestCost;
    }
}
