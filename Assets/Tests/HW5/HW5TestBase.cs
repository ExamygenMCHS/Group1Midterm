using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Shared helpers for the Play Mode tests.
// The game scripts are in the FarmGame assembly (Assets/Game/Scripts/FarmGame.asmdef), which both test assemblies
// reference, so new tests can use the game types directly. The older tests reach them by name through reflection
// (GameType / Call / Get / Set below): when renaming a member, search the tests for its old name.
public abstract class HW5TestBase
{
    protected const string Scene0 = "Scene0-Intro";
    protected const string Scene1 = "Scene1-FarmingSim";
    protected const string Scene2 = "Scene2-Store";
    protected const string Scene3 = "Scene3-Plant";
    protected const string Scene4 = "Scene4-Harvest";
    protected const string Scene5 = "Scene5-InventoryMerge";
    protected const string Scene6 = "Scene6-FarmLevel5";

    private const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

#if UNITY_EDITOR
    private UnityEngine.InputSystem.InputSettings.EditorInputBehaviorInPlayMode editorInputBehavior;
#endif

    [SetUp]
    public void RememberSettings()
    {
#if UNITY_EDITOR
        editorInputBehavior = UnityEngine.InputSystem.InputSystem.settings.editorInputBehaviorInPlayMode;
#endif
        ForgetFarmFund(); // HW6: the Farm Fund survives scene loads, so every test starts without the last test's one
    }

    [TearDown]
    public void RestoreSettings()
    {
        Time.timeScale = 1f;
#if UNITY_EDITOR
        UnityEngine.InputSystem.InputSystem.settings.editorInputBehaviorInPlayMode = editorInputBehavior;
#endif
    }

    // lets simulated keyboard and mouse events reach the game even when the Game view does not have the focus
    protected static void SendInputToGameView()
    {
#if UNITY_EDITOR
        UnityEngine.InputSystem.InputSystem.settings.editorInputBehaviorInPlayMode =
            UnityEngine.InputSystem.InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
    }

    // ---------------------------------------------------------------- reflection

    protected static Type GameType(string name)
    {
        Type type = Type.GetType(name + ", FarmGame");
        Assert.IsNotNull(type, "Game type not found: " + name);
        return type;
    }

    protected static Component FindOne(string typeName)
    {
        var found = UnityEngine.Object.FindObjectsByType(GameType(typeName), FindObjectsInactive.Include);
        return found.Length > 0 ? (Component)found[0] : null;
    }

    protected static object Call(object target, string method, params object[] args)
    {
        MethodInfo info = target.GetType().GetMethod(method, Any);
        Assert.IsNotNull(info, target.GetType().Name + " has no method " + method);
        return info.Invoke(target, args);
    }

    protected static object CallStatic(string typeName, string method, params object[] args)
    {
        MethodInfo info = GameType(typeName).GetMethod(method, Any);
        Assert.IsNotNull(info, typeName + " has no method " + method);
        return info.Invoke(null, args);
    }

    protected static object Get(object target, string member)
    {
        Type type = target as Type ?? target.GetType();
        object instance = target is Type ? null : target;
        FieldInfo field = type.GetField(member, Any);
        if (field != null) return field.GetValue(instance);
        PropertyInfo property = type.GetProperty(member, Any);
        Assert.IsNotNull(property, type.Name + " has no member " + member);
        return property.GetValue(instance);
    }

    protected static void Set(object target, string member, object value)
    {
        FieldInfo field = target.GetType().GetField(member, Any);
        Assert.IsNotNull(field, target.GetType().Name + " has no field " + member);
        field.SetValue(target, value);
    }

    // ---------------------------------------------------------------- HW6: the Farm Fund object (fund and inventory)

    protected static Component FarmFund()
    {
        object instance = Get(GameType("FarmFund"), "Instance");
        Assert.IsNotNull(instance, "no Farm Fund object is alive");
        return (Component)instance;
    }
    protected static Component Inventory()
    {
        return FarmFund().GetComponent(GameType("FarmInventory"));
    }

    protected static int Fund => (int)Get(FarmFund(), "fund");
    protected static int Seeds => (int)Call(Inventory(), "GetTotalSeeds"); // all crops
    protected static int SeedsOf(string crop) => (int)Call(Inventory(), "GetSeeds", Crop(crop));
    protected static int HarvestedOf(string crop) => (int)Call(Inventory(), "GetHarvested", Crop(crop));

    // a Farm Fund that is already alive when the next scene loads (as if the player came through the earlier scenes);
    // the scene's own test copy then removes itself
    protected static void StartSession(int fund, int seeds, int levelsWon = 1)
    {
        ForgetFarmFund();
        var go = new GameObject("Farm Fund");
        Component farmFund = go.AddComponent(GameType("FarmFund")); // Awake: becomes the instance, DontDestroyOnLoad
        Set(farmFund, "fund", fund);
        Set(farmFund, "levelsWon", levelsWon);
        if (seeds > 0)
            Call(go.GetComponent(GameType("FarmInventory")), "AddSeeds", Crop("Sunflower"), seeds);
    }

    protected static void ForgetFarmFund()
    {
        foreach (var found in UnityEngine.Object.FindObjectsByType(GameType("FarmFund"), FindObjectsInactive.Include))
            UnityEngine.Object.DestroyImmediate(((Component)found).gameObject);
    }

    protected static ScriptableObject Crop(string name)
    {
#if UNITY_EDITOR
        var crop = UnityEditor.AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/Game/Data/Plants/" + name + ".asset");
        Assert.IsNotNull(crop, "missing crop asset " + name);
        return crop;
#else
        Assert.Ignore("crop assets are loaded through the AssetDatabase");
        return null;
#endif
    }

    // ---------------------------------------------------------------- scenes

    // Hotbar: in a farm level the player starts with every tool already picked up and a full watering can, as a player
    // who has done that (most tests are not about picking tools up); withTools: false leaves them lying in the level
    protected static IEnumerator Load(string sceneName, bool withTools = true)
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(sceneName);
        yield return null; // scene objects Awake/Start
        yield return null; // runtime-spawned tiles Start
        yield return new WaitForFixedUpdate();
        Assert.AreEqual(sceneName, SceneManager.GetActiveScene().name);
        if (withTools && FindOne("Hotbar") != null)
            GiveTools();
    }

    protected static IEnumerator WaitForScene(string sceneName)
    {
        for (int i = 0; i < 30 && SceneManager.GetActiveScene().name != sceneName; i++)
            yield return null;
        yield return null;
        yield return null;
        Assert.AreEqual(sceneName, SceneManager.GetActiveScene().name);
    }

    protected static GameObject FindInScene(string name)
    {
        foreach (Transform t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
            if (t.name == name) return t.gameObject;
        return null;
    }

    protected static string TextOf(string objectName)
    {
        GameObject go = FindInScene(objectName);
        Assert.IsNotNull(go, "No object named " + objectName);
        return go.GetComponent<TMP_Text>().text;
    }

    protected static Button ButtonNamed(string objectName)
    {
        GameObject go = FindInScene(objectName);
        Assert.IsNotNull(go, "No button named " + objectName);
        return go.GetComponent<Button>();
    }

    // ---------------------------------------------------------------- farm

    protected static Component[] Tiles()
    {
        Component[] tiles = GameObject.Find("Farm Tile Manager").GetComponentsInChildren(GameType("FarmTileControl"));
        Assert.AreEqual(16, tiles.Length, "the 4 x 4 farm should have 16 tiles");
        return tiles.OrderBy(t => int.Parse(t.name.Substring("Farm Tile ".Length))).ToArray();
    }

    protected static string Cond(Component tile)
    {
        return Get(tile, "tileCond").ToString(); // Grass, Tilled or Watered
    }

    // Grass -> Tilled -> Watered, straight through the tile's own API (no player, no energy)
    protected static void Water(Component tile)
    {
        while (Cond(tile) != "Watered")
            Call(tile, "InteractWithFarmTile");
    }

    protected static Component DayNight()
    {
        return GameObject.Find("DayNightController").GetComponent(GameType("DayNightControl"));
    }

    // fires the day passed event without moving the calendar's day counter (so the season stays the same):
    // the HW5 tests use it for tile drying and plant growth
    protected static void PassDays(int days)
    {
        var dayPassedEvent = (UnityEvent)Get(DayNight(), "dayPassedEvent");
        for (int i = 0; i < days; i++)
            dayPassedEvent.Invoke();
    }

    // HW6: what DayNightControl does when a day ends: count the day, then fire the event (the calendar follows)
    protected static void AdvanceDays(int days)
    {
        Component dayNight = DayNight();
        var dayPassedEvent = (UnityEvent)Get(dayNight, "dayPassedEvent");
        for (int i = 0; i < days; i++)
        {
            Set(dayNight, "DaysPassedInGame", (int)Get(dayNight, "DaysPassedInGame") + 1);
            dayPassedEvent.Invoke();
        }
    }

    protected static Component Calendar()
    {
        return GameObject.Find("DayNightController").GetComponent(GameType("GameCalendar"));
    }
    protected static string Season()
    {
        return Get(Calendar(), "season").ToString();
    }

    // ---------------------------------------------------------------- HW6: the player's state machine

    protected static Component PlayerState()
    {
        return FindOne("PlayerStateControl");
    }
    protected static string State()
    {
        return Get(PlayerState(), "playerState").ToString();
    }
    protected static string Tool()
    {
        return Get(FindOne("ToolManager"), "curTool").ToString();
    }

    // actions (till, water, plant, harvest) change the tile or plant at the tool's hit and then end; wait for that
    protected static IEnumerator WaitForPlayerIdle()
    {
        float giveUp = Time.time + 5f;
        while ((bool)Call(PlayerState(), "IsBusy") && Time.time < giveUp)
            yield return null;
        Assert.IsFalse((bool)Call(PlayerState(), "IsBusy"), "the action never ended");
        yield return null;
    }

    // the HUD canvas (the canopy model also has a child called "Canvas", so GameObject.Find is not safe)
    protected static Transform HudCanvas()
    {
        foreach (Canvas canvas in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include))
            if (canvas.name == "Canvas" && canvas.transform.parent == null)
                return canvas.transform;
        Assert.Fail("no HUD Canvas");
        return null;
    }

    // stops real day ticks from happening in the middle of a test
    protected static void FreezeCalendar()
    {
        Set(DayNight(), "cycleLengthInSeconds", 100000f);
    }

    protected static int DaysUntilRevert()
    {
        return (int)Get(Tiles()[0], "daysUntilRevert");
    }

    protected static Component InteractPoint()
    {
        return FindOne("PlayerInteractPoint");
    }

    protected static Component Energy()
    {
        return FindOne("PlayerEnergy");
    }

    protected static float CurEnergy()
    {
        return (float)Call(Energy(), "GetCurEnergy");
    }

    // makes the given tile the one the player is interacting with, as if the interact point had entered it
    protected static void PointAt(Component tile)
    {
        Set(InteractPoint(), "curTile", tile == null ? null : tile.gameObject);
    }

    protected static void DrainEnergy()
    {
        Set(Energy(), "curEnergy", 0f);
    }

    // ---------------------------------------------------------------- Hotbar: tools picked up from the ground, the can's water

    protected static Component Hotbar()
    {
        return FindOne("Hotbar");
    }
    protected static Component Tank()
    {
        return FindOne("WateringCanTank");
    }
    protected static object ToolType(string name)
    {
        return Enum.Parse(GameType("ToolManager+ToolType"), name);
    }
    protected static int CanWater()
    {
        return (int)Call(Tank(), "GetWater");
    }
    protected static int CanCapacity()
    {
        return (int)Call(Tank(), "GetCapacity");
    }
    protected static void FillCan()
    {
        Call(Tank(), "Refill");
    }

    // as if the player had picked up the hoe, the watering can and the sickle and filled the can
    protected static void GiveTools()
    {
        foreach (string tool in new[] { "Hoe", "WateringCan", "Sickle" })
            Call(Inventory(), "AddTool", ToolType(tool));
        FillCan();
    }

    // selects the hotbar slot with that item (crop: which seeds), like pressing its number key
    protected static void Hold(string tool, ScriptableObject crop = null)
    {
        Assert.IsTrue((bool)Call(Hotbar(), "SelectItem", ToolType(tool), crop), "the hotbar has no " + tool);
    }
    protected static string HeldItem()
    {
        return Call(Hotbar(), "GetSelectedTool").ToString();
    }

    // the tool Space needs on the tile in front of the player: the hoe on grass, the watering can (with water) on soil
    protected static void HoldToolForCurTile()
    {
        var tile = (GameObject)Call(InteractPoint(), "GetCurTile");
        if (tile == null)
            return;
        bool grass = Cond(tile.GetComponent(GameType("FarmTileControl"))) == "Grass";
        Hold(grass ? "Hoe" : "WateringCan");
        if (!grass && CanWater() <= 0)
            FillCan();
    }

    // Space on the tile in front of the player with the right tool in the hand; returns true if the action started
    protected static bool Space()
    {
        HoldToolForCurTile();
        return (bool)Call(InteractPoint(), "InteractWithCurTile");
    }

    // puts the player next to something on the ground and lets the reach find it
    protected static IEnumerator StandNear(Vector3 spot, Vector3 offset)
    {
        Rigidbody rb = GameObject.Find("Player").GetComponent<Rigidbody>();
        Vector3 position = spot + offset;
        position.y = rb.position.y;
        rb.position = position;
        rb.transform.position = position;
        rb.linearVelocity = Vector3.zero;
        yield return new WaitForFixedUpdate();
        yield return null;
        yield return null;
    }

    protected static Component PickupOf(string tool)
    {
        return (Component)CallStatic("ToolPickup", "Find", ToolType(tool));
    }

    // walks up to the tool lying in the level and picks it up with E (through the WorldInteractor, as the E key does)
    protected static IEnumerator PickUp(string tool)
    {
        Component pickup = PickupOf(tool);
        Assert.IsNotNull(pickup, "no " + tool + " lies in this level");
        yield return StandNear(pickup.transform.position, new Vector3(0.5f, 0f, -0.6f));
        Assert.AreSame(pickup, Call(FindOne("WorldInteractor"), "GetTarget"), "the " + tool + " is in reach");
        Assert.IsTrue((bool)Call(FindOne("WorldInteractor"), "Interact"), "E picks up the " + tool);
        yield return null;
    }
}
