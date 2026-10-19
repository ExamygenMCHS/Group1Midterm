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
    public IEnumerator NewGame_StartsWithAnEmptyHotbar_AndTheToolsOnTheGround()
    {
        yield return Load(Scene1, withTools: false);
        FreezeCalendar();
        for (int i = 0; i < 5; i++)
            Assert.AreEqual("None", Slot(i), "slot " + (i + 1) + " is empty");
        Assert.AreEqual("None", Tool(), "empty hands");

        Vector3 start = GameObject.Find("Player").transform.position;
        Component hoe = PickupOf("Hoe"), can = PickupOf("WateringCan");
        Assert.IsNotNull(hoe, "the hoe lies in the level");
        Assert.IsNotNull(can, "the watering can lies in the level");
        Assert.IsNull(PickupOf("Sickle"), "nothing to harvest in Scene1");
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
    public IEnumerator Hotbar_HasEightNumberedSlots_AtTheBottomMiddle()
    {
        yield return Load(Scene1, withTools: false);
        Transform bar = HudCanvas().Find("Hotbar");
        Assert.IsNotNull(bar, "the HUD has a hotbar");
        Assert.AreEqual(8, (int)GameType("Hotbar").GetField("SlotCount").GetValue(null), "room for more seeds before an inventory screen");
        for (int i = 1; i <= 8; i++)
            Assert.AreEqual(i.ToString(), bar.Find("Slot " + i + "/Number").GetComponent<TMP_Text>().text, "slot " + i + " shows its number");
        Assert.IsNull(bar.Find("Slot 9"));
        Rect rect = ScreenRect(bar);
        Assert.AreEqual(Screen.width / 2f, rect.center.x, 2f, "in the middle");
        Assert.Less(rect.yMin, Screen.height * 0.05f, "at the bottom");
        Assert.IsFalse(rect.Overlaps(ScreenRect(HudCanvas().Find("MinimapBorder"))), "clear of the mini-map");
        if ((float)Screen.width / Screen.height >= 1.7f) // the layout is made for 16:9
            foreach (string other in new[] { "ControlsHint", "Instruction", "ToolGuide/Panel" })
                Assert.IsFalse(rect.Overlaps(VisibleScreenRect(HudCanvas().Find(other))), "clear of " + other);
    }

    [UnityTest]
    public IEnumerator WithoutTheHoe_SpaceDoesNothing_AndTheHudSaysWhereTheHoeIs()
    {
        yield return Load(Scene1, withTools: false);
        FreezeCalendar();
        Component tile = Tiles()[0];
        PointAt(tile);
        Assert.IsFalse((bool)Call(InteractPoint(), "InteractWithCurTile"));
        yield return null;
        Assert.AreEqual("Idle", State());
        Assert.AreEqual("Grass", Cond(tile));
        StringAssert.Contains("You need the hoe", HudMessage());
        StringAssert.Contains("barrel", HudMessage());
    }

    [UnityTest]
    public IEnumerator PickingUpTheHoe_WithE_PutsItInSlot1_AndInTheHand()
    {
        yield return Load(Scene1, withTools: false);
        FreezeCalendar();
        Component hoe = PickupOf("Hoe");
        yield return StandNear(hoe.transform.position, new Vector3(0.5f, 0f, -0.6f));
        Assert.AreEqual("Pick up the hoe", Prompt(), "the popup prompt");
        Assert.IsTrue(PromptShowsKey(), "with the E key");

        yield return Press(Key.E);
        Assert.AreEqual("Hoe", Slot(0), "the hoe goes into slot 1");
        Assert.AreEqual(0, (int)Call(Hotbar(), "GetSelectedSlot"), "and into the hand");
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
        yield return Load(Scene1, withTools: false);
        FreezeCalendar();
        yield return PickUp("Hoe");
        yield return PickUp("WateringCan");
        Assert.AreEqual("WateringCan", Slot(1), "the can goes into slot 2");
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
        Assert.IsNull(GuideText(), "the guide has nothing left to say");

        PointAt(tile);
        Assert.IsTrue((bool)Call(InteractPoint(), "InteractWithCurTile"));
        Assert.AreEqual(CanCapacity() - 1, CanWater(), "one fill per watering");
        yield return WaitForPlayerIdle();
        Assert.AreEqual("Watered", Cond(tile));
        StringAssert.Contains((CanCapacity() - 1) + " / " + CanCapacity(), HudText("WaterMeter/WaterLabel"));

        yield return Press(Key.Digit1);
        yield return new WaitForSeconds(0.3f);
        Assert.IsFalse(WaterMeterShown(), "no water meter with the hoe in the hand");
        yield return Press(Key.Digit2);
        yield return new WaitForSeconds(0.3f);
        Assert.IsTrue(WaterMeterShown());
    }

    [UnityTest]
    public IEnumerator TheFarmTakesTwoFullCans_AndThePondFillsTheCanToo()
    {
        yield return Load(Scene1);
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
        yield return Load(Scene3);
        FreezeCalendar();
        Assert.AreEqual("Hoe", Slot(0));
        Assert.AreEqual("WateringCan", Slot(1));
        Assert.AreEqual("SeedBag(Sunflower)", Slot(2), "the level's seeds after the tools");
        Assert.AreEqual("Sickle", Slot(3), "the sickle in the first free slot after the seeds");
        Assert.AreEqual("None", Slot(4));
        StringAssert.Contains("8", HudText("Hotbar/Slot 3/Count"), "the seed slot shows how many seeds there are");

        yield return Press(Key.Digit8);
        Assert.AreEqual(7, (int)Call(Hotbar(), "GetSelectedSlot"), "8 selects the last of the eight slots");

        foreach (var key in new[] { (Key.Digit2, "WateringCan"), (Key.Digit3, "SeedBag"), (Key.Digit4, "Sickle"), (Key.Digit5, "None"), (Key.Digit1, "Hoe") })
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
        Assert.AreEqual(1, (int)Call(Hotbar(), "GetSelectedSlot"), "the wheel moves to the next slot");
    }

    [UnityTest]
    public IEnumerator Tools_StayWithThePlayer_FromLevelToLevel_AndTheSickleWaitsInTheHarvestLevel()
    {
        yield return Load(Scene1, withTools: false);
        FreezeCalendar();
        yield return PickUp("Hoe");
        yield return PickUp("WateringCan");
        Assert.IsTrue((bool)Call(FindOne("WorldInteractor"), "Interact"), "E fills the can at the well");
        Assert.AreEqual(CanCapacity(), CanWater());

        yield return Load(Scene3, withTools: false); // the Farm Fund from Scene1 (DontDestroyOnLoad) brings them along
        FreezeCalendar();
        Assert.IsNull(PickupOf("Hoe"), "the hoe does not lie in Scene3: the player has it");
        Assert.IsNull(PickupOf("WateringCan"));
        Assert.AreEqual("Hoe", Slot(0));
        Assert.AreEqual("WateringCan", Slot(1));
        Assert.AreEqual(CanCapacity(), CanWater(), "the water in the can came along too");
        Assert.IsNotNull(PickupOf("Sickle"), "the sickle waits in the harvest level");
        Assert.IsFalse(HasItem("Sickle"));
        StringAssert.Contains("sickle", GuideText());

        AddSeeds("Sunflower", 2);
        Component tile = Tiles()[0];
        yield return PlantOn(tile);
        PassDays(4);
        PointAt(tile);
        Hold("SeedBag");
        Assert.IsFalse((bool)Call(PlantManagerScript(), "InteractWithCurTile"), "no harvest without the sickle");
        StringAssert.Contains("sickle", HudMessage());
        StringAssert.Contains("fence", HudMessage(), "the HUD says where the sickle is");

        yield return PickUp("Sickle");
        Assert.AreEqual("Sickle", Slot(3), "the sickle goes into slot 4");
        Assert.AreEqual("Sickle", Tool(), "and into the hand");
        yield return EOn(tile);
        Assert.AreEqual(1, HarvestedOf("Sunflower"));
    }

    [UnityTest]
    public IEnumerator Scene4_HotbarHoldsBothSeeds_AndTheSickle_QPicksTheNextSeeds()
    {
        StartSession(0, 0, 2);
        AddSeeds("Pumpkin", 3);
        AddSeeds("Sunflower", 3);
        yield return Load(Scene4);
        FreezeCalendar();
        Assert.AreEqual("SeedBag(Sunflower)", Slot(2));
        Assert.AreEqual("SeedBag(Pumpkin)", Slot(3));
        Assert.AreEqual("Sickle", Slot(4));
        Call(PlantManagerScript(), "SwitchSeed"); // Q
        Assert.AreEqual(2, (int)Call(Hotbar(), "GetSelectedSlot"), "Q: the first seeds");
        Call(PlantManagerScript(), "SwitchSeed");
        Assert.AreEqual(3, (int)Call(Hotbar(), "GetSelectedSlot"), "Q again: the next seeds");
        yield return null;
        StringAssert.Contains("Pumpkin seeds: 3", TextOf("SeedAmount"));
    }

    [UnityTest]
    public IEnumerator E_NextToAToolOnTheGround_PicksItUp_InsteadOfPlanting()
    {
        StartSession(50, 5);
        yield return Load(Scene3, withTools: false);
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
