using UnityEngine;

//Visual only: plays the farmer's animations and never changes gameplay.
//The poses come from an Animator Controller (Assets/Game/Animation/Farmer.controller) whose state machine has
//Idle, Walk (a walk / run blend), Interact (a sub-state machine: Till, Water, Plant, Harvest) and Rest.
//This script copies the player's state (PlayerStateControl) into the Animator's "State" parameter and sets the
//walking and action speeds, so the animation always matches the player's state machine. It also turns the farmer
//toward the walking direction (leaning into turns), lays the farmer on the bed while resting, and plays the particles
//at the right moment of each action (soil thrown up by the hoe, water pouring from the can, seeds, cut leaves).
[RequireComponent(typeof(Animator))]
public class FarmerAnimator : MonoBehaviour
{
    [SerializeField]
    private Transform farmer;//root of the model, origin between the feet
    [SerializeField]
    private PlayerStateControl playerStateControl;
    [SerializeField]
    private ToolManager toolManager;
    [SerializeField]
    private PlayerEnergy playerEnergy;
    [SerializeField]
    private ParticleSystem splash;//drips where the water lands on the tile
    [SerializeField]
    private ParticleSystem dirtBurst;//clods thrown up where the hoe bites
    [SerializeField]
    private ParticleSystem harvestBurst;//leaves and petals where the sickle cuts
    [SerializeField]
    private ParticleSystem dust;//little puffs kicked up while running

    [Header("Walk / run")]
    [SerializeField]
    private float strideLength = 0.85f;//metres covered by one walk cycle (two steps), keeps the feet from sliding
    [SerializeField]
    private float turnSpeed = 16f;
    [SerializeField]
    private float dustPerMetre = 2.2f;
    [SerializeField]
    private float runSpeed = 2.75f;//dust only shows above about half of this speed

    [Header("Actions")]
    [SerializeField, Range(0f, 1f)]
    private float pourEnd = 0.84f;//the can pours from just before the hit moment until here
    [SerializeField]
    private float bedBlendSeconds = 0.45f;

    private static readonly int StateParam = Animator.StringToHash("State");
    private static readonly int SpeedParam = Animator.StringToHash("Speed");
    private static readonly int WalkCycleParam = Animator.StringToHash("WalkCycleSpeed");
    private static readonly int ActionSpeedParam = Animator.StringToHash("ActionSpeed");

    private Animator animator;
    private BedVisual bed;
    private PlayerMove playerMove;
    private Vector3 lastPosition;
    private Vector3 velocity;//smoothed ground velocity
    private float speed;
    private float lastSpeed;
    private float accelLean;
    private float yaw = 0f;
    private float roll = 0f;
    private float sleepBlend = 0f;
    private Vector3 farmerRestPos;
    private PlayerStateControl.PlayerState shownState = PlayerStateControl.PlayerState.Idle;
    private bool hitPlayed = false;
    private bool pouring = false;

    private void Start()
    {
        animator = GetComponent<Animator>();
        bed = FindAnyObjectByType<BedVisual>();
        playerMove = GetComponentInParent<PlayerMove>();
        if (playerMove != null)
            runSpeed = Mathf.Max(0.5f, playerMove.forwardSpeed);
        lastPosition = transform.position;
        farmerRestPos = farmer.localPosition;
        yaw = farmer.eulerAngles.y;
    }

    //before the Animator updates this frame: hand it the player's state and speeds
    private void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f || animator == null)
            return;

        //how fast the player moves on the ground (smoothed a little so single frames do not twitch the pose)
        Vector3 raw = (transform.position - lastPosition) / dt;
        raw.y = 0f;
        lastPosition = transform.position;
        velocity = Vector3.Lerp(velocity, raw, 1f - Mathf.Exp(-25f * dt));
        speed = velocity.magnitude;

        PlayerStateControl.PlayerState state = CurrentState();
        if (state != shownState)
        {
            shownState = state;
            hitPlayed = false;
            SetPouring(false);
        }
        animator.SetInteger(StateParam, (int)state);
        animator.SetFloat(SpeedParam, speed);
        //one walk cycle per stride length travelled, so the feet keep up with the ground
        animator.SetFloat(WalkCycleParam, Mathf.Max(speed, 0.6f) / strideLength);
        float actionSeconds = playerStateControl != null ? playerStateControl.GetActionSeconds() : 0f;
        //the action clips last one second, so they are played at 1 / (the action's length in seconds)
        animator.SetFloat(ActionSpeedParam, actionSeconds > 0f ? 1f / actionSeconds : 1f);
    }

    private PlayerStateControl.PlayerState CurrentState()
    {
        if (playerStateControl != null)
            return playerStateControl.GetPlayerState();
        //no player (the farmer in the menu backdrops): walk or stand
        return speed > 0.15f ? PlayerStateControl.PlayerState.Walking : PlayerStateControl.PlayerState.Idle;
    }

    private void LateUpdate()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f)
            return;

        //leaning forward when speeding up and back when braking gives the body some weight
        float accel = (speed - lastSpeed) / dt;
        lastSpeed = speed;
        accelLean = Mathf.Lerp(accelLean, Mathf.Clamp(accel * 0.9f, -10f, 10f), 1f - Mathf.Exp(-10f * dt));

        //face the walking direction; when working or standing, face the way the player faces
        //(the highlighted tile is in front of the player in that direction)
        bool working = PlayerStateControl.IsAction(shownState);
        float previousYaw = yaw;
        float targetYaw;
        float turn = turnSpeed;
        Vector3 facing = playerMove != null ? playerMove.GetFacing() : Vector3.forward;
        float facingYaw = Mathf.Atan2(facing.x, facing.z) * Mathf.Rad2Deg;
        if (playerMove == null)
            targetYaw = yaw;
        else if (working)
        {
            targetYaw = facingYaw;
            turn *= 1.5f;
        }
        else if (speed > 0.3f)
            targetYaw = Mathf.Atan2(velocity.x, velocity.z) * Mathf.Rad2Deg;
        else
            targetYaw = facingYaw;
        yaw = Mathf.LerpAngle(yaw, targetYaw, 1f - Mathf.Exp(-turn * dt));
        float yawRate = Mathf.DeltaAngle(previousYaw, yaw) / dt;
        float moving = Mathf.Clamp01(speed / 0.6f);
        roll = Mathf.Lerp(roll, Mathf.Clamp(-yawRate * 0.025f, -12f, 12f) * moving, 1f - Mathf.Exp(-12f * dt));

        bool resting = shownState == PlayerStateControl.PlayerState.Resting && bed != null && bed.SleepAnchor != null;
        sleepBlend = Mathf.MoveTowards(sleepBlend, resting ? 1f : 0f, dt / bedBlendSeconds);

        if (dust != null)
        {
            var emission = dust.emission;
            emission.rateOverDistanceMultiplier = sleepBlend <= 0f && speed > runSpeed * 0.55f ? dustPerMetre : 0f;
        }

        PlayActionEffects();
        PlaceFarmer();
    }

    //particles at the right moment of each action (the moment the tool reaches the tile or plant)
    private void PlayActionEffects()
    {
        if (playerStateControl == null || !PlayerStateControl.IsAction(shownState))
            return;
        float progress = playerStateControl.GetActionProgress();
        float hit = playerStateControl.HitFractionFor(shownState);
        bool reachedHit = progress >= hit;
        HeldTool tool = toolManager != null && toolManager.GetCurToolObject() != null ? toolManager.GetCurToolObject().GetComponent<HeldTool>() : null;

        switch (shownState)
        {
            case PlayerStateControl.PlayerState.Tilling:
                if (reachedHit && !hitPlayed && dirtBurst != null)
                    dirtBurst.Emit(14);
                break;
            case PlayerStateControl.PlayerState.Watering:
                bool pour = progress >= hit - 0.13f && progress < pourEnd;
                SetPouring(pour);
                if (tool != null)
                    tool.SetEffectPlaying(pour);
                break;
            case PlayerStateControl.PlayerState.Planting:
                if (reachedHit && !hitPlayed)
                {
                    if (tool != null)
                        tool.EmitEffect(10);
                    if (dirtBurst != null)
                        dirtBurst.Emit(5);
                }
                break;
            case PlayerStateControl.PlayerState.Harvesting:
                if (reachedHit && !hitPlayed && harvestBurst != null)
                    harvestBurst.Emit(18);
                break;
        }
        if (reachedHit)
            hitPlayed = true;
    }

    private void SetPouring(bool on)
    {
        if (on == pouring || splash == null)
            return;
        pouring = on;
        if (on)
            splash.Play();
        else
            splash.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }

    private void PlaceFarmer()
    {
        //standing pose: on the player, turned toward yaw, leaning into turns and with the speed changes
        Vector3 standPos = transform.TransformPoint(farmerRestPos);
        Quaternion standRot = Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(accelLean * Mathf.Clamp01(speed), 0f, roll);
        if (sleepBlend <= 0f)
        {
            farmer.SetPositionAndRotation(standPos, standRot);
            return;
        }
        //blend to the sleeping pose on the bed with a little hop
        Transform anchor = bed.SleepAnchor;
        float s = Mathf.SmoothStep(0f, 1f, sleepBlend);
        Vector3 pos = Vector3.Lerp(standPos, anchor.position, s) + Vector3.up * Mathf.Sin(s * Mathf.PI) * 0.35f;
        farmer.SetPositionAndRotation(pos, Quaternion.Slerp(standRot, anchor.rotation, s));
    }
}
