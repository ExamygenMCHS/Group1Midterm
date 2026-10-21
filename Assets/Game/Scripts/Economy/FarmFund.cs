using UnityEngine;
using UnityEngine.Events;

//the "Farm Fund" object keeps the player's money (and the inventory next to it) alive across scene loads.
//Scene1 creates it and calls DontDestroyOnLoad, so the store and the later levels all use the same fund.
//Only one Farm Fund may exist: a copy placed in a later scene (for playing that scene on its own in the editor)
//removes itself when the one from Scene1 is already alive.
[DefaultExecutionOrder(-100)]//Awake must run before any other script asks for FarmFund.Instance
[RequireComponent(typeof(FarmInventory))]
public class FarmFund : MonoBehaviour
{
    public static FarmFund Instance { get; private set; }

    [Tooltip("On (Scene1): a new game starts here, an older Farm Fund from a previous game is replaced.\n" +
             "Off (Scene2 and the later levels): the values below are only test values, used when this scene is played on its own.")]
    [SerializeField]
    private bool startsNewGame = false;

    public int fund = 0;//how much money the player has
    public int levelsWon = 0;//farming levels finished: 1 after Scene1, 2 after Scene3, 3 after Scene4, 4 after Scene5

    //this event gets fired every time the fund changes, so the "Fund: $" labels can update
    public UnityEvent fundChangedEvent = new UnityEvent();

    private FarmInventory inventory;
    private int checkpointFund, checkpointLevelsWon;
    private bool hasCheckpoint = false;

    //Play Mode in this project starts without a domain reload, so the static value from the last play session must be cleared
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            if (!startsNewGame)
            {
                Destroy(gameObject);//the Farm Fund that came from the earlier scenes stays, this test copy goes
                return;
            }
            Destroy(Instance.gameObject);//a new game: the old farm is forgotten
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);//this object is not destroyed when the next scene loads
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public FarmInventory Inventory
    {
        get
        {
            if (inventory == null)
                inventory = GetComponent<FarmInventory>();
            return inventory;
        }
    }

    public void AddFund(int amount)
    {
        if (amount < 0 || (long)fund + amount > int.MaxValue)
            return;
        fund += amount;
        fundChangedEvent.Invoke();//make announcement to all listeners
    }

    //returns false (and changes nothing) when the player cannot afford the price
    public bool TrySpendFund(int price)
    {
        if (price < 0 || fund < price)
            return false;
        fund -= price;
        fundChangedEvent.Invoke();
        return true;
    }

    //called by a level's win condition; levels are numbered 1 (Scene1), 2 (Scene3), 3 (Scene4), 4 (Scene5)
    public void LevelWon(int levelNumber)
    {
        levelsWon = Mathf.Max(levelsWon, levelNumber);
    }

    // ---- level checkpoint: a level that is lost starts again with what the player had when it began

    public void SaveCheckpoint()
    {
        checkpointFund = fund;
        checkpointLevelsWon = levelsWon;
        hasCheckpoint = true;
        Inventory.SaveCheckpoint();
    }

    public void RestoreCheckpoint()
    {
        if (!hasCheckpoint)
            return;
        fund = checkpointFund;
        levelsWon = checkpointLevelsWon;
        Inventory.RestoreCheckpoint();
        fundChangedEvent.Invoke();
    }
}
