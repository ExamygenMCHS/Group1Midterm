using System.Collections;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UI;

// Midterm Task 3 (was Scene5-InventoryMerge, now The Farm): the instructor's bag linked to the 3D farm. The tools picked
// up land in the inventory bar, the bag's bar is the hotbar (1-8, the mouse wheel), the item selected moves to the hand
// slot at the bottom left, planting takes a seed out of the bag and harvesting puts a plant in.
public class Scene5InventoryTests : MidtermTestBase
{
    private static Transform HandSlot()
    {
        return Bag().transform.Find("HoldingUI/ToolSlot");
    }

    [UnityTest]
    public IEnumerator Farm_PickedUpTools_LandInTheInventoryBar_AfterTheSeeds()
    {
        yield return Load(Farm, withTools: false);
        FreezeCalendar();
        Assert.IsFalse(FarmItems().hoe || FarmItems().wateringCan || FarmItems().sickle, "a new game starts without tools");
        Assert.IsNull(Object.FindAnyObjectByType<HotbarUI>(FindObjectsInactive.Include), "the bag's bar replaces the hotbar");
        Assert.IsTrue(Bag().IsShown());
        Assert.AreEqual(8, Bag().GetBarSlotCount(), "eight bar slots, keys 1-8");
        Assert.AreEqual(Bag().GetInventoryManager().transform.Find("InventoryBar"), BagSlot(0).transform.parent, "slot 1 of the bar fills first");
        Assert.AreEqual("Sunflower Seeds", BarItem(0), "the starting seeds come first");
        Assert.AreEqual("Pumpkin Seeds", BarItem(1));
        Assert.AreEqual("4", ItemInSlot(1).counterText.text, "a stack shows how many");

        yield return PickUp("Hoe");
        yield return PickUp("WateringCan");
        yield return PickUp("Sickle");
        string[] order = { "Sunflower Seeds", "Pumpkin Seeds", "Hoe", "Watering Can", "Sickle", "", "", "" };
        for (int i = 0; i < 8; i++)
            Assert.AreEqual(order[i], BarItem(i), "slot " + (i + 1) + ": each tool lands in the next free bar slot");
        Assert.AreEqual("Sickle", InHand(), "the tool just picked up is in the hand");
        Assert.AreEqual(HandSlot(), Bag().GetItemInHand().transform.parent, "its sprite moved to the hand slot");
        Assert.IsNull(ItemInSlot(4), "out of the bar slot, which keeps a faded copy");
        Assert.AreEqual("", BarItem(5));
    }

    // a real new game (through the intro) has no bag: the bar still works as the hotbar, B only says where to get the bag
    [UnityTest]
    public IEnumerator NewGame_WithoutTheBag_TheBarIsTheHotbar_AndBOnlySaysWhereToBuyTheBag()
    {
        yield return Load(Scene0);
        ButtonNamed("Start").onClick.Invoke();
        yield return WaitForScene(Farm);
        FreezeCalendar();
        Assert.IsFalse(FarmItems().bag, "a new game starts without the bag");
        Assert.IsFalse(Bag().IsShown());

        yield return PickUp("Hoe");
        Assert.AreEqual("Hoe", BarItem(2), "a picked-up tool lands in the bar after the starting seeds");
        Assert.AreEqual("Hoe", InHand());
        Component tile = Tiles()[0];
        PointAt(tile);
        Assert.IsTrue((bool)Call(InteractPoint(), "InteractWithCurTile"), "the hoe in the hand tills without the bag");
        yield return WaitForPlayerIdle();
        Assert.AreEqual("Tilled", Cond(tile));

        SendInputToGameView();
        yield return Task2MinimapAndEnergyTests.PressKey(Keyboard.current ?? InputSystem.AddDevice<Keyboard>(), Key.B);
        Assert.IsFalse(Bag().IsBagOpen(), "B does not open a bag the player has not bought");
        StringAssert.Contains("Buy the bag", HudMessage());
        Assert.IsFalse(Bag().transform.Find("BagShowHide").gameObject.activeSelf, "no bag button before the bag is bought");
    }

    [UnityTest]
    public IEnumerator Farm_NumberKeys_MoveTheSlotsItemToTheHand_AndSwapTheSprites()
    {
        yield return LoadFarmInSummer();
        SendInputToGameView();
        Keyboard keyboard = Keyboard.current ?? InputSystem.AddDevice<Keyboard>();

        yield return Task2MinimapAndEnergyTests.PressKey(keyboard, Key.Digit2);
        Assert.AreEqual(1, (int)Call(Hotbar(), "GetSelectedSlot"));
        Assert.AreEqual("Watering Can", InHand());
        Assert.AreEqual(HandSlot(), Bag().GetItemInHand().transform.parent);
        Assert.AreEqual("Hoe", ItemInSlot(0).name, "the hoe went back to slot 1");
        Assert.AreEqual("WateringCan", Tool(), "and the farmer holds the can");
        Assert.IsTrue(HandSlot().Find("Frame").GetComponent<Image>().enabled, "the hand slot is framed while it holds something");

        yield return Task2MinimapAndEnergyTests.PressKey(keyboard, Key.Digit5);
        Assert.AreEqual("Sickle", InHand());
        Assert.AreEqual("Watering Can", ItemInSlot(1).name);

        yield return Task2MinimapAndEnergyTests.PressKey(keyboard, Key.Digit8);
        Assert.AreEqual("", InHand(), "slot 8 is empty: empty hands");
        Assert.AreEqual("None", Tool());
        Assert.AreEqual("Sickle", ItemInSlot(4).name);
        Assert.IsFalse(HandSlot().Find("Frame").GetComponent<Image>().enabled);
        Vector3 frame = Bag().transform.Find("InventoryBar/SelectedFrame").position;
        Assert.AreEqual(((RectTransform)BagSlot(7).transform).TransformPoint(((RectTransform)BagSlot(7).transform).rect.center).x, frame.x, 1f, "the gold frame marks slot 8");
    }

    [UnityTest]
    public IEnumerator Farm_SpaceAndE_UseTheItemInTheHand_OrSayWhichKeyHoldsTheRightOne()
    {
        yield return LoadFarmInSummer();
        Component tile = Tiles()[0];
        PointAt(tile);
        Assert.IsTrue((bool)Call(InteractPoint(), "InteractWithCurTile"), "the hoe is in the hand: Space tills");
        yield return WaitForPlayerIdle();
        Assert.AreEqual("Tilled", Cond(tile));

        Assert.IsFalse((bool)Call(InteractPoint(), "InteractWithCurTile"), "the hoe does not water");
        StringAssert.Contains("watering can", HudMessage());
        StringAssert.Contains(">2<", HudMessage(), "the message names the key of its bar slot");
        Hold("WateringCan");
        Assert.IsTrue((bool)Call(InteractPoint(), "InteractWithCurTile"));
        yield return WaitForPlayerIdle();
        Assert.AreEqual("Watered", Cond(tile));

        Assert.IsFalse((bool)Call(PlantManagerScript(), "InteractWithCurTile"), "E with the can in the hand does not plant");
        StringAssert.Contains(">3<", HudMessage());
        Hold("SeedBag", Crop("Sunflower"));
        Assert.AreEqual("Sunflower Seeds", InHand());
        Assert.IsTrue((bool)Call(PlantManagerScript(), "InteractWithCurTile"));
        yield return WaitForPlayerIdle();
        Assert.AreEqual(3, InBag("Sunflower Seeds"), "planting takes a seed out of the bag");
        Assert.AreEqual("3", Bag().GetItemInHand().GetComponent<InventoryItem>().counterText.text, "the stack in the hand counts down");
    }

    [UnityTest]
    public IEnumerator Farm_SeedsOrCropsInTheHand_StillShowTheirCountInTheBar()
    {
        yield return LoadFarmInSummer();
        Hold("SeedBag", Crop("Sunflower"));
        yield return null;
        Assert.AreEqual("Sunflower Seeds", InHand());
        Assert.IsNull(ItemInSlot(2), "the stack itself is in the hand slot");
        TMP_Text barCount = BagSlot(2).GetComponentInChildren<TMP_Text>();
        Assert.IsNotNull(barCount, "the faded copy in slot 3 has a count");
        Assert.AreEqual("4", barCount.text);
        Assert.AreEqual("4", Bag().GetItemInHand().GetComponent<InventoryItem>().counterText.text, "and the hand slot too");

        Component tile = Tiles()[0];
        Water(tile);
        PointAt(tile);
        Assert.IsTrue((bool)Call(PlantManagerScript(), "InteractWithCurTile"));
        yield return WaitForPlayerIdle();
        Assert.AreEqual("3", BagSlot(2).GetComponentInChildren<TMP_Text>().text, "planting counts down in the bar as well");

        Hold("WateringCan");
        yield return null;
        Assert.AreEqual("3", ItemInSlot(2).counterText.text, "back in its slot with its own count");
        Assert.AreEqual(1, BagSlot(2).transform.childCount, "the faded copy and its count are gone");
        Assert.AreEqual("", BagSlot(1).GetComponentInChildren<TMP_Text>().text, "a tool's faded copy shows no count");
    }

    [UnityTest]
    public IEnumerator Farm_Harvesting_PutsAPlantInTheBag()
    {
        yield return LoadFarmInSummer();
        Component tile = Tiles()[0];
        yield return PlantOn(tile);
        Assert.AreEqual(3, SeedsOf("Sunflower"));
        PassDays(4);
        yield return EOn(tile); // the sickle into the hand, then E
        Assert.AreEqual("Sickle", InHand(), "the sickle is in the hand slot");
        Assert.AreEqual(1, HarvestedOf("Sunflower"));
        Assert.AreEqual(1, InBag("Sunflower"), "the harvested plant is in the bag");
        Assert.AreEqual("Sunflower", BarItem(5), "in the first free bar slot");
    }

    [UnityTest]
    public IEnumerator Farm_Q_SelectsTheNextSeeds()
    {
        yield return LoadFarmInSummer();
        Call(PlantManagerScript(), "SwitchSeed");
        yield return null;
        Assert.AreEqual("Sunflower Seeds", InHand(), "Q from the hoe: the first seeds");
        Call(PlantManagerScript(), "SwitchSeed");
        yield return null;
        Assert.AreEqual("Pumpkin Seeds", InHand(), "Q again: the next seeds");
        Assert.AreEqual("Sunflower Seeds", ItemInSlot(2).name, "the sunflower seeds went back to slot 3");
        StringAssert.Contains("Pumpkin seeds: 4", TextOf("SeedAmount"));
    }

    [UnityTest]
    public IEnumerator Farm_UsedUpSeeds_LeaveTheBag_AndEmptyTheHand()
    {
        StartBagSession(60);
        OwnTools();
        AddSeeds("Sunflower", 1);
        AddSeeds("Pumpkin", 1);
        yield return Load(Farm);
        FreezeCalendar();
        AdvanceDays(7); // summer: both crops can be planted
        Component[] tiles = Tiles();
        Water(tiles[0]);
        Water(tiles[1]);
        Hold("SeedBag", Crop("Sunflower"));
        PointAt(tiles[0]);
        Assert.IsTrue((bool)Call(PlantManagerScript(), "InteractWithCurTile"));
        yield return WaitForPlayerIdle();
        Assert.IsNull(Bag().GetInventoryManager().GetItem("Sunflower Seeds"), "the last seed planted: its sprite leaves the bag");
        Assert.AreEqual("", InHand(), "and the hand is empty");
        Assert.AreEqual("", BarItem(2));

        PointAt(tiles[1]);
        Assert.IsFalse((bool)Call(PlantManagerScript(), "InteractWithCurTile"));
        StringAssert.Contains(">4<", HudMessage(), "the pumpkin seeds are in slot 4");
        Hold("SeedBag");
        Assert.IsTrue((bool)Call(PlantManagerScript(), "InteractWithCurTile"));
        yield return WaitForPlayerIdle();
        Assert.AreEqual("Pumpkin", Get(Get(PlantOnTile(tiles[1]), "crop"), "cropName"));
    }

    [UnityTest]
    public IEnumerator Farm_ItemsDraggedToOtherSlots_TheKeysFollowTheBag()
    {
        yield return LoadFarmInSummer();
        Bag().SetBagOpen(true);
        AudioSource playerAudio = Object.FindFirstObjectByType<FarmPlayerAudio>().GetComponent<AudioSource>();
        playerAudio.Stop();
        PointerEventData drag = BeginBagDrag(4);
        EndBagDrag(drag, BagSlot(7));
        Assert.IsTrue(playerAudio.isPlaying, "moving an item plays the inventory cue");
        Assert.AreEqual(7, (int)Call(Hotbar(), "SlotOf", ToolType("Sickle"), null), "the sickle is on key 8 now");
        Assert.AreEqual("", BarItem(4));

        FarmItems().AddHarvested((PlantData)Crop("Sunflower"), 1);
        Assert.AreEqual("Sunflower", BarItem(4), "the completed drag releases its source for new items");

        // the watering can into the main bag: Space on soil says where it is
        drag = BeginBagDrag(1);
        EndBagDrag(drag, BagSlot(10));
        AssertBagSlotsHaveAtMostOneStack();
        Component tile = Tiles()[0];
        Call(tile, "InteractWithFarmTile"); // tilled
        PointAt(tile);
        Assert.IsFalse((bool)Call(InteractPoint(), "InteractWithCurTile"), "the hoe does not water");
        StringAssert.Contains("in your bag", HudMessage());
    }

    [UnityTest]
    public IEnumerator Farm_HarvestDuringDrag_CancelReturnsTheToolWithoutSharingASlot()
    {
        yield return LoadFarmInSummer();
        Component tile = Tiles()[0];
        yield return PlantOn(tile);
        PassDays(4);
        Hold("Sickle");
        Bag().SetBagOpen(true);
        PointerEventData drag = BeginBagDrag(0);
        Assert.AreEqual("Sickle", HeldItem(), "dragging the hoe leaves the sickle selected");

        // The harvesting action adds the first sunflower stack while the hoe is still under the pointer.
        PointAt(tile);
        Assert.IsTrue((bool)Call(PlantManagerScript(), "InteractWithCurTile"));
        yield return WaitForPlayerIdle();
        Assert.AreEqual(1, FarmItems().GetHarvested((PlantData)Crop("Sunflower")));
        Assert.AreEqual(1, InBag("Sunflower"));
        Assert.AreNotEqual(BagSlot(0).transform, Bag().GetInventoryManager().GetItem("Sunflower").transform.parent,
            "new harvest must not occupy the dragged tool's return slot");
        Assert.AreEqual("Sickle", HeldItem());

        EndBagDrag(drag); // released outside all slots: return to the source
        yield return null;
        AssertBagSlotsHaveAtMostOneStack();
        Assert.AreEqual("Hoe", BarItem(0));
        Assert.AreEqual("Sunflower", BarItem(5));
        Assert.AreEqual(1, InBag("Hoe"));
        Assert.AreEqual(1, InBag("Sunflower"));
        Assert.AreEqual("Sickle", InHand());
        Hold("Hoe");
        Assert.AreEqual("Hoe", InHand(), "the returned tool can still be selected");
        Assert.AreEqual("Hoe", Tool());
    }

    [UnityTest]
    public IEnumerator Farm_DragOntoOccupiedSlot_SwapsBothToolsAndTheirKeys()
    {
        yield return LoadFarmInSummer();
        Hold("Sickle");
        PointerEventData drag = BeginBagDrag(0);
        EndBagDrag(drag, BagSlot(1));
        yield return null;

        AssertBagSlotsHaveAtMostOneStack();
        Assert.AreEqual("Watering Can", BarItem(0));
        Assert.AreEqual("Hoe", BarItem(1));
        Assert.AreEqual("Watering Can", ItemInSlot(0).name);
        Assert.AreEqual("Hoe", ItemInSlot(1).name);
        Assert.AreEqual(1, (int)Call(Hotbar(), "SlotOf", ToolType("Hoe"), null));
        Assert.AreEqual(0, (int)Call(Hotbar(), "SlotOf", ToolType("WateringCan"), null));
        Assert.AreEqual(1, InBag("Hoe"));
        Assert.AreEqual(1, InBag("Watering Can"));
        Assert.AreEqual("Sickle", InHand(), "swapping other tools does not change the selected item");
    }

    [UnityTest]
    public IEnumerator Farm_DragOntoTheHeldItemsSlot_SwapsTheHandAndKeepsBothTools()
    {
        yield return LoadFarmInSummer();
        Assert.AreEqual("Hoe", InHand());
        PointerEventData drag = BeginBagDrag(1);
        EndBagDrag(drag, BagSlot(0));
        yield return null;

        AssertBagSlotsHaveAtMostOneStack();
        Assert.AreEqual(0, (int)Call(Hotbar(), "GetSelectedSlot"), "the same bar slot remains selected");
        Assert.AreEqual("Watering Can", BarItem(0));
        Assert.AreEqual("Hoe", BarItem(1));
        Assert.AreEqual("Watering Can", InHand());
        Assert.AreEqual("WateringCan", Tool());
        Assert.AreEqual("Hoe", ItemInSlot(1).name, "the previously held tool returns to the dragged tool's source");
        Assert.AreEqual(1, InBag("Hoe"));
        Assert.AreEqual(1, InBag("Watering Can"));
        Hold("Hoe");
        Assert.AreEqual("Hoe", InHand());
        Assert.AreEqual("Watering Can", ItemInSlot(0).name);
        AssertBagSlotsHaveAtMostOneStack();
    }

    [UnityTest]
    public IEnumerator Farm_SelectingDraggedTool_CancelRestoresTheHand()
    {
        yield return SelectDraggedToolAndReturnToSource(false);
    }

    [UnityTest]
    public IEnumerator Farm_SelectingDraggedTool_DropOnSourceRestoresTheHand()
    {
        yield return SelectDraggedToolAndReturnToSource(true);
    }

    private static IEnumerator SelectDraggedToolAndReturnToSource(bool dropOnSource)
    {
        yield return LoadFarmInSummer();
        Bag().SetBagOpen(true);
        Assert.AreEqual("Hoe", InHand());
        GameObject hoe = Bag().GetItemInHand();
        GameObject can = ItemInSlot(1).gameObject;
        int stackCount = Bag().GetComponentsInChildren<InventoryItem>(true).Length;

        PointerEventData drag = BeginBagDrag(1);
        Call(Hotbar(), "SelectSlot", 1); // the number key / wheel selects the tool while its sprite is being dragged
        Assert.AreEqual("WateringCan", HeldItem(), "the dragged tool is selected for gameplay");
        Assert.AreEqual("WateringCan", Tool());

        EndBagDrag(drag, dropOnSource ? BagSlot(1) : null);
        yield return null;

        Assert.AreEqual(1, (int)Call(Hotbar(), "GetSelectedSlot"));
        Assert.AreSame(can, Bag().GetItemInHand(), "returning to the unchanged slot must restore its selected item to the hand");
        Assert.AreEqual(HandSlot(), can.transform.parent);
        Assert.IsTrue(HandSlot().Find("Frame").GetComponent<Image>().enabled, "the restored hand item has its frame");
        Assert.AreEqual("Watering Can", InHand());
        Assert.AreEqual("WateringCan", HeldItem());
        Assert.AreEqual("WateringCan", Tool());
        Assert.AreEqual("Watering Can", BarItem(1));
        Assert.IsNull(ItemInSlot(1), "the bar keeps a placeholder, not another watering-can stack");
        Assert.AreEqual(1, BagSlot(1).transform.childCount);
        Assert.AreSame(hoe, ItemInSlot(0).gameObject, "the previous hand item returns to its own slot");
        Assert.AreEqual(1, InBag("Hoe"));
        Assert.AreEqual(1, InBag("Watering Can"));
        Assert.AreEqual(stackCount, Bag().GetComponentsInChildren<InventoryItem>(true).Length, "dragging neither duplicates nor loses a stack");
        AssertBagSlotsHaveAtMostOneStack();
    }

    private static PointerEventData BeginBagDrag(int index)
    {
        Assert.IsNotNull(EventSystem.current, "dragging needs the scene's EventSystem");
        InventoryItem item = ItemInSlot(index);
        Assert.IsNotNull(item, "the source slot must contain a draggable item");
        var drag = new PointerEventData(EventSystem.current)
        {
            pointerDrag = item.gameObject,
            button = PointerEventData.InputButton.Left,
            position = new Vector2(-100f, -100f)
        };
        Assert.IsTrue(ExecuteEvents.Execute(item.gameObject, drag, ExecuteEvents.beginDragHandler));
        Assert.IsTrue(ExecuteEvents.Execute(item.gameObject, drag, ExecuteEvents.dragHandler));
        return drag;
    }

    private static void EndBagDrag(PointerEventData drag, GameObject destination = null)
    {
        if (destination != null)
            Assert.IsTrue(ExecuteEvents.Execute(destination, drag, ExecuteEvents.dropHandler));
        Assert.IsTrue(ExecuteEvents.Execute(drag.pointerDrag, drag, ExecuteEvents.endDragHandler));
        Bag().CheckBarChanged(); // the mouse release normally triggers this in LateUpdate
    }

    private static void AssertBagSlotsHaveAtMostOneStack()
    {
        foreach (GameObject slot in Bag().GetInventoryManager().inventorySlots)
            Assert.LessOrEqual(slot.GetComponentsInChildren<InventoryItem>(true).Length, 1,
                slot.name + " contains two item stacks");
    }

    [UnityTest]
    public IEnumerator Farm_TheBagOpensWithB_ItsButtonSitsNextToTheHand()
    {
        yield return LoadFarmInSummer();
        GameObject main = Bag().GetInventoryManager().inventoryMain;
        Assert.IsFalse(main.activeSelf);
        Bag().SetBagOpen(true);
        Assert.IsTrue(main.activeSelf);
        Assert.AreEqual(35, main.GetComponentsInChildren<InventorySlot>(true).Length, "five rows of seven");
        Assert.IsFalse(Bag().transform.Find("BagShowHide").gameObject.activeSelf, "the bag button hides while the bag is open");
        Bag().SetBagOpen(false);
        Assert.IsFalse(main.activeSelf);
        Assert.IsTrue(Bag().transform.Find("BagShowHide").gameObject.activeSelf);
    }

    [UnityTest]
    public IEnumerator Farm_Hud_TheBagSitsClearOfTheOtherHud_AtCommonAspectRatios()
    {
        yield return LoadFarmInSummer();
        var scaler = Bag().GetComponent<CanvasScaler>();
        var hudScaler = HudCanvas().GetComponent<CanvasScaler>();
        Assert.AreEqual(hudScaler.referenceResolution, scaler.referenceResolution, "the bag scales like the HUD");
        Assert.AreEqual(hudScaler.matchWidthOrHeight, scaler.matchWidthOrHeight);

        Transform bag = Bag().transform;
        string[] names = { "InventoryBar", "BagShowHide", "HoldingUI", "MinimapBorder", "Instruction", "ControlsHint" };
        RectTransform[] rts = { (RectTransform)bag.Find("InventoryBar"), (RectTransform)bag.Find("BagShowHide"), (RectTransform)bag.Find("HoldingUI"),
            (RectTransform)FindInScene("MinimapBorder").transform, (RectTransform)FindInScene("Instruction").transform, (RectTransform)FindInScene("ControlsHint").transform };
        Vector2[] screens = { new Vector2(1920, 1080), new Vector2(1280, 720), new Vector2(1920, 1200), new Vector2(1280, 800), new Vector2(1024, 768) };
        foreach (Vector2 screen in screens)
        {
            Vector2 reference = scaler.referenceResolution;
            float scale = Mathf.Pow(2f, Mathf.Lerp(Mathf.Log(screen.x / reference.x, 2f), Mathf.Log(screen.y / reference.y, 2f), scaler.matchWidthOrHeight));
            Vector2 canvasSize = screen / scale;
            var rects = new Rect[rts.Length];
            for (int i = 0; i < rts.Length; i++)
            {
                RectTransform rt = rts[i];
                Vector2 min = Vector2.Scale(rt.anchorMin, canvasSize) + rt.anchoredPosition - Vector2.Scale(rt.pivot, rt.sizeDelta);
                rects[i] = new Rect(min, rt.sizeDelta);
                var text = rt.GetComponent<TMP_Text>();
                if (text != null && text.alignment.ToString().Contains("Right"))
                {
                    // a right-aligned text: what is drawn, not its box
                    text.ForceMeshUpdate();
                    float width = text.textBounds.size.x;
                    rects[i] = Rect.MinMaxRect(rects[i].xMax - width, rects[i].yMin, rects[i].xMax, rects[i].yMax);
                }
                Assert.IsTrue(rects[i].xMin >= 0 && rects[i].yMin >= 0 && rects[i].xMax <= canvasSize.x && rects[i].yMax <= canvasSize.y,
                    names[i] + " is off screen at " + screen + ": " + rects[i]);
            }
            bool wide = screen.x / screen.y >= 1.7f; // like the hotbar's test: the controls hint is laid out for 16:9
            for (int a = 0; a < rects.Length; a++)
                for (int b = a + 1; b < rects.Length; b++)
                    if (wide || names[b] != "ControlsHint")
                        Assert.IsFalse(rects[a].Overlaps(rects[b]), names[a] + " overlaps " + names[b] + " at " + screen);
        }
    }
}
