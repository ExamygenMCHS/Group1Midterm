using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UI;

// HW5 Task 2: mini-map and energy bar in Scene1-FarmingSim
public class Task2MinimapAndEnergyTests : HW5TestBase
{
    private static float Cost()
    {
        return (float)Call(Energy(), "GetTileInteractionCost");
    }

    // Space with the right tool in the hand (Hotbar: the hoe for grass, the watering can for soil)
    private static bool Interact()
    {
        return Space();
    }

    private static bool IsResting()
    {
        return (bool)Call(Energy(), "IsResting");
    }

    private static IEnumerator Teleport(Vector3 position)
    {
        Rigidbody rb = GameObject.Find("Player").GetComponent<Rigidbody>();
        position.y = rb.position.y;
        rb.position = position;
        rb.transform.position = position;
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
        yield return null;
    }

    // ---------------------------------------------------------------- energy

    [UnityTest]
    public IEnumerator Energy_StartsFull_AndEachTileInteractionCostsEnergy()
    {
        yield return Load(Scene1);
        FreezeCalendar();
        Component tile = Tiles()[0];
        float max = (float)Call(Energy(), "GetMaxEnergy");
        Assert.AreEqual(max, CurEnergy(), 0.001f);

        PointAt(tile);
        Assert.IsTrue(Interact());
        Assert.AreEqual(max - Cost(), CurEnergy(), 0.2f, "the energy is paid on the key press");
        yield return WaitForPlayerIdle(); // HW6: the tile changes when the hoe hits it
        Assert.AreEqual("Tilled", Cond(tile));

        Assert.IsTrue(Interact());
        yield return WaitForPlayerIdle();
        Assert.AreEqual("Watered", Cond(tile));
        Assert.AreEqual(max - 2 * Cost(), CurEnergy(), 0.2f);

        yield return null;
        Image fill = FindInScene("EnergyBar").transform.Find("Fill").GetComponent<Image>();
        Assert.AreEqual(CurEnergy() / max, fill.fillAmount, 0.01f, "the bar shows the energy that is left");
    }

    [UnityTest]
    public IEnumerator Energy_AtZero_TileInteractionDoesNotFunction()
    {
        yield return Load(Scene1);
        FreezeCalendar();
        Component tile = Tiles()[5];
        int blocked = 0;
        ((UnityEvent)Get(Energy(), "notEnoughEnergyEvent")).AddListener(() => blocked++);

        DrainEnergy();
        PointAt(tile);
        Assert.IsFalse(Interact(), "no energy, no interaction");
        Assert.AreEqual("Grass", Cond(tile), "the tile must not change state");
        Assert.AreEqual(1, blocked);

        yield return null;
        StringAssert.Contains("Out of energy", TextOf("MessageLabel"), "the player gets feedback");
    }

    [UnityTest]
    public IEnumerator Energy_RunsOut_AfterEnoughInteractions()
    {
        yield return Load(Scene1);
        FreezeCalendar();
        Component[] tiles = Tiles();
        int affordable = Mathf.FloorToInt((float)Call(Energy(), "GetMaxEnergy") / Cost());
        Assert.Less(affordable, 32, "one full bar must not be enough for all 32 interactions (16 tiles x till + water)");

        int done = 0;
        Time.timeScale = 4f;
        for (int i = 0; i < tiles.Length && done < affordable; i++)
        {
            PointAt(tiles[i]);
            for (int press = 0; press < 2 && done < affordable; press++)
            {
                Assert.IsTrue(Interact());
                done++;
                yield return WaitForPlayerIdle();
            }
        }
        Time.timeScale = 1f;
        PointAt(tiles[15]);
        Assert.IsFalse(Interact(), "the next interaction is refused");
        Assert.AreEqual("Grass", Cond(tiles[15]));
        yield return null;
    }

    [UnityTest]
    public IEnumerator SpaceKey_InteractsWithTheTile_UntilEnergyRunsOut()
    {
        yield return Load(Scene1);
        FreezeCalendar();
        SendInputToGameView();
        Keyboard keyboard = Keyboard.current ?? InputSystem.AddDevice<Keyboard>();
        Component[] tiles = Tiles();

        PointAt(tiles[0]);
        yield return PressKey(keyboard, Key.Space);
        Assert.AreEqual("Tilling", State(), "Space on grass starts tilling");
        yield return PressKey(keyboard, Key.Space); // still tilling: ignored
        yield return WaitForActionUnlock();
        Assert.AreEqual("Tilled", Cond(tiles[0]), "Space tills the grass tile, and the press during the hoe animation was ignored");
        yield return PressKey(keyboard, Key.Space); // Hotbar: the hoe does not water
        yield return WaitForActionUnlock();
        Assert.AreEqual("Tilled", Cond(tiles[0]), "the hoe in the hand cannot water");
        yield return PressKey(keyboard, Key.Digit2); // the watering can into the hand
        yield return PressKey(keyboard, Key.Space);
        yield return WaitForActionUnlock();
        Assert.AreEqual("Watered", Cond(tiles[0]), "Space waters the tilled tile");

        DrainEnergy();
        PointAt(tiles[1]);
        yield return PressKey(keyboard, Key.Digit1); // the hoe, so only the energy can stop it
        yield return PressKey(keyboard, Key.Space);
        Assert.AreEqual("Grass", Cond(tiles[1]), "out of energy: Space does nothing");
    }

    //the next key action is accepted once the farmer's tool animation has finished
    public static IEnumerator WaitForActionUnlock()
    {
        float giveUp = Time.time + 5f;
        while ((bool)Call(InteractPoint(), "IsActionLocked") && Time.time < giveUp)
            yield return null;
        Assert.IsFalse((bool)Call(InteractPoint(), "IsActionLocked"), "the action lock never ends");
    }

    [UnityTest]
    public IEnumerator InteractPoint_TurnsWithThePlayer()
    {
        yield return Load(Scene1);
        FreezeCalendar();
        SendInputToGameView();
        Keyboard keyboard = Keyboard.current ?? InputSystem.AddDevice<Keyboard>();
        Transform player = GameObject.Find("Player").transform;
        Transform point = InteractPoint().transform;
        Assert.Greater(point.position.z - player.position.z, 0.3f, "the player starts facing the farm (north)");
        foreach (var dir in new[] { (Key.D, Vector3.right), (Key.S, Vector3.back), (Key.A, Vector3.left), (Key.W, Vector3.forward) })
        {
            yield return PressKey(keyboard, dir.Item1);
            Vector3 offset = point.position - player.position;
            offset.y = 0f;
            Assert.Greater(Vector3.Dot(offset.normalized, dir.Item2), 0.99f, dir.Item1 + " turns the interact point that way");
        }
    }

    public static IEnumerator PressKey(Keyboard keyboard, Key key)
    {
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
        yield return null;
        yield return null;
        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        yield return null;
        yield return null;
    }

    [UnityTest]
    public IEnumerator SleepArea_RestoresEnergy_AndStopsWhenThePlayerLeaves()
    {
        yield return Load(Scene1);
        FreezeCalendar();
        GameObject sleepArea = GameObject.Find("SleepArea");
        Assert.IsNotNull(sleepArea);
        Assert.IsTrue(sleepArea.GetComponent<Collider>().isTrigger);
        Assert.IsFalse(IsResting());

        DrainEnergy();
        yield return Teleport(sleepArea.transform.position);
        Assert.IsTrue(IsResting(), "standing in the sleep area = resting");

        Time.timeScale = 5f;
        yield return new WaitForSeconds(2f);
        Time.timeScale = 1f;
        float restored = CurEnergy();
        Assert.Greater(restored, 5f, "energy comes back in the sleep area");

        yield return Teleport(new Vector3(0, 0, 0));
        Assert.IsFalse(IsResting(), "leaving the sleep area stops the rest");
    }

    // HW6: holding F speeds up the clock, and sleep follows the clock
    [UnityTest]
    public IEnumerator Sleeping_WhileTheClockIsFastForwarded_RestoresEnergyAsFastAsTheClock()
    {
        yield return Load(Scene1);
        FreezeCalendar();
        Component dayNight = DayNight();
        ((Behaviour)dayNight.GetComponent(GameType("TimeFastForward"))).enabled = false; // the test sets the clock speed (F is not held)
        yield return Teleport(GameObject.Find("SleepArea").transform.position);
        Assert.IsTrue(IsResting());

        DrainEnergy();
        yield return new WaitForSeconds(1f);
        float normal = CurEnergy();

        DrainEnergy();
        Set(dayNight, "timeSpeed", 6f);
        yield return new WaitForSeconds(1f);
        float fast = CurEnergy();
        Set(dayNight, "timeSpeed", 1f);
        Assert.Greater(normal, 2f, "energy comes back in bed");
        Assert.Greater(fast, normal * 4f, "with the clock at x6 the bed gives energy back about six times as fast");
    }

    [UnityTest]
    public IEnumerator StandingStill_RestoresEnergySlowly_MuchSlowerThanTheSleepArea()
    {
        yield return Load(Scene1);
        FreezeCalendar();
        DrainEnergy();
        Time.timeScale = 5f;
        yield return new WaitForSeconds(4f);
        Time.timeScale = 1f;

        float idle = CurEnergy();
        Assert.Greater(idle, 0.5f, "standing still gives a little energy back");
        Assert.Less(idle, 8f, "but slowly");
    }

    // ---------------------------------------------------------------- mini-map

    [UnityTest]
    public IEnumerator Minimap_TopDownCameraRendersIntoTheTextureShownOnTheCanvas()
    {
        yield return Load(Scene1);
        Camera minimapCamera = GameObject.Find("MinimapCamera").GetComponent<Camera>();
        Assert.IsTrue(minimapCamera.orthographic);
        Assert.IsNotNull(minimapCamera.targetTexture, "the mini-map camera renders into a render texture, not onto the screen");
        Assert.AreEqual(90f, minimapCamera.transform.eulerAngles.x, 0.01f, "looking straight down");
        Assert.IsNull(minimapCamera.GetComponent<AudioListener>());

        RawImage map = FindInScene("Minimap").GetComponent<RawImage>();
        Assert.AreSame(minimapCamera.targetTexture, map.texture);
        Assert.IsNotNull(map.GetComponentInParent<Canvas>());
        Assert.AreEqual("MinimapBorder", map.transform.parent.name, "the map has a border");
    }

    [UnityTest]
    public IEnumerator Minimap_PlayerMarkerIsASprite_SeenOnlyByTheMinimapCamera()
    {
        yield return Load(Scene1);
        int minimapLayer = LayerMask.NameToLayer("Minimap");
        Assert.GreaterOrEqual(minimapLayer, 0);

        SpriteRenderer marker = GameObject.Find("Player").transform.Find("MinimapMarker").GetComponent<SpriteRenderer>();
        Assert.IsNotNull(marker.sprite);
        Assert.AreEqual(minimapLayer, marker.gameObject.layer);

        Camera minimapCamera = GameObject.Find("MinimapCamera").GetComponent<Camera>();
        Assert.AreNotEqual(0, minimapCamera.cullingMask & (1 << minimapLayer), "mini-map camera draws the marker");
        Assert.AreEqual(0, Camera.main.cullingMask & (1 << minimapLayer), "main camera does not");
        Assert.AreEqual(0, minimapCamera.cullingMask & (1 << LayerMask.NameToLayer("UI")), "mini-map camera does not draw UI");
    }

    [UnityTest]
    public IEnumerator Minimap_CameraFollowsThePlayer()
    {
        yield return Load(Scene1);
        Transform minimapCamera = GameObject.Find("MinimapCamera").transform;
        float height = minimapCamera.position.y;

        yield return Teleport(new Vector3(2.5f, 0, 5f));
        yield return null;

        Vector3 player = GameObject.Find("Player").transform.position;
        Assert.AreEqual(player.x, minimapCamera.position.x, 0.05f);
        Assert.AreEqual(player.z, minimapCamera.position.z, 0.05f);
        Assert.AreEqual(height, minimapCamera.position.y, 0.001f, "the camera keeps its own height");
    }

    // ---------------------------------------------------------------- HUD layout

    // Works out where every HUD element lands for 16:9, 16:10 and 4:3 screens from its anchors,
    // so the result does not depend on the size the Game view happens to have while the tests run.
    [UnityTest]
    public IEnumerator Hud_ElementsAreOnScreen_AndDoNotOverlap_AtCommonAspectRatios([Values(Scene1, Scene3, Scene4, Scene5)] string sceneName)
    {
        StartSession(50, 5);
        yield return Load(sceneName);
        yield return null;
        CanvasScaler scaler = HudCanvas().GetComponent<CanvasScaler>();
        Assert.AreEqual(CanvasScaler.ScaleMode.ScaleWithScreenSize, scaler.uiScaleMode);
        CanvasScaler timeScaler = GameObject.Find("TimeDisplayCanvas").GetComponent<CanvasScaler>();
        Assert.AreEqual(scaler.referenceResolution, timeScaler.referenceResolution, "the time display canvas scales like the HUD");
        Assert.AreEqual(scaler.matchWidthOrHeight, timeScaler.matchWidthOrHeight);

        // HW6: the time display panel replaces the "Days" label; the state line sits under the energy bar
        var names = new System.Collections.Generic.List<string> { "EnergyBar", "HudMessage", "StateMachineHud", "TimePanel", "FundAmount", "MinimapBorder", "Instruction", "ControlsHint" };
        if (sceneName != Scene1) { names.Add("SeedAmount"); names.Add("GoalPanel"); }

        Vector2[] screens = { new Vector2(1920, 1080), new Vector2(1280, 720), new Vector2(1920, 1200), new Vector2(1280, 800), new Vector2(1024, 768) };
        foreach (Vector2 screen in screens)
        {
            // CanvasScaler "Scale With Screen Size", Match Width Or Height
            Vector2 reference = scaler.referenceResolution;
            float scale = Mathf.Pow(2f, Mathf.Lerp(Mathf.Log(screen.x / reference.x, 2f), Mathf.Log(screen.y / reference.y, 2f), scaler.matchWidthOrHeight));
            Vector2 canvasSize = screen / scale;

            Rect[] rects = new Rect[names.Count];
            for (int i = 0; i < names.Count; i++)
            {
                GameObject go = FindInScene(names[i]);
                Assert.IsNotNull(go, names[i] + " is missing from the HUD");
                RectTransform rt = go.GetComponent<RectTransform>();
                Assert.AreEqual(rt.anchorMin, rt.anchorMax, names[i] + " is expected to use a point anchor");
                Vector2 pivotPosition = Vector2.Scale(rt.anchorMin, canvasSize) + rt.anchoredPosition;
                Vector2 min = pivotPosition - Vector2.Scale(rt.pivot, rt.sizeDelta);
                rects[i] = new Rect(min, rt.sizeDelta);
                Assert.IsTrue(rects[i].xMin >= 0 && rects[i].yMin >= 0 && rects[i].xMax <= canvasSize.x && rects[i].yMax <= canvasSize.y,
                    names[i] + " is off screen at " + screen + ": " + rects[i]);
            }
            for (int a = 0; a < names.Count; a++)
                for (int b = a + 1; b < names.Count; b++)
                    Assert.IsFalse(rects[a].Overlaps(rects[b]), names[a] + " overlaps " + names[b] + " at " + screen);
        }
    }
}
