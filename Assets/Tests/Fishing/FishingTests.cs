using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

//Fishing at the farm pond, driven through the FishingControl's public API (no simulated mouse): where a cast may land,
//the bite and the fight, bait use, the shop's Fishing tab, the rod by the pond and the hole in the winter ice.
public class FishingTests : HW6TestBase
{
    private static readonly Vector3 Bankside = new Vector3(7.2f, 1f, 6.6f);//on the grass south-west of the pond
    private static readonly Vector3 OpenWater = new Vector3(8.6f, 0.05f, 8.9f);//2.7 m out, clear of the bank

    private const string DataDir = "Assets/Game/Data/Fishing/";

    private static FishingControl Fishing() { return Object.FindAnyObjectByType<FishingControl>(); }
    private static FarmInventory Items() { return global::FarmFund.Instance.Inventory; }
    private static PlayerStateControl Player() { return Object.FindAnyObjectByType<PlayerStateControl>(); }

    private static FishData Fish(string name)
    {
#if UNITY_EDITOR
        var fish = UnityEditor.AssetDatabase.LoadAssetAtPath<FishData>(DataDir + "Fish/" + name + ".asset");
        Assert.IsNotNull(fish, "missing fish " + name);
        return fish;
#else
        Assert.Ignore("fish assets are loaded through the AssetDatabase");
        return null;
#endif
    }

    private static BaitData Bait(string name)
    {
#if UNITY_EDITOR
        var bait = UnityEditor.AssetDatabase.LoadAssetAtPath<BaitData>(DataDir + "Bait/" + name + ".asset");
        Assert.IsNotNull(bait, "missing bait " + name);
        return bait;
#else
        Assert.Ignore("bait assets are loaded through the AssetDatabase");
        return null;
#endif
    }

    private static IEnumerator MoveTo(Vector3 position)
    {
        Rigidbody rb = Player().GetComponent<Rigidbody>();
        rb.position = position;
        rb.linearVelocity = Vector3.zero;
        Player().transform.position = position;
        yield return new WaitForFixedUpdate();
        yield return null;
    }

    private static IEnumerator WaitFor(FishingControl.FishingState state, float seconds)
    {
        float giveUp = Time.realtimeSinceStartup + seconds;
        while (Fishing().GetFishingState() != state && Time.realtimeSinceStartup < giveUp)
            yield return null;
        Assert.AreEqual(state, Fishing().GetFishingState(), "the fishing never reached " + state);
    }

    //on the farm with the rod in the hand, standing on the grass by the pond, aiming
    private static IEnumerator StandByThePondWithTheRod()
    {
        StartSession(100, 0);
        yield return Load(Farm);
        FreezeCalendar();
        Items().AddTool(ToolManager.ToolType.FishingRod);
        yield return MoveTo(Bankside);
        Hold("FishingRod");
        Fishing().SetRandomSeed(12345);
        yield return WaitFor(FishingControl.FishingState.Aiming, 3f);
    }

    //casts with a fish on its way (it always bites) and waits, sped up, until the bobber goes under
    private static IEnumerator CastForABite(FishData fish, float weight, Vector3 spot)
    {
        Fishing().ForceNextFish(fish, weight);
        Assert.IsTrue(Fishing().TryCastAt(spot), "the cast starts");
        Time.timeScale = 6f;
        float giveUp = Time.realtimeSinceStartup + 15f;
        while (Fishing().GetFishingState() != FishingControl.FishingState.Biting && Time.realtimeSinceStartup < giveUp)
            yield return null;
        Time.timeScale = 1f;
        Assert.AreEqual(FishingControl.FishingState.Biting, Fishing().GetFishingState(), "the fish bites");
        Assert.AreEqual(fish, Fishing().GetFish());
    }

    //a clean fight: reel while the fish is calm, let go while it pulls
    private static IEnumerator FightCleanly()
    {
        Time.timeScale = 2f;
        float giveUp = Time.realtimeSinceStartup + 20f;
        while (Fishing().GetFishingState() == FishingControl.FishingState.Fighting && Time.realtimeSinceStartup < giveUp)
        {
            Fishing().SetReeling(!Fishing().IsSurging());
            yield return null;
        }
        Fishing().SetReeling(false);
        Time.timeScale = 1f;
    }

    [UnityTest]
    public IEnumerator Cast_GoesOnlyToOpenWaterInRange()
    {
        yield return StandByThePondWithTheRod();
        FishingControl fishing = Fishing();
        Hotbar hotbar = Object.FindAnyObjectByType<Hotbar>();
        float energy = CurEnergy();

        var refused = new[]
        {
            (new Vector3(9.6f, 0f, 6.85f), FishingControl.AimProblem.NotWater, "the sandy bank"),
            (new Vector3(6.0f, 0f, 6.5f), FishingControl.AimProblem.NotWater, "the grass"),
            (new Vector3(9.6f, 0.05f, 7.25f), FishingControl.AimProblem.TooCloseToShore, "the water right at the bank"),
        };
        foreach (var (spot, problem, what) in refused)
        {
            Assert.AreEqual(problem, fishing.ProblemAt(spot), what);
            Assert.IsFalse(fishing.TryCastAt(spot), "no cast onto " + what);
            Assert.AreEqual(FishingControl.FishingState.Aiming, fishing.GetFishingState(), what + ": still aiming");
            Assert.AreEqual(PlayerStateControl.PlayerState.Idle, Player().GetPlayerState(), what + ": the player stays free");
            Assert.IsFalse((bool)Get(hotbar, "selectionLocked"), what + ": the hotbar is not locked");
            Assert.AreEqual(energy, CurEnergy(), 0.001f, what + ": no energy spent");
        }

        //out of reach: the middle of the pond from far back on the grass
        yield return MoveTo(new Vector3(3.5f, 1f, 4.5f));
        Vector3 middle = new Vector3(10.5f, 0.05f, 9.5f);
        Assert.AreEqual(FishingControl.AimProblem.TooFar, fishing.ProblemAt(middle));
        Assert.IsFalse(fishing.TryCastAt(middle), "no cast out of range");
        Assert.AreEqual(FishingControl.FishingState.Aiming, fishing.GetFishingState());

        yield return MoveTo(Bankside);
        Assert.AreEqual(FishingControl.AimProblem.None, fishing.ProblemAt(OpenWater));
        Assert.IsTrue(fishing.TryCastAt(OpenWater), "open water in reach takes the cast");
        Assert.AreEqual(FishingControl.FishingState.Casting, fishing.GetFishingState());
        Assert.AreEqual(PlayerStateControl.PlayerState.Fishing, Player().GetPlayerState(), "the player stands still while the line is out");
        Assert.IsTrue((bool)Get(hotbar, "selectionLocked"), "the item in the hand cannot change while the line is out");
        Assert.Less(CurEnergy(), energy, "a cast costs a little energy");
    }

    [UnityTest]
    public IEnumerator ReelIn_BringsTheLineBack_AndFreesThePlayer()
    {
        yield return StandByThePondWithTheRod();
        Assert.IsTrue(Fishing().TryCastAt(OpenWater));
        yield return WaitFor(FishingControl.FishingState.Waiting, 3f);
        Fishing().ReelIn();
        Assert.AreEqual(FishingControl.FishingState.Landing, Fishing().GetFishingState());
        Assert.AreEqual(FishingControl.LandingKind.Empty, Fishing().GetLandingKind());
        yield return WaitFor(FishingControl.FishingState.Aiming, 3f);
        Assert.AreEqual(PlayerStateControl.PlayerState.Idle, Player().GetPlayerState());
        Assert.IsFalse((bool)Get(Object.FindAnyObjectByType<Hotbar>(), "selectionLocked"), "the hotbar is free again");
    }

    [UnityTest]
    public IEnumerator HookedFish_IsReeledIn_AndGoesIntoTheInventoryAndTheBag()
    {
        yield return StandByThePondWithTheRod();
        FishData crappie = Fish("Crappie");
        yield return CastForABite(crappie, 1.4f, OpenWater);
        Assert.IsTrue(Fishing().TryHook(), "a click while the bobber is under hooks the fish");
        Assert.AreEqual(FishingControl.FishingState.Fighting, Fishing().GetFishingState());
        yield return FightCleanly();

        Assert.AreEqual(FishingControl.FishingState.Landing, Fishing().GetFishingState());
        Assert.AreEqual(FishingControl.LandingKind.Fish, Fishing().GetLandingKind(), "a clean fight lands the fish");
        FishingControl.Catch caught = Fishing().GetLastCatch();
        Assert.AreEqual(crappie, caught.fish);
        Assert.AreEqual(1.4f, caught.weight, 0.001f);
        Assert.AreEqual(crappie.PriceFor(1.4f), caught.price);
        Assert.AreEqual(1, Items().GetFishCount(crappie));
        Assert.AreEqual(1.4f, Items().GetBestFish(crappie), 0.001f);
        Assert.AreEqual(crappie.PriceFor(1.4f), Items().GetFishValue(crappie, 1));
        yield return null;
        FarmBag bag = Object.FindAnyObjectByType<FarmBag>();
        Assert.AreEqual(1, bag.GetInventoryManager().GetAmount(crappie.item.itemName), "the fish shows in the bag");
        yield return WaitFor(FishingControl.FishingState.Aiming, 4f);
        Assert.AreEqual(PlayerStateControl.PlayerState.Idle, Player().GetPlayerState());
    }

    [UnityTest]
    public IEnumerator Worm_IsEatenByABite_ButALureIsNot()
    {
        yield return StandByThePondWithTheRod();
        BaitData worm = Bait("Worm"), lure = Bait("Lure");
        Items().AddBait(worm, 2);
        Items().SelectBait(worm);
        Assert.AreEqual(worm, Fishing().GetBait());

        //a cast reeled in before anything bites keeps its worm
        Assert.IsTrue(Fishing().TryCastAt(OpenWater));
        yield return WaitFor(FishingControl.FishingState.Waiting, 3f);
        Fishing().ReelIn();
        yield return WaitFor(FishingControl.FishingState.Aiming, 3f);
        Assert.AreEqual(2, Items().GetBait(worm), "an untouched reel-in keeps the worm");

        yield return CastForABite(Fish("Bluegill"), 0.6f, OpenWater);
        Assert.AreEqual(1, Items().GetBait(worm), "the fish that bites eats the worm");
        Fishing().ReelIn();
        yield return WaitFor(FishingControl.FishingState.Aiming, 3f);

        Items().AddBait(lure, 1);
        Items().SelectBait(lure);
        Assert.AreEqual(lure, Fishing().GetBait());
        yield return CastForABite(Fish("Largemouth Bass"), 2f, OpenWater);
        Assert.AreEqual(1, Items().GetBait(lure), "a lure stays on the line");
        Assert.AreEqual(1, Items().GetBait(worm), "the worms were not touched");
        Fishing().ReelIn();
        yield return WaitFor(FishingControl.FishingState.Aiming, 3f);
    }

    [UnityTest]
    public IEnumerator HoldingTheReelThroughAStrongPull_SnapsTheLine()
    {
        yield return StandByThePondWithTheRod();
        FishData catfish = Fish("Catfish");
        yield return CastForABite(catfish, 11.5f, OpenWater);
        Assert.IsTrue(Fishing().TryHook());
        Time.timeScale = 2f;
        float giveUp = Time.realtimeSinceStartup + 15f;
        while (Fishing().GetFishingState() == FishingControl.FishingState.Fighting && Time.realtimeSinceStartup < giveUp)
        {
            Fishing().SetReeling(true);//never lets go
            yield return null;
        }
        Fishing().SetReeling(false);
        Time.timeScale = 1f;
        Assert.AreEqual(FishingControl.FishingState.Landing, Fishing().GetFishingState());
        Assert.AreEqual(FishingControl.LandingKind.Snapped, Fishing().GetLandingKind(), "the line snaps");
        Assert.AreEqual(0, Items().GetFishCount(catfish), "and the fish is gone");
        yield return WaitFor(FishingControl.FishingState.Aiming, 3f);
        Assert.AreEqual(PlayerStateControl.PlayerState.Idle, Player().GetPlayerState());
    }

    [UnityTest]
    public IEnumerator Bait_OldLureIsReplacedByTheLure_AndSwitchingSkipsMissingBait()
    {
        yield return StandByThePondWithTheRod();
        BaitData old = Bait("Old Lure"), worm = Bait("Worm"), lure = Bait("Lure");
        Assert.AreEqual(old, Fishing().GetBait(), "the rod comes with the old lure");
        Assert.IsFalse(Fishing().NextBait(), "nothing to switch to yet");
        Assert.AreEqual(old, Fishing().GetBait());

        Items().AddBait(worm, 3);
        Assert.IsTrue(Fishing().NextBait());
        Assert.AreEqual(worm, Fishing().GetBait());
        Assert.IsTrue(Fishing().NextBait());
        Assert.AreEqual(old, Fishing().GetBait(), "the right click skips the lure the player does not own");

        Items().AddBait(lure, 1);
        Assert.IsFalse(Items().CanUseBait(old), "the old lure is put away once the lure is owned");
        Assert.AreEqual(lure, Fishing().GetBait(), "the lure takes its place");
        Assert.IsTrue(Fishing().NextBait());
        Assert.AreEqual(worm, Fishing().GetBait());
        Assert.IsTrue(Fishing().NextBait());
        Assert.AreEqual(lure, Fishing().GetBait(), "the old lure is skipped");
    }

    [UnityTest]
    public IEnumerator Shop_FishingTab_SellsBaitAndBuysFish()
    {
        StartSession(100, 0);
        yield return Load(Farm);
        FreezeCalendar();
        Items().AddTool(ToolManager.ToolType.FishingRod);
        FishData bass = Fish("Largemouth Bass");
        Items().AddFish(bass, 2.0f);
        Items().AddFish(bass, 5.5f);
        Items().AddFish(bass, 3.0f);
        yield return OpenShop();

        ShopTabs tabs = Object.FindAnyObjectByType<ShopTabs>();
        ButtonNamed("Tab - Fishing").onClick.Invoke();
        Assert.AreEqual(ShopTabs.Tab.Fishing, tabs.Current);
        int shown = 0;
        foreach (StoreItemRow row in Shop().Rows)
        {
            bool fishing = row.GetKind() == StoreItemRow.RowKind.BuyBait || row.GetKind() == StoreItemRow.RowKind.SellFish;
            Assert.AreEqual(fishing, row.gameObject.activeInHierarchy, row.name);
            if (fishing)
                shown++;
        }
        Assert.AreEqual(6, shown, "worms, the lure and four fish");

        BaitData worm = Bait("Worm"), lure = Bait("Lure");
        ((StoreItemRow)Row("Row - Worms")).SetQuantity(5);
        ButtonNamed("Buy Worms").onClick.Invoke();
        Assert.AreEqual(5, Items().GetBait(worm));
        Assert.AreEqual(100 - 5 * worm.price, Fund);

        ButtonNamed("Buy Lure").onClick.Invoke();
        Assert.AreEqual(1, Items().GetBait(lure));
        int afterLure = 100 - 5 * worm.price - lure.price;
        Assert.AreEqual(afterLure, Fund);
        ButtonNamed("Buy Lure").onClick.Invoke();
        Assert.AreEqual(afterLure, Fund, "the lure is bought only once");
        Assert.AreEqual(1, Items().GetBait(lure));

        ButtonNamed("Sell Largemouth Bass").onClick.Invoke();
        Assert.AreEqual(afterLure + bass.PriceFor(5.5f), Fund, "the heaviest bass goes first, paid by its weight");
        Assert.AreEqual(2, Items().GetFishCount(bass));
        Assert.AreEqual(bass.PriceFor(3.0f), Items().GetFishValue(bass, 1), "the 3 lb bass is the heaviest one left");
    }

    [UnityTest]
    public IEnumerator RodPickup_ByThePond_GivesTheRod()
    {
        StartSession(100, 0);
        yield return Load(Farm);
        ToolPickup pickup = ToolPickup.Find(ToolManager.ToolType.FishingRod);
        Assert.IsNotNull(pickup, "the rod lies by the pond");
        WorldInteractor player = Object.FindAnyObjectByType<WorldInteractor>();
        yield return MoveTo(new Vector3(pickup.transform.position.x - 0.4f, 1f, pickup.transform.position.z - 0.6f));
        Assert.LessOrEqual(pickup.DistanceOutsideReach(player.transform.position), 0f, "the player can reach it from the grass");
        pickup.Interact(player);
        yield return null;
        Assert.IsTrue(Items().HasTool(ToolManager.ToolType.FishingRod));
        Assert.IsTrue(Object.FindAnyObjectByType<Hotbar>().IsHolding(ToolManager.ToolType.FishingRod), "straight into the hand");
        Assert.AreEqual(Bait("Old Lure"), Fishing().GetBait(), "with the old lure on the line");
    }

    [UnityTest]
    public IEnumerator Winter_OnlyTheHoleInTheIceIsCastable()
    {
        yield return StandByThePondWithTheRod();
        AdvanceDays(Object.FindAnyObjectByType<GameCalendar>().DaysUntil(GameCalendar.Season.Winter));
        Assert.AreEqual("Winter", Season());
        yield return null;
        yield return WaitFor(FishingControl.FishingState.Aiming, 3f);
        FishingControl fishing = Fishing();
        Assert.IsTrue(fishing.GetWater().IsFrozen());
        Assert.AreEqual(FishingControl.AimProblem.Ice, fishing.ProblemAt(OpenWater), "the open pond is ice now");
        Assert.IsFalse(fishing.TryCastAt(OpenWater));
        Transform hole = FindInScene("Winter Fishing Hole").transform;
        Assert.IsTrue(fishing.TryCastAt(hole.position), "the hole takes the cast");
        yield return WaitFor(FishingControl.FishingState.Waiting, 3f);
        Fishing().ReelIn();
        yield return WaitFor(FishingControl.FishingState.Aiming, 3f);
    }
}
