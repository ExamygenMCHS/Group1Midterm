using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

// HW5: the whole game through its buttons, and a scripted "ideal player" that checks the level balance
public class GameFlowAndBalanceTests : HW5TestBase
{
    [UnityTest]
    public IEnumerator ButtonFlow_Scene0_To_Scene3_WithNoManualSteps()
    {
        yield return Load(Scene0);
        ButtonNamed("Start").onClick.Invoke();
        yield return WaitForScene(Scene1);
        Assert.AreEqual("Fund: $ 0", TextOf("FundAmount"));

        foreach (Component tile in Tiles())
            Water(tile);
        yield return null;
        int reward = Fund;
        ButtonNamed("Next Level").onClick.Invoke();
        yield return WaitForScene(Scene2);

        ButtonNamed("Buy Seed").onClick.Invoke(); // 5 seeds (the quantity the next level needs)
        ButtonNamed("Buy Seed").onClick.Invoke();
        yield return null;
        Assert.AreEqual(10, Seeds);
        ButtonNamed("Start Next Level").onClick.Invoke();
        yield return WaitForScene(Scene3);

        Assert.AreEqual("Seeds: 10", TextOf("SeedAmount"));
        Assert.AreEqual("Fund: $ " + Fund, TextOf("FundAmount"));
        Assert.Less(Fund, reward);
        Assert.IsNotNull(GameObject.Find("PlantManager"));
    }

    [UnityTest]
    public IEnumerator SpaceKey_InTheStoreAndAfterSceneChanges_CausesNoErrors()
    {
        // the example never unsubscribed its Space handler; pressing Space in a later scene then hit a destroyed object
        yield return Load(Scene1);
        SendInputToGameView();
        Keyboard keyboard = Keyboard.current ?? InputSystem.AddDevice<Keyboard>();
        yield return Task2MinimapAndEnergyTests.PressKey(keyboard, Key.Space); // no tile selected yet: must be ignored

        StartSession(100, 0);
        yield return Load(Scene2);
        yield return Task2MinimapAndEnergyTests.PressKey(keyboard, Key.Space);

        yield return Load(Scene3);
        FreezeCalendar();
        Component tile = Tiles()[0];
        PointAt(tile);
        yield return Task2MinimapAndEnergyTests.PressKey(keyboard, Key.Space);
        yield return WaitForPlayerIdle();
        Assert.AreEqual("Tilled", Cond(tile), "one press = exactly one step, handlers did not pile up");
    }

    // in bed the body is locked; a fresh movement key gets the player up, and only then do they walk
    [UnityTest]
    public IEnumerator Bed_LocksMovement_UntilAMovementKeyGetsThePlayerUp()
    {
        yield return Load(Scene1);
        FreezeCalendar();
        SendInputToGameView();
        Keyboard keyboard = Keyboard.current ?? InputSystem.AddDevice<Keyboard>();
        Rigidbody rb = GameObject.Find("Player").GetComponent<Rigidbody>();
        Component energy = Energy();

        // walk into the bed holding D: that key must not get the player straight back out
        InputSystem.QueueStateEvent(keyboard, new UnityEngine.InputSystem.LowLevel.KeyboardState(Key.D));
        yield return null;
        Vector3 bed = GameObject.Find("SleepArea").transform.position;
        bed.y = rb.position.y;
        rb.position = bed;
        rb.transform.position = bed;
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
        Assert.IsTrue((bool)Call(energy, "IsResting"), "on the bed = resting");
        Vector3 inBed = rb.position;
        yield return new WaitForSeconds(0.5f);
        Assert.IsTrue((bool)Call(energy, "IsResting"), "still holding the key that walked in: stays in bed");
        Assert.Less(Vector3.Distance(inBed, rb.position), 0.02f, "the body does not move while in bed");

        InputSystem.QueueStateEvent(keyboard, new UnityEngine.InputSystem.LowLevel.KeyboardState());
        yield return new WaitForSeconds(0.2f);
        Assert.IsTrue((bool)Call(energy, "IsResting"));

        yield return Task2MinimapAndEnergyTests.PressKey(keyboard, Key.S);
        Assert.IsFalse((bool)Call(energy, "IsResting"), "a movement key gets the player out of bed");
        yield return HoldKeyUntil(keyboard, Key.S, () => rb.position.z < inBed.z - 1.5f);
        Assert.Less(rb.position.z, inBed.z - 1.5f, "and then they walk normally");
    }

    // the player walks around the scenery instead of through it
    [UnityTest]
    public IEnumerator Scenery_HasSolidColliders()
    {
        yield return Load(Scene1);
        foreach (string group in new[] { "Props", "Fences", "Trees", "Bushes" })
        {
            Transform g = GameObject.Find("FarmScenery").transform.Find(group);
            foreach (Transform t in g)
            {
                Collider c = t.GetComponent<Collider>();
                Assert.IsTrue(c != null && !c.isTrigger, t.name + " in " + group + " is solid");
            }
        }
        Collider house = GameObject.Find("Storage_House").GetComponent<Collider>();
        Assert.IsTrue(house != null && !house.isTrigger, "the storage house is solid");
    }

    // the real thing: W / D to walk, Space to till and water, exactly as a player does it
    [UnityTest]
    public IEnumerator RealKeys_WalkOntoTheFarm_AndWaterTwoTiles()
    {
        yield return Load(Scene1);
        FreezeCalendar();
        SendInputToGameView();
        Keyboard keyboard = Keyboard.current ?? InputSystem.AddDevice<Keyboard>();
        Transform player = GameObject.Find("Player").transform;
        Component[] tiles = Tiles();
        Time.timeScale = 3f;

        // the player works on the tile in front of them, in the direction they last walked
        yield return HoldKeyUntil(keyboard, Key.W, () => player.position.z >= 2.95f);
        Assert.AreSame(tiles[4].gameObject, Call(InteractPoint(), "GetCurTile"), "walking up onto the farm selects the tile ahead, Farm Tile 5");
        // Hotbar: 1 = the hoe tills, 2 = the watering can waters
        yield return Task2MinimapAndEnergyTests.PressKey(keyboard, Key.Digit1);
        yield return Task2MinimapAndEnergyTests.PressKey(keyboard, Key.Space);
        yield return Task2MinimapAndEnergyTests.WaitForActionUnlock();
        yield return Task2MinimapAndEnergyTests.PressKey(keyboard, Key.Digit2);
        yield return Task2MinimapAndEnergyTests.PressKey(keyboard, Key.Space);
        yield return Task2MinimapAndEnergyTests.WaitForActionUnlock();
        Assert.AreEqual("Watered", Cond(tiles[4]));

        yield return HoldKeyUntil(keyboard, Key.D, () => player.position.x >= 0.1f);
        Assert.AreSame(tiles[1].gameObject, Call(InteractPoint(), "GetCurTile"), "turning right selects the tile to the right, Farm Tile 2");
        yield return Task2MinimapAndEnergyTests.PressKey(keyboard, Key.Digit1);
        yield return Task2MinimapAndEnergyTests.PressKey(keyboard, Key.Space);
        yield return Task2MinimapAndEnergyTests.WaitForActionUnlock();
        yield return Task2MinimapAndEnergyTests.PressKey(keyboard, Key.Digit2);
        yield return Task2MinimapAndEnergyTests.PressKey(keyboard, Key.Space);
        yield return Task2MinimapAndEnergyTests.WaitForActionUnlock();
        Assert.AreEqual("Watered", Cond(tiles[1]));
        Assert.AreEqual(CanCapacity() - 2, CanWater(), "each watering used one fill of the can");

        float cost = (float)Call(Energy(), "GetTileInteractionCost");
        Assert.AreEqual((float)Call(Energy(), "GetMaxEnergy") - 4 * cost, CurEnergy(), 1.5f, "four presses were paid for");

        Assert.AreEqual("Grass", Cond(tiles[0]), "the tile the player stood on was never the target");

        // walking off the farm deselects the tile, so Space no longer reaches it
        yield return HoldKeyUntil(keyboard, Key.S, () => player.position.z <= 1f);
        Assert.IsNull(Call(InteractPoint(), "GetCurTile"));
    }

    private static IEnumerator HoldKeyUntil(Keyboard keyboard, Key key, System.Func<bool> done)
    {
        InputSystem.QueueStateEvent(keyboard, new UnityEngine.InputSystem.LowLevel.KeyboardState(key));
        float giveUp = Time.time + 20f;
        while (!done() && Time.time < giveUp)
            yield return null;
        InputSystem.QueueStateEvent(keyboard, new UnityEngine.InputSystem.LowLevel.KeyboardState());
        yield return null;
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
        Assert.IsTrue(done(), "the player did not get there while holding " + key);
    }

    // The player body is free to turn around Y (as in the example). If walking over the tile edges turned it,
    // W would stop meaning "up the screen" and the interact point would drift off the tile in front.
    [UnityTest]
    public IEnumerator RealKeys_ZigZagOverTheTileEdges_DoesNotTurnThePlayer()
    {
        yield return Load(Scene1);
        FreezeCalendar();
        SendInputToGameView();
        Keyboard keyboard = Keyboard.current ?? InputSystem.AddDevice<Keyboard>();
        Transform player = GameObject.Find("Player").transform;
        Time.timeScale = 4f;

        yield return HoldKeyUntil(keyboard, Key.W, () => player.position.z >= 3f);
        for (int lap = 0; lap < 3; lap++)
        {
            float row = lap;
            yield return HoldKeyUntil(keyboard, Key.D, () => player.position.x >= 3.6f);
            yield return HoldKeyUntil(keyboard, Key.W, () => player.position.z >= 4.1f + row);
            yield return HoldKeyUntil(keyboard, Key.A, () => player.position.x <= -0.3f);
            yield return HoldKeyUntil(keyboard, Key.S, () => player.position.z <= 3.4f + row);
        }
        Assert.AreEqual(0f, Mathf.DeltaAngle(0f, player.eulerAngles.y), 1f, "the player turned while walking over the farm");
    }

    // FarmBalance copies the EnergySettings asset onto the player, the clock and the tiles when the scene starts
    // (so the values typed on those components in the Inspector do not matter)
    [UnityTest]
    public IEnumerator Balance_FarmBalanceAppliesTheEnergySettingsAsset()
    {
        yield return Load(Scene1);
        object settings = Get(FindOne("FarmBalance"), "settings");
        float speed = (float)Get(settings, "playerSpeed");
        float dayLength = (float)Get(settings, "dayLengthInSeconds");
        int revertDays = (int)Get(settings, "daysUntilTileReverts");
        Assert.AreEqual(speed, (float)Get(FindOne("PlayerMove"), "forwardSpeed"), 0.0001f);
        Assert.AreEqual(dayLength, (float)Get(DayNight(), "cycleLengthInSeconds"), 0.0001f);
        Assert.AreEqual(revertDays, DaysUntilRevert());
    }

    [UnityTest]
    public IEnumerator MouseClicks_OnTheStoreButtons_GoThroughTheEventSystem()
    {
        StartSession(100, 0);
        yield return Load(Scene2);
        SendInputToGameView();
        Mouse mouse = Mouse.current ?? InputSystem.AddDevice<Mouse>();

        // clicking the locked button does nothing
        yield return Click(mouse, ButtonNamed("Start Next Level"));
        Assert.AreEqual(Scene2, SceneManager.GetActiveScene().name, "Start Next Level is locked");

        yield return Click(mouse, ButtonNamed("Buy Seed"));
        Assert.AreEqual(5, Seeds, "a real click on Buy Seed buys the chosen number of seeds");

        yield return Click(mouse, ButtonNamed("Start Next Level"));
        yield return WaitForScene(Scene3);
    }

    private static IEnumerator Click(Mouse mouse, UnityEngine.UI.Button button)
    {
        Vector3[] corners = new Vector3[4];
        button.GetComponent<RectTransform>().GetWorldCorners(corners); // overlay canvas: world corners are screen pixels
        Vector2 centre = (corners[0] + corners[2]) / 2f;
        InputSystem.QueueStateEvent(mouse, new UnityEngine.InputSystem.LowLevel.MouseState { position = centre });
        yield return null;
        yield return null;
        InputSystem.QueueStateEvent(mouse, new UnityEngine.InputSystem.LowLevel.MouseState { position = centre }.WithButton(UnityEngine.InputSystem.LowLevel.MouseButton.Left));
        yield return null;
        yield return null;
        InputSystem.QueueStateEvent(mouse, new UnityEngine.InputSystem.LowLevel.MouseState { position = centre });
        yield return null;
        yield return null;
        yield return null;
    }

    // A bot walks the shortest sensible route at the real player speed with real energy rules and real day ticks:
    // (Hotbar) it picks up the hoe at the barrel and the watering can at the well and fills it there, then tills and waters
    // tile after tile in a snake pattern, fills the can again at the pond when it runs dry (a full can waters 8 tiles)
    // and goes to the sleep area once when the energy runs out. It is the best case; a human needs roughly 1.4x as long.
    [UnityTest]
    [Timeout(180000)]
    public IEnumerator Balance_IdealRoute_WinsWithOneRestAndOneRefill_BeforeTheFirstTileDriesOut()
    {
        yield return Load(Scene1, withTools: false);
        Time.timeScale = 8f;

        GameObject player = GameObject.Find("Player");
        Rigidbody rb = player.GetComponent<Rigidbody>();
        Component playerMove = player.GetComponent(GameType("PlayerMove"));
        float speed = (float)Get(playerMove, "forwardSpeed");
        ((Behaviour)playerMove).enabled = false; // the bot moves the body itself, at the same speed

        Component energy = Energy();
        float cost = (float)Call(energy, "GetTileInteractionCost");
        float maxEnergy = (float)Call(energy, "GetMaxEnergy");
        Component interactPoint = InteractPoint();
        Vector3 sleepSpot = GameObject.Find("SleepArea").transform.position;
        float dayLength = (float)Get(DayNight(), "cycleLengthInSeconds");
        int daysUntilRevert = DaysUntilRevert();

        // the tools first: the hoe leaning on the barrel, then the can by the well, filled right there
        float start = Time.time;
        yield return Walk(rb, PickupOf("Hoe").transform.position + new Vector3(0.5f, 0f, -0.6f), speed);
        yield return UseWhatIsInReach("the hoe");
        foreach (Vector3 p in new[] { new Vector3(-2.4f, 0f, 6.8f), new Vector3(-5.5f, 0f, 7.4f), new Vector3(-8.7f, 0f, 7.3f) })
            yield return Walk(rb, p, speed);
        yield return UseWhatIsInReach("the watering can");
        yield return UseWhatIsInReach("the well");
        Assert.AreEqual(CanCapacity(), CanWater(), "filled at the well");
        yield return Walk(rb, new Vector3(-5.5f, 0f, 7.4f), speed);
        yield return Walk(rb, new Vector3(-2.4f, 0f, 7.0f), speed);
        float toolsReady = Time.time;

        Component[] tiles = Tiles();
        int[] route = { 0, 1, 2, 3, 7, 6, 5, 4, 8, 9, 10, 11, 15, 14, 13, 12 }; // snake through the 4 x 4 grid
        // through the gap in the east fence to the pond, where the path meets the water
        Vector3[] toPond = { new Vector3(4.6f, 0f, 5.0f), new Vector3(6.6f, 0f, 5.65f), new Vector3(7.2f, 0f, 7.0f) };
        float firstWatered = -1f;
        float restSeconds = 0f;
        int rests = 0, refills = 0;

        for (int step = 0; step < route.Length; step++)
        {
            Component tile = tiles[route[step]];
            if (CurEnergy() < 2 * cost)
            {
                rests++;
                // the fence north of the farm is solid: go round its west end, along the path to the bed, and back
                Vector3 fenceEnd = new Vector3(-2.3f, 0f, 7.6f);
                yield return Walk(rb, fenceEnd, speed);
                yield return Walk(rb, sleepSpot, speed);
                float needed = Mathf.Min(maxEnergy, (route.Length - step) * 2 * cost);
                float restStart = Time.time;
                while (CurEnergy() < needed)
                    yield return null;
                restSeconds += Time.time - restStart;
                yield return Walk(rb, fenceEnd, speed);
            }
            if (CanWater() == 0)
            {
                refills++;
                foreach (Vector3 p in toPond)
                    yield return Walk(rb, p, speed);
                Hold("WateringCan");
                yield return UseWhatIsInReach("the pond");
                Assert.AreEqual(CanCapacity(), CanWater(), "filled at the pond");
                yield return Walk(rb, toPond[1], speed);
                yield return Walk(rb, toPond[0], speed);
            }

            // the interact point is 0.5 in front of the player, so stop 0.5 before the tile's centre
            yield return Walk(rb, tile.transform.position + new Vector3(0, 0, -0.5f), speed);
            float waitUntil = Time.time + 2f;
            while ((GameObject)Call(interactPoint, "GetCurTile") != tile.gameObject && Time.time < waitUntil)
                yield return new WaitForFixedUpdate();
            Assert.AreSame(tile.gameObject, Call(interactPoint, "GetCurTile"), "the bot should be pointing at " + tile.name);

            // HW6: each press is an action of the player's state machine; Hotbar: 1 = hoe, 2 = watering can
            Hold("Hoe");
            Assert.IsTrue((bool)Call(interactPoint, "InteractWithCurTile"), "till " + tile.name);
            yield return WaitForPlayerIdle();
            Hold("WateringCan");
            Assert.IsTrue((bool)Call(interactPoint, "InteractWithCurTile"), "water " + tile.name);
            yield return WaitForPlayerIdle();
            if (firstWatered < 0f) firstWatered = Time.time;
        }
        yield return null;

        float total = Time.time - start;
        float wateringSpan = Time.time - firstWatered;
        float guaranteedLifetime = (daysUntilRevert - 1) * dayLength; // a watered tile lasts between (n-1) and n days
        Debug.Log("[HW5/HW6 balance] ideal route: total " + total.ToString("F1") + " s (tools ready after " + (toolsReady - start).ToString("F1")
            + " s), first-to-last watering " + wateringSpan.ToString("F1") + " s, rests " + rests + " (" + restSeconds.ToString("F1") + " s), refills "
            + refills + ", watered tiles last " + guaranteedLifetime.ToString("F0") + "-" + (daysUntilRevert * dayLength).ToString("F0") + " s");

        Assert.IsTrue((bool)Call(FindOne("FarmWinCondition"), "HasWon"), "the ideal route must win");
        Assert.AreEqual(1, rests, "the energy cap should force exactly one trip to the sleep area");
        Assert.AreEqual(1, refills, "two full watering cans water the 16 tiles: exactly one trip to refill");
        Assert.Less(wateringSpan, guaranteedLifetime, "the ideal route must finish before the first tile can dry out");
        Assert.Greater(wateringSpan * 2.5f, guaranteedLifetime, "too easy: a player 2.5x slower than the ideal route would still always win");
    }

    // E on whatever is in reach (a tool on the ground, the well, the pond)
    private static IEnumerator UseWhatIsInReach(string what)
    {
        yield return null;
        yield return null;
        Assert.IsTrue((bool)Call(FindOne("WorldInteractor"), "Interact"), "E on " + what);
        yield return null;
    }

    private static IEnumerator Walk(Rigidbody rb, Vector3 target, float speed)
    {
        target.y = rb.position.y;
        float giveUp = Time.time + Vector3.Distance(rb.position, target) / speed * 1.5f + 2f;
        while ((rb.position - target).sqrMagnitude > 0.0004f && Time.time < giveUp)
        {
            target.y = rb.position.y;
            rb.MovePosition(Vector3.MoveTowards(rb.position, target, speed * Time.deltaTime));
            yield return null;
        }
        Assert.Less((rb.position - target).sqrMagnitude, 0.01f, "the bot got stuck on the way to " + target);
    }
}
