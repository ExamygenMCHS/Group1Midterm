using UnityEngine;
using UnityEngine.Events;

//a state machine for the tool in the farmer's hand. It listens to the player's state machine
//and, like the farm tile's switch, picks the tool for the new state: the hoe for tilling, the watering can for watering,
//the seed bag for planting, the sickle for harvesting, and empty hands otherwise. Each tool prefab is spawned once at the
//"Tool Spawn Position" object under the farmer's hand; after that it is only shown or hidden.
//when idle or walking the farmer holds the item selected in the hotbar (Minecraft style), in bed nothing.
//An action only starts when its tool is the one in the hand, so the tool stays the same through the action.
//curTool is the item the player has ready; what the farmer visibly holds can differ: seeds stay in the bag
//(empty hands) until planting, and while the inventory bag is open (B) the farmer holds the bag itself.
public class ToolManager : MonoBehaviour
{
    public enum ToolType //used for the switch states
    {
        None, Hoe, WateringCan, SeedBag, Sickle
    }
    public ToolType curTool = ToolType.None;

    //this event gets fired every time the tool in the hand changes
    public UnityEvent toolChangedEvent = new UnityEvent();

    [SerializeField]
    private PlayerStateControl playerStateControl;
    [SerializeField]
    private Hotbar hotbar;//the item in the hand while the player is not busy
    [SerializeField]
    private Transform toolSpawnPosition;//empty object under the farmer's hand

    [Header("Tool prefabs: an empty parent with the tool model as a child")]
    [SerializeField]
    private GameObject hoePrefab;
    [SerializeField]
    private GameObject wateringCanPrefab;
    [SerializeField]
    private GameObject seedBagPrefab;
    [SerializeField]
    private GameObject sicklePrefab;

    private GameObject hoe, wateringCan, seedBag, sickle;//spawned once, then shown and hidden
    private ToolType shownTool = ToolType.None;//the model in the farmer's hand right now
    private FarmBag bag;//the inventory bag (null in levels without it)

    private void Start()
    {
        playerStateControl.playerStateChangedEvent.AddListener(OnPlayerStateChanged);
        if (hotbar == null)//on the same player (an empty reference in a scene opened before the field existed)
            hotbar = GetComponent<Hotbar>();
        hotbar.selectedSlotChangedEvent.AddListener(OnPlayerStateChanged);//another item into the hand
        bag = hotbar.GetBag();
        if (bag != null)
            bag.bagOpenChangedEvent.AddListener(OnPlayerStateChanged);//the bag comes out while it is open
        OnPlayerStateChanged();
    }

    private void OnDestroy()
    {
        if (playerStateControl != null)
            playerStateControl.playerStateChangedEvent.RemoveListener(OnPlayerStateChanged);
        if (hotbar != null)
            hotbar.selectedSlotChangedEvent.RemoveListener(OnPlayerStateChanged);
        if (bag != null)
            bag.bagOpenChangedEvent.RemoveListener(OnPlayerStateChanged);
    }

    //called by the player's state machine when the player changes state (and by the hotbar when the player picks an item)
    public void OnPlayerStateChanged()
    {
        switch (playerStateControl.GetPlayerState())
        {
            case PlayerStateControl.PlayerState.Tilling://grass is tilled with the hoe
                SwitchTool(ToolType.Hoe);
                break;
            case PlayerStateControl.PlayerState.Watering://soil is watered with the watering can
                SwitchTool(ToolType.WateringCan);
                break;
            case PlayerStateControl.PlayerState.Planting://seeds come out of the seed bag
                SwitchTool(ToolType.SeedBag);
                break;
            case PlayerStateControl.PlayerState.Harvesting://mature plants are cut with the sickle
                SwitchTool(ToolType.Sickle);
                break;
            case PlayerStateControl.PlayerState.Resting://in bed: empty hands
                SwitchTool(ToolType.None);
                break;
            default://idle and walking: the item selected in the hotbar
                SwitchTool(hotbar.GetSelectedTool());
                break;
        }
        UpdateShownTool();
    }

    public void SwitchTool(ToolType newTool)
    {
        if (newTool == curTool)
            return;
        curTool = newTool;
        UpdateShownTool();
        toolChangedEvent.Invoke();//make announcement to all listeners
    }

    //picks the model for the farmer's hand: the tool for an action, otherwise what is carried
    private void UpdateShownTool()
    {
        PlayerStateControl.PlayerState state = playerStateControl.GetPlayerState();
        bool action = PlayerStateControl.IsAction(state);
        ToolType show = curTool;
        if (!action && state != PlayerStateControl.PlayerState.Resting)
        {
            if (bag != null && bag.IsShown() && bag.IsBagOpen())
                show = ToolType.SeedBag;//looking into the bag: the bag is in the hand
            else if (curTool == ToolType.SeedBag)
                show = ToolType.None;//the seeds stay in the bag until they are planted
        }
        if (show != shownTool)
        {
            SetShown(shownTool, false);//hide the tool that was in the hand
            shownTool = show;
            SetShown(shownTool, true);//spawn (the first time) or show the new one
        }
        //a tool that is only carried sits in its carry pose, an action uses the working grip
        HeldTool held = GetCurToolObject() != null ? GetCurToolObject().GetComponent<HeldTool>() : null;
        if (held != null)
            held.SetCarried(!action);
    }

    //true while the farmer holds the open inventory bag (the arm lifts it in front of the body)
    public bool IsHoldingOpenBag()
    {
        //outside an action the bag only shows while it is open
        return shownTool == ToolType.SeedBag && !PlayerStateControl.IsAction(playerStateControl.GetPlayerState());
    }

    private void SetShown(ToolType tool, bool shown)
    {
        if (tool == ToolType.None)
            return;
        GameObject toolObject = GetOrSpawn(tool, shown);
        if (toolObject == null)
            return;
        HeldTool held = toolObject.GetComponent<HeldTool>();
        if (held != null)
        {
            if (shown)
                held.Show();
            else
                held.PutAway();
        }
        else
            toolObject.SetActive(shown);
    }

    private GameObject GetOrSpawn(ToolType tool, bool spawnIfMissing)
    {
        switch (tool)
        {
            case ToolType.Hoe:
                if (hoe == null && spawnIfMissing) hoe = Spawn(hoePrefab);
                return hoe;
            case ToolType.WateringCan:
                if (wateringCan == null && spawnIfMissing) wateringCan = Spawn(wateringCanPrefab);
                return wateringCan;
            case ToolType.SeedBag:
                if (seedBag == null && spawnIfMissing) seedBag = Spawn(seedBagPrefab);
                return seedBag;
            case ToolType.Sickle:
                if (sickle == null && spawnIfMissing) sickle = Spawn(sicklePrefab);
                return sickle;
        }
        return null;
    }

    //the tool prefab's root goes exactly onto the Tool Spawn Position; its model child holds the grip offset and rotation
    private GameObject Spawn(GameObject prefab)
    {
        if (prefab == null)
            return null;
        GameObject tool = Instantiate(prefab, toolSpawnPosition);
        tool.name = prefab.name;
        tool.transform.localPosition = Vector3.zero;
        tool.transform.localRotation = Quaternion.identity;
        tool.transform.localScale = Vector3.one;
        return tool;
    }

    public ToolType GetCurTool()
    {
        return curTool;
    }

    //the tool object in the hand right now (null for empty hands)
    public GameObject GetCurToolObject()
    {
        return shownTool == ToolType.None ? null : GetOrSpawn(shownTool, false);
    }
}
