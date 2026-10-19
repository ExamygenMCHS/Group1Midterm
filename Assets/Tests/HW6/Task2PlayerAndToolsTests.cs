using System.Collections;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

// HW6 Task 2: the player's state machine, the tools (Tool Manager, Tool Spawn Position, tool prefabs) and the animations
public class Task2PlayerAndToolsTests : HW6TestBase
{
    private static Transform SpawnPosition()
    {
        Transform spawn = (Transform)Get(FindOne("ToolManager"), "toolSpawnPosition");
        Assert.IsNotNull(spawn, "the Tool Manager needs the Tool Spawn Position");
        return spawn;
    }

    private static Animator FarmerAnimator()
    {
        return GameObject.Find("Player").transform.Find("FarmerRig").GetComponent<Animator>();
    }

    // the tool object the Tool Manager shows right now; it must sit on the Tool Spawn Position
    private static GameObject ToolInHand()
    {
        var tool = (GameObject)Call(FindOne("ToolManager"), "GetCurToolObject");
        if (tool != null)
        {
            Assert.IsTrue(tool.activeInHierarchy, tool.name + " is shown");
            Assert.AreSame(SpawnPosition(), tool.transform.parent, tool.name + " is spawned at the Tool Spawn Position");
        }
        return tool;
    }

    // any tool still visible in the hand (a put-away tool shrinks for a moment before it is hidden)
    private static bool AnyToolShown()
    {
        foreach (Transform child in SpawnPosition())
            if (child.gameObject.activeSelf)
                return true;
        return false;
    }

    [UnityTest]
    public IEnumerator ToolSpawnPosition_IsAnEmptyObjectUnderTheFarmersHand()
    {
        yield return Load(Scene1);
        Transform spawn = SpawnPosition();
        Assert.AreEqual("Tool Spawn Position", spawn.name);
        Assert.AreEqual("HandRight", spawn.parent.name, "under the character's hand");
        Assert.IsTrue(spawn.IsChildOf(GameObject.Find("Player").transform));
        Assert.IsNull(spawn.GetComponent<Renderer>(), "an empty object");
        Assert.IsNull(spawn.GetComponent<MeshFilter>());
    }

#if UNITY_EDITOR
    [Test]
    public void ToolPrefabs_AreAnEmptyParent_WithTheModelPositionedAsAChild()
    {
        foreach (string name in new[] { "Hoe", "WateringCan", "SeedBag", "Sickle" })
        {
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Prefabs/Tools/" + name + ".prefab");
            Assert.IsNotNull(prefab, name + " prefab is missing");
            Assert.IsNull(prefab.GetComponent<MeshFilter>(), name + ": the root is an empty parent");
            Assert.IsNotNull(prefab.GetComponent(GameType("HeldTool")));
            Transform model = prefab.transform.Find(name + "Model");
            Assert.IsNotNull(model, name + " has its model as a child");
            Assert.IsNotNull(model.GetComponent<MeshFilter>().sharedMesh);
            bool placed = model.localPosition != Vector3.zero || model.localRotation != Quaternion.identity;
            Assert.IsTrue(placed, name + ": the model child carries the offset / rotation that puts it in the hand");
        }
    }

    [Test]
    public void FarmerAnimatorController_HasIdleWalkInteractAndRest()
    {
        var controller = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>("Assets/Game/Animation/Farmer.controller");
        Assert.IsNotNull(controller);
        var sm = controller.layers[0].stateMachine;
        var names = new System.Collections.Generic.List<string>();
        foreach (var s in sm.states) names.Add(s.state.name);
        CollectionAssert.IsSupersetOf(names, new[] { "Idle", "Walk", "Rest" });
        Assert.AreEqual("Idle", sm.defaultState.name);
        var interact = System.Array.Find(sm.stateMachines, m => m.stateMachine.name == "Interact").stateMachine;
        Assert.IsNotNull(interact, "the interaction animations are grouped in an Interact state machine");
        var actions = new System.Collections.Generic.List<string>();
        foreach (var s in interact.states) actions.Add(s.state.name);
        CollectionAssert.AreEquivalent(new[] { "Till", "Water", "Plant", "Harvest" }, actions);
        Assert.IsTrue(System.Array.Exists(controller.parameters, p => p.name == "State" && p.type == AnimatorControllerParameterType.Int));
    }
#endif

    [UnityTest]
    public IEnumerator Tilling_UsesTheHoe_AndTheSoilChangesWhenTheHoeHits()
    {
        yield return Load(Scene1);
        FreezeCalendar();
        Component tile = Tiles()[0];
        Call(Hotbar(), "SelectSlot", 4); // an empty hotbar slot
        yield return null;
        Assert.AreEqual("Idle", State());
        Assert.AreEqual("None", Tool());
        Assert.IsNull(ToolInHand(), "empty hands");

        PointAt(tile);
        Assert.IsFalse((bool)Call(InteractPoint(), "InteractWithCurTile"), "Hotbar: no tilling with empty hands");
        Assert.AreEqual("Idle", State());

        Hold("Hoe");
        yield return null;
        Assert.AreEqual("Hoe", Tool(), "the hoe selected in the hotbar is carried while idle");
        Assert.AreEqual("Hoe", ToolInHand().name, "the hoe is in the hand, at the Tool Spawn Position");
        Assert.IsTrue((bool)Call(InteractPoint(), "InteractWithCurTile"));
        Assert.AreEqual("Tilling", State(), "Space on grass: Idle -> Tilling");
        Assert.AreEqual("Hoe", Tool(), "the Tool Manager keeps the hoe for tilling");
        yield return null;
        Assert.AreEqual("Hoe", ToolInHand().name);
        Assert.AreEqual("Grass", Cond(tile), "the soil has not been hit yet");

        // read the player's state when the tile changes, not on a later frame: one long frame can hold both the hit and the end of the action
        string stateAtHit = null;
        var tileChanged = (UnityEvent)Get(tile, "tileCondChangedEvent");
        UnityAction recordState = () => stateAtHit = stateAtHit ?? State();
        tileChanged.AddListener(recordState);
        float giveUp = Time.time + 3f;
        while (Cond(tile) == "Grass" && Time.time < giveUp)
            yield return null;
        tileChanged.RemoveListener(recordState);
        Assert.AreEqual("Tilled", Cond(tile));
        Assert.AreEqual("Tilling", stateAtHit, "the hoe hits in the middle of the action");

        yield return WaitForPlayerIdle();
        Assert.AreEqual("Idle", State(), "Tilling -> Idle when the animation is over");
        Assert.AreEqual("Hoe", Tool(), "Hotbar: the hoe stays in the hand");

        Call(Hotbar(), "SelectSlot", 4);
        yield return new WaitForSeconds(0.2f);
        Assert.AreEqual("None", Tool());
        Assert.IsNull(ToolInHand());
        Assert.IsFalse(AnyToolShown(), "an empty hotbar slot puts the hoe away");
    }

    [UnityTest]
    public IEnumerator Watering_UsesTheWateringCan()
    {
        yield return Load(Scene1);
        FreezeCalendar();
        Component tile = Tiles()[1];
        Call(tile, "InteractWithFarmTile"); // tilled
        PointAt(tile);
        Hold("WateringCan");
        int water = CanWater();
        Assert.IsTrue((bool)Call(InteractPoint(), "InteractWithCurTile"));
        Assert.AreEqual("Watering", State(), "Space on dry soil: Idle -> Watering");
        Assert.AreEqual("WateringCan", Tool());
        Assert.AreEqual(water - 1, CanWater(), "one fill of the can per watering");
        yield return null;
        Assert.AreEqual("WateringCan", ToolInHand().name);
        yield return WaitForPlayerIdle();
        Assert.AreEqual("Watered", Cond(tile));
    }

    // a day can pass between Space and the moment the water lands and turn the dry soil back into grass
    [UnityTest]
    public IEnumerator Watering_SoilThatTurnsToGrassBeforeTheWaterLands_IsNotTilledByTheCan()
    {
        yield return Load(Scene1);
        FreezeCalendar();
        Component tile = Tiles()[2];
        Call(tile, "InteractWithFarmTile"); // tilled
        PointAt(tile);
        Hold("WateringCan");
        Assert.IsTrue((bool)Call(InteractPoint(), "InteractWithCurTile"));
        Assert.AreEqual("Watering", State());
        Set(tile, "tileCond", System.Enum.Parse(GameType("FarmTileControl+FarmTileCond"), "Grass")); // dried out before the hit
        yield return WaitForPlayerIdle();
        Assert.AreEqual("Grass", Cond(tile), "only the hoe tills grass");
    }

    // a scene that was open in the editor while these fields were added keeps them empty until it is reopened;
    // the can must still use its water then (it watered for free in Robbie's 2026-10-13 test run)
    [UnityTest]
    public IEnumerator InteractPoint_FindsTheHotbarAndTheCan_OnThePlayer_WhenItsReferencesAreEmpty()
    {
        yield return Load(Scene1);
        FreezeCalendar();
        Component point = InteractPoint();
        Set(point, "hotbar", null);
        Set(point, "wateringCanTank", null);
        Call(point, "Start");
        Assert.AreSame(Hotbar(), Get(point, "hotbar"));
        Assert.AreSame(Tank(), Get(point, "wateringCanTank"));

        Component tile = Tiles()[3];
        Call(tile, "InteractWithFarmTile"); // tilled
        PointAt(tile);
        Hold("WateringCan");
        int water = CanWater();
        Assert.IsTrue((bool)Call(InteractPoint(), "InteractWithCurTile"));
        Assert.AreEqual(water - 1, CanWater(), "the watering still costs one fill");
        yield return WaitForPlayerIdle();
    }

    [UnityTest]
    public IEnumerator Planting_UsesTheSeedBag_AndHarvesting_UsesTheSickle()
    {
        StartSession(50, 5);
        yield return Load(Scene3);
        FreezeCalendar();
        Component tile = Tiles()[2];
        Water(tile);
        PointAt(tile);
        Hold("SeedBag");
        Assert.IsTrue((bool)Call(PlantManagerScript(), "InteractWithCurTile"));
        Assert.AreEqual("Planting", State(), "E on a watered tile: Idle -> Planting");
        Assert.AreEqual("SeedBag", Tool());
        yield return WaitForPlayerIdle();
        Assert.AreEqual(1, PlantsParent().childCount);

        PassDays(4);
        Hold("Sickle");
        Assert.IsTrue((bool)Call(PlantManagerScript(), "InteractWithCurTile"));
        Assert.AreEqual("Harvesting", State(), "E on a mature plant: Idle -> Harvesting");
        Assert.AreEqual("Sickle", Tool());
        yield return null;
        Assert.AreEqual("Sickle", ToolInHand().name);
        yield return WaitForPlayerIdle();
        yield return new WaitForSeconds(0.3f);
        Assert.AreEqual(0, PlantsParent().childCount, "the harvested plant is removed");
        Assert.AreEqual(1, HarvestedOf("Sunflower"));
    }

    [UnityTest]
    public IEnumerator Animator_PlaysTheAnimationOfThePlayersState()
    {
        yield return Load(Scene1);
        FreezeCalendar();
        SendInputToGameView();
        Keyboard keyboard = Keyboard.current ?? InputSystem.AddDevice<Keyboard>();
        Animator animator = FarmerAnimator();
        Assert.AreEqual("Farmer", animator.runtimeAnimatorController.name);
        yield return null;
        Assert.IsTrue(animator.GetCurrentAnimatorStateInfo(0).IsName("Idle"), "idle animation");

        // walk
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.S));
        yield return new WaitForSeconds(0.4f);
        Assert.AreEqual("Walking", State());
        Assert.AreEqual(1, animator.GetInteger("State"));
        Assert.IsTrue(animator.GetCurrentAnimatorStateInfo(0).IsName("Walk") || animator.GetNextAnimatorStateInfo(0).IsName("Walk"), "walk animation");
        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        yield return new WaitForSeconds(0.5f);
        Assert.AreEqual("Idle", State());

        // interact (tilling)
        PointAt(Tiles()[3]);
        Assert.IsTrue((bool)Call(InteractPoint(), "InteractWithCurTile"));
        yield return null;
        yield return new WaitForSeconds(0.15f);
        Assert.AreEqual(2, animator.GetInteger("State"), "the Animator's State parameter follows the player's state");
        Assert.IsTrue(animator.GetCurrentAnimatorStateInfo(0).IsName("Interact.Till"), "the hoe animation of the Interact state machine plays");
        yield return WaitForPlayerIdle();
        yield return new WaitForSeconds(0.3f);
        Assert.IsTrue(animator.GetCurrentAnimatorStateInfo(0).IsName("Idle"), "back to idle");
    }

    [UnityTest]
    public IEnumerator Player_StandsStillWhileAnActionPlays()
    {
        yield return Load(Scene1);
        FreezeCalendar();
        SendInputToGameView();
        Keyboard keyboard = Keyboard.current ?? InputSystem.AddDevice<Keyboard>();
        Rigidbody rb = GameObject.Find("Player").GetComponent<Rigidbody>();
        PointAt(Tiles()[0]);
        Assert.IsTrue((bool)Call(InteractPoint(), "InteractWithCurTile"));
        Vector3 start = rb.position;
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.D));
        yield return new WaitForSeconds(0.3f);
        Assert.AreEqual("Tilling", State());
        Assert.Less(Vector3.Distance(start, rb.position), 0.02f, "a movement key does not move the player during the action");
        yield return WaitForPlayerIdle();
        yield return new WaitForSeconds(0.3f);
        Assert.Greater(Vector3.Distance(start, rb.position), 0.2f, "after the action the player walks again");
        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        yield return null;
    }

    [UnityTest]
    public IEnumerator Resting_IsAPlayerState_WithItsOwnAnimation()
    {
        yield return Load(Scene1);
        FreezeCalendar();
        Rigidbody rb = GameObject.Find("Player").GetComponent<Rigidbody>();
        Vector3 bed = GameObject.Find("SleepArea").transform.position;
        bed.y = rb.position.y;
        rb.position = bed;
        rb.transform.position = bed;
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
        // wait for the Rest animation rather than sampling at a fixed time: the cross-fade into it can still be running after a slow frame
        Animator animator = FarmerAnimator();
        float giveUp = Time.time + 3f;
        while (!animator.GetCurrentAnimatorStateInfo(0).IsName("Rest") && Time.time < giveUp)
            yield return null;
        Assert.AreEqual("Resting", State(), "the sleep area event: Idle -> Resting");
        Assert.AreEqual("None", Tool());
        Assert.IsTrue(animator.GetCurrentAnimatorStateInfo(0).IsName("Rest"));
        PointAt(Tiles()[0]);
        Assert.IsFalse((bool)Call(InteractPoint(), "InteractWithCurTile"), "no tile work from the bed");
    }

    [UnityTest]
    public IEnumerator StateLine_OnTheHud_ShowsTheStateMachinesAtWork()
    {
        yield return Load(Scene1);
        FreezeCalendar();
        PointAt(Tiles()[0]);
        Assert.IsTrue((bool)Call(InteractPoint(), "InteractWithCurTile"));
        yield return null;
        string line = HudText("StateMachineHud/Panel/StateText");
        StringAssert.Contains("Tilling", line);
        StringAssert.Contains("Hoe", line);
        StringAssert.Contains("Spring", line);
        yield return WaitForPlayerIdle();
        StringAssert.Contains("Idle", HudText("StateMachineHud/Panel/StateText"));
    }
}
