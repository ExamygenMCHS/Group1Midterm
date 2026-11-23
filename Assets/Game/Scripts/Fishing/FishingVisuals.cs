using System.Collections.Generic;
using UnityEngine;

//Visual only: shows what the FishingControl decides and never changes gameplay.
//It poses the farmer's rod arm (wind-up, whip, holding, reeling), bends the rod, flies the bobber and draws the line,
//bobs the bobber on a little spring when a fish taps it, swims a fish shadow up to the bait, makes rings and splashes
//on the water, and lifts the caught fish out of the pond into the farmer's hand.
//It runs after the Animator and the FarmerAnimator have posed the arm (LateUpdate, late execution order), so the
//rod arm pose here wins while the line is out and blends back to the animation afterwards.
[DefaultExecutionOrder(200)]
public class FishingVisuals : MonoBehaviour
{
    [SerializeField]
    private FishingControl fishing;
    [SerializeField]
    private ToolManager toolManager;
    [SerializeField]
    private PlayerMove playerMove;//optional: the way the farmer faces (where the caught fish is held up)
    [SerializeField]
    private LineRenderer line;
    [SerializeField]
    private Transform bobber;
    [SerializeField]
    private Transform hookHolder;//child of the bobber: the bait's hook model hangs here
    [SerializeField]
    private ParticleSystem ripples;//rings on the water (world space)
    [SerializeField]
    private ParticleSystem splash;//droplets
    [SerializeField]
    private Transform shadow;//flat quad lying on the water, the fish's nose toward +z
    [SerializeField]
    private Renderer shadowRenderer;

    [Header("Line")]
    [SerializeField]
    private int linePoints = 16;
    [SerializeField]
    private float lineWidth = 0.02f;
    [SerializeField]
    private Color lineColor = new Color(0.95f, 0.95f, 0.92f, 1f);
    [SerializeField]
    private Color strainColor = new Color(1f, 0.6f, 0.15f, 1f);//the line at half strain
    [SerializeField]
    private Color dangerColor = new Color(0.95f, 0.18f, 0.12f, 1f);//about to snap

    [Header("Bobber")]
    [SerializeField]
    private float dangleLength = 0.22f;//below the rod tip while the line is in
    [SerializeField]
    private float dangleLag = 0.07f;
    [SerializeField]
    private float springStiffness = 120f;
    [SerializeField]
    private float springDamping = 9f;
    [SerializeField]
    private float biteStiffness = 300f;
    [SerializeField]
    private float biteDepth = 0.32f;//how far the bite pulls the bobber under
    [SerializeField]
    private float fightDepth = 0.25f;//the bobber under the surface while the fish runs (the pond bed hides it)

    [Header("Fish shadow")]
    [SerializeField]
    private Color shadowColor = new Color(0.04f, 0.07f, 0.09f, 1f);
    [SerializeField]
    private float shadowAlpha = 0.85f;//the texture's own alpha is lower; this reads as a dark shape on the light pond
    [SerializeField]
    private float shadowFadeSeconds = 0.4f;
    [SerializeField]
    private float shadowLeaveSeconds = 0.7f;

    [Header("Catch")]
    [SerializeField]
    private float catchHump = 1f;//metres the fish flies up out of the water
    [SerializeField]
    private float shrinkSeconds = 0.18f;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    //rings that start a moment later (the three rings when the bobber lands)
    private struct PendingRipple
    {
        public float at;
        public Vector3 position;
        public float size;
        public float life;
    }
    private readonly PendingRipple[] pending = new PendingRipple[8];
    private int pendingCount = 0;

    private Vector3[] points;
    private MaterialPropertyBlock block;
    private readonly Dictionary<BaitData, GameObject> hooks = new Dictionary<BaitData, GameObject>();
    private GameObject shownHook;

    //the rod in the hand
    private GameObject rodObject;
    private FishingRod rod;
    private Transform armRight;

    //arm pose
    private float armWeight = 0f;
    private float lift = 14f;
    private float jerk = 0f;
    private float nibbleKick = 0f;

    //bobber and line
    private bool lineShown = true, bobberShown = true;//so the first frame hides them
    private Vector3 bobberPos, lineEnd, dangleVelocity;
    private bool dangling = false;
    private bool released = false;
    private Vector3 castStart;
    private float springOffset, springVelocity;
    private float slack = 0f;
    private Vector3 landStart;
    private float tinyRippleLeft = 0f, wakeLeft = 0f;

    //the hooked fish
    private Vector3 nearPoint, fishPos;
    private float hookProgress;
    private float zigPhase, zigAmp;

    //the fish shadow
    private bool shadowActive = false, shadowLeaving = false;
    private bool shadowShown = true;
    private float shadowAlphaNow = 0f, shadowLeaveTime = 0f;
    private Vector3 shadowPos, leaveDir;
    private float shadowYaw = 0f, shadowLength = 0.3f, dartTime = 10f;

    //the caught fish
    private GameObject catchModel;
    private Vector3 catchStart;
    private float catchLength;

    private FishingControl.FishingState lastState = FishingControl.FishingState.Stowed;

    private void Awake()
    {
        block = new MaterialPropertyBlock();
        points = new Vector3[Mathf.Max(2, linePoints)];
        if (line != null)
        {
            line.positionCount = points.Length;
            line.useWorldSpace = true;
            line.startWidth = lineWidth;
            line.endWidth = lineWidth;
        }
    }

    private void Start()
    {
        if (fishing == null)
            return;
        fishing.fishingStateChangedEvent.AddListener(OnStateChanged);
        fishing.bobberLandedEvent.AddListener(OnBobberLanded);
        fishing.fishApproachEvent.AddListener(OnFishApproach);
        fishing.nibbleEvent.AddListener(OnNibble);
        fishing.fishLeftEvent.AddListener(OnFishLeft);
        fishing.biteEvent.AddListener(OnBite);
        fishing.hookedEvent.AddListener(OnHooked);
        fishing.surgeWarningEvent.AddListener(OnSurgeWarning);
        fishing.fishCaughtEvent.AddListener(OnFishCaught);
        lastState = fishing.GetFishingState();
    }

    private void OnDestroy()
    {
        if (fishing == null)
            return;
        fishing.fishingStateChangedEvent.RemoveListener(OnStateChanged);
        fishing.bobberLandedEvent.RemoveListener(OnBobberLanded);
        fishing.fishApproachEvent.RemoveListener(OnFishApproach);
        fishing.nibbleEvent.RemoveListener(OnNibble);
        fishing.fishLeftEvent.RemoveListener(OnFishLeft);
        fishing.biteEvent.RemoveListener(OnBite);
        fishing.hookedEvent.RemoveListener(OnHooked);
        fishing.surgeWarningEvent.RemoveListener(OnSurgeWarning);
        fishing.fishCaughtEvent.RemoveListener(OnFishCaught);
    }

    private void OnDisable()
    {
        DestroyCatch();
        shadowActive = false;
        shadowAlphaNow = 0f;
        SetShadowShown(false);
        pendingCount = 0;
    }

    //the curve the bobber flies along: straight from the rod to the water, with a hump that grows with the distance.
    //The cast preview (CastGhost) draws the same curve, so the ghost arc shows the real flight.
    public static Vector3 FlightPoint(Vector3 from, Vector3 to, float u)
    {
        u = Mathf.Clamp01(u);
        Vector3 point = Vector3.Lerp(from, to, u);
        float flatDistance = new Vector2(to.x - from.x, to.z - from.z).magnitude;
        point.y += (0.7f + 0.2f * flatDistance) * 4f * u * (1f - u);
        return point;
    }

    // ---------------------------------------------------------------- events

    private void OnStateChanged()
    {
        FishingControl.FishingState state = fishing.GetFishingState();
        FishingControl.FishingState before = lastState;
        lastState = state;
        if (before == FishingControl.FishingState.Landing)
            DestroyCatch();
        switch (state)
        {
            case FishingControl.FishingState.Stowed:
                DestroyCatch();
                shadowActive = false;
                shadowLeaving = false;
                pendingCount = 0;
                break;
            case FishingControl.FishingState.Casting:
                released = false;
                break;
            case FishingControl.FishingState.Waiting:
                tinyRippleLeft = 1.8f;
                if (!shadowLeaving)
                    shadowActive = false;
                break;
            case FishingControl.FishingState.Landing:
                landStart = lineEnd;
                if (fishing.GetLandingKind() == FishingControl.LandingKind.Snapped)
                    StartShadowLeaving();
                else if (fishing.GetLandingKind() == FishingControl.LandingKind.Fish)
                {
                    shadowActive = false;//the fish is out of the water
                    shadowAlphaNow = 0f;
                }
                break;
        }
    }

    private void OnBobberLanded()
    {
        Vector3 target = fishing.GetCastTarget();
        springOffset = 0f;
        springVelocity = -1.5f;
        QueueRipple(target, 0.45f, 1.0f, 0f);
        QueueRipple(target, 0.75f, 1.2f, 0.12f);
        QueueRipple(target, 1.05f, 1.4f, 0.24f);
        Splash(target, 8);
    }

    private void OnFishApproach()
    {
        FishingWater water = fishing.GetWater();
        if (water == null || water.IsFrozen())
            return;
        shadowActive = true;
        shadowLeaving = false;
        shadowAlphaNow = 0f;
        shadowPos = fishing.GetApproachFrom();
        dartTime = 10f;
        FishData fish = fishing.GetFish();
        if (fish != null)
            shadowLength = fish.LengthFor(fishing.GetFishWeight()) * 1.7f;
    }

    private void OnNibble()
    {
        springVelocity += -0.7f;
        nibbleKick = 5f;
        dartTime = 0f;
        QueueRipple(bobberPos, 0.3f, 0.8f, 0f);
    }

    private void OnFishLeft()
    {
        StartShadowLeaving();
    }

    private void OnBite()
    {
        Vector3 target = fishing.GetCastTarget();
        springVelocity += -2.5f;
        QueueRipple(target, 0.9f, 1.1f, 0f);
        QueueRipple(target, 1.3f, 1.4f, 0.1f);
        Splash(target, 14);
    }

    private void OnHooked()
    {
        Vector3 target = fishing.GetCastTarget();
        FishingWater water = fishing.GetWater();
        Vector3 player = fishing.transform.position;
        Vector3 toTarget = Flat(target - player);
        Vector3 direction = toTarget.sqrMagnitude > 0.0001f ? toTarget.normalized : Vector3.forward;
        Vector3 near = player + direction * 1.2f;
        nearPoint = water != null ? water.KeepInWater(near, target, 0.15f) : new Vector3(near.x, target.y, near.z);
        hookProgress = fishing.GetFightProgress();
        fishPos = target;
        zigPhase = 0f;
        zigAmp = 0f;
        jerk = 45f;
        wakeLeft = 0.25f;
        Splash(target, 10);
    }

    private void OnSurgeWarning()
    {
        Splash(fishPos, 10);
    }

    private void OnFishCaught(FishingControl.Catch caught)
    {
        DestroyCatch();
        catchStart = fishPos;
        Splash(fishPos, 22);
        QueueRipple(fishPos, 1.1f, 1.3f, 0f);
        if (caught.fish == null)
            return;
        catchLength = caught.fish.LengthFor(caught.weight) * 1.25f;
        if (caught.fish.modelPrefab == null)
            return;
        catchModel = Instantiate(caught.fish.modelPrefab, catchStart, Quaternion.identity);
        catchModel.transform.localScale = Vector3.one * catchLength;
    }

    private void DestroyCatch()
    {
        if (catchModel != null)
            Destroy(catchModel);
        catchModel = null;
    }

    // ---------------------------------------------------------------- every frame

    private void LateUpdate()
    {
        if (fishing == null)
            return;
        float dt = Time.deltaTime;
        float time = Time.time;
        FindRod();
        FishingControl.FishingState state = fishing.GetFishingState();
        float stateTime = fishing.GetStateTime();
        FishingWater water = fishing.GetWater();
        float surface = water != null ? water.GetSurfaceY() : float.NegativeInfinity;
        bool rodHeld = rod != null && rod.gameObject.activeInHierarchy;

        jerk *= Mathf.Exp(-9f * dt);
        nibbleKick *= Mathf.Exp(-10f * dt);
        dartTime += dt;

        PoseArm(state, stateTime, dt, time);
        BendRod(state, stateTime);
        bool showBobber = rodHeld;
        bool showLine = rodHeld;
        if (rodHeld)
        {
            UpdateLineEnd(state, stateTime, dt, time, water, surface);
            if (state == FishingControl.FishingState.Landing && fishing.GetLandingKind() == FishingControl.LandingKind.Snapped)
                showBobber = showLine = false;
        }
        else
            dangling = false;
        SetLineShown(showLine);
        SetBobberShown(showBobber);
        if (showLine)
            DrawLine(state, dt, time, surface);
        UpdateHook(state, showBobber);
        UpdateWaterEffects(state, dt, time);
        UpdateShadow(state, stateTime, dt, time, water);
        UpdateCatch(state, stateTime);
    }

    //the rod in the hand: cached when the tool object changes (no GetComponent every frame)
    private void FindRod()
    {
        GameObject current = toolManager != null ? toolManager.GetCurToolObject() : null;
        if (current == rodObject)
            return;
        rodObject = current;
        rod = current != null ? current.GetComponent<FishingRod>() : null;
        armRight = null;
        //Tool Spawn Position -> HandRight -> ArmRight
        if (rod != null && rod.transform.parent != null && rod.transform.parent.parent != null)
            armRight = rod.transform.parent.parent.parent;
    }

    // ---------------------------------------------------------------- the rod arm

    private void PoseArm(FishingControl.FishingState state, float stateTime, float dt, float time)
    {
        bool lineOut = fishing.IsLineOut() && rod != null;
        armWeight = Mathf.MoveTowards(armWeight, lineOut ? 1f : 0f, dt / 0.12f);
        if (lineOut)
        {
            float target = TargetLift(state, stateTime, time);
            //the cast follows its own curve exactly; everything else glides from pose to pose
            lift = state == FishingControl.FishingState.Casting ? target : Mathf.Lerp(lift, target, 1f - Mathf.Exp(-14f * dt));
        }
        if (armRight == null || armWeight <= 0f)
            return;
        float w = Mathf.SmoothStep(0f, 1f, armWeight);
        armRight.localRotation = Quaternion.Slerp(armRight.localRotation, Quaternion.Euler(-(lift + jerk), 0f, 0f), w);
    }

    //degrees the arm swings forward (up) for each moment of fishing
    private float TargetLift(FishingControl.FishingState state, float t, float time)
    {
        switch (state)
        {
            case FishingControl.FishingState.Casting:
                float windUp = Mathf.Max(0.01f, fishing.GetWindUpSeconds());
                if (t < windUp)
                {
                    float u = t / windUp;
                    return Mathf.Lerp(14f, 120f, 1f - (1f - u) * (1f - u));//swings back over the shoulder, easing out
                }
                float after = t - windUp;
                if (after < 0.14f)
                {
                    float u = after / 0.14f;
                    return Mathf.Lerp(120f, -8f, u * u);//whips forward, faster and faster
                }
                return Mathf.Lerp(-8f, 14f, Mathf.SmoothStep(0f, 1f, (after - 0.14f) / 0.25f));
            case FishingControl.FishingState.Waiting:
            case FishingControl.FishingState.Nibbling:
                return 14f + 1.5f * Mathf.Sin(time * 1.7f) + nibbleKick;
            case FishingControl.FishingState.Biting:
                return 10f;
            case FishingControl.FishingState.Fighting:
                float shake = fishing.IsSurging() ? Mathf.Sin(time * 47f) * 2.5f : 0f;
                return 34f + fishing.GetTension() * 22f + shake;
            case FishingControl.FishingState.Landing:
                float length = Mathf.Max(0.01f, fishing.GetLandingLength());
                switch (fishing.GetLandingKind())
                {
                    case FishingControl.LandingKind.Fish:
                        return t < fishing.GetLandSeconds() ? 85f : 45f;
                    case FishingControl.LandingKind.Snapped:
                        return Mathf.Lerp(70f, 18f, Mathf.SmoothStep(0f, 1f, t / length));
                }
                return Mathf.Lerp(45f, 18f, Mathf.SmoothStep(0f, 1f, t / length));
        }
        return 14f;
    }

    private void BendRod(FishingControl.FishingState state, float stateTime)
    {
        if (rod == null)
            return;
        float bend = 0f;
        if (state == FishingControl.FishingState.Biting)
            bend = 12f;
        else if (state == FishingControl.FishingState.Fighting)
            bend = 14f + fishing.GetTension() * 46f;
        else if (state == FishingControl.FishingState.Landing && fishing.GetLandingKind() == FishingControl.LandingKind.Fish
            && stateTime < fishing.GetLandSeconds())
            bend = 25f;
        rod.SetBend(bend);
    }

    // ---------------------------------------------------------------- the bobber and the end of the line

    private void UpdateLineEnd(FishingControl.FishingState state, float t, float dt, float time, FishingWater water, float surface)
    {
        Vector3 tip = rod.GetTipPosition();
        switch (state)
        {
            case FishingControl.FishingState.Casting:
                float windUp = fishing.GetWindUpSeconds();
                if (t < windUp)
                {
                    Dangle(tip, dt);
                    break;
                }
                if (!released)
                {
                    released = true;
                    castStart = bobberPos;
                }
                dangling = false;
                float u = (t - windUp) / Mathf.Max(0.01f, fishing.GetFlightSeconds());
                bobberPos = FlightPoint(castStart, fishing.GetCastTarget(), u);
                lineEnd = bobberPos;
                if (bobber != null)
                    bobber.SetPositionAndRotation(bobberPos, Quaternion.Euler(u * 540f, 0f, u * 200f));
                break;
            case FishingControl.FishingState.Waiting:
            case FishingControl.FishingState.Nibbling:
            case FishingControl.FishingState.Biting:
                dangling = false;
                Float(state == FishingControl.FishingState.Biting, dt, time, surface);
                break;
            case FishingControl.FishingState.Fighting:
                dangling = false;
                FollowFish(t, dt, time, water, surface);
                break;
            case FishingControl.FishingState.Landing:
                dangling = false;
                Landing(t, tip, time, surface);
                break;
            default://stowed or aiming: the bobber hangs under the rod tip
                Dangle(tip, dt);
                break;
        }
    }

    //hangs under the rod tip and swings a little behind it
    private void Dangle(Vector3 tip, float dt)
    {
        Vector3 target = tip + Vector3.down * dangleLength;
        if (!dangling)
        {
            dangling = true;
            bobberPos = target;
            dangleVelocity = Vector3.zero;
        }
        bobberPos = Vector3.SmoothDamp(bobberPos, target, ref dangleVelocity, dangleLag, Mathf.Infinity, Mathf.Max(dt, 0.0001f));
        lineEnd = bobberPos;
        if (bobber != null)
        {
            float pitch = Mathf.Clamp(dangleVelocity.z * 25f, -30f, 30f);
            float roll = Mathf.Clamp(-dangleVelocity.x * 25f, -30f, 30f);
            bobber.SetPositionAndRotation(bobberPos, Quaternion.Euler(pitch, 0f, roll));
        }
    }

    //on the water: a damped spring bobs it after it lands and when a fish taps it; the bite pulls it under
    private void Float(bool biting, float dt, float time, float surface)
    {
        float h = Mathf.Min(dt, 1f / 30f);
        float k = biting ? biteStiffness : springStiffness;
        float rest = biting ? -biteDepth : 0f;
        springVelocity += (-k * (springOffset - rest) - springDamping * springVelocity) * h;//semi-implicit Euler
        springOffset += springVelocity * h;
        Vector3 target = fishing.GetCastTarget();
        float y = (float.IsInfinity(surface) ? target.y : surface) + springOffset + 0.006f * Mathf.Sin(time * 2.3f);
        bobberPos = new Vector3(target.x, y, target.z);
        lineEnd = bobberPos;
        if (bobber != null)
        {
            float tilt = Mathf.Clamp(springVelocity * 14f, -25f, 25f);
            bobber.SetPositionAndRotation(bobberPos, Quaternion.Euler(tilt, 0f, 2.5f * Mathf.Sin(time * 1.3f)));
        }
    }

    //the hooked fish runs from where it bit toward the bank in front of the player, zig-zagging, harder while it pulls
    private void FollowFish(float t, float dt, float time, FishingWater water, float surface)
    {
        Vector3 target = fishing.GetCastTarget();
        float s = (fishing.GetFightProgress() - hookProgress) / Mathf.Max(0.01f, 1f - hookProgress);
        float e = s >= 0f ? Mathf.SmoothStep(0f, 1f, s) : Mathf.Max(s, -0.35f);//a fish that takes line swims out past the bite
        Vector3 basePoint = Vector3.LerpUnclamped(target, nearPoint, e);
        if (water != null)
            basePoint = water.KeepInWater(basePoint, target, 0.1f);
        bool surging = fishing.IsSurging();
        zigAmp = Mathf.Lerp(zigAmp, surging ? 0.5f : 0.18f, 1f - Mathf.Exp(-4f * dt));
        zigPhase += dt * (surging ? 7f : 2.6f);
        Vector3 along = Flat(nearPoint - target);
        Vector3 side = along.sqrMagnitude > 0.0001f ? Vector3.Cross(Vector3.up, along.normalized) : Vector3.right;
        Vector3 goal = basePoint + side * (Mathf.Sin(zigPhase) * zigAmp + Mathf.Sin(zigPhase * 2.3f + 1f) * zigAmp * 0.25f);
        if (water != null)
            goal = water.KeepInWater(goal, basePoint, 0.1f);
        fishPos = Vector3.Lerp(fishPos, goal, 1f - Mathf.Exp(-10f * dt));
        float y = float.IsInfinity(surface) ? target.y : surface;
        fishPos.y = y;
        lineEnd = new Vector3(fishPos.x, y - 0.03f, fishPos.z);
        bobberPos = new Vector3(fishPos.x, y - fightDepth, fishPos.z);
        if (bobber != null)
            bobber.SetPositionAndRotation(bobberPos, Quaternion.Euler(Mathf.Sin(time * 11f) * 20f, 0f, 0f));
    }

    private void Landing(float t, Vector3 tip, float time, float surface)
    {
        switch (fishing.GetLandingKind())
        {
            case FishingControl.LandingKind.Fish:
                Vector3 pos;
                Quaternion rot;
                float scale;
                CatchPose(t, out pos, out rot, out scale);
                lineEnd = pos + rot * Vector3.forward * (catchLength * 0.5f * scale);//the nose
                Vector3 up = tip - lineEnd;
                bobberPos = lineEnd + (up.sqrMagnitude > 0.0001f ? up.normalized : Vector3.up) * 0.35f;
                if (bobber != null)
                    bobber.SetPositionAndRotation(bobberPos, Quaternion.identity);
                break;
            case FishingControl.LandingKind.Snapped:
                break;
            default://the empty line comes back to the rod
                float u = Mathf.SmoothStep(0f, 1f, t / Mathf.Max(0.01f, fishing.GetLandingLength()));
                bobberPos = Vector3.Lerp(landStart, tip + Vector3.down * dangleLength, u);
                bobberPos.y += 0.5f * 4f * u * (1f - u);
                lineEnd = bobberPos;
                if (bobber != null)
                    bobber.SetPositionAndRotation(bobberPos, Quaternion.Euler(u * 360f, 0f, 0f));
                break;
        }
    }

    // ---------------------------------------------------------------- the line

    private void DrawLine(FishingControl.FishingState state, float dt, float time, float surface)
    {
        if (line == null)
            return;
        float targetSlack = 0f;
        float rate = 8f;
        float tension = fishing.GetTension();
        switch (state)
        {
            case FishingControl.FishingState.Casting:
                targetSlack = released ? 0.25f : 0f;
                break;
            case FishingControl.FishingState.Waiting:
                targetSlack = 1f;
                rate = 2f;//the line settles slowly onto the water
                break;
            case FishingControl.FishingState.Nibbling:
                targetSlack = 0.8f;
                break;
            case FishingControl.FishingState.Biting:
                targetSlack = 0.1f;
                rate = 18f;
                break;
            case FishingControl.FishingState.Fighting:
                targetSlack = (1f - tension) * 0.3f;
                break;
        }
        slack = Mathf.Lerp(slack, targetSlack, 1f - Mathf.Exp(-rate * dt));

        Vector3 tip = rod.GetTipPosition();
        float distance = Vector3.Distance(tip, lineEnd);
        float sag = slack * (0.06f + 0.1f * distance);
        float floor = surface + 0.012f;
        int last = points.Length - 1;
        for (int i = 0; i <= last; i++)
        {
            float s = i / (float)last;
            Vector3 p = Vector3.Lerp(tip, lineEnd, s);
            p.y -= sag * 4f * s * (1f - s);
            if (i > 0 && i < last && p.y < floor)
                p.y = floor;//the line lies on the water, never in it
            points[i] = p;
        }
        line.SetPositions(points);

        //white, then orange and red as the line strains; it flashes just before it snaps
        Color color = lineColor;
        if (state == FishingControl.FishingState.Fighting)
        {
            color = tension < 0.55f ? Color.Lerp(lineColor, strainColor, tension / 0.55f)
                : Color.Lerp(strainColor, dangerColor, (tension - 0.55f) / 0.3f);
            if (tension > 0.85f)
                color = Color.Lerp(color, Color.white, 0.4f * (0.5f + 0.5f * Mathf.Sin(time * 26f)));
        }
        line.startColor = color;
        line.endColor = color;
    }

    // ---------------------------------------------------------------- the bait under the bobber

    private void UpdateHook(FishingControl.FishingState state, bool bobberVisible)
    {
        if (hookHolder == null)
            return;
        bool inWater = state == FishingControl.FishingState.Waiting || state == FishingControl.FishingState.Nibbling
            || state == FishingControl.FishingState.Biting || state == FishingControl.FishingState.Fighting
            || (state == FishingControl.FishingState.Landing && fishing.GetLandingKind() != FishingControl.LandingKind.Empty);
        BaitData bait = fishing.IsLineOut() ? fishing.GetCastBait() : fishing.GetBait();
        GameObject wanted = bobberVisible && !inWater ? HookFor(bait) : null;
        if (wanted == shownHook)
            return;
        if (shownHook != null)
            shownHook.SetActive(false);
        shownHook = wanted;
        if (shownHook != null)
            shownHook.SetActive(true);
    }

    //each bait's hook model is made once and kept
    private GameObject HookFor(BaitData bait)
    {
        if (bait == null || bait.hookModel == null)
            return null;
        if (hooks.TryGetValue(bait, out GameObject hook) && hook != null)
            return hook;
        hook = Instantiate(bait.hookModel, hookHolder, false);
        hook.transform.localPosition = Vector3.zero;
        hook.transform.localRotation = Quaternion.identity;
        hook.SetActive(false);
        hooks[bait] = hook;
        return hook;
    }

    // ---------------------------------------------------------------- rings and splashes

    private void UpdateWaterEffects(FishingControl.FishingState state, float dt, float time)
    {
        //rings that were waiting for their moment
        for (int i = pendingCount - 1; i >= 0; i--)
        {
            if (time < pending[i].at)
                continue;
            EmitRipple(pending[i].position, pending[i].size, pending[i].life);
            pending[i] = pending[pendingCount - 1];
            pendingCount--;
        }
        if (state == FishingControl.FishingState.Waiting)
        {
            tinyRippleLeft -= dt;
            if (tinyRippleLeft <= 0f)
            {
                tinyRippleLeft = 1.8f;
                EmitRipple(bobberPos, 0.22f, 0.9f);
            }
        }
        else if (state == FishingControl.FishingState.Fighting)
        {
            wakeLeft -= dt;
            if (wakeLeft <= 0f)
            {
                wakeLeft = 0.25f;
                EmitRipple(fishPos, fishing.IsSurging() ? 0.55f : 0.35f, 0.8f);
            }
        }
    }

    private void QueueRipple(Vector3 position, float size, float life, float delay)
    {
        if (delay <= 0f)
        {
            EmitRipple(position, size, life);
            return;
        }
        if (pendingCount >= pending.Length)
            return;
        pending[pendingCount++] = new PendingRipple { at = Time.time + delay, position = position, size = size, life = life };
    }

    private void EmitRipple(Vector3 position, float size, float life)
    {
        if (ripples == null)
            return;
        FishingWater water = fishing.GetWater();
        if (water != null)
            position.y = water.GetSurfaceY() + 0.008f;
        ParticleSystem.EmitParams emit = new ParticleSystem.EmitParams
        {
            position = position,
            startSize = size,
            startLifetime = life,
            applyShapeToPosition = false
        };
        ripples.Emit(emit, 1);
    }

    private void Splash(Vector3 position, int count)
    {
        if (splash == null)
            return;
        FishingWater water = fishing.GetWater();
        if (water != null)
            position.y = water.GetSurfaceY();
        splash.transform.position = position;
        splash.Emit(count);
    }

    // ---------------------------------------------------------------- the fish shadow under the water

    private void StartShadowLeaving()
    {
        if (!shadowActive || shadowLeaving)
            return;
        shadowLeaving = true;
        shadowLeaveTime = 0f;
        Vector3 away = Flat(shadowPos - fishing.GetCastTarget());
        if (away.sqrMagnitude < 0.0004f)
            away = -Flat(Quaternion.Euler(0f, shadowYaw, 0f) * Vector3.forward);
        leaveDir = away.sqrMagnitude > 0.0001f ? away.normalized : Vector3.forward;
    }

    private void UpdateShadow(FishingControl.FishingState state, float t, float dt, float time, FishingWater water)
    {
        if (shadow == null || water == null || water.IsFrozen())
        {
            SetShadowShown(false);
            return;
        }
        if (!shadowActive)
        {
            shadowAlphaNow = 0f;
            SetShadowShown(false);
            return;
        }
        Vector3 target = fishing.GetCastTarget();
        float targetYaw = shadowYaw;
        float wag = 0f;
        if (shadowLeaving)
        {
            shadowLeaveTime += dt;
            shadowPos += leaveDir * 1.3f * dt;
            targetYaw = Yaw(leaveDir);
            wag = Mathf.Sin(time * 14f) * 12f;
            shadowAlphaNow = shadowAlpha * Mathf.Clamp01(1f - shadowLeaveTime / shadowLeaveSeconds);
            if (shadowLeaveTime >= shadowLeaveSeconds)
            {
                shadowActive = false;
                shadowLeaving = false;
                SetShadowShown(false);
                return;
            }
        }
        else
        {
            switch (state)
            {
                case FishingControl.FishingState.Nibbling:
                    Vector3 from = fishing.GetApproachFrom();
                    Vector3 toBait = Flat(target - from);
                    Vector3 dir = toBait.sqrMagnitude > 0.0001f ? toBait.normalized : Vector3.forward;
                    Vector3 side = Vector3.Cross(Vector3.up, dir);
                    float u = Mathf.SmoothStep(0f, 1f, t / Mathf.Max(0.01f, fishing.GetApproachSeconds()));
                    Vector3 stop = target - dir * 0.28f;
                    float dart = 0.2f * Mathf.Sin(Mathf.PI * Mathf.Clamp01(dartTime / 0.28f));
                    shadowPos = Vector3.Lerp(from, stop, u) + side * (Mathf.Sin(t * 5f) * 0.12f * (1f - u)) + dir * dart;
                    targetYaw = Yaw(dir);
                    wag = Mathf.Sin(time * 9f) * Mathf.Lerp(12f, 5f, u);
                    break;
                case FishingControl.FishingState.Biting:
                    Vector3 toTarget = Flat(target - shadowPos);
                    if (toTarget.sqrMagnitude > 0.0001f)
                        targetYaw = Yaw(toTarget);
                    shadowPos = Vector3.Lerp(shadowPos, target - Flat(Quaternion.Euler(0f, shadowYaw, 0f) * Vector3.forward) * 0.12f,
                        1f - Mathf.Exp(-12f * dt));
                    wag = Mathf.Sin(time * 20f) * 14f;
                    break;
                case FishingControl.FishingState.Fighting:
                    shadowPos = fishPos;
                    Vector3 away = Flat(fishPos - fishing.transform.position);
                    if (away.sqrMagnitude > 0.0001f)
                        targetYaw = Yaw(away);
                    wag = Mathf.Sin(time * (fishing.IsSurging() ? 24f : 15f)) * (fishing.IsSurging() ? 26f : 16f);
                    break;
                default:
                    shadowAlphaNow = 0f;
                    SetShadowShown(false);
                    return;
            }
            shadowAlphaNow = Mathf.MoveTowards(shadowAlphaNow, shadowAlpha, shadowAlpha * dt / Mathf.Max(0.01f, shadowFadeSeconds));
        }
        shadowYaw = Mathf.LerpAngle(shadowYaw, targetYaw, 1f - Mathf.Exp(-10f * dt));
        SetShadowShown(true);
        shadow.SetPositionAndRotation(new Vector3(shadowPos.x, water.GetSurfaceY() + 0.004f, shadowPos.z),
            Quaternion.Euler(0f, shadowYaw + wag, 0f));
        shadow.localScale = new Vector3(shadowLength * 0.42f, 1f, shadowLength);
        if (shadowRenderer != null)
        {
            Color color = shadowColor;
            color.a = shadowAlphaNow;
            block.SetColor(BaseColorId, color);
            shadowRenderer.SetPropertyBlock(block);
        }
    }

    // ---------------------------------------------------------------- the caught fish

    private void UpdateCatch(FishingControl.FishingState state, float t)
    {
        if (catchModel == null)
            return;
        if (state != FishingControl.FishingState.Landing || fishing.GetLandingKind() != FishingControl.LandingKind.Fish)
        {
            DestroyCatch();
            return;
        }
        CatchPose(t, out Vector3 pos, out Quaternion rot, out float scale);
        catchModel.transform.SetPositionAndRotation(pos, rot);
        catchModel.transform.localScale = Vector3.one * Mathf.Max(0.0001f, catchLength * scale);
    }

    //where the caught fish is at this moment of the landing: it leaves the water in a tumbling arc, then hangs from the
    //line in front of the farmer, nose up and flat side to the camera, flopping, and shrinks away at the very end
    private void CatchPose(float t, out Vector3 pos, out Quaternion rot, out float scale)
    {
        Vector3 facing = playerMove != null ? playerMove.GetFacing() : Vector3.forward;
        facing = Flat(facing).sqrMagnitude > 0.0001f ? Flat(facing).normalized : Vector3.forward;
        Vector3 right = Vector3.Cross(Vector3.up, facing);
        //held up by the line beside the rod hand, out to the side so the farmer's body does not hide it from the camera
        //behind; a long fish hangs lower, since it hangs by its nose
        Vector3 hand = rod != null ? rod.transform.position : fishing.transform.position;
        Vector3 hold = hand + right * 0.28f + facing * 0.12f + Vector3.up * (0.12f - catchLength * 0.5f);
        Quaternion holdRot = Quaternion.LookRotation(Vector3.up, Vector3.left);
        float land = Mathf.Max(0.01f, fishing.GetLandSeconds());
        if (t < land)
        {
            float u = t / land;
            float e = 1f - (1f - u) * (1f - u);
            pos = Vector3.Lerp(catchStart, hold, e) + Vector3.up * (catchHump * 4f * e * (1f - e));
            Vector3 toPlayer = Flat(hold - catchStart);
            Vector3 dir = toPlayer.sqrMagnitude > 0.0001f ? toPlayer.normalized : Vector3.forward;
            Quaternion tumble = Quaternion.AngleAxis(e * 600f, Vector3.Cross(Vector3.up, dir)) * Quaternion.LookRotation(dir);
            rot = Quaternion.Slerp(tumble, holdRot, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.6f, 1f, e)));
        }
        else
        {
            float h = t - land;
            float flop = Mathf.Sin(h * 21f) * 28f * Mathf.Exp(-2.4f * h) + Mathf.Sin(h * 4.5f) * 4f;
            rot = Quaternion.AngleAxis(flop, Vector3.forward) * holdRot;
            pos = hold + Vector3.up * (0.025f * Mathf.Sin(h * 3.4f));
        }
        float left = fishing.GetLandingLength() - t;
        scale = Mathf.Clamp01(left / Mathf.Max(0.01f, shrinkSeconds));
    }

    // ---------------------------------------------------------------- helpers

    private void SetLineShown(bool on)
    {
        if (on == lineShown)
            return;
        lineShown = on;
        if (line != null)
            line.enabled = on;
        if (!on)
            slack = 0f;
    }

    private void SetBobberShown(bool on)
    {
        if (on == bobberShown)
            return;
        bobberShown = on;
        if (bobber != null)
            bobber.gameObject.SetActive(on);
    }

    private void SetShadowShown(bool on)
    {
        if (on == shadowShown)
            return;
        shadowShown = on;
        if (shadow != null)
            shadow.gameObject.SetActive(on);
    }

    private static float Yaw(Vector3 direction)
    {
        return Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
    }

    private static Vector3 Flat(Vector3 v)
    {
        return new Vector3(v.x, 0f, v.z);
    }
}
