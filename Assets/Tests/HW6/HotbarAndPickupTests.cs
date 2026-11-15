using System.Collections;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

// Hotbar (2026-10-13): Minecraft-style tools. The hoe and the watering can lie in the level until the player picks them
// up with E; the number keys (and the mouse wheel) put an item of the hotbar in the hand, and only the item in the hand
// works. The watering can holds 8 waterings and is filled at the well or the pond.
public class HotbarAndPickupTests : HW6TestBase
{
    private static IEnumerator Press(Key key)
    {
        SendInputToGameView();
        yield return Task2MinimapAndEnergyTests.PressKey(Keyboard.current ?? InputSystem.AddDevice<Keyboard>(), key);
    }

    // "Hoe", "SeedBag(Sunflower)", "None" (empty)
    private static string Slot(int index)
    {
        object slot = Call(Hotbar(), "GetSlot", index);
        string tool = Get(slot, "tool").ToString();
        var crop = Get(slot, "crop") as ScriptableObject;
        return crop != null ? tool + "(" + Get(crop, "cropName") + ")" : tool;
    }

    private static string Prompt()
    {
        return (string)Call(FindOne("InteractionPrompt"), "GetText");
    }

    private static bool PromptShowsKey()
    {
        return HudCanvas().Find("InteractionPrompt/Panel/Key").gameObject.activeSelf;
    }

    // the guide's tip, or null when it is hidden
    private static string GuideText()
    {
        Transform panel = HudCanvas().Find("ToolGuide/Panel");
        return panel.gameObject.activeSelf ? panel.Find("Label").GetComponent<TMP_Text>().text : null;
    }

    private static bool WaterMeterShown()
    {
        return (bool)Call(FindOne("WaterMeter"), "IsShown");
    }

    private static bool HasItem(string tool)
    {
        return (bool)Call(Hotbar(), "HasItem", ToolType(tool));
    }

    private static float Flat(Vector3 v)
    {
        v.y = 0f;
        return v.magnitude;
    }

    private static Rect ScreenRect(Transform t)
    {
        var corners = new Vector3[4];
        ((RectTransform)t).GetWorldCorners(corners); // overlay canvas: world corners are screen pixels
        return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
    }

    // what the player sees of a HUD element: for a text, the drawn letters (a right-aligned text's box is wider than its lines), else its rect
    private static Rect VisibleScreenRect(Transform t)
    {
        var text = t.GetComponent<TMP_Text>();
        if (text == null)
            return ScreenRect(t);
        text.ForceMeshUpdate();
        if (text.textInfo.characterCount == 0)
            return Rect.zero;
        Vector3 min = t.TransformPoint(text.textBounds.min), max = t.TransformPoint(text.textBounds.max);
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }

    [UnityTest]
    public IEnumerator NewGame_StartsWithoutTools_AndTheToolsOnTheGround()
    {
        yield return Load(Farm, withTools: false);
        FreezeCalendar();
        foreach (string tool in new[] { "Hoe", "WateringCan", "Sickle" })
            Assert.IsFalse(HasItem(tool), "no " + tool + " in the hotbar yet");
        Assert.AreEqual("SeedBag", Tool(), "the starting seeds in slot 1 are in the hand");

        Vector3 start = GameObject.Find("Player").transform.position;
        Component hoe = PickupOf("Hoe"), can = PickupOf("WateringCan");
        Assert.IsNotNull(hoe, "the hoe lies on the farm");
        Assert.IsNotNull(can, "the watering can lies on the farm");
        Assert.IsNotNull(PickupOf("Sickle"), "the sickle lies on the farm too");
        Assert.Less(hoe.transform.position.x, start.x - 2f, "the hoe is to the left of the start");
        float barrel = float.MaxValue;
        foreach (Transform t in GameObject.Find("FarmScenery").transform.Find("Props"))
            if (t.name == "Barrel")
                barrel = Mathf.Min(barrel, Flat(t.position - hoe.transform.position));
        Assert.Less(barrel, 0.8f, "leaning on the barrel");
        Vector3 well = GameObject.Find("FarmScenery").transform.Find("Props/Well").position;
        Assert.Less(Flat(can.transform.position - well), 1.5f, "the can stands next to the well");
        StringAssert.Contains("Pick up the hoe", GuideText(), "the guide tells the player what to do first");
    }

    [UnityTest]
    public IEnumerator WithoutTheHoe_SpaceDoesNothing_AndTheHudSaysWhereTheHoeIs()
    {
        yield return Load(Farm, withTools: false);
        FreezeCalendar();
        Component tile = Tiles()[0];
        PointAt(tile);
        Assert.IsFalse((bool)Call(InteractPoint(), "InteractWithCurTile"));
        yield return null;
        var hud = (Component)Get(Hotbar(), "hudMessage");
        var rejectionSound = (AudioSource)Get(hud, "rejectionSound");
        Assert.IsTrue(rejectionSound.isPlaying, "using the wrong tool plays the rejection cue");
        Assert.AreEqual("Idle", State());
        Assert.AreEqual("Grass", Cond(tile));
        StringAssert.Contains("You need the hoe", HudMessage());
        StringAssert.Contains("barrel", HudMessage());
    }

    [UnityTest]
    public IEnumerator PickingUpTheHoe_WithE_PutsItInTheNextFreeSlot_AndInTheHand()
    {
        yield return Load(Farm, withTools: false);
        FreezeCalendar();
        Component hoe = PickupOf("Hoe");
        yield return StandNear(hoe.transform.position, new Vector3(0.5f, 0f, -0.6f));
        Assert.AreEqual("Pick up the hoe", Prompt(), "the popup prompt");
        Assert.IsTrue(PromptShowsKey(), "with the E key");

        yield return Press(Key.E);
        Assert.AreEqual("Hoe", Slot(2), "the hoe goes into slot 3, the first free one after the starting seeds");
        Assert.AreEqual(2, (int)Call(Hotbar(), "GetSelectedSlot"), "and into the hand");
        Assert.AreEqual("Hoe", Tool(), "the farmer carries it");
        StringAssert.Contains("picked up the hoe", HudMessage());
        yield return new WaitForSeconds(0.4f);
        Assert.IsTrue(hoe == null, "the hoe is no longer on the ground");
        Assert.IsNull(Prompt());
        StringAssert.Contains("watering can", GuideText(), "the guide moves on to the watering can");

        Component tile = Tiles()[0];
        PointAt(tile);
        Assert.IsTrue((bool)Call(InteractPoint(), "InteractWithCurTile"), "now Space tills");
        yield return WaitForPlayerIdle();
        Assert.AreEqual("Tilled", Cond(tile));
    }

    [UnityTest]
    public IEnumerator WateringCan_IsFoundEmpty_FilledAtTheWell_AndEachWateringUsesOneFill()
    {
        yield return Load(Farm, withTools: false);
        FreezeCalendar();
        yield return PickUp("Hoe");
        yield return PickUp("WateringCan");
        Assert.AreEqual("WateringCan", Slot(3), "the can goes into slot 4, the next free one");
        Assert.AreEqual("WateringCan", Tool());
        Assert.AreEqual(0, CanWater(), "it is found empty");
        yield return new WaitForSeconds(0.3f);
        Assert.IsTrue(WaterMeterShown(), "the water meter shows while the can is in the hand");
        StringAssert.Contains("Empty", HudText("WaterMeter/WaterLabel"));
        StringAssert.Contains("Fill the watering can", GuideText());

        Component tile = Tiles()[0];
        Call(tile, "InteractWithFarmTile"); // tilled
        PointAt(tile);
        Assert.IsFalse((bool)Call(InteractPoint(), "InteractWithCurTile"), "an empty can does not water");
        StringAssert.Contains("empty", HudMessage());
        Assert.AreEqual("Tilled", Cond(tile));

        Assert.AreEqual("Fill the watering can at the well", Prompt(), "the can lies right beside the well");
        yield return Press(Key.E);
        Assert.AreEqual(CanCapacity(), CanWater(), "E at the well fills it");
        Assert.AreEqual("The watering can is full", Prompt());
        Assert.IsFalse(PromptShowsKey(), "a full can: the prompt is only a hint");
        StringAssert.Contains("sickle", GuideText(), "the can is full: the guide moves on to the sickle, which lies on the farm too");

        PointAt(tile);
        Assert.IsTrue((bool)Call(InteractPoint(), "InteractWithCurTile"));
        Assert.AreEqual(CanCapacity() - 1, CanWater(), "one fill per watering");
        yield return WaitForPlayerIdle();
        Assert.AreEqual("Watered", Cond(tile));
        StringAssert.Contains((CanCapacity() - 1) + " / " + CanCapacity(), HudText("WaterMeter/WaterLabel"));

        yield return Press(Key.Digit3);
        yield return new WaitForSeconds(0.3f);
        Assert.IsFalse(WaterMeterShown(), "no water meter with the hoe in the hand");
        yield return Press(Key.Digit4);
        yield return new WaitForSeconds(0.3f);
        Assert.IsTrue(WaterMeterShown());
    }

    [UnityTest]
    public IEnumerator TheFarmTakesTwoFullCans_AndThePondFillsTheCanToo()
    {
        yield return Load(Farm);
        FreezeCalendar();
        int capacity = CanCapacity();
        Assert.Less(capacity, 16, "one full can does not water the 16 tiles");
        Assert.GreaterOrEqual(2 * capacity, 16, "two full cans do");

        Hold("WateringCan");
        Component[] tiles = Tiles();
        Time.timeScale = 4f;
        for (int i = 0; i < capacity; i++)
        {
            Call(tiles[i], "InteractWithFarmTile"); // tilled
            PointAt(tiles[i]);
            Assert.IsTrue((bool)Call(InteractPoint(), "InteractWithCurTile"), "water " + tiles[i].name);
            yield return WaitForPlayerIdle();
            Set(Energy(), "curEnergy", 100f); // this test is about water, not energy
        }
        Time.timeScale = 1f;
        Assert.AreEqual(0, CanWater(), capacity + " waterings empty a full can");
        Call(tiles[capacity], "InteractWithFarmTile");
        PointAt(tiles[capacity]);
        Assert.IsFalse((bool)Call(InteractPoint(), "InteractWithCurTile"), "the can is empty");

        // where the path from the farm meets the pond
        yield return StandNear(new Vector3(7.2f, 0f, 7.0f), Vector3.zero);
        Assert.AreSame(FindInScene("Pond Water").GetComponent(GameType("WaterSource")), Call(FindOne("WorldInteractor"), "GetTarget"));
        Assert.AreEqual("Fill the watering can at the pond", Prompt());
        yield return Press(Key.E);
        Assert.AreEqual(capacity, CanWater(), "filled at the pond");
    }

    [UnityTest]
    public IEnumerator NumberKeysAndTheMouseWheel_SelectTheHotbarSlot()
    {
        yield return Load(Farm);
        FreezeCalendar();
        // a new game's seeds fill the first slots; the tools picked up after them take the next free ones
        Assert.AreEqual("SeedBag(Sunflower)", Slot(0));
        Assert.AreEqual("SeedBag(Pumpkin)", Slot(1));
        Assert.AreEqual("Hoe", Slot(2));
        Assert.AreEqual("WateringCan", Slot(3));
        Assert.AreEqual("Sickle", Slot(4));
        Assert.AreEqual("None", Slot(5));

        yield return Press(Key.Digit8);
        Assert.AreEqual(7, (int)Call(Hotbar(), "GetSelectedSlot"), "8 selects the last of the eight slots");

        foreach (var key in new[] { (Key.Digit4, "WateringCan"), (Key.Digit1, "SeedBag"), (Key.Digit5, "Sickle"), (Key.Digit6, "None"), (Key.Digit3, "Hoe") })
        {
            yield return Press(key.Item1);
            Assert.AreEqual(key.Item2, Tool(), key.Item1 + " puts it in the hand");
        }

        Mouse mouse = Mouse.current ?? InputSystem.AddDevice<Mouse>();
        InputSystem.QueueStateEvent(mouse, new MouseState { scroll = new Vector2(0f, -120f) });
        yield return null;
        InputSystem.QueueStateEvent(mouse, new MouseState());
        yield return null;
        yield return null;
        Assert.AreEqual(3, (int)Call(Hotbar(), "GetSelectedSlot"), "the wheel moves to the next slot");
    }

    [UnityTest]
    public IEnumerator SelectingTheCanAndPressingSpaceTogether_WatersOnceUntilSpaceIsPressedAgain()
    {
        yield return Load(Farm);
        FreezeCalendar();
        Component tile = Tiles()[0];
        Call(tile, "InteractWithFarmTile"); // tilled, so it needs the watering can
        PointAt(tile);
        Hold("Hoe");
        int water = CanWater();

        SendInputToGameView();
        Keyboard keyboard = Keyboard.current ?? InputSystem.AddDevice<Keyboard>();
        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        yield return null;
        yield return null;
        try
        {
            //One input event: the old callback used the hoe before Hotbar.Update selected the can.
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Digit4, Key.Space));
            yield return null;
            yield return null;
            Assert.AreEqual("WateringCan", HeldItem());
            Assert.AreEqual(water - 1, CanWater(), "Space uses the slot selected in this same frame");
            yield return WaitForPlayerIdle();
            Assert.AreEqual("Watered", Cond(tile));

            //Keep Space down beyond the action: it must not start another watering.
            yield return new WaitForSeconds(1f);
            Assert.AreEqual(water - 1, CanWater(), "holding Space uses water only once");

            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            yield return null;
            yield return Press(Key.Space);
            yield return WaitForPlayerIdle();
            Assert.AreEqual(water - 2, CanWater(), "releasing and pressing Space starts the next action");
        }
        finally
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        }
    }

    [UnityTest]
    public IEnumerator E_NextToAToolOnTheGround_PicksItUp_InsteadOfPlanting()
    {
        StartSession(50, 5);
        yield return Load(Farm, withTools: false);
        FreezeCalendar();
        SendInputToGameView();
        Component sickle = PickupOf("Sickle");
        yield return StandNear(sickle.transform.position, new Vector3(0.5f, 0f, -0.6f));
        Component tile = Tiles()[0];
        Water(tile);
        PointAt(tile);
        Hold("SeedBag");
        yield return Press(Key.E);
        yield return WaitForPlayerIdle();
        Assert.IsTrue(HasItem("Sickle"), "E picked up the sickle");
        Assert.AreEqual(0, PlantsParent().childCount, "and did not plant");

        Hold("SeedBag");
        yield return Press(Key.E); // nothing in reach any more: E plants
        yield return WaitForPlayerIdle();
        Assert.AreEqual(1, PlantsParent().childCount);
    }
}
