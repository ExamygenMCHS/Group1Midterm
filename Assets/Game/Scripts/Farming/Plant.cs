using System.Collections;
using UnityEngine;
using UnityEngine.Events;

//a plant grows from planted to growing to mature as the in-game days pass.
//Same idea as FarmTileControl: a state machine that changes state on the day passed event.
//the plant also listens to the calendar's new season event: when its PlantData's disappear season starts it is
//Withered, then removed. A mature plant can be harvested (Harvested, then it is removed).
//The growth days and the disappear season come from its PlantData.
public class Plant : MonoBehaviour
{
    public enum PlantStage //used for the switch states
    {
        Planted, Growing, Mature,
        Harvested,//picked by the player, about to be removed
        Withered//killed by its season, about to be removed
    }
    public PlantStage plantStage;

    //3 child models representing the 3 plant stages, only one of them is active at a time
    public GameObject plantedModel, growingModel, matureModel;

    public int daysToGrowing = 2;//days spent in the planted stage
    public int daysToMature = 2;//days spent in the growing stage

    //what kind of plant this is (growth days, prices, season rules); set by the PlantManager when it spawns the plant
    public PlantData crop;
    [SerializeField]
    private float witherSeconds = 0.9f;//how long the plant takes to shrink away when it withers

    //fired when the plant is harvested or withers
    public UnityEvent plantRemovedEvent = new UnityEvent();

    private int daysPassedInCurStage = 0;
    private DayNightControl dayNightControl;
    private GameCalendar calendar;

    private void Start()
    {
        if (crop != null)//the crop decides how fast this plant grows
        {
            daysToGrowing = crop.daysToGrowing;
            daysToMature = crop.daysToMature;
        }
        plantStage = PlantStage.Planted;//start as a small plant
        UpdatePlantModel();

        //listen to the same day passed event as the farm tiles
        GameObject dayNightController = GameObject.Find("DayNightController");
        dayNightControl = dayNightController.GetComponent<DayNightControl>();
        dayNightControl.dayPassedEvent.AddListener(ADayPassed);

        //and to the calendar's new season event
        calendar = dayNightController.GetComponent<GameCalendar>();
        if (calendar != null)
        {
            calendar.newSeasonEvent.AddListener(ANewSeasonStarted);
            ANewSeasonStarted();//planted just as its disappear season began: it dies straight away
        }
    }

    private void OnDestroy()
    {
        if (dayNightControl != null)
            dayNightControl.dayPassedEvent.RemoveListener(ADayPassed);
        if (calendar != null)
            calendar.newSeasonEvent.RemoveListener(ANewSeasonStarted);
    }

    public void UpdatePlantModel()//called to show the model of the current stage and hide the other two
    {
        plantedModel.SetActive(plantStage == PlantStage.Planted);
        growingModel.SetActive(plantStage == PlantStage.Growing);
        matureModel.SetActive(plantStage == PlantStage.Mature || plantStage == PlantStage.Harvested);
    }

    //called by the day night counter when a day just passed
    public void ADayPassed()
    {
        daysPassedInCurStage++;
        switch (plantStage)
        {
            case PlantStage.Planted:
                if (daysPassedInCurStage >= daysToGrowing)
                {
                    plantStage = PlantStage.Growing;
                    UpdatePlantModel();
                    daysPassedInCurStage = 0;
                }
                break;
            case PlantStage.Growing:
                if (daysPassedInCurStage >= daysToMature)
                {
                    plantStage = PlantStage.Mature;
                    UpdatePlantModel();
                    daysPassedInCurStage = 0;
                }
                break;
            case PlantStage.Mature://a mature plant no longer changes
                break;
            case PlantStage.Harvested://on its way out
            case PlantStage.Withered:
                break;
        }
    }

    //called by the calendar when a new season starts: the PlantData says if this plant disappears in it
    public void ANewSeasonStarted()
    {
        if (calendar == null)
            return;
        if (crop == null)//no data assigned: keep the old default, winter kills the plant
        {
            if (calendar.IsWinter())
                Wither();
            return;
        }
        if (crop.DiesIn(calendar.season))
            Wither();
    }

    //its disappear season kills the plant: Planted / Growing / Mature -> Withered, then it is removed
    public void Wither()
    {
        if (plantStage == PlantStage.Harvested || plantStage == PlantStage.Withered)
            return;
        plantStage = PlantStage.Withered;
        plantRemovedEvent.Invoke();
        StartCoroutine(RemoveAfter(witherSeconds, true));
    }

    //the player cuts a mature plant: Mature -> Harvested, then it is removed. Returns false if it is not mature.
    public bool Harvest()
    {
        if (plantStage != PlantStage.Mature)
            return false;
        plantStage = PlantStage.Harvested;
        plantRemovedEvent.Invoke();
        StartCoroutine(RemoveAfter(0.18f, false));
        return true;
    }

    public bool IsMature()
    {
        return plantStage == PlantStage.Mature;
    }

    public bool IsGone()
    {
        return plantStage == PlantStage.Harvested || plantStage == PlantStage.Withered;
    }

    //days until this plant is mature (0 when it already is)
    public int DaysUntilMature()
    {
        switch (plantStage)
        {
            case PlantStage.Planted:
                return Mathf.Max(0, daysToGrowing - daysPassedInCurStage) + daysToMature;
            case PlantStage.Growing:
                return Mathf.Max(0, daysToMature - daysPassedInCurStage);
        }
        return 0;
    }

    //the plant shrinks (and droops when it withers) before it is removed from the farm
    private IEnumerator RemoveAfter(float seconds, bool droop)
    {
        Vector3 startScale = transform.localScale;
        Quaternion startRotation = transform.localRotation;
        Quaternion droopRotation = startRotation * Quaternion.Euler(35f, 0f, 20f);
        float t = 0f;
        while (t < seconds)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / seconds);
            float scale = droop ? 1f - k * k : 1f + 0.25f * Mathf.Sin(k * Mathf.PI) - k;
            transform.localScale = startScale * Mathf.Max(0.01f, scale);
            if (droop)
                transform.localRotation = Quaternion.Slerp(startRotation, droopRotation, k);
            yield return null;
        }
        Destroy(gameObject);
    }
}