using UnityEngine;
using UnityEngine.Events;

//the player's state machine, set up like the farm tile's: an enum of states and a switch that decides
//when the state changes. Other scripts never set the state directly. They ask for an action (StartAction) or send
//an event (PlayerEnergy.restingChangedEvent), and they listen to playerStateChangedEvent to react to the new state:
//the ToolManager puts the right tool in the farmer's hand, the FarmerAnimator plays the right animation.
[DefaultExecutionOrder(-20)]//runs before the FarmerAnimator reads the state each frame
public class PlayerStateControl : MonoBehaviour
{
    public enum PlayerState //used for the switch states
    {
        Idle, Walking, Tilling, Watering, Planting, Harvesting, Resting
    }
    public PlayerState playerState = PlayerState.Idle;

    //this event gets fired every time the player changes state, so all the other objects that listen to it know
    public UnityEvent playerStateChangedEvent = new UnityEvent();

    [SerializeField]
    private PlayerMove playerMove;
    [SerializeField]
    private PlayerEnergy playerEnergy;
    [SerializeField]
    private float walkingSpeed = 0.15f;//ground speed above which the player counts as walking

    [Header("Actions: how long each one takes, and when the tool hits (0 = start, 1 = end)")]
    [SerializeField]
    private float tillingSeconds = 0.55f;
    [SerializeField, Range(0f, 1f)]
    private float tillingHitAt = 0.5f;//the hoe bites into the soil
    [SerializeField]
    private float wateringSeconds = 0.75f;
    [SerializeField, Range(0f, 1f)]
    private float wateringHitAt = 0.45f;//the water reaches the soil
    [SerializeField]
    private float plantingSeconds = 0.55f;
    [SerializeField, Range(0f, 1f)]
    private float plantingHitAt = 0.5f;//the seed goes into the soil
    [SerializeField]
    private float harvestingSeconds = 0.62f;
    [SerializeField, Range(0f, 1f)]
    private float harvestingHitAt = 0.5f;//the sickle cuts the stem

    private float actionTime = 0f;
    private float actionSeconds = 0f;
    private float hitTime = 0f;
    private bool hitDone = false;
    private UnityAction onHit;//what the action does to the tile or plant, called at the hit moment

    private void Start()
    {
        if (playerMove == null)
            playerMove = GetComponent<PlayerMove>();
        if (playerEnergy == null)
            playerEnergy = GetComponent<PlayerEnergy>();
        if (playerEnergy != null)
            playerEnergy.restingChangedEvent.AddListener(OnRestingChanged);
    }

    private void OnDestroy()
    {
        if (playerEnergy != null)
            playerEnergy.restingChangedEvent.RemoveListener(OnRestingChanged);
    }

    private void Update()
    {
        switch (playerState)
        {
            case PlayerState.Idle://a movement key moves the player: Idle -> Walking
                if (IsMoving())
                    ChangeState(PlayerState.Walking);
                break;
            case PlayerState.Walking://the player stops: Walking -> Idle
                if (!IsMoving())
                    ChangeState(PlayerState.Idle);
                break;
            case PlayerState.Tilling:
            case PlayerState.Watering:
            case PlayerState.Planting:
            case PlayerState.Harvesting://an action plays to its end, then the player is free again: action -> Idle
                actionTime += Time.deltaTime;
                if (!hitDone && actionTime >= hitTime)
                {
                    hitDone = true;
                    if (onHit != null)
                        onHit.Invoke();
                }
                if (actionTime >= actionSeconds)
                    FinishAction();
                break;
            case PlayerState.Resting://leaves the bed through the resting changed event (a movement key gets the player up)
                break;
        }
    }

    //the sleep area puts the player into bed and PlayerMove gets them out: Idle/Walking <-> Resting
    private void OnRestingChanged()
    {
        bool resting = playerEnergy.IsResting();
        if (resting && (playerState == PlayerState.Idle || playerState == PlayerState.Walking))
            ChangeState(PlayerState.Resting);
        else if (!resting && playerState == PlayerState.Resting)
            ChangeState(PlayerState.Idle);
    }

    private void FinishAction()
    {
        onHit = null;
        bool resting = playerEnergy != null && playerEnergy.IsResting();
        ChangeState(resting ? PlayerState.Resting : PlayerState.Idle);
    }

    private void ChangeState(PlayerState newState)
    {
        if (newState == playerState)
            return;
        playerState = newState;
        playerStateChangedEvent.Invoke();//make announcement to all listeners
    }

    private bool IsMoving()
    {
        return playerMove != null && playerMove.GetPlanarVelocity().magnitude > walkingSpeed;
    }

    // ---------------------------------------------------------------- API for the scripts that start actions

    //Space on a tile (PlayerInteractPoint) or E on a tile or plant (PlantManager) starts an action: Idle/Walking -> action.
    //The player stops, the tool and the animation start, 'hit' is called when the tool reaches the tile or plant,
    //and the player is free again when the action ends. Returns false when the player is busy or in bed.
    public bool StartAction(PlayerState action, UnityAction hit)
    {
        if (!CanStartAction() || !IsAction(action))
            return false;
        actionSeconds = SecondsFor(action);
        hitTime = actionSeconds * HitFractionFor(action);
        actionTime = 0f;
        hitDone = false;
        onHit = hit;
        if (playerMove != null)
            playerMove.StopPlayer();
        ChangeState(action);
        return true;
    }

    public bool CanStartAction()
    {
        return playerState == PlayerState.Idle || playerState == PlayerState.Walking;
    }

    //true while an action is playing (movement and other actions wait)
    public bool IsBusy()
    {
        return IsAction(playerState);
    }

    public static bool IsAction(PlayerState state)
    {
        return state == PlayerState.Tilling || state == PlayerState.Watering || state == PlayerState.Planting || state == PlayerState.Harvesting;
    }

    public PlayerState GetPlayerState()
    {
        return playerState;
    }

    //0 at the start of the current action, 1 at its end (0 when no action is playing)
    public float GetActionProgress()
    {
        if (!IsBusy() || actionSeconds <= 0f)
            return 0f;
        return Mathf.Clamp01(actionTime / actionSeconds);
    }

    public float GetActionSeconds()
    {
        return IsBusy() ? actionSeconds : 0f;
    }

    public float SecondsFor(PlayerState action)
    {
        switch (action)
        {
            case PlayerState.Tilling: return tillingSeconds;
            case PlayerState.Watering: return wateringSeconds;
            case PlayerState.Planting: return plantingSeconds;
            case PlayerState.Harvesting: return harvestingSeconds;
        }
        return 0f;
    }

    public float HitFractionFor(PlayerState action)
    {
        switch (action)
        {
            case PlayerState.Tilling: return tillingHitAt;
            case PlayerState.Watering: return wateringHitAt;
            case PlayerState.Planting: return plantingHitAt;
            case PlayerState.Harvesting: return harvestingHitAt;
        }
        return 0f;
    }
}
