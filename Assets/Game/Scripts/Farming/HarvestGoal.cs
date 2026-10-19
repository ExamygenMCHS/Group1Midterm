using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using TMPro;

//the win condition of the planting levels (our choice): harvest enough plants before winter comes.
//It is a small state machine of its own: Playing -> Won when the last plant needed is harvested,
//Playing -> Lost when the calendar's new season event announces winter first.
//It also keeps the goal panel on the HUD up to date: the goal, how many plants are harvested, and the days left.
[DefaultExecutionOrder(100)]
public class HarvestGoal : MonoBehaviour
{
    public enum GoalState //used for the switch states
    {
        Playing, Won, Lost
    }
    public GoalState goalState = GoalState.Playing;

    [Serializable]
    public class Requirement
    {
        public PlantData crop;
        public int count = 5;
    }
    [SerializeField]
    private Requirement[] requirements;
    [SerializeField]
    private int levelNumber = 2;//Scene3 is the 2nd farming level, Scene4 the 3rd
    [SerializeField]
    private int fundReward = 50;//bonus added to the fund for winning

    [SerializeField]
    private PlantManager plantManager;
    [SerializeField]
    private GameCalendar calendar;
    [SerializeField]
    private TimedMessage hudMessage;

    [Header("GUI")]
    [SerializeField]
    private TMP_Text goalLabel;//"Harvest 5 sunflowers before winter"
    [SerializeField]
    private TMP_Text progressLabel;//"Sunflowers harvested: 2 / 5"
    [SerializeField]
    private TMP_Text deadlineLabel;//"Winter in 6 days"
    [SerializeField]
    private GameObject winPanel;//winning message and the "Next Level" button
    [SerializeField]
    private TMP_Text winMessage;
    [SerializeField]
    private GameObject losePanel;//"Try Again" button
    [SerializeField]
    private TMP_Text loseMessage;
    [SerializeField]
    private bool lastLevel = false;//Scene4: the win message ends the game

    public UnityEvent playerWonEvent = new UnityEvent();
    public UnityEvent playerLostEvent = new UnityEvent();

    private int[] harvested;

    private void Start()
    {
        harvested = new int[requirements.Length];
        if (winPanel != null)
            winPanel.SetActive(false);
        if (losePanel != null)
            losePanel.SetActive(false);

        //if this level is lost, "Try Again" restarts it with what the player had at this moment
        if (FarmFund.Instance != null)
            FarmFund.Instance.SaveCheckpoint();

        plantManager.plantHarvestedEvent.AddListener(OnPlantHarvested);
        calendar.newSeasonEvent.AddListener(OnNewSeason);
        calendar.GetDayNightControl().dayPassedEvent.AddListener(UpdateGui);
        UpdateGui();
    }

    private void OnDestroy()
    {
        if (plantManager != null)
            plantManager.plantHarvestedEvent.RemoveListener(OnPlantHarvested);
        if (calendar != null)
        {
            calendar.newSeasonEvent.RemoveListener(OnNewSeason);
            if (calendar.GetDayNightControl() != null)
                calendar.GetDayNightControl().dayPassedEvent.RemoveListener(UpdateGui);
        }
    }

    //called by the plant manager every time the player harvests a plant
    public void OnPlantHarvested(PlantData crop)
    {
        if (goalState != GoalState.Playing)
            return;
        int index = IndexOf(crop);
        if (index >= 0)
        {
            harvested[index]++;
            hudMessage.Show("Harvested a " + crop.cropName.ToLower() + "!  " + crop.Plural() + ": " + harvested[index] + " / " + requirements[index].count);
        }
        else if (crop != null)
            hudMessage.Show("Harvested a " + crop.cropName.ToLower() + "!");
        UpdateGui();
        if (IsGoalReached())
            Win();
    }

    //called by the calendar when a new season starts
    public void OnNewSeason()
    {
        switch (goalState)
        {
            case GoalState.Playing://winter came before the harvest was done: Playing -> Lost
                if (calendar.IsWinter())
                    Lose();
                else
                    UpdateGui();
                break;
            case GoalState.Won:
            case GoalState.Lost:
                break;
        }
    }

    public bool IsGoalReached()
    {
        for (int i = 0; i < requirements.Length; i++)
            if (harvested[i] < requirements[i].count)
                return false;
        return requirements.Length > 0;
    }

    private void Win()
    {
        goalState = GoalState.Won;
        if (FarmFund.Instance != null)
        {
            FarmFund.Instance.AddFund(fundReward);
            FarmFund.Instance.LevelWon(levelNumber);
        }
        if (winMessage != null)
        {
            winMessage.text = lastLevel
                ? "You Win!\nYou finished the farming game!\nFinal fund: $ " + CurrentFund() + " (bonus $ " + fundReward + ")"
                : "You Win!\nThe harvest is in before winter.\nBonus: $ " + fundReward + ". Sell your crops in the store.";
        }
        if (winPanel != null)
            winPanel.SetActive(true);
        UpdateGui();
        playerWonEvent.Invoke();//make announcement to all listeners
    }

    private void Lose()
    {
        goalState = GoalState.Lost;
        if (loseMessage != null)
            loseMessage.text = "Winter came too early!\nThe frost killed the plants.\nTry the level again.";
        if (losePanel != null)
            losePanel.SetActive(true);
        UpdateGui();
        playerLostEvent.Invoke();//make announcement to all listeners
    }

    //API prepared for the "Try Again" button: the level restarts with the fund and seeds the player had when it began
    public void TryAgain()
    {
        if (FarmFund.Instance != null)
            FarmFund.Instance.RestoreCheckpoint();
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    // ---------------------------------------------------------------- GUI

    public string DescribeGoal()
    {
        string goal = "Harvest ";
        for (int i = 0; i < requirements.Length; i++)
        {
            if (i > 0)
                goal += i == requirements.Length - 1 ? " and " : ", ";
            goal += requirements[i].count + " " + requirements[i].crop.NameFor(requirements[i].count).ToLower();
        }
        return goal + " before winter";
    }

    public void UpdateGui()
    {
        if (goalLabel != null)
            goalLabel.text = DescribeGoal();
        if (progressLabel != null && harvested != null)
        {
            string progress = "";
            for (int i = 0; i < requirements.Length; i++)
            {
                bool done = harvested[i] >= requirements[i].count;
                progress += (i > 0 ? "\n" : "") + (done ? "<color=#9BE06A>" : "") + requirements[i].crop.Plural() + " harvested:  "
                    + Mathf.Min(harvested[i], requirements[i].count) + " / " + requirements[i].count + (done ? "  done!</color>" : "");
            }
            progressLabel.text = progress;
        }
        if (deadlineLabel != null)
        {
            switch (goalState)
            {
                case GoalState.Playing:
                    int days = calendar.DaysUntil(GameCalendar.Season.Winter);
                    deadlineLabel.text = days <= 1 ? "<color=#FF8A6A>Winter comes tomorrow!</color>" : "Winter in " + days + " days";
                    break;
                case GoalState.Won:
                    deadlineLabel.text = "<color=#9BE06A>Goal reached!</color>";
                    break;
                case GoalState.Lost:
                    deadlineLabel.text = "<color=#FF8A6A>Winter is here.</color>";
                    break;
            }
        }
    }

    public int GetHarvested(PlantData crop)
    {
        int index = IndexOf(crop);
        return index >= 0 && harvested != null ? harvested[index] : 0;
    }

    public int GetRequired(PlantData crop)
    {
        int index = IndexOf(crop);
        return index >= 0 ? requirements[index].count : 0;
    }

    private int IndexOf(PlantData crop)
    {
        for (int i = 0; i < requirements.Length; i++)
            if (requirements[i].crop == crop)
                return i;
        return -1;
    }

    private static int CurrentFund()
    {
        return FarmFund.Instance != null ? FarmFund.Instance.fund : 0;
    }
}
