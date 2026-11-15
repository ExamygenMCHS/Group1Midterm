using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Sets up everything a delivery needs around the mountain road: collision damage on the pickup, the traffic cars and
// their route, the mission and its HUD in the Mountain Delivery scene, the Wholesale Market scene, and the truck
// parked on the farm that starts a delivery. Every menu item replaces only what it made, so hand edits are kept.
public static class DeliverySetup
{
    const string DeliveryScenePath = "Assets/Game/Scenes/Mountain Delivery.unity";
    const string MarketScenePath = "Assets/Game/Scenes/Wholesale Market.unity";
    const string FarmScenePath = "Assets/Game/Scenes/The Farm.unity";
    const string PickupPath = "Assets/Game/Art/Pickup Truck/Prefabs/Pickup.prefab";
    const string BodyMaterialPath = "Assets/Game/Art/Pickup Truck/Materials/pickupBody.mat";
    const string SmokeMaterialPath = "Assets/Game/Art/Pickup Truck/Effects/TireDust.mat";
    const string PrefabFolder = "Assets/Game/Prefabs/Delivery";
    const string OldTrafficCarPath = PrefabFolder + "/Traffic Car.prefab";
    const string TrafficPickupPath = PrefabFolder + "/Traffic Pickup.prefab";
    // The traffic models (hatchback, sedan, utility pickup, box truck) made by Tools > Mountain Delivery > Create Traffic Assets.
    const string TrafficModelsFolder = "Assets/Game/Prefabs/Traffic";
    // The player's own pickup in another colour also drives by, but less often than each traffic model.
    const float TrafficPickupWeight = .5f;
    const string DepotPath = PrefabFolder + "/Delivery Truck Stop.prefab";
    const string FarmFundPath = "Assets/Game/Prefabs/Farm Fund.prefab";
    const string MarketArtFolder = "Assets/Game/Art/Market";
    const string HudFontMaterialPath = "Assets/Game/Art/Fonts/LiberationSans SDF - HUD.mat";
    const string SaleSoundGuid = "807500ab08d5945b0a46570b6a205df2"; // the trader shop's sale sound

    const string TrafficName = "Traffic", MissionName = "Delivery Mission", HudName = "Delivery HUD";
    // Road segments left out at each end of the traffic route, so a car never pokes through the wall at the map border.
    const int TrimSegments = 5;
    // Where the truck is parked on the farm: the cleared patch of meadow between the hoe and the well, side-on to the camera.
    static readonly Vector3 DepotPosition = new Vector3(-7.7f, 0f, 3.4f);
    const float DepotYaw = 90f;

    static readonly Color Cream = new Color(1f, .97f, .88f), Gold = new Color(1f, .83f, .37f), Shade = new Color(.1f, .12f, .14f, .72f);

    [MenuItem("Tools/Mountain Delivery/Add Damage, Traffic and Mission (keeps hand edits)")]
    public static void AddToDeliveryScene()
    {
        RequireEditMode();
        var scene = EditorSceneManager.OpenScene(DeliveryScenePath, OpenSceneMode.Single);
        Apply(scene);
        EditorSceneManager.SaveScene(scene);
    }

    // Called by the level builder too, after the truck and the drop-off zone are in the scene.
    public static void Apply(Scene scene)
    {
        AddDamageToPickup();
        var pickupPrefab = MakeTrafficPickup();
        var car = FindInScene<BasicCarController>(scene);
        if (car == null) throw new InvalidOperationException("Missing the truck in " + scene.path);
        var damage = car.GetComponent<TruckDamage>();
        if (damage == null) throw new InvalidOperationException("The truck in the scene did not get TruckDamage from its prefab.");

        foreach (var root in scene.GetRootGameObjects())
            if (root.name == TrafficName || root.name == MissionName || root.name == HudName) Object.DestroyImmediate(root);

        AddTraffic(scene, pickupPrefab, car.transform);
        var mission = AddMission(scene, damage);
        AddHud(scene, mission, damage);
        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("Delivery damage, traffic, mission and HUD are set up in " + scene.path + ".");
    }

    static void RequireEditMode()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
    }

    static T FindInScene<T>(Scene scene) where T : Component
    {
        foreach (var r in scene.GetRootGameObjects()) { var c = r.GetComponentInChildren<T>(true); if (c) return c; }
        return null;
    }

    static GameObject NewRoot(Scene scene, string name)
    {
        var go = new GameObject(name);
        SceneManager.MoveGameObjectToScene(go, scene);
        return go;
    }

    // ---- Truck damage

    static void AddDamageToPickup()
    {
        var root = PrefabUtility.LoadPrefabContents(PickupPath);
        try
        {
            var damage = root.GetComponent<TruckDamage>() ?? root.AddComponent<TruckDamage>();
            var old = root.transform.Find("Engine Smoke");
            if (old) Object.DestroyImmediate(old.gameObject);
            var smoke = new GameObject("Engine Smoke");
            smoke.transform.SetParent(root.transform, false);
            smoke.transform.localPosition = new Vector3(0f, 1.55f, 1.5f); // over the hood
            smoke.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f); // the cone points up
            var so = new SerializedObject(damage);
            so.FindProperty("smoke").objectReferenceValue = BuildSmoke(smoke);
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, PickupPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    static ParticleSystem BuildSmoke(GameObject go)
    {
        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop = true; main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(.45f, .9f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(.8f, 1.8f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.gravityModifier = -.12f;
        main.maxParticles = 80;
        main.startColor = new Color(.16f, .16f, .17f, .55f);
        var emission = ps.emission; emission.rateOverTime = 0f;
        var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 14f; shape.radius = .18f;
        var size = ps.sizeOverLifetime; size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, .6f, 1f, 2.2f));
        var colour = ps.colorOverLifetime; colour.enabled = true;
        var fade = new Gradient();
        fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, .15f), new GradientAlphaKey(0f, 1f) });
        colour.color = fade;
        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(SmokeMaterialPath);
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return ps;
    }

    // ---- Traffic

    // A copy of the pickup's model with nothing that drives it. Its tyres sit on the root's ground plane.
    static GameObject PickupVisual(Transform parent)
    {
        var pickup = AssetDatabase.LoadAssetAtPath<GameObject>(PickupPath);
        if (pickup == null) throw new InvalidOperationException("Missing " + PickupPath);
        var source = pickup.transform.Find("Model");
        if (source == null) throw new InvalidOperationException("Missing Model in " + PickupPath);
        var model = Object.Instantiate(source.gameObject, parent);
        model.name = "Model";
        model.transform.localPosition = source.localPosition;
        model.transform.localRotation = source.localRotation;
        model.transform.localScale = source.localScale;
        float lowest = float.MaxValue;
        foreach (var filter in model.GetComponentsInChildren<MeshFilter>())
        {
            if (filter.sharedMesh == null) continue;
            var b = filter.sharedMesh.bounds;
            for (int i = 0; i < 8; i++)
            {
                var corner = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                lowest = Mathf.Min(lowest, parent.InverseTransformPoint(filter.transform.TransformPoint(corner)).y);
            }
        }
        if (lowest < float.MaxValue) model.transform.localPosition -= Vector3.up * lowest;
        return model;
    }

    // The player's pickup as a traffic vehicle: the same model without the driving parts, repainted when it spawns.
    static GameObject MakeTrafficPickup()
    {
        AssetDatabase.DeleteAsset(OldTrafficCarPath);
        var pickup = AssetDatabase.LoadAssetAtPath<GameObject>(PickupPath);
        var root = new GameObject("Traffic Pickup");
        try
        {
            var model = PickupVisual(root.transform);
            var box = root.AddComponent<BoxCollider>();
            var pickupBox = pickup.GetComponent<BoxCollider>();
            box.center = pickupBox.center; box.size = pickupBox.size;
            var body = root.AddComponent<Rigidbody>();
            body.mass = pickup.GetComponent<Rigidbody>().mass; body.isKinematic = true; body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            var car = root.AddComponent<TrafficCar>();
            var bodyMaterial = AssetDatabase.LoadAssetAtPath<Material>(BodyMaterialPath);
            var painted = model.GetComponentsInChildren<Renderer>().Where(r => r.sharedMaterials.Contains(bodyMaterial)).ToArray();
            var wheels = model.transform.Find("WheelsMesh");
            var so = new SerializedObject(car);
            so.FindProperty("paintMaterial").objectReferenceValue = bodyMaterial;
            SetArray(so.FindProperty("paintRenderers"), painted);
            SetArray(so.FindProperty("wheels"), wheels ? wheels.Cast<Transform>().ToArray() : new Transform[0]);
            var wheelCollider = pickup.GetComponentInChildren<WheelCollider>();
            if (wheelCollider) so.FindProperty("wheelRadius").floatValue = wheelCollider.radius;
            so.ApplyModifiedPropertiesWithoutUndo();
            return PrefabUtility.SaveAsPrefabAsset(root, TrafficPickupPath);
        }
        finally { Object.DestroyImmediate(root); }
    }

    static void SetArray<T>(SerializedProperty property, T[] values) where T : Object
    {
        property.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }

    // The route is read from the road mesh: six vertices per segment, vertex 0 on the left edge and vertex 5 on the
    // right edge, ordered downhill. The left edge is always 5 m from the centre line (bays only widen the right side).
    static void AddTraffic(Scene scene, GameObject pickupPrefab, Transform truck)
    {
        MeshCollider road = null;
        foreach (var r in scene.GetRootGameObjects())
            foreach (var c in r.GetComponentsInChildren<MeshCollider>(true))
                if (c.name.StartsWith("Drivable road")) road = c;
        if (road == null) throw new InvalidOperationException("Missing the drivable road in " + scene.path);
        var v = road.sharedMesh.vertices;
        var centre = new List<Vector3>(); var right = new List<Vector3>();
        for (int i = 0; i + 5 < v.Length; i += 6)
        {
            Vector3 l = road.transform.TransformPoint(v[i]), r = road.transform.TransformPoint(v[i + 5]);
            var across = Vector3.ProjectOnPlane(r - l, Vector3.up).normalized;
            centre.Add(l + across * 5f); right.Add(across);
        }
        if (centre.Count <= TrimSegments * 2 + 2) throw new InvalidOperationException("The road mesh is too short for traffic.");
        centre = centre.GetRange(TrimSegments, centre.Count - TrimSegments * 2);
        right = right.GetRange(TrimSegments, right.Count - TrimSegments * 2);

        var go = NewRoot(scene, TrafficName);
        var route = go.AddComponent<TrafficRoute>();
        route.SetPath(centre.ToArray(), right.ToArray());
        var so = new SerializedObject(route);
        var models = AssetDatabase.FindAssets("t:Prefab", new[] { TrafficModelsFolder })
            .Select(guid => AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid)))
            .Where(p => p != null && p.GetComponent<Rigidbody>() != null).OrderBy(p => p.name).ToList();
        if (models.Count == 0) Debug.LogWarning("No traffic models in " + TrafficModelsFolder + "; only the recoloured pickup will drive.");
        var vehicles = so.FindProperty("vehicles");
        vehicles.arraySize = models.Count + 1;
        for (int i = 0; i <= models.Count; i++)
        {
            var entry = vehicles.GetArrayElementAtIndex(i);
            entry.FindPropertyRelative("prefab").objectReferenceValue = i < models.Count ? models[i] : pickupPrefab;
            entry.FindPropertyRelative("weight").floatValue = i < models.Count || models.Count == 0 ? 1f : TrafficPickupWeight;
        }
        so.FindProperty("player").objectReferenceValue = truck;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ---- Mission and HUD

    static DeliveryMission AddMission(Scene scene, TruckDamage damage)
    {
        var zone = FindInScene<DropOffZone>(scene);
        if (zone == null) throw new InvalidOperationException("Missing the drop-off zone in " + scene.path);
        // Older levels wired the zone straight to the respawn; the mission decides that now.
        for (int i = zone.TruckArrived.GetPersistentEventCount() - 1; i >= 0; i--)
            UnityEventTools.RemovePersistentListener(zone.TruckArrived, i);
        EditorUtility.SetDirty(zone);
        var mission = NewRoot(scene, MissionName).AddComponent<DeliveryMission>();
        var so = new SerializedObject(mission);
        so.FindProperty("truck").objectReferenceValue = damage;
        so.FindProperty("dropOff").objectReferenceValue = zone;
        so.FindProperty("respawn").objectReferenceValue = FindInScene<TruckRespawn>(scene);
        so.ApplyModifiedPropertiesWithoutUndo();
        return mission;
    }

    static void AddHud(Scene scene, DeliveryMission mission, TruckDamage damage)
    {
        var canvas = MakeCanvas(NewRoot(scene, HudName), false);
        var panel = Box(canvas.transform, "TruckPanel", Shade, new Vector2(0, 1), new Vector2(0, 1), new Vector2(16, -16), new Vector2(260, 74));
        var healthLabel = Text(panel, "TruckLabel", "Truck 100%", 19, Cream, TextAlignmentOptions.Left, new Vector2(12, -8), new Vector2(236, 24));
        var frame = Box(panel, "TruckBarFrame", new Color(0f, 0f, 0f, .55f), new Vector2(0, 1), new Vector2(0, 1), new Vector2(12, -34), new Vector2(236, 12));
        var fill = new GameObject("TruckBarFill", typeof(RectTransform)).GetComponent<RectTransform>();
        fill.SetParent(frame, false);
        fill.anchorMin = Vector2.zero; fill.anchorMax = Vector2.one; fill.offsetMin = Vector2.zero; fill.offsetMax = Vector2.zero;
        var fillImage = fill.gameObject.AddComponent<Image>(); fillImage.color = new Color(.35f, .8f, .4f); fillImage.raycastTarget = false;
        var cargoLabel = Text(panel, "CargoLabel", "Cargo", 16, Cream, TextAlignmentOptions.Left, new Vector2(12, -50), new Vector2(236, 20));
        var messageBox = Box(canvas.transform, "MessagePanel", Shade, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -16), new Vector2(560, 36));
        var message = Text(messageBox, "Message", "", 19, Gold, TextAlignmentOptions.Center, Vector2.zero, Vector2.zero);
        Stretch(message.rectTransform);

        var hud = canvas.gameObject.AddComponent<DeliveryHud>();
        var so = new SerializedObject(hud);
        so.FindProperty("mission").objectReferenceValue = mission;
        so.FindProperty("truck").objectReferenceValue = damage;
        so.FindProperty("healthFill").objectReferenceValue = fillImage;
        so.FindProperty("healthLabel").objectReferenceValue = healthLabel;
        so.FindProperty("cargoLabel").objectReferenceValue = cargoLabel;
        so.FindProperty("messageLabel").objectReferenceValue = message;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ---- UI helpers (the farm's HUD look: 1280 x 720 reference, the HUD font material, cream and gold text)

    static Canvas MakeCanvas(GameObject go, bool clickable)
    {
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720); scaler.matchWidthOrHeight = .5f;
        if (clickable) go.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    // A flat coloured rectangle placed from one anchor point; the offset is from that anchor, the pivot matches it.
    static RectTransform Box(Transform parent, string name, Color color, Vector2 anchor, Vector2 pivot, Vector2 offset, Vector2 size)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = anchor; rect.pivot = pivot;
        rect.anchoredPosition = offset; rect.sizeDelta = size;
        var image = rect.gameObject.AddComponent<Image>(); image.color = color; image.raycastTarget = false;
        return rect;
    }

    static TMP_Text Text(Transform parent, string name, string content, float size, Color color, TextAlignmentOptions alignment, Vector2 offset, Vector2 boxSize)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0, 1); rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = offset; rect.sizeDelta = boxSize;
        var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.text = content; text.fontSize = size; text.color = color; text.alignment = alignment; text.raycastTarget = false;
        var hudMaterial = AssetDatabase.LoadAssetAtPath<Material>(HudFontMaterialPath);
        if (hudMaterial != null && text.font != null && hudMaterial.GetTexture("_MainTex") == text.font.atlasTexture) text.fontSharedMaterial = hudMaterial;
        return text;
    }

    static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.pivot = new Vector2(.5f, .5f);
        rect.offsetMin = new Vector2(8, 2); rect.offsetMax = new Vector2(-8, -2);
    }

    static Button MakeButton(Transform parent, string name, string label, Color color, Vector2 offset, Vector2 size)
    {
        var rect = Box(parent, name, color, new Vector2(0, 1), new Vector2(0, 1), offset, size);
        var image = rect.GetComponent<Image>(); image.raycastTarget = true;
        var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image;
        var colors = button.colors; colors.highlightedColor = new Color(1f, 1f, 1f, .85f); colors.pressedColor = new Color(.8f, .8f, .8f); button.colors = colors;
        var text = Text(rect, "Label", label, 22, new Color(.14f, .1f, .05f), TextAlignmentOptions.Center, Vector2.zero, Vector2.zero);
        Stretch(text.rectTransform);
        return button;
    }

    // ---- Wholesale Market scene

    [MenuItem("Tools/Mountain Delivery/Build Wholesale Market Scene (discards hand edits)")]
    public static void BuildMarketScene()
    {
        RequireEditMode();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        if (!AssetDatabase.IsValidFolder(MarketArtFolder)) AssetDatabase.CreateFolder("Assets/Game/Art", "Market");

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(.7f, .8f, .85f); RenderSettings.ambientEquatorColor = new Color(.53f, .58f, .52f); RenderSettings.ambientGroundColor = new Color(.28f, .32f, .3f);
        RenderSettings.fog = false;
        var sun = new GameObject("Afternoon sun").AddComponent<Light>();
        sun.type = LightType.Directional; sun.color = new Color(1f, .91f, .76f); sun.intensity = 1.5f; sun.shadows = LightShadows.Soft;
        sun.transform.rotation = Quaternion.Euler(48f, -35f, 0f);

        var camera = new GameObject("Main Camera").AddComponent<Camera>(); camera.tag = "MainCamera";
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.72f, .81f, .82f);
        camera.fieldOfView = 45f; camera.transform.position = new Vector3(-7.5f, 4.2f, -9.5f);
        camera.transform.LookAt(new Vector3(1.5f, 1.3f, 1.5f));
        camera.gameObject.AddComponent<AudioListener>();

        // The set: a yard, the market stall with crates of produce, and the delivery truck backed up to it.
        var set = new GameObject("Market").transform;
        Block(set, "Yard", "MarketYard", "#B9AB87", new Vector3(0, -.1f, 0), new Vector3(60, .2f, 60));
        Block(set, "Meadow", "MarketMeadow", "#788866", new Vector3(0, -.15f, 0), new Vector3(400, .2f, 400));
        Block(set, "Stall floor", "MarketWood", "#765440", new Vector3(4, .1f, 3), new Vector3(7, .2f, 4));
        foreach (var x in new[] { .8f, 7.2f }) foreach (var z in new[] { 1.3f, 4.7f })
            Block(set, "Post", "MarketWood", "#765440", new Vector3(x, 1.6f, z), new Vector3(.22f, 3f, .22f));
        var roof = Block(set, "Roof", "MarketRoof", "#B75D44", new Vector3(4, 3.2f, 3), new Vector3(7.8f, .2f, 4.8f));
        roof.localRotation = Quaternion.Euler(-8f, 0f, 0f);
        Block(set, "Counter", "MarketWall", "#D7C7A1", new Vector3(4, .7f, 1.6f), new Vector3(6, 1f, .8f));
        Block(set, "Sign", "MarketSign", "#EDB94F", new Vector3(4, 3.75f, 1.2f), new Vector3(4.2f, .8f, .12f));
        for (int i = 0; i < 5; i++)
            Block(set, "Crate", "MarketWood", "#765440", new Vector3(1.6f + i * 1.2f, 1.45f, 1.6f), new Vector3(.8f, .5f, .6f)).localRotation = Quaternion.Euler(0, (i * 37) % 20 - 10, 0);
        for (int i = 0; i < 3; i++)
            Block(set, "Stacked crate", "MarketWood", "#765440", new Vector3(8.6f, .45f + i * .82f, 2f + (i % 2) * .15f), new Vector3(.9f, .8f, .9f));
        var truck = new GameObject("Delivery truck").transform; truck.SetParent(set, false);
        PickupVisual(truck);
        truck.SetPositionAndRotation(new Vector3(-1.2f, 0f, 1.2f), Quaternion.Euler(0f, 200f, 0f));

        // A Farm Fund for playing this scene on its own; it removes itself when the real one is alive.
        var fundPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(FarmFundPath);
        if (fundPrefab != null) PrefabUtility.InstantiatePrefab(fundPrefab, scene);

        var events = new GameObject("EventSystem");
        events.AddComponent<UnityEngine.EventSystems.EventSystem>(); events.AddComponent<InputSystemUIInputModule>();

        var canvas = MakeCanvas(new GameObject("Market Canvas"), true);
        var panel = Box(canvas.transform, "Panel", Shade, new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(-40, 0), new Vector2(440, 520));
        Text(panel, "Title", "Wholesale Market", 34, Gold, TextAlignmentOptions.Left, new Vector2(24, -20), new Vector2(392, 44));
        var receipt = Text(panel, "Receipt", "", 22, Cream, TextAlignmentOptions.TopLeft, new Vector2(24, -80), new Vector2(392, 210));
        var total = Text(panel, "Total", "", 28, Gold, TextAlignmentOptions.Left, new Vector2(24, -300), new Vector2(392, 36));
        var fund = Text(panel, "FundAmount", "Fund: $ 0", 24, Cream, TextAlignmentOptions.Left, new Vector2(24, -342), new Vector2(392, 32));
        var sell = MakeButton(panel, "Sell", "Sell", Gold, new Vector2(24, -392), new Vector2(392, 48));
        var home = MakeButton(panel, "Back to the Farm", "Back to the Farm", Cream, new Vector2(24, -452), new Vector2(392, 44));

        var market = canvas.gameObject.AddComponent<WholesaleMarket>();
        var audio = canvas.gameObject.AddComponent<AudioSource>(); audio.playOnAwake = false;
        var so = new SerializedObject(market);
        so.FindProperty("receiptLabel").objectReferenceValue = receipt;
        so.FindProperty("totalLabel").objectReferenceValue = total;
        so.FindProperty("fundLabel").objectReferenceValue = fund;
        so.FindProperty("sellButton").objectReferenceValue = sell;
        so.FindProperty("homeButton").objectReferenceValue = home;
        so.FindProperty("saleAudio").objectReferenceValue = audio;
        so.FindProperty("saleSound").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(SaleSoundGuid));
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.SaveScene(scene, MarketScenePath);
        WholesaleMarketArt.Apply();
        AssetDatabase.SaveAssets();
        Debug.Log("Wholesale Market scene built.");
    }

    static Transform Block(Transform parent, string name, string material, string hex, Vector3 position, Vector3 size)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.name = name; go.isStatic = true;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position; go.transform.localScale = size;
        go.GetComponent<MeshRenderer>().sharedMaterial = MarketMaterial(material, hex);
        return go.transform;
    }

    static Material MarketMaterial(string name, string hex)
    {
        string path = MarketArtFolder + "/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null) return material;
        ColorUtility.TryParseHtmlString(hex, out Color color);
        material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name, color = color };
        material.SetFloat("_Smoothness", .08f); material.enableInstancing = true;
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    // ---- The truck parked on the farm

    [MenuItem("Tools/Mountain Delivery/Add Delivery Truck to The Farm (keeps hand edits)")]
    public static void AddDepotToFarm()
    {
        RequireEditMode();
        var prefab = MakeDepotPrefab();
        var scene = EditorSceneManager.OpenScene(FarmScenePath, OpenSceneMode.Single);
        foreach (var root in scene.GetRootGameObjects())
            if (root.GetComponent<DeliveryDepot>() != null) Object.DestroyImmediate(root);
        var depot = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        depot.transform.SetPositionAndRotation(DepotPosition, Quaternion.Euler(0f, DepotYaw, 0f));
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Delivery truck parked on the farm at " + DepotPosition + ".");
    }

    static GameObject MakeDepotPrefab()
    {
        var root = new GameObject("Delivery Truck Stop");
        try
        {
            PickupVisual(root.transform);
            var pickupBox = AssetDatabase.LoadAssetAtPath<GameObject>(PickupPath).GetComponent<BoxCollider>();
            // The collider reaches the ground so the farmer cannot walk under the body.
            float top = pickupBox.center.y + pickupBox.size.y * .5f;
            var box = root.AddComponent<BoxCollider>();
            box.center = new Vector3(pickupBox.center.x, top * .5f, pickupBox.center.z);
            box.size = new Vector3(pickupBox.size.x, top, pickupBox.size.z);
            var depot = root.AddComponent<DeliveryDepot>();
            var so = new SerializedObject(depot);
            so.FindProperty("reach").floatValue = 3.4f; // from the middle of the truck: reaches a farmer standing at any side
            so.ApplyModifiedPropertiesWithoutUndo();
            return PrefabUtility.SaveAsPrefabAsset(root, DepotPath);
        }
        finally { Object.DestroyImmediate(root); }
    }

    // ---- Build list

    [MenuItem("Tools/Mountain Delivery/Add Delivery Scenes to the Build List")]
    public static void AddScenesToBuild()
    {
        var scenes = EditorBuildSettings.scenes.ToList();
        foreach (string path in new[] { DeliveryScenePath, MarketScenePath })
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null) throw new InvalidOperationException("Missing " + path);
            scenes.RemoveAll(s => s.path == path);
            scenes.Add(new EditorBuildSettingsScene(path, true));
        }
        EditorBuildSettings.scenes = scenes.ToArray();
        AssetDatabase.SaveAssets();
        Debug.Log("Build list: " + string.Join(", ", scenes.Select(s => s.path)));
    }
}
