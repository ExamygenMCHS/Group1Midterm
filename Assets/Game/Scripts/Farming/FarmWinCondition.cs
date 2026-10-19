using UnityEngine;
using UnityEngine.Events;
using TMPro;

//the player wins the level when every farm tile is in the Watered condition at the same time
[DefaultExecutionOrder(100)]//Start() must run after FarmTIleManager.Start() has spawned the tiles
public class FarmWinCondition : MonoBehaviour
{
    [SerializeField]
    private Transform farmTileManager;//the tiles are spawned as children of this object when the game starts
    [SerializeField]
    private DayNightControl dayNightControl;

    [SerializeField]
    private int fundReward = 100;//fund given to the player for winning, used for shopping in Scene2-Store

    [SerializeField]
    private GameObject winPanel;//holds the winning message and the "Next Level" button, hidden until the player wins
    [SerializeField]
    private TMP_Text winMessage;

    //this event gets fired once when the player wins, more listeners can be added in the Unity editor
    public UnityEvent playerWonEvent = new UnityEvent();

    private FarmTileControl[] farmTiles;
    private bool hasWon = false;

    private void Start()
    {
        winPanel.SetActive(false);

        farmTiles = farmTileManager.GetComponentsInChildren<FarmTileControl>();
        //check again every time a tile changes condition (the player waters it, or it dries) and every time a day passes
        foreach (FarmTileControl tile in farmTiles)
            tile.tileCondChangedEvent.AddListener(CheckWinCondition);
        dayNightControl.dayPassedEvent.AddListener(CheckWinCondition);
    }

    private void OnDestroy()
    {
        if (farmTiles != null)
        {
            foreach (FarmTileControl tile in farmTiles)
            {
                if (tile != null)
                    tile.tileCondChangedEvent.RemoveListener(CheckWinCondition);
            }
        }
        if (dayNightControl != null)
            dayNightControl.dayPassedEvent.RemoveListener(CheckWinCondition);
    }

    public bool HasWon()
    {
        return hasWon;
    }

    //true only if no tile is in Grass or Tilled (dry soil) condition
    public bool AreAllTilesWatered()
    {
        if (farmTiles == null || farmTiles.Length == 0)
            return false;
        foreach (FarmTileControl tile in farmTiles)
        {
            if (tile.tileCond != FarmTileControl.FarmTileCond.Watered)
                return false;
        }
        return true;
    }

    public void CheckWinCondition()
    {
        if (hasWon)//the player can only win once
            return;
        if (!AreAllTilesWatered())
            return;

        hasWon = true;
        //the reward goes to the Farm Fund object, which carries it to the store (the FundDisplay script updates the "Fund: $" text)
        if (FarmFund.Instance != null)
        {
            FarmFund.Instance.AddFund(fundReward);
            FarmFund.Instance.LevelWon(1);//the store now offers the seeds for the next level
        }
        winMessage.text = "You Win!\nAll the tiles are watered.\nYou earned $ " + fundReward.ToString() + " to spend in the store.";
        winPanel.SetActive(true);
        playerWonEvent.Invoke();//make announcement to all listeners
    }
}
