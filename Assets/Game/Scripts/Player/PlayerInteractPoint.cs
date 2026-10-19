using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

public class PlayerInteractPoint:MonoBehaviour
{
    [SerializeField]
    private GameObject curTile;
    private InputAction interactAction;

    //each tile interaction costs energy. Leave empty to interact without an energy limit
    [SerializeField]
    private PlayerEnergy playerEnergy;

    //the point sits in front of the player in the direction they face, so the tile they work on is the one they look at
    private PlayerMove playerMove;
    private Vector3 restLocalPos;//where the scene placed the point (in front = +z); its distance and height are kept

    //the player's state machine. Space starts a tilling or watering action there, so the right tool and animation play
    [SerializeField]
    private PlayerStateControl playerStateControl;

    //Space uses the tool in the player's hand: the hoe tills grass, the watering can waters soil (one fill each)
    [SerializeField]
    private Hotbar hotbar;
    [SerializeField]
    private WateringCanTank wateringCanTank;

    private void Start()
    {
        interactAction = InputSystem.actions.FindAction("Jump");
        //register OnTileInteractPerformed to the action so it gets called when the space key is pressed
        interactAction.performed += OnTileInteractPerformed;

        playerMove = GetComponentInParent<PlayerMove>();
        restLocalPos = transform.localPosition;
        if (playerStateControl == null)
            playerStateControl = GetComponentInParent<PlayerStateControl>();
        //all three sit on the player. Looked up when the scene's references are empty (a scene that was open in the
        //editor while these fields were added keeps them empty until it is reopened), so the can never waters for free
        if (hotbar == null)
            hotbar = GetComponentInParent<Hotbar>();
        if (wateringCanTank == null)
            wateringCanTank = GetComponentInParent<WateringCanTank>();
    }

    //turn the point around the player to the direction they face
    private void Update()
    {
        if (playerMove == null)
            return;
        Vector3 facing = playerMove.GetFacing();
        float reach = new Vector2(restLocalPos.x, restLocalPos.z).magnitude;
        transform.localPosition = new Vector3(facing.x * reach, restLocalPos.y, facing.z * reach);
    }
    //the action outlives this scene, so stop listening when this object is destroyed (scene change, exit play mode)
    private void OnDestroy()
    {
        if (interactAction != null)
            interactAction.performed -= OnTileInteractPerformed;
    }
    //this callback method will make sure each Space Key press only call the InteractWithFarmTile() once
    //do not use Update() and check Action.IsPressed() since this function can trigger multiple times 
    //across multiple frames each time the player press the Space Key
    private void OnTileInteractPerformed(InputAction.CallbackContext context)
    {
        if (IsActionLocked())//the previous action is still playing, or the player is in bed
            return;
        InteractWithCurTile();

    }

    //returns true if the tile was interacted with
    //the interaction is an action of the player's state machine: grass is tilled with the hoe, dry or wet soil
    //is watered with the watering can, and the tile changes when the tool reaches it (in the middle of the animation)
    //only with that tool in the hand (otherwise the HUD says which key selects it or where to find it)
    public bool InteractWithCurTile()
    {
        if (curTile == null)//the player is not on any tile
            return false;
        if (IsActionLocked())//busy with another action, or in bed
            return false;
        FarmTileControl tile = curTile.GetComponent<FarmTileControl>();
        bool tilling = tile.tileCond == FarmTileControl.FarmTileCond.Grass;
        //grass needs the hoe in the hand, soil the watering can with water in it
        if (tilling && !hotbar.RequireInHand(ToolManager.ToolType.Hoe, "to till the grass"))
            return false;
        if (!tilling && !hotbar.RequireInHand(ToolManager.ToolType.WateringCan, "to water the soil"))
            return false;
        if (!tilling && wateringCanTank.IsEmpty())
        {
            wateringCanTank.TryUseWater();//tells the water meter to flash
            hotbar.Tell("The watering can is empty! Fill it at the well or the pond.");
            return false;
        }
        //when the player is out of energy, the interaction with the tile does not function
        if (playerEnergy != null && !playerEnergy.TrySpendEnergy(playerEnergy.GetTileInteractionCost()))
            return false;
        if (!tilling)
            wateringCanTank.TryUseWater();//paid on the key press, like the energy
        //a day can pass between the key press and the hit and dry the soil back to grass; the water then soaks
        //into the grass instead of tilling it (only the hoe tills)
        UnityAction hit = tilling ? (UnityAction)tile.InteractWithFarmTile : () =>
        {
            if (tile.tileCond != FarmTileControl.FarmTileCond.Grass)
                tile.InteractWithFarmTile();
        };
        PlayerStateControl.PlayerState action = tilling
            ? PlayerStateControl.PlayerState.Tilling
            : PlayerStateControl.PlayerState.Watering;
        playerStateControl.StartAction(action, hit);
        return true;
    }

    //true while key-driven actions (Space, E) must wait: an action is still playing or the player is in bed
    public bool IsActionLocked()
    {
        return !playerStateControl.CanStartAction();
    }

    //API prepared for other objects (the plant manager) to ask which tile the player is interacting with
    public GameObject GetCurTile()
    {
        return curTile;
    }

    private void OnTriggerEnter(Collider other)
    {
        if(other.gameObject.tag.Contains("Farm Tile"))
        {
            other.GetComponent<FarmTileControl>().EnterTile();//call the tile object's public API to interact with it
            curTile = other.gameObject;//store which Tile the player is interacting with
        }
    }
    private void OnTriggerExit(Collider other)
    {
        if (other.gameObject.tag.Contains("Farm Tile"))
        {
            other.GetComponent<FarmTileControl>().ExitTile();
            if (curTile == other.gameObject)//forget the tile when the player walks off it
                curTile = null;
        }
    }
}
