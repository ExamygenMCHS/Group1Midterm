using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;

//Fishing at the farm pond, driven through the FishingControl's public API (no simulated mouse): where a cast may land,
//the bite and the fight, bait loaded into the rod (through the inventory and by drag and drop in the bag), lures that
//wear out, the shop's Fishing tab, the rod and the old lure by the pond and the hole in the winter ice.
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

    //on the farm with the rod in the hand, standing on the grass by the pond, aiming (with an old lure in the rod, unless
    //the test wants the rod empty)
    private static IEnumerator StandByThePondWithTheRod(bool withOldLure = true)
    {
        StartSession(100, 0);
        yield return Load(Farm);
        FreezeCalendar();
        Items().AddTool(ToolManager.ToolType.FishingRod);
        if (withOldLure)
            LoadNew(Bait("Old Lure"));
        yield return MoveTo(Bankside);
        Hold("FishingRod");
        Fishing().SetRandomSeed(12345);
        yield return WaitFor(FishingControl.FishingState.Aiming, 3f);
    }

    //a new bait into the bag and straight into the rod (a lure: the newest one)
    private static void LoadNew(BaitData bait, int amount = 1)
    {
        Items().AddBait(bait, amount);
        Assert.IsTrue(Items().LoadBait(bait, Items().GetBait(bait) - 1));
    }

    private static FarmBag TheBag() { return Object.FindAnyObjectByType<FarmBag>(); }
    private static BaitBagUI BaitUI() { return Object.FindAnyObjectByType<BaitBagUI>(); }

    //casts and reels straight back in, the line is back when this returns
    private static IEnumerator CastAndReelIn()
    {
        Assert.IsTrue(Fishing().TryCastAt(OpenWater), "the cast starts");
        yield return WaitFor(FishingControl.FishingState.Waiting, 3f);
        Fishing().ReelIn();
        yield return WaitFor(FishingControl.FishingState.Aiming, 3f);
    }

    // ---- drag and drop in the bag, as the EventSystem does it (begin, drag, drop on the first handler up from the
    // object under the mouse, end); a dropped bait that was loaded is gone, so it gets no end of drag, as in the game

    private static PointerEventData BeginDrag(GameObject item)
    {
        var drag = new PointerEventData(EventSystem.current)
        {
            pointerDrag = item,
            button = PointerEventData.InputButton.Left,
            position = new Vector2(-100f, -100f)
        };
        Assert.IsTrue(ExecuteEvents.Execute(item, drag, ExecuteEvents.beginDragHandler), "the item can be dragged");
        Assert.IsTrue(ExecuteEvents.Execute(item, drag, ExecuteEvents.dragHandler));
        return drag;
    }

    private static void DropOn(PointerEventData drag, GameObject underMouse)
    {
        ExecuteEvents.ExecuteHierarchy(underMouse, drag, ExecuteEvents.dropHandler);
        ExecuteEvents.Execute(drag.pointerDrag, drag, ExecuteEvents.endDragHandler);
        TheBag().CheckBarChanged();//the mouse release normally triggers this in LateUpdate
    }

    private static List<int> Durabilities(BaitData bait)
    {
        return new List<int>(Items().GetBaitDurabilities(bait));
    }

    private static void SetDurabilities(BaitData bait, params int[] values)
    {
        FarmInventory.BaitStock stock = Items().baits.Find(s => s.bait == bait);
        Assert.AreEqual(values.Length, stock.durabilities.Count);
        for (int i = 0; i < values.Length; i++)
            stock.durabilities[i] = values[i];
        Items().inventoryChangedEvent.Invoke();
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
        yield return StandByThePondWithTheRod(false);
        BaitData worm = Bait("Worm"), lure = Bait("Lure"), old = Bait("Old Lure");
        Items().AddBait(worm, 2);
        Assert.IsTrue(Items().LoadBait(worm));
        Assert.AreEqual(worm, Fishing().GetBait());
        Assert.AreEqual(2, Fishing().GetBaitCount(), "the whole worm stack goes into the rod");
        Assert.AreEqual(0, Items().GetBait(worm), "and leaves the bag");

        //a cast reeled in before anything bites keeps its worm
        yield return CastAndReelIn();
        Assert.AreEqual(2, Fishing().GetBaitCount(), "an untouched reel-in keeps the worm");

        yield return CastForABite(Fish("Bluegill"), 0.6f, OpenWater);
        Assert.AreEqual(1, Fishing().GetBaitCount(), "the fish that bites eats the worm");
        Fishing().ReelIn();
        yield return WaitFor(FishingControl.FishingState.Aiming, 3f);

        //more worms dropped on a rod with worms add up
        Items().AddBait(worm, 3);
        Assert.IsTrue(Items().LoadBait(worm));
        Assert.AreEqual(4, Fishing().GetBaitCount(), "worms on worms merge");
        Assert.AreEqual(4, Items().GetBaitOwned(worm));

        //a lure takes the worms' place: they go back to the bag
        Items().AddBait(lure, 1);
        Assert.IsTrue(Items().LoadBait(lure, 0));
        Assert.AreEqual(lure, Fishing().GetBait());
        Assert.AreEqual(4, Items().GetBait(worm), "the worms are back in the bag");
        Assert.AreEqual(30, Fishing().GetBaitDurability(), "a new lure");
        yield return CastForABite(Fish("Largemouth Bass"), 2f, OpenWater);
        Assert.AreEqual(lure, Fishing().GetBait(), "a lure stays on the line");
        Assert.AreEqual(29, Fishing().GetBaitDurability(), "one cast of durability, spent at the cast");
        Assert.AreEqual(4, Items().GetBait(worm), "the worms were not touched");
        Fishing().ReelIn();
        yield return WaitFor(FishingControl.FishingState.Aiming, 3f);

        //swapping returns the lure with what is left of it
        Items().AddBait(old, 1);
        Assert.IsTrue(Items().LoadBait(old, 0));
        CollectionAssert.AreEqual(new[] { 29 }, Durabilities(lure), "the lure went back with 29 casts left");
        Assert.AreEqual(old, Fishing().GetBait());
    }

    [UnityTest]
    public IEnumerator Lure_WearsOutIntoAnOldLure_ThatBreaks()
    {
        yield return StandByThePondWithTheRod(false);
        BaitData lure = Bait("Lure"), old = Bait("Old Lure");
        Assert.AreEqual(30, lure.durability);
        Assert.AreEqual(old, lure.wornInto);
        Assert.AreEqual(30, old.durability);
        Assert.IsNull(old.wornInto, "the old lure breaks");
        LoadNew(lure);
        Items().loadedDurability = 2;

        yield return CastAndReelIn();
        Assert.AreEqual(1, Fishing().GetBaitDurability(), "one durability per cast");
        Assert.IsTrue(Fishing().TryCastAt(OpenWater));
        Assert.AreEqual(0, Fishing().GetBaitDurability(), "the last cast of the lure");
        Assert.AreEqual(lure, Fishing().GetCastBait(), "the worn lure finishes this cast");
        yield return WaitFor(FishingControl.FishingState.Waiting, 3f);
        Assert.AreEqual(lure, Fishing().GetBait(), "still the lure while the line is out");
        Fishing().ReelIn();
        yield return WaitFor(FishingControl.FishingState.Aiming, 3f);
        Assert.AreEqual(old, Fishing().GetBait(), "the worn lure is an old lure now");
        Assert.AreEqual(30, Fishing().GetBaitDurability(), "with a fresh durability");
        StringAssert.Contains("old lure", HudMessage());

        Items().loadedDurability = 1;
        yield return CastAndReelIn();
        Assert.IsNull(Fishing().GetBait(), "the old lure broke");
        Assert.IsFalse(Items().HasLoadedBait());
        Assert.AreEqual(0, Items().GetBaitOwned(old), "and is gone");
        StringAssert.Contains("broke", HudMessage());
    }

    [UnityTest]
    public IEnumerator Cast_WithNoBait_DoesNothing()
    {
        yield return StandByThePondWithTheRod(false);
        bool told = false;
        Fishing().noBaitEvent.AddListener(() => told = true);
        float energy = CurEnergy();
        Assert.IsNull(Fishing().GetBait(), "the rod comes empty");
        Assert.IsFalse(Fishing().TryCastAt(OpenWater), "no bait, no cast");
        Assert.IsTrue(told, "the HUD is told to show where the bait goes");
        Assert.AreEqual(FishingControl.FishingState.Aiming, Fishing().GetFishingState());
        Assert.AreEqual(PlayerStateControl.PlayerState.Idle, Player().GetPlayerState());
        Assert.AreEqual(energy, CurEnergy(), 0.001f, "no energy spent");
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
    public IEnumerator BagDrop_LoadsTheDraggedBait_IntoTheRod()
    {
        yield return StandByThePondWithTheRod(false);
        BaitData lure = Bait("Lure"), worm = Bait("Worm");
        FarmBag bag = TheBag();
        InventoryManager manager = bag.GetInventoryManager();
        Items().SetBag(true);
        bag.SetBagOpen(true);//the bar is full of tools and seeds: some bait lands in the main bag
        Items().AddBait(lure, 3);
        Items().AddBait(worm, 6);
        SetDurabilities(lure, 30, 12, 25);
        yield return null;
        List<GameObject> lures = manager.GetItems("Lure");
        Assert.AreEqual(3, lures.Count, "every lure is an item of its own");
        BaitBagUI ui = BaitUI();
        ui.RefreshNow();
        Assert.IsFalse(lures[0].GetComponent<BaitBadge>().IsBarShown(), "a new lure shows no bar");
        Assert.AreEqual(12f / 30f, lures[1].GetComponent<BaitBadge>().GetBarShare(), 0.001f, "a used lure shows what is left");
        StringAssert.Contains("12 / 30", ui.TextFor(lures[1]));
        StringAssert.Contains("x 6", ui.TextFor(manager.GetItem("Worms")));
        StringAssert.Contains("No bait", ui.TextFor(bag.GetItemInHand()), "the rod in the hand is empty");

        //the rod is in the hand: drop the middle lure on its faded copy in the bar
        GameObject placeholderSlot = bag.GetPlaceholder().transform.parent.gameObject;
        Transform secondHome = lures[1].transform.parent;
        DropOn(BeginDrag(lures[1]), placeholderSlot);
        yield return null;
        Assert.AreEqual(lure, Fishing().GetBait(), "dropped on the faded copy: loaded");
        Assert.AreEqual(12, Fishing().GetBaitDurability(), "the lure that was dragged");
        Assert.IsFalse(manager.Contains(lures[1]), "that very item left the bag");
        Assert.IsTrue(manager.Contains(lures[0]) && manager.Contains(lures[2]), "the other lures stay");
        CollectionAssert.AreEqual(new[] { 30, 25 }, Durabilities(lure), "and keep their values");
        StringAssert.Contains("25 / 30", ui.TextFor(lures[2]));
        ui.RefreshNow();
        BaitBadge rodBadge = bag.GetItemInHand().GetComponent<BaitBadge>();
        Assert.AreEqual(lure.item.itemSprite, rodBadge.GetBaitSprite(), "the rod shows the lure in its corner");
        Assert.AreEqual(12f / 30f, rodBadge.GetBarShare(), 0.001f, "and its durability");
        Assert.AreEqual(lure.item.itemSprite, bag.GetPlaceholder().GetComponent<BaitBadge>().GetBaitSprite(), "so does its faded copy");
        StringAssert.Contains("Lure   <color=#FFD45E>12 / 30", ui.TextFor(bag.GetItemInHand()));

        //drop the new lure on the hand slot: it swaps with the lure in the rod, which takes the dragged one's slot
        Transform firstHome = lures[0].transform.parent;
        DropOn(BeginDrag(lures[0]), bag.GetHandSlot().gameObject);
        yield return null;
        Assert.AreEqual(30, Fishing().GetBaitDurability(), "dropped on the hand slot: loaded");
        CollectionAssert.AreEqual(new[] { 25, 12 }, Durabilities(lure), "the lure from the rod came back with its 12");
        List<GameObject> now = manager.GetItems("Lure");
        Assert.AreEqual(firstHome, now[1].transform.parent, "into the slot the dragged lure came from");
        StringAssert.Contains("12 / 30", ui.TextFor(now[1]));
        ui.RefreshNow();
        Assert.IsFalse(bag.GetItemInHand().GetComponent<BaitBadge>().IsBarShown(), "a full lure in the rod shows no bar");

        //the rod back in the bar (the watering can in the hand): drop the worms on the rod item itself
        Hold("WateringCan");
        yield return null;
        GameObject rod = manager.GetItem("Fishing Rod");
        Assert.IsTrue(rod.GetComponent<UnityEngine.UI.Image>().raycastTarget, "the rod in the bar is what the mouse is over");
        DropOn(BeginDrag(manager.GetItem("Worms")), rod);
        yield return null;
        Assert.AreEqual(worm, Fishing().GetBait(), "the worm stack loaded");
        Assert.AreEqual(6, Fishing().GetBaitCount());
        Assert.AreEqual(0, manager.GetAmount("Worms"), "and left the bag");
        Assert.AreEqual(3, manager.GetItems("Lure").Count, "the lure came back");
        ui.RefreshNow();
        Assert.AreEqual(worm.item.itemSprite, rod.GetComponent<BaitBadge>().GetBaitSprite());
        Assert.AreEqual("6", rod.GetComponent<BaitBadge>().GetCountText(), "with the number of worms");

        //any other item dropped on the rod swaps slots as before
        GameObject hoe = manager.GetItem("Hoe");
        Transform hoeHome = hoe.transform.parent, rodHome = rod.transform.parent;
        DropOn(BeginDrag(hoe), rod);
        yield return null;
        Assert.AreEqual(rodHome, hoe.transform.parent, "the hoe took the rod's slot");
        Assert.AreEqual(hoeHome, rod.transform.parent, "and the rod the hoe's");
        Assert.AreEqual(worm, Fishing().GetBait(), "nothing was loaded");

        //no bait change while the line is out: the bait snaps back
        Hold("FishingRod");
        bag.SetBagOpen(false);//the open bag keeps the mouse from fishing
        yield return WaitFor(FishingControl.FishingState.Aiming, 3f);
        Assert.IsTrue(Fishing().TryCastAt(OpenWater));
        bag.SetBagOpen(true);
        yield return null;
        GameObject someLure = manager.GetItems("Lure")[0];
        Transform home = someLure.transform.parent;
        DropOn(BeginDrag(someLure), bag.GetHandSlot().gameObject);
        yield return null;
        Assert.AreEqual(worm, Fishing().GetBait(), "still the worms");
        Assert.AreEqual(home, someLure.transform.parent, "the lure went back to its slot");
        bag.SetBagOpen(false);
        Fishing().ReelIn();
        yield return WaitFor(FishingControl.FishingState.Aiming, 3f);
    }

    [UnityTest]
    public IEnumerator Shop_FishingTab_SellsBaitAndBuysFish()
    {
        StartSession(300, 0);
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
        Assert.AreEqual(300 - 5 * worm.price, Fund);

        //lures are bought by the handful like the worms
        StoreItemRow lureRow = (StoreItemRow)Row("Row - Lure");
        lureRow.SetQuantity(2);
        Assert.AreEqual(2, lureRow.GetQuantity(), "the lure row takes a quantity");
        ButtonNamed("Buy Lure").onClick.Invoke();
        Assert.AreEqual(2, Items().GetBait(lure), "two lures");
        CollectionAssert.AreEqual(new[] { 30, 30 }, Durabilities(lure), "both new");
        int afterLures = 300 - 5 * worm.price - 2 * lure.price;
        Assert.AreEqual(afterLures, Fund);
        Assert.IsTrue(Items().LoadBait(lure, 0));
        lureRow.Refresh();
        StringAssert.Contains("You have 2", RowText("Row - Lure", "Owned"), "the bag and the rod count");
        lureRow.SetQuantity(1);
        ButtonNamed("Buy Lure").onClick.Invoke();
        Assert.AreEqual(afterLures - lure.price, Fund, "another lure can be bought");
        Assert.AreEqual(2, Items().GetBait(lure));
        int afterLure = afterLures - lure.price;

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
        Assert.IsNull(Fishing().GetBait(), "the rod comes without bait");
    }

    [UnityTest]
    public IEnumerator OldLurePickup_NextToTheRod_IsTakenOnce()
    {
        StartSession(100, 0);
        yield return Load(Farm);
        BaitPickup pickup = Object.FindAnyObjectByType<BaitPickup>();
        Assert.IsNotNull(pickup, "the old lure lies by the pond");
        BaitData old = Bait("Old Lure");
        Assert.AreEqual(old, pickup.GetBait());
        ToolPickup rod = ToolPickup.Find(ToolManager.ToolType.FishingRod);
        Vector3 apart = pickup.transform.position - rod.transform.position;
        apart.y = 0f;
        Assert.Less(apart.magnitude, 1.5f, "right next to the rod");
        WorldInteractor player = Object.FindAnyObjectByType<WorldInteractor>();
        yield return MoveTo(new Vector3(pickup.transform.position.x, 1f, pickup.transform.position.z - 0.7f));
        Assert.LessOrEqual(pickup.DistanceOutsideReach(player.transform.position), 0f, "the player can reach it from the grass");
        Assert.AreEqual("Pick up the old lure", pickup.GetPrompt(player));
        pickup.Interact(player);
        yield return null;
        Assert.AreEqual(1, Items().GetBait(old), "into the bag");
        Assert.IsFalse(Items().HasLoadedBait(), "not into a rod");
        Assert.AreEqual(1, TheBag().GetInventoryManager().GetAmount("Old Lure"), "it shows in the bag");
        CollectionAssert.AreEqual(new[] { 30 }, Durabilities(old));
        StringAssert.Contains("Drag it onto the fishing rod", HudMessage());
        pickup.Interact(player);
        Assert.AreEqual(1, Items().GetBait(old), "only once");

        yield return Load(Farm);
        Assert.IsNull(Object.FindAnyObjectByType<BaitPickup>(), "taken for good: not lying there after a scene load");
        Assert.AreEqual(1, Items().GetBait(old));
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
