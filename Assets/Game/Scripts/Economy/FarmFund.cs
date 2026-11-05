using UnityEngine;
using UnityEngine.Events;

//the "Farm Fund" object keeps the player's money (and the inventory next to it) alive across scene loads.
//The Farm creates it on a new game and calls DontDestroyOnLoad, so the store uses the same fund and coming back to
//the farm (the hub) keeps it.
//Only one Farm Fund may exist: a copy placed in a later scene (for playing that scene on its own in the editor)
//removes itself when the one from The Farm is already alive.
[DefaultExecutionOrder(-100)]//Awake must run before any other script asks for FarmFund.Instance
[RequireComponent(typeof(FarmInventory))]
public class FarmFund : MonoBehaviour
{
    public static FarmFund Instance { get; private set; }

    [Tooltip("On: a new game starts here, an older Farm Fund from a previous game is replaced.\n" +
             "Off (The Farm): the values below are only used when no Farm Fund is alive yet - a new game\n" +
             "from the intro (The Farm), or the scene played on its own; coming back to the scene keeps the player's fund.")]
    [SerializeField]
    private bool startsNewGame = false;

    public int fund = 0;//how much money the player has

    //this event gets fired every time the fund changes, so the "Fund: $" labels can update
    public UnityEvent fundChangedEvent = new UnityEvent();

    private FarmInventory inventory;

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

}
