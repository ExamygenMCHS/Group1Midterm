using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

//the fishing state machine, set up like the player's and the farm tile's: an enum of states, a switch that decides
//when the state changes, and one method (ChangeState) that changes it. It sits on the player.
//With the fishing rod in the hand the mouse aims at the pond (Aiming): the aim is good over open water in reach, and
//only a good aim can be cast. A left click casts (Casting), the bobber lands and waits (Waiting), a fish swims up and
//taps the bait (Nibbling), the bobber goes under (Biting), a click in time hooks the fish (Fighting: hold the button to
//reel, let go while the fish pulls or the line snaps), and the line comes back with or without a fish (Landing).
//A left click while waiting reels the line in, and so does a movement key. The bait is the one loaded into the rod
//(the player drags it onto the rod in the bag); without bait a click does not cast. A lure loses one cast of
//durability at the cast and a worm is eaten at the bite; a worn-out lure is settled when the line is back.
//This script only decides; the bobber, line, ghost, sounds and HUD listen to its events and read its state.
//The player's own state machine is in Fishing from the cast until the line is back, so the player stands still.
[DefaultExecutionOrder(-15)]//after the Hotbar and the player's state machine, before the scripts that show the result
public class FishingControl : MonoBehaviour
{
    public enum FishingState //used for the switch states
    {
        Stowed, Aiming, Casting, Waiting, Nibbling, Biting, Fighting, Landing
    }
    public FishingState fishingState = FishingState.Stowed;

    public enum AimProblem //why the spot under the mouse cannot be cast to
    {
        None, NotWater, TooCloseToShore, Ice, TooFar, TooClose
    }

    public enum LandingKind //how the line comes back
    {
        Empty, Fish, Snapped
    }

    //a fish that was just landed
    public struct Catch
    {
        public FishData fish;
        public float weight;
        public int price;
        public bool trophy;
        public bool best;//the heaviest of its kind so far
    }

    [SerializeField]
    private PlayerStateControl playerStateControl;
    [SerializeField]
    private PlayerMove playerMove;
    [SerializeField]
    private PlayerEnergy playerEnergy;//optional: a cast costs a little energy
    [SerializeField]
    private Hotbar hotbar;
    [SerializeField]
    private GameCalendar calendar;//optional: some fish feed at night
    [Tooltip("Empty: the main camera")]
    [SerializeField]
    private Camera aimCamera;

    [Header("Fish")]
    [SerializeField]
    private FishData[] fish;

    [Header("Casting")]
    [SerializeField]
    private float minCast = 1.3f;//metres from the player
    [SerializeField]
    private float maxCast = 6.2f;
    [SerializeField]
    private float aimShowsWithin = 2.5f;//the ghost shows while the player is this close to casting range of the water
    [SerializeField]
    private float windUpSeconds = 0.2f;//the rod swings back before the bobber flies
    [SerializeField]
    private Vector2 flightSeconds = new Vector2(0.42f, 0.72f);//a short cast, the longest cast
    [SerializeField]
    private float castEnergyCost = 2f;

    [Header("Bite")]
    [SerializeField]
    private Vector2 approachSeconds = new Vector2(1.4f, 2.2f);//the fish swims up to the bait
    [SerializeField]
    private Vector2 nibbleGapSeconds = new Vector2(0.45f, 0.9f);
    [SerializeField]
    private float waitAfterMiss = 0.55f;//the next wait is shorter after a fish turned away or was missed
    [SerializeField]
    private float minBiteWindow = 0.45f;

    [Header("Fight")]
    [SerializeField, Range(0f, 1f)]
    private float startProgress = 0.2f;
    [SerializeField, Range(0f, 1f)]
    private float startTension = 0.3f;
    [SerializeField]
    private float calmTension = 0.2f;//line strain per second while reeling a calm fish
    [SerializeField]
    private float surgeTension = 1.15f;//and while reeling against a fish that pulls (times the fish's strength)
    [SerializeField]
    private float relaxRate = 0.95f;//strain let off per second while not reeling
    [SerializeField]
    private float surgeReelFactor = 0.3f;//reeling against a pull brings the fish in this much slower
    [SerializeField]
    private float runSpeed = 0.08f;//progress a pulling fish takes back per second while the player lets go
    [SerializeField, Range(0.2f, 1f)]
    private float reelShare = 0.6f;//the part of a clean fight the player can reel (the rest the fish pulls or warns)
    [SerializeField]
    private Vector2 calmSeconds = new Vector2(0.7f, 1.3f);
    [SerializeField]
    private float surgeWarningSeconds = 0.28f;//the fish splashes this long before it pulls
    [SerializeField]
    private float snapGraceSeconds = 0.22f;//the line holds this long at full strain
    [SerializeField]
    private float slackSeconds = 1.4f;//a fish left alone this long at the far end throws the hook
    [SerializeField]
    private float maxFightSeconds = 25f;

    [Header("Landing")]
    [SerializeField]
    private float reelInSeconds = 0.4f;//an empty line coming back
    [SerializeField]
    private float landSeconds = 0.55f;//the fish flying to the player
    [SerializeField]
    private float showSeconds = 1.6f;//the player holds the fish up
    [SerializeField]
    private float snapRecoverSeconds = 0.6f;

    //this event gets fired every time the fishing state changes, so all the other objects that listen to it know
    public UnityEvent fishingStateChangedEvent = new UnityEvent();
    public UnityEvent castEvent = new UnityEvent();//the rod swings
    public UnityEvent bobberLandedEvent = new UnityEvent();
    public UnityEvent fishApproachEvent = new UnityEvent();//a fish shows up near the bait
    public UnityEvent nibbleEvent = new UnityEvent();
    public UnityEvent fishLeftEvent = new UnityEvent();//it turned away, was scared off or stole the bait
    public UnityEvent biteEvent = new UnityEvent();
    public UnityEvent hookedEvent = new UnityEvent();
    public UnityEvent surgeWarningEvent = new UnityEvent();//the fish is about to pull
    public UnityEvent surgeChangedEvent = new UnityEvent();//it starts or stops pulling
    public UnityEvent lineSnappedEvent = new UnityEvent();
    public UnityEvent<Catch> fishCaughtEvent = new UnityEvent<Catch>();
    public UnityEvent baitChangedEvent = new UnityEvent();//another bait was loaded into the rod (or the rod went empty)
    public UnityEvent castRefusedEvent = new UnityEvent();//a click on a spot that cannot be cast to
    public UnityEvent noBaitEvent = new UnityEvent();//a click to cast with no bait in the rod

    private FarmInventory inventory;
    private BaitData loadedBefore;//to tell when the bait in the rod changes
    private FishingWater water;//the pond the aim or the line is on
    private FishingWater listeningTo;
    private InputAction moveAction;
    private System.Random random = new System.Random();
    private float stateTime = 0f;

    //input of this frame
    private bool clickPressed, clickHeld, pointerOverUi;
    private bool refusedForBait = false;//the last cast refused was refused for want of bait
    private bool reelOverride = false;//the tests hold the reel through this
    private bool moveArmed = false;//the movement keys were let go after the cast (a key held from before does not reel in)

    //aiming
    private bool nearWater = false;
    private bool aimVisible = false;
    private Vector3 aimPoint;
    private AimProblem aimProblem = AimProblem.NotWater;

    //the cast that is out
    private BaitData castBait;
    private Vector3 castTarget;
    private float flightTime;
    private float waitSeconds;
    private int turnedAway = 0;//fish that looked and left in a row

    //the fish at the bait
    private FishData curFish;
    private float curWeight;
    private FishData forcedFish;
    private float forcedWeight;
    private Vector3 approachFrom;
    private float approachTime;
    private readonly List<float> nibbleTimes = new List<float>();
    private int nextNibble;
    private float nibbleEnd;
    private bool willBite;
    private float biteWindow;

    //the fight
    private float progress, tension, reelSpeed;
    private bool surging, surgeWarned;
    private float phaseLeft, overTime, slackTime;

    //landing
    private LandingKind landingKind = LandingKind.Empty;
    private Catch lastCatch;

    private void Start()
    {
        if (playerStateControl == null)
            playerStateControl = GetComponent<PlayerStateControl>();
        if (playerMove == null)
            playerMove = GetComponent<PlayerMove>();
        if (playerEnergy == null)
            playerEnergy = GetComponent<PlayerEnergy>();
        if (hotbar == null)
            hotbar = GetComponent<Hotbar>();
        if (calendar == null)
            calendar = FindFirstObjectByType<GameCalendar>();//once; not every scene has a calendar
        if (FarmFund.Instance != null)
        {
            inventory = FarmFund.Instance.Inventory;
            inventory.inventoryChangedEvent.AddListener(OnInventoryChanged);
            loadedBefore = GetBait();
        }
        moveAction = InputSystem.actions.FindAction("Move");
    }

    private void OnDisable()
    {
        Abort(false);//the scene may be closing: a worn lure is settled on the next line back
    }

    private void OnDestroy()
    {
        ListenTo(null);
        if (inventory != null)
            inventory.inventoryChangedEvent.RemoveListener(OnInventoryChanged);
    }

    //a bait dragged onto the rod, or the rod emptied: tell the listeners (the bait sound, the HUD)
    private void OnInventoryChanged()
    {
        BaitData now = GetBait();
        if (now == loadedBefore)
            return;
        loadedBefore = now;
        baitChangedEvent.Invoke();
    }

    private void Update()
    {
        stateTime += Time.deltaTime;
        ReadInput();
        //the rod left the hand or the player's state machine left Fishing while the line was out: everything stops
        if (IsLineOut() && (!HoldsRod() || !playerStateControl.IsFishing()))
            Abort();

        switch (fishingState)
        {
            case FishingState.Stowed://the rod comes into the hand: Stowed -> Aiming
                if (CanAim())
                    ChangeState(FishingState.Aiming);
                break;
            case FishingState.Aiming://a click on a good spot casts: Aiming -> Casting
                if (!CanAim())
                {
                    ChangeState(FishingState.Stowed);
                    break;
                }
                UpdateAim();
                if (clickPressed && aimVisible && !TryCastAt(aimPoint) && !refusedForBait)
                    castRefusedEvent.Invoke();
                break;
            case FishingState.Casting://the bobber reaches the water: Casting -> Waiting
                if (stateTime >= windUpSeconds + flightTime)
                {
                    bobberLandedEvent.Invoke();
                    BeginWait(1f);
                }
                break;
            case FishingState.Waiting://a fish comes to look: Waiting -> Nibbling
                if (ReelInAsked())
                    ReelIn();
                else if (stateTime >= waitSeconds)
                    BeginApproach();
                break;
            case FishingState.Nibbling://the fish bites (Nibbling -> Biting) or turns away (Nibbling -> Waiting)
                UpdateNibbling();
                break;
            case FishingState.Biting://a click in time hooks it: Biting -> Fighting
                if (clickPressed)
                    TryHook();
                else if (stateTime >= biteWindow)
                    MissBite();
                break;
            case FishingState.Fighting://reeled all the way in, snapped, or got away: Fighting -> Landing
                UpdateFight(Time.deltaTime);
                break;
            case FishingState.Landing://the line is back: Landing -> Aiming
                bool skip = landingKind == LandingKind.Fish && clickPressed && stateTime >= landSeconds + 0.35f;
                if (skip || stateTime >= LandingLength())
                    EndFishing();
                break;
        }
    }

    private void ChangeState(FishingState newState)
    {
        if (newState == fishingState)
            return;
        fishingState = newState;
        stateTime = 0f;
        if (newState == FishingState.Stowed || newState == FishingState.Aiming)
        {
            aimVisible = false;
            nearWater = false;
        }
        //the item in the hand cannot change while the line is out (the mouse wheel would drop the rod in the middle of a fight)
        if (hotbar != null)
            hotbar.SetSelectionLocked(IsLineOut());
        fishingStateChangedEvent.Invoke();//make announcement to all listeners
    }

    // ---------------------------------------------------------------- input

    private void ReadInput()
    {
        Mouse mouse = Mouse.current;
        pointerOverUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        bool blocked = pointerOverUi || MenuOpen();
        clickPressed = mouse != null && mouse.leftButton.wasPressedThisFrame && !blocked;
        clickHeld = mouse != null && mouse.leftButton.isPressed && !MenuOpen();
    }

    //the bag or the trader's shop is open: the mouse belongs to them
    private bool MenuOpen()
    {
        FarmBag bag = hotbar != null ? hotbar.GetBag() : null;
        return TraderShopPanel.IsAnyOpen || (bag != null && bag.IsShown() && bag.IsBagOpen());
    }

    private bool MoveKeysDown()
    {
        return moveAction != null && moveAction.ReadValue<Vector2>().sqrMagnitude > 0.25f;
    }

    //a click, or a fresh press of a movement key, brings the line in
    private bool ReelInAsked()
    {
        bool moving = MoveKeysDown();
        if (!moving)
            moveArmed = true;
        return clickPressed || (moving && moveArmed);
    }

    private bool HoldsRod()
    {
        return hotbar != null && hotbar.IsHolding(ToolManager.ToolType.FishingRod);
    }

    private bool CanAim()
    {
        return isActiveAndEnabled && inventory != null && HoldsRod() && playerStateControl != null
            && playerStateControl.CanStartAction() && !MenuOpen();
    }

    // ---------------------------------------------------------------- aiming

    private void UpdateAim()
    {
        aimVisible = false;
        nearWater = false;
        FishingWater nearest = NearestWater();
        if (nearest == null)
            return;
        water = nearest;
        nearWater = true;
        Camera cam = aimCamera != null ? aimCamera : Camera.main;
        if (cam == null || Mouse.current == null || pointerOverUi)
            return;
        Ray ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());
        Plane surface = new Plane(Vector3.up, new Vector3(0f, water.GetSurfaceY(), 0f));
        if (!surface.Raycast(ray, out float enter))
            return;
        Vector3 point = water.SnapAim(ray.GetPoint(enter));
        if (Flat(point - transform.position).magnitude > maxCast + aimShowsWithin + 2f)
            return;//the mouse is nowhere near: no ghost out in the fields
        aimPoint = point;
        aimProblem = ProblemAt(point);
        aimVisible = true;
        //standing still, the farmer turns to where the cast will go
        if (aimProblem == AimProblem.None && playerMove != null
            && playerStateControl.GetPlayerState() == PlayerStateControl.PlayerState.Idle)
            playerMove.FaceToward(point);
    }

    //the closest pond in reach of a cast (null when the player is nowhere near water)
    private FishingWater NearestWater()
    {
        FishingWater best = null;
        float bestDistance = maxCast + aimShowsWithin;
        foreach (FishingWater candidate in FishingWater.All)
        {
            float distance = candidate.DistanceToWater(transform.position);
            if (distance <= bestDistance)
            {
                best = candidate;
                bestDistance = distance;
            }
        }
        return best;
    }

    //why the line cannot be cast to this spot from where the player stands (None: it can)
    public AimProblem ProblemAt(Vector3 point)
    {
        if (water == null)
            return AimProblem.NotWater;
        switch (water.Check(point))
        {
            case FishingWater.CastCheck.NotWater: return AimProblem.NotWater;
            case FishingWater.CastCheck.TooCloseToShore: return AimProblem.TooCloseToShore;
            case FishingWater.CastCheck.Ice: return AimProblem.Ice;
        }
        float distance = Flat(point - transform.position).magnitude;
        if (distance > maxCast)
            return AimProblem.TooFar;
        if (distance < minCast)
            return AimProblem.TooClose;
        return AimProblem.None;
    }

    // ---------------------------------------------------------------- bait

    //the bait loaded into the rod (null: the rod is empty)
    public BaitData GetBait()
    {
        return inventory != null && inventory.HasLoadedBait() ? inventory.loadedBait : null;
    }

    //worms in the rod, 1 for a lure, 0 when it is empty
    public int GetBaitCount()
    {
        return GetBait() != null ? inventory.loadedAmount : 0;
    }

    //casts left on the lure in the rod (0 for worms and an empty rod)
    public int GetBaitDurability()
    {
        BaitData bait = GetBait();
        return bait != null && bait.Wears() ? inventory.loadedDurability : 0;
    }

    //true when the player has bait somewhere (in the rod or in the bag)
    public bool HasAnyBait()
    {
        return inventory != null && inventory.HasAnyBait();
    }

    // ---------------------------------------------------------------- casting and waiting

    //casts the line to a spot on the water (what a left click on a good aim does). Returns false, and nothing happens,
    //when the spot cannot be cast to, or when no bait is loaded into the rod (then noBaitEvent tells the HUD).
    public bool TryCastAt(Vector3 point)
    {
        refusedForBait = false;
        if (fishingState != FishingState.Aiming || !CanAim())
            return false;
        BaitData bait = GetBait();
        if (bait == null)
        {
            refusedForBait = true;
            noBaitEvent.Invoke();
            return false;
        }
        if (water == null)
            water = NearestWater();
        if (water == null)
            return false;
        point = water.OnSurface(point);
        if (ProblemAt(point) != AimProblem.None)
            return false;
        if (playerEnergy != null && !playerEnergy.TrySpendEnergy(castEnergyCost))
        {
            hotbar.TellRejection("Too tired to cast. Stand still for a moment, or sleep.");
            return false;
        }
        if (!playerStateControl.StartFishing())
            return false;
        inventory.SpendLoadedCast();//a lure wears one cast at the cast; worms are eaten at the bite
        castBait = bait;
        castTarget = point;
        float distance = Flat(point - transform.position).magnitude;
        flightTime = Mathf.Lerp(flightSeconds.x, flightSeconds.y, Mathf.InverseLerp(minCast, maxCast, distance));
        turnedAway = 0;
        moveArmed = false;
        curFish = null;
        if (playerMove != null)
            playerMove.FaceToward(point);
        ListenTo(water);
        ChangeState(FishingState.Casting);
        castEvent.Invoke();
        return true;
    }

    private void BeginWait(float scale)
    {
        Vector2 range = castBait.waitSeconds;
        waitSeconds = Mathf.Max(0.3f, Range(range.x, range.y) * scale * (water.IsFrozen() ? 1.15f : 1f));
        curFish = null;
        ChangeState(FishingState.Waiting);
    }

    //brings the line in without a fish (a click or a movement key while waiting)
    public void ReelIn()
    {
        if (!IsLineOut() || fishingState == FishingState.Landing)
            return;
        if (fishingState == FishingState.Nibbling)
            fishLeftEvent.Invoke();//the fish at the bait darts off
        landingKind = LandingKind.Empty;
        ChangeState(FishingState.Landing);
    }

    // ---------------------------------------------------------------- the fish at the bait

    private void BeginApproach()
    {
        bool forced = forcedFish != null;//a fish the tests asked for always bites
        PickFish();
        approachTime = Range(approachSeconds.x, approachSeconds.y);
        approachFrom = water.PointAround(castTarget, Range(1.7f, 2.8f), (float)random.NextDouble());
        nibbleTimes.Clear();
        nextNibble = 0;
        int nibbles = random.Next(0, Mathf.Max(0, curFish.maxNibbles) + 1);
        float time = approachTime;
        for (int i = 0; i < nibbles; i++)
        {
            time += Range(nibbleGapSeconds.x, nibbleGapSeconds.y);
            nibbleTimes.Add(time);
        }
        nibbleEnd = time + Range(nibbleGapSeconds.x, nibbleGapSeconds.y);
        //a fish may only look and leave, but never three times in a row
        willBite = forced || turnedAway >= 2 || random.NextDouble() < castBait.biteChance;
        ChangeState(FishingState.Nibbling);
        fishApproachEvent.Invoke();
    }

    //which fish comes to this bait, at this spot, at this time of day
    private void PickFish()
    {
        if (forcedFish != null)
        {
            curFish = forcedFish;
            curWeight = forcedWeight;
            forcedFish = null;
            return;
        }
        bool deep = water.IsDeep(castTarget), cover = water.IsNearCover(castTarget), frozen = water.IsFrozen(), night = IsNight();
        float total = 0f;
        float[] odds = new float[fish.Length];
        for (int i = 0; i < fish.Length; i++)
        {
            FishData kind = fish[i];
            if (kind == null)
                continue;
            float weight = kind.TasteFor(castBait);
            if (deep) weight *= kind.deepWater;
            if (cover) weight *= kind.nearCover;
            if (night) weight *= kind.atNight;
            if (frozen) weight *= kind.underIce;
            odds[i] = Mathf.Max(0f, weight);
            total += odds[i];
        }
        float roll = (float)random.NextDouble() * total;
        curFish = null;
        for (int i = 0; i < fish.Length; i++)
        {
            if (fish[i] == null)
                continue;
            curFish = fish[i];//no bait weights at all: the last fish in the list
            if (odds[i] > 0f && roll < odds[i])
                break;
            roll -= odds[i];
        }
        curWeight = curFish.WeightFor((float)random.NextDouble(), curFish.SizeSkewFor(castBait));
    }

    private bool IsNight()
    {
        if (calendar == null)
            return false;
        int minutes = calendar.GetMinutesSinceMidnight();
        return minutes >= 20 * 60 || minutes < 5 * 60;
    }

    private void UpdateNibbling()
    {
        if (ReelInAsked())
        {
            ReelIn();
            return;
        }
        while (nextNibble < nibbleTimes.Count && stateTime >= nibbleTimes[nextNibble])
        {
            nextNibble++;
            nibbleEvent.Invoke();
        }
        if (stateTime < nibbleEnd)
            return;
        if (willBite)
            BeginBite();
        else
        {
            turnedAway++;
            fishLeftEvent.Invoke();
            BeginWait(waitAfterMiss);
        }
    }

    private void BeginBite()
    {
        biteWindow = Mathf.Max(minBiteWindow, curFish.biteWindowSeconds * Mathf.Lerp(1.1f, 0.8f, curFish.Size01(curWeight)));
        if (!castBait.reusable)
            inventory.UseLoadedWorm();//a worm is gone now, hooked or not; a lure stays
        ChangeState(FishingState.Biting);
        biteEvent.Invoke();
    }

    //sets the hook while the bobber is under (what a left click does then). Returns false at any other time.
    public bool TryHook()
    {
        if (fishingState != FishingState.Biting)
            return false;
        float size = curFish.Size01(curWeight);
        float length = Mathf.Lerp(curFish.fightSeconds.x, curFish.fightSeconds.y, size);
        //fast enough that a clean fight (reel while calm, let go while it pulls) lands the fish in a few pulls: a big
        //bass or catfish has to be reachable without ever risking the line
        reelSpeed = (1f - startProgress) / Mathf.Max(0.2f, length * reelShare);
        progress = startProgress;
        tension = startTension;
        surging = false;
        surgeWarned = false;
        phaseLeft = Range(calmSeconds.x, calmSeconds.y) + 0.2f;//a calm moment first, to start reeling
        overTime = 0f;
        slackTime = 0f;
        ChangeState(FishingState.Fighting);
        hookedEvent.Invoke();
        return true;
    }

    private void MissBite()
    {
        fishLeftEvent.Invoke();
        if (!castBait.reusable)
        {
            hotbar.Tell("Too slow: the fish stole your " + castBait.baitName.ToLower() + ".");
            ReelIn();
            return;
        }
        BeginWait(waitAfterMiss);
    }

    // ---------------------------------------------------------------- the fight

    private void UpdateFight(float dt)
    {
        bool reeling = clickHeld || reelOverride;
        //the fish takes turns: calm, a splash as a warning, then a pull
        phaseLeft -= dt;
        if (!surging && !surgeWarned && phaseLeft <= surgeWarningSeconds)
        {
            surgeWarned = true;
            surgeWarningEvent.Invoke();
        }
        if (phaseLeft <= 0f)
        {
            surging = !surging;
            surgeWarned = false;
            phaseLeft = surging ? Range(curFish.surgeSeconds.x, curFish.surgeSeconds.y) : Range(calmSeconds.x, calmSeconds.y);
            surgeChangedEvent.Invoke();
        }

        float strength = curFish.strength * Mathf.Lerp(0.85f, 1.25f, curFish.Size01(curWeight));
        if (reeling)
        {
            progress += reelSpeed * (surging ? surgeReelFactor : 1f) * dt;
            tension += (surging ? surgeTension * strength : calmTension) * dt;
        }
        else
        {
            progress -= runSpeed * (surging ? strength : 0.15f) * dt;
            tension -= relaxRate * dt;
        }
        progress = Mathf.Clamp01(progress);
        tension = Mathf.Clamp01(tension);

        if (progress >= 1f)
        {
            LandFish();
            return;
        }
        overTime = tension >= 1f ? overTime + dt : 0f;
        if (overTime >= snapGraceSeconds)
        {
            SnapLine();
            return;
        }
        slackTime = !reeling && progress <= 0.001f ? slackTime + dt : 0f;
        if (slackTime >= slackSeconds || stateTime >= maxFightSeconds)
        {
            fishLeftEvent.Invoke();
            hotbar.Tell("It got away. Keep reeling while the fish is calm.");
            landingKind = LandingKind.Empty;
            ChangeState(FishingState.Landing);
        }
    }

    private void LandFish()
    {
        lastCatch = new Catch
        {
            fish = curFish,
            weight = curWeight,
            price = curFish.PriceFor(curWeight),
            trophy = curFish.IsTrophy(curWeight),
        };
        lastCatch.best = inventory.AddFish(curFish, curWeight);//into the bag
        landingKind = LandingKind.Fish;
        ChangeState(FishingState.Landing);
        fishCaughtEvent.Invoke(lastCatch);
    }

    private void SnapLine()
    {
        landingKind = LandingKind.Snapped;
        ChangeState(FishingState.Landing);
        lineSnappedEvent.Invoke();
        hotbar.TellRejection("The line snapped! Let go while the fish pulls.");
    }

    // ---------------------------------------------------------------- the line comes back

    private float LandingLength()
    {
        switch (landingKind)
        {
            case LandingKind.Fish: return landSeconds + showSeconds;
            case LandingKind.Snapped: return snapRecoverSeconds;
        }
        return reelInSeconds;
    }

    private void EndFishing()
    {
        playerStateControl.StopFishing();
        ListenTo(null);
        curFish = null;
        SettleBait();
        ChangeState(CanAim() ? FishingState.Aiming : FishingState.Stowed);
    }

    //the line is back: a worn-out lure becomes an old lure or breaks, the last worm leaves an empty rod
    private void SettleBait()
    {
        if (inventory == null || castBait == null)
            return;
        BaitData cast = castBait;
        string message = null;
        switch (inventory.SettleLoadedBait())
        {
            case FarmInventory.BaitSettle.WornOut:
                message = "Your " + cast.baitName.ToLower() + " is worn out: it is an " + cast.wornInto.baitName.ToLower() + " now.";
                break;
            case FarmInventory.BaitSettle.Broke:
                message = "The " + cast.baitName.ToLower() + " broke.";
                break;
            case FarmInventory.BaitSettle.RanOut:
                message = "That was your last " + cast.baitName.ToLower() + ".";
                break;
        }
        if (message != null && hotbar != null)
            hotbar.Tell(message);
    }

    //stops everything at once (the rod left the hand, the pond froze, the scene is closing)
    public void Abort()
    {
        Abort(true);
    }

    private void Abort(bool settle)
    {
        if (fishingState == FishingState.Stowed)
            return;
        bool lineWasOut = IsLineOut();
        ListenTo(null);
        curFish = null;
        landingKind = LandingKind.Empty;
        if (lineWasOut && playerStateControl != null)
            playerStateControl.StopFishing();
        if (lineWasOut && settle)
            SettleBait();
        ChangeState(FishingState.Stowed);
    }

    private void ListenTo(FishingWater pond)
    {
        if (listeningTo == pond)
            return;
        if (listeningTo != null)
            listeningTo.frozenChangedEvent.RemoveListener(OnFrozenChanged);
        listeningTo = pond;
        if (listeningTo != null)
            listeningTo.frozenChangedEvent.AddListener(OnFrozenChanged);
    }

    //the pond froze or thawed under the line
    private void OnFrozenChanged()
    {
        if (!IsLineOut())
            return;
        bool frozen = listeningTo != null && listeningTo.IsFrozen();
        Abort();
        if (hotbar != null)
            hotbar.Tell(frozen ? "The pond froze over. Fish through the hole in the ice." : "The ice is gone.");
    }

    // ---------------------------------------------------------------- helpers

    private float Range(float min, float max)
    {
        return min + (float)random.NextDouble() * (max - min);
    }

    private static Vector3 Flat(Vector3 v)
    {
        return new Vector3(v.x, 0f, v.z);
    }

    // ---------------------------------------------------------------- API for the visuals, the HUD and the tests

    public FishingState GetFishingState()
    {
        return fishingState;
    }
    //seconds since the state changed
    public float GetStateTime()
    {
        return stateTime;
    }
    //true from the cast until the line is back in
    public bool IsLineOut()
    {
        return fishingState != FishingState.Stowed && fishingState != FishingState.Aiming;
    }
    //the rod is out and the player is close enough to water to fish
    public bool IsNearWater()
    {
        return fishingState == FishingState.Aiming && nearWater;
    }
    public bool IsAimVisible()
    {
        return fishingState == FishingState.Aiming && aimVisible;
    }
    public bool IsAimGood()
    {
        return IsAimVisible() && aimProblem == AimProblem.None;
    }
    public AimProblem GetAimProblem()
    {
        return aimProblem;
    }
    public Vector3 GetAimPoint()
    {
        return aimPoint;
    }
    public Vector3 GetCastTarget()
    {
        return castTarget;
    }
    public FishingWater GetWater()
    {
        return water;
    }
    public BaitData GetCastBait()
    {
        return castBait;
    }
    public float GetWindUpSeconds()
    {
        return windUpSeconds;
    }
    public float GetFlightSeconds()
    {
        return flightTime;
    }
    public float GetMaxCast()
    {
        return maxCast;
    }
    //the fish at the bait or on the line (null while nothing is there)
    public FishData GetFish()
    {
        return curFish;
    }
    public float GetFishWeight()
    {
        return curWeight;
    }
    public Vector3 GetApproachFrom()
    {
        return approachFrom;
    }
    public float GetApproachSeconds()
    {
        return approachTime;
    }
    public float GetBiteWindow()
    {
        return biteWindow;
    }
    //the fight: 0 = the fish is where it bit, 1 = at the player's feet
    public float GetFightProgress()
    {
        return progress;
    }
    //the fight: 0 = slack line, 1 = about to snap
    public float GetTension()
    {
        return fishingState == FishingState.Fighting ? tension : 0f;
    }
    public bool IsSurging()
    {
        return fishingState == FishingState.Fighting && surging;
    }
    public bool IsReeling()
    {
        return fishingState == FishingState.Fighting && (clickHeld || reelOverride);
    }
    public LandingKind GetLandingKind()
    {
        return landingKind;
    }
    public float GetLandSeconds()
    {
        return landSeconds;
    }
    public float GetLandingLength()
    {
        return LandingLength();
    }
    public Catch GetLastCatch()
    {
        return lastCatch;
    }
    public FishData[] GetFishKinds()
    {
        return fish;
    }

    //for the tests: the same fish every time
    public void SetRandomSeed(int seed)
    {
        random = new System.Random(seed);
    }
    //for the tests: the next fish that comes to the bait (it bites)
    public void ForceNextFish(FishData kind, float weight)
    {
        forcedFish = kind;
        forcedWeight = weight;
    }
    //for the tests: holds or lets go of the reel, as the left mouse button does
    public void SetReeling(bool on)
    {
        reelOverride = on;
    }
}
