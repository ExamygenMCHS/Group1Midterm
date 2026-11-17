using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Object = UnityEngine.Object;

//puts the fishing into the farm: the rig prefab (bobber, line, ghost cast, ripples, splashes, fish shadow, sounds), the
//HUD prefab, and the install into The Farm scene, the bag and the trader's shop.
//Every step finds what an earlier run made (by name) and only fills in what is missing or different, so running a menu
//twice changes nothing and never adds a second object, row or tab. Prefabs are edited in place (same objects, same
//file IDs), so the scene's links to them survive a rebuild.
public static partial class FishingBuilder
{
    private const string RigPath = PrefabDir + "/Fishing Rig.prefab";
    private const string HudPath = PrefabDir + "/Fishing Hud.prefab";
    private const string RodPath = PrefabDir + "/Fishing Rod.prefab";
    private const string RodPickupPath = PrefabDir + "/Fishing Rod Pickup.prefab";
    private const string BobberPath = PrefabDir + "/Bobber.prefab";
    private const string FarmScenePath = "Assets/Game/Scenes/The Farm.unity";
    private const string BagPrefabPath = "Assets/Game/Prefabs/Inventory/Bag.prefab";
    private const string ShopPrefabPath = "Assets/Game/Prefabs/Trader/TraderShopCanvas.prefab";
    private const string DropMatPath = "Assets/Game/Art/Materials/ParticleDrop.mat";
    private const string UiSpriteDir = "Assets/Game/Art/Sprites";
    private const string FontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";
    private const string HudFontMatPath = "Assets/Game/Art/Fonts/LiberationSans SDF - HUD.mat";

    //the rod pickup: on the grass at the south bank, leaning across the stone at about (9.26, 0, 6.81)
    private static readonly Vector3 RodPickupPosition = new Vector3(9.95f, 0f, 6.70f);
    private const float RodPickupYaw = 56f;//the rod runs east along the bank, so the camera sees all of it
    public const string RodPickupWhere = "leaning on a stone at the near edge of the pond";

    private static readonly Color PanelColor = Hex("#12170FD9");
    private static readonly Color HudGold = Hex("#FFD45E");
    private static readonly Color HudText = Hex("#FFF7E0");

    private static Color Hex(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out Color c);
        return c;
    }

    // ================================================================== small find-or-make helpers

    private static Transform Child(Transform parent, string name)
    {
        Transform t = parent.Find(name);
        if (t != null)
            return t;
        var go = new GameObject(name);
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        return go.transform;
    }

    private static RectTransform UiChild(Transform parent, string name)
    {
        Transform t = parent.Find(name);
        if (t == null)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;//UI
            go.transform.SetParent(parent, false);
            t = go.transform;
        }
        return (RectTransform)t;
    }

    private static T Comp<T>(GameObject go) where T : Component
    {
        T c = go.GetComponent<T>();
        return c != null ? c : go.AddComponent<T>();
    }

    private static SerializedProperty Prop(SerializedObject so, string field)
    {
        SerializedProperty p = so.FindProperty(field);
        if (p == null)
            throw new Exception("no field '" + field + "' on " + so.targetObject.GetType().Name);
        return p;
    }

    //sets serialized fields only when they differ (so an unchanged scene stays clean); returns true when something changed
    private static bool Wire(Object target, params (string field, object value)[] fields)
    {
        var so = new SerializedObject(target);
        foreach (var (field, value) in fields)
        {
            SerializedProperty p = Prop(so, field);
            switch (value)
            {
                case Object obj:
                    if (p.objectReferenceValue != obj) p.objectReferenceValue = obj;
                    break;
                case null:
                    if (p.propertyType == SerializedPropertyType.ObjectReference && p.objectReferenceValue != null) p.objectReferenceValue = null;
                    break;
                case float f:
                    if (!Mathf.Approximately(p.floatValue, f)) p.floatValue = f;
                    break;
                case int i:
                    if (p.propertyType == SerializedPropertyType.Enum) { if (p.enumValueIndex != i) p.enumValueIndex = i; }
                    else if (p.intValue != i) p.intValue = i;
                    break;
                case bool b:
                    if (p.boolValue != b) p.boolValue = b;
                    break;
                case string s:
                    if (p.stringValue != s) p.stringValue = s;
                    break;
                case Object[] list:
                    if (p.arraySize != list.Length) p.arraySize = list.Length;
                    for (int k = 0; k < list.Length; k++)
                    {
                        SerializedProperty e = p.GetArrayElementAtIndex(k);
                        if (e.objectReferenceValue != list[k]) e.objectReferenceValue = list[k];
                    }
                    break;
                case Vector2[] points:
                    if (p.arraySize != points.Length) p.arraySize = points.Length;
                    for (int k = 0; k < points.Length; k++)
                    {
                        SerializedProperty e = p.GetArrayElementAtIndex(k);
                        if ((e.vector2Value - points[k]).sqrMagnitude > 1e-10f) e.vector2Value = points[k];
                    }
                    break;
                default:
                    throw new Exception("Wire: unsupported value type " + value.GetType().Name + " for " + field);
            }
        }
        return so.ApplyModifiedProperties();
    }

    private static Sprite UiSprite(string name) { return Load<Sprite>(UiSpriteDir + "/" + name + ".png"); }
    private static AudioClip Clip(string name) { return Load<AudioClip>(AudioDir + "/" + name + ".wav"); }

    // ================================================================== A. the rig

    [MenuItem("Tools/Fishing/Build Rig and HUD Prefabs")]
    public static void BuildRigAndHudMenu()
    {
        Debug.Log(BuildRigAndHud());
    }

    public static string BuildRigAndHud()
    {
        EnsureFolder(PrefabDir);
        string rig = EditPrefab(RigPath, "Fishing Rig", BuildRig);
        string hud = EditPrefab(HudPath, "Fishing Hud", BuildHud, true);
        AssetDatabase.SaveAssets();
        return "Fishing prefabs: " + rig + " | " + hud;
    }

    //opens an existing prefab for editing (keeping its objects), or makes a new one
    private static string EditPrefab(string path, string rootName, Action<GameObject> build, bool ui = false)
    {
        bool exists = AssetDatabase.LoadAssetAtPath<GameObject>(path) != null;
        GameObject root = exists ? PrefabUtility.LoadPrefabContents(path) : (ui ? new GameObject(rootName, typeof(RectTransform)) : new GameObject(rootName));
        try
        {
            if (ui)
                root.layer = 5;
            build(root);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            if (exists)
                PrefabUtility.UnloadPrefabContents(root);
            else
                Object.DestroyImmediate(root);
        }
        return (exists ? "updated " : "created ") + path;
    }

    private static MeshRenderer FlatMesh(Transform t, Mesh mesh, Material mat)
    {
        Comp<MeshFilter>(t.gameObject).sharedMesh = mesh;
        MeshRenderer r = Comp<MeshRenderer>(t.gameObject);
        r.sharedMaterial = mat;
        r.shadowCastingMode = ShadowCastingMode.Off;
        r.receiveShadows = false;
        r.lightProbeUsage = LightProbeUsage.Off;
        r.reflectionProbeUsage = ReflectionProbeUsage.Off;
        return r;
    }

    private static LineRenderer Line(Transform t, Material mat, float width, int points, LineTextureMode mode)
    {
        LineRenderer line = Comp<LineRenderer>(t.gameObject);
        line.useWorldSpace = true;
        line.positionCount = points;
        line.SetPositions(new Vector3[points]);
        line.widthMultiplier = width;
        line.widthCurve = AnimationCurve.Constant(0f, 1f, 1f);
        line.sharedMaterial = mat;
        line.shadowCastingMode = ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.textureMode = mode;
        line.numCapVertices = 0;
        line.numCornerVertices = 0;
        line.alignment = LineAlignment.View;
        line.lightProbeUsage = LightProbeUsage.Off;
        line.reflectionProbeUsage = ReflectionProbeUsage.Off;
        line.generateLightingData = false;
        return line;
    }

    private static ParticleSystem Particles(Transform t, Material mat, ParticleSystemRenderMode mode, int max)
    {
        ParticleSystem ps = Comp<ParticleSystem>(t.gameObject);
        //the visuals call Emit(); the system itself never emits
        var main = ps.main;
        main.loop = true;
        main.playOnAwake = true;
        main.duration = 1f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.maxParticles = max;
        main.startColor = Color.white;
        main.startRotation = 0f;
        var emission = ps.emission;
        emission.enabled = false;
        emission.rateOverTime = 0f;
        ParticleSystemRenderer r = Comp<ParticleSystemRenderer>(t.gameObject);
        r.renderMode = mode;
        r.sharedMaterial = mat;
        r.shadowCastingMode = ShadowCastingMode.Off;
        r.receiveShadows = false;
        r.lightProbeUsage = LightProbeUsage.Off;
        r.reflectionProbeUsage = ReflectionProbeUsage.Off;
        return ps;
    }

    private static Gradient FadeOut(float startAlpha, float holdUntil)
    {
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(startAlpha, 0f), new GradientAlphaKey(startAlpha * 0.8f, holdUntil), new GradientAlphaKey(0f, 1f) });
        return g;
    }

    private static void BuildRig(GameObject root)
    {
        root.name = "Fishing Rig";
        Transform rt = root.transform;
        FishingVisuals visuals = Comp<FishingVisuals>(root);
        FishingAudio audio = Comp<FishingAudio>(root);
        List<AudioSource> sources = root.GetComponents<AudioSource>().ToList();
        while (sources.Count < 2)
            sources.Add(root.AddComponent<AudioSource>());
        AudioSource oneShots = sources[0], reelLoop = sources[1];
        foreach (AudioSource s in sources)
        {
            s.playOnAwake = false;
            s.spatialBlend = 0f;
            s.volume = 1f;
        }
        oneShots.loop = false;
        oneShots.clip = null;
        reelLoop.loop = true;
        reelLoop.clip = Clip("Reel Loop");

        //the line from the rod tip to the bobber
        LineRenderer line = Line(Child(rt, "Line"), Mat("FishingLine"), 0.02f, 16, LineTextureMode.Stretch);

        //the bobber, with the hook holder just under it (the bait's model hangs there)
        Transform bobber = rt.Find("Bobber");
        if (bobber == null)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>(BobberPath), rt);
            instance.name = "Bobber";
            bobber = instance.transform;
        }
        Transform hookHolder = Child(bobber, "Hook Holder");
        hookHolder.localPosition = new Vector3(0f, -0.055f, 0f);
        hookHolder.localRotation = Quaternion.identity;

        //rings on the water: flat, growing from a quarter of their size and fading out
        ParticleSystem ripples = Particles(Child(rt, "Ripples"), Mat("Ripple"), ParticleSystemRenderMode.HorizontalBillboard, 40);
        {
            var main = ripples.main;
            main.startLifetime = 1f;
            main.startSpeed = 0f;
            main.startSize = 0.5f;
            main.gravityModifier = 0f;
            var shape = ripples.shape;
            shape.enabled = false;
            var size = ripples.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.25f, 0f, 1.6f), new Keyframe(1f, 1f, 0.3f, 0f)));
            var color = ripples.colorOverLifetime;
            color.enabled = true;
            color.color = new ParticleSystem.MinMaxGradient(FadeOut(1f, 0.35f));
        }

        //droplets thrown up in a cone, falling back
        ParticleSystem splash = Particles(Child(rt, "Splash"), Load<Material>(DropMatPath), ParticleSystemRenderMode.Billboard, 120);
        {
            var main = splash.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 3f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.09f);
            main.gravityModifier = 1f;
            var shape = splash.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 35f;
            shape.radius = 0.05f;
            shape.rotation = new Vector3(-90f, 0f, 0f);//the cone opens upward
            var size = splash.sizeOverLifetime;
            size.enabled = false;
            var color = splash.colorOverLifetime;
            color.enabled = true;
            color.color = new ParticleSystem.MinMaxGradient(FadeOut(1f, 0.7f));
        }

        //the fish under the water
        Transform shadow = Child(rt, "Shadow");
        MeshRenderer shadowRenderer = FlatMesh(shadow, MeshAsset("ShadowQuad"), Mat("FishShadow"));

        //the ghost cast; its own object stays on, it shows and hides the marker, the ghost bobber and the arc
        Transform ghostRoot = Child(rt, "Cast Ghost");
        CastGhost ghost = Comp<CastGhost>(ghostRoot.gameObject);
        Transform marker = Child(ghostRoot, "Marker");
        MeshRenderer markerRenderer = FlatMesh(marker, MeshAsset("CastGhostMarker"), Mat("CastGhost"));
        Transform ghostBobber = Child(ghostRoot, "Ghost Bobber");
        MeshRenderer ghostBobberRenderer = FlatMesh(ghostBobber, MeshAsset("Bobber"), Mat("CastGhost"));
        LineRenderer arc = Line(Child(ghostRoot, "Arc"), Mat("AimDash"), 0.03f, 22, LineTextureMode.Tile);
        arc.textureScale = new Vector2(7f, 1f);//Tile repeats per metre: about seven dashes a metre

        foreach (Transform t in new[] { line.transform, ripples.transform, splash.transform, shadow, ghostRoot, marker, ghostBobber, arc.transform })
        {
            t.localPosition = Vector3.zero;
            t.localRotation = Quaternion.identity;
            t.localScale = Vector3.one;
        }
        bobber.localPosition = Vector3.zero;

        Wire(visuals, ("line", line), ("bobber", bobber), ("hookHolder", hookHolder), ("ripples", ripples), ("splash", splash),
            ("shadow", shadow), ("shadowRenderer", shadowRenderer));
        Wire(ghost, ("marker", marker), ("tinted", new Object[] { markerRenderer, ghostBobberRenderer }), ("ghostBobber", ghostBobber), ("arc", arc));
        Wire(audio, ("oneShots", oneShots), ("reelLoop", reelLoop),
            ("castSound", Clip("Cast")), ("landSound", Clip("Bobber Land")), ("nibbleSound", Clip("Nibble")), ("biteSound", Clip("Bite")),
            ("hookSound", Clip("Hook")), ("surgeSound", Clip("Surge")), ("snapSound", Clip("Line Snap")), ("reelInSound", Clip("Reel In")),
            ("catchSound", Clip("Catch")), ("trophySound", Clip("Trophy")), ("baitSound", Clip("Bait")));
    }

    // ================================================================== B. the HUD

    private static RectTransform Place(RectTransform r, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        r.anchorMin = r.anchorMax = anchor;
        r.pivot = pivot;
        r.anchoredPosition = pos;
        r.sizeDelta = size;
        r.localScale = Vector3.one;
        r.localRotation = Quaternion.identity;
        return r;
    }

    private static RectTransform Stretch(RectTransform r, float inset)
    {
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.pivot = new Vector2(0.5f, 0.5f);
        r.offsetMin = new Vector2(inset, inset);
        r.offsetMax = new Vector2(-inset, -inset);
        r.localScale = Vector3.one;
        r.localRotation = Quaternion.identity;
        return r;
    }

    private static Image Img(RectTransform r, Sprite sprite, Color color, Image.Type type = Image.Type.Simple)
    {
        Image image = Comp<Image>(r.gameObject);
        image.sprite = sprite;
        image.color = color;
        image.type = type;
        image.preserveAspect = type == Image.Type.Simple && sprite != null;
        image.raycastTarget = false;
        return image;
    }

    private static TextMeshProUGUI Text(RectTransform r, string text, float size, Color color, TextAlignmentOptions align, bool bold = false)
    {
        TextMeshProUGUI tmp = Comp<TextMeshProUGUI>(r.gameObject);
        tmp.font = Load<TMP_FontAsset>(FontPath);
        tmp.fontSharedMaterial = Load<Material>(HudFontMatPath);
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.alignment = align;
        tmp.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.overflowMode = TextOverflowModes.Overflow;
        tmp.raycastTarget = false;
        tmp.richText = true;
        return tmp;
    }

    private static LayoutElement Layout(GameObject go, float width, float height)
    {
        LayoutElement le = Comp<LayoutElement>(go);
        le.minWidth = width;
        le.preferredWidth = width;
        le.minHeight = height;
        le.preferredHeight = height;
        le.flexibleWidth = -1f;
        le.flexibleHeight = -1f;
        return le;
    }

    private static HorizontalLayoutGroup Row(GameObject go, int left, int right, int vertical, float spacing)
    {
        HorizontalLayoutGroup h = Comp<HorizontalLayoutGroup>(go);
        h.padding = new RectOffset(left, right, vertical, vertical);
        h.spacing = spacing;
        h.childAlignment = TextAnchor.MiddleCenter;
        h.childControlWidth = true;
        h.childControlHeight = true;
        h.childForceExpandWidth = false;
        h.childForceExpandHeight = false;
        h.childScaleWidth = false;
        h.childScaleHeight = false;
        return h;
    }

    private static CanvasGroup Group(GameObject go)
    {
        CanvasGroup g = Comp<CanvasGroup>(go);
        g.alpha = 0f;
        g.blocksRaycasts = false;
        g.interactable = false;
        g.ignoreParentGroups = false;
        return g;
    }

    private static void BuildHud(GameObject rootGo)
    {
        rootGo.name = "Fishing Hud";
        var root = (RectTransform)rootGo.transform;
        Stretch(root, 0f);
        FishingHud hud = Comp<FishingHud>(rootGo);
        Sprite rounded = UiSprite("UIRounded");

        //the hint chip, bottom centre: above the interaction prompt (y 122), the item name and the hotbar
        RectTransform hint = Place(UiChild(root, "Hint"), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 176f), new Vector2(40f, 46f));
        CanvasGroup hintGroup = Group(hint.gameObject);
        RectTransform panel = Place(UiChild(hint, "Panel"), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(240f, 46f));
        Img(panel, rounded, PanelColor, Image.Type.Sliced);
        Row(panel.gameObject, 9, 16, 6, 8f);
        ContentSizeFitter fit = Comp<ContentSizeFitter>(panel.gameObject);
        fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        RectTransform leftClick = UiChild(panel, "Left Click");
        Img(leftClick, Spr("IconMouseLeft"), Color.white);
        Layout(leftClick.gameObject, 30f, 30f);
        RectTransform label = UiChild(panel, "Label");
        TextMeshProUGUI hintLabel = Text(label, "Cast", 20f, HudText, TextAlignmentOptions.MidlineLeft);
        Layout(label.gameObject, -1f, 34f).minWidth = -1f;
        RectTransform bait = UiChild(panel, "Bait");
        Row(bait.gameObject, 4, 0, 0, 6f);
        RectTransform divider = UiChild(bait, "Divider");
        Img(divider, null, new Color(HudGold.r, HudGold.g, HudGold.b, 0.35f));
        Layout(divider.gameObject, 2f, 24f);
        RectTransform rightClick = UiChild(bait, "Right Click");
        Img(rightClick, Spr("IconMouseRight"), Color.white);
        Layout(rightClick.gameObject, 30f, 30f);
        RectTransform baitIcon = UiChild(bait, "Bait Icon");
        Image baitImage = Img(baitIcon, Spr("IconOldLure"), Color.white);
        Layout(baitIcon.gameObject, 30f, 30f);
        RectTransform baitText = UiChild(bait, "Bait Label");
        TextMeshProUGUI baitLabel = Text(baitText, "Old Lure", 18f, HudGold, TextAlignmentOptions.MidlineLeft);
        Layout(baitText.gameObject, -1f, 30f).minWidth = -1f;
        foreach (RectTransform r in new[] { leftClick, label, bait, divider, rightClick, baitIcon, baitText })
        {
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
            r.localScale = Vector3.one;
        }
        leftClick.SetSiblingIndex(0);
        label.SetSiblingIndex(1);
        bait.SetSiblingIndex(2);
        divider.SetSiblingIndex(0);
        rightClick.SetSiblingIndex(1);
        baitIcon.SetSiblingIndex(2);
        baitText.SetSiblingIndex(3);

        //the line's strain, a slim bar just above the chip
        RectTransform tension = Place(UiChild(root, "Tension"), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 232f), new Vector2(260f, 14f));
        CanvasGroup tensionGroup = Group(tension.gameObject);
        RectTransform back = Stretch(UiChild(tension, "Back"), -3f);
        Img(back, UiSprite("UIPill"), PanelColor, Image.Type.Sliced);
        RectTransform fill = Stretch(UiChild(tension, "Fill"), 0f);
        Image fillImage = Img(fill, UiSprite("UIBarFill"), new Color(0.40f, 0.82f, 0.32f), Image.Type.Filled);
        fillImage.preserveAspect = false;
        fillImage.fillMethod = Image.FillMethod.Horizontal;
        fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
        fillImage.fillAmount = 0.3f;
        RectTransform danger = Place(UiChild(tension, "Danger"), new Vector2(0.85f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(3f, 22f));
        Img(danger, null, HudGold);
        RectTransform pulling = Place(UiChild(tension, "Label"), new Vector2(1f, 0.5f), new Vector2(0f, 0.5f), new Vector2(12f, 0f), new Vector2(120f, 24f));
        TextMeshProUGUI tensionLabel = Text(pulling, "", 18f, new Color(0.95f, 0.22f, 0.16f), TextAlignmentOptions.MidlineLeft, true);
        back.SetSiblingIndex(0);
        fill.SetSiblingIndex(1);
        danger.SetSiblingIndex(2);
        pulling.SetSiblingIndex(3);

        //the "!" over the bobber; the HUD moves it, anchored at the middle so its position is relative to the centre
        RectTransform bite = Place(UiChild(root, "Bite Alert"), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(56f, 56f));
        Img(bite, Spr("IconBite"), Color.white);

        //the catch card, right of the farmer (the camera keeps him in the middle of the screen, holding the fish up)
        RectTransform card = Place(UiChild(root, "Catch"), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(340f, 290f), new Vector2(320f, 92f));
        CanvasGroup catchGroup = Group(card.gameObject);
        RectTransform cardPanel = Place(UiChild(card, "Panel"), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(320f, 92f));
        Img(cardPanel, rounded, PanelColor, Image.Type.Sliced);
        RectTransform trim = Stretch(UiChild(cardPanel, "Trim"), 0f);
        Img(trim, UiSprite("UIRoundedOutline"), new Color(HudGold.r, HudGold.g, HudGold.b, 0.6f), Image.Type.Sliced);
        RectTransform icon = Place(UiChild(cardPanel, "Icon"), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(14f, 0f), new Vector2(64f, 64f));
        Image catchIcon = Img(icon, Spr("IconBass"), Color.white);
        RectTransform nameRect = Place(UiChild(cardPanel, "Name"), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(92f, 22f), new Vector2(214f, 28f));
        TextMeshProUGUI catchName = Text(nameRect, "Largemouth Bass", 22f, HudText, TextAlignmentOptions.MidlineLeft, true);
        RectTransform detailRect = Place(UiChild(cardPanel, "Detail"), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(92f, -2f), new Vector2(214f, 22f));
        TextMeshProUGUI catchDetail = Text(detailRect, "3.4 lb   $ 20", 18f, new Color(HudText.r, HudText.g, HudText.b, 0.85f), TextAlignmentOptions.MidlineLeft);
        RectTransform tagRect = Place(UiChild(cardPanel, "Tag"), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(92f, -25f), new Vector2(214f, 20f));
        TextMeshProUGUI catchTag = Text(tagRect, "New best!", 16f, HudGold, TextAlignmentOptions.MidlineLeft, true);
        trim.SetSiblingIndex(0);

        //nothing in the HUD takes a click
        foreach (Graphic g in rootGo.GetComponentsInChildren<Graphic>(true))
            g.raycastTarget = false;

        Wire(hud, ("hintGroup", hintGroup), ("hintPanel", panel), ("leftClickIcon", leftClick.gameObject), ("rightClickIcon", rightClick.gameObject),
            ("hintLabel", hintLabel), ("baitLabel", baitLabel), ("baitIcon", baitImage), ("baitGroup", bait.gameObject),
            ("tensionGroup", tensionGroup), ("tensionFill", fillImage), ("tensionDanger", danger), ("tensionLabel", tensionLabel),
            ("biteAlert", bite),
            ("catchGroup", catchGroup), ("catchPanel", cardPanel), ("catchIcon", catchIcon), ("catchName", catchName),
            ("catchDetail", catchDetail), ("catchTag", catchTag));
    }

    // ================================================================== C. the install

    [MenuItem("Tools/Fishing/Install In The Farm")]
    public static void InstallMenu()
    {
        try
        {
            Debug.Log(Install());
        }
        catch (Exception e)
        {
            Debug.LogError("Fishing install refused: " + e.Message);
        }
    }

    public static string Install()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != FarmScenePath)
            throw new Exception("open " + FarmScenePath + " first (the active scene is '" + scene.path + "')");
        if (scene.isDirty)
            throw new Exception("The Farm has unsaved changes; save or discard them first, so the install only adds the fishing");
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new Exception("leave Play Mode first");
        if (AssetDatabase.LoadAssetAtPath<GameObject>(RigPath) == null || AssetDatabase.LoadAssetAtPath<GameObject>(HudPath) == null)
            BuildRigAndHud();

        var log = new List<string>();
        FishData[] fish = FishNames.Select(n => Load<FishData>(DataDir + "/Fish/" + n + ".asset")).ToArray();
        BaitData lure = Load<BaitData>(DataDir + "/Bait/Lure.asset");
        BaitData oldLure = Load<BaitData>(DataDir + "/Bait/Old Lure.asset");
        BaitData worm = Load<BaitData>(DataDir + "/Bait/Worm.asset");

        // ---- the player
        GameObject player = Find("Player");
        FishingControl control = player.GetComponent<FishingControl>();
        if (control == null)
        {
            control = Undo.AddComponent<FishingControl>(player);
            log.Add("added FishingControl");
        }
        GameCalendar calendar = Find("DayNightController").GetComponent<GameCalendar>();
        ToolManager toolManager = player.GetComponent<ToolManager>();
        PlayerMove playerMove = player.GetComponent<PlayerMove>();
        Wire(control, ("playerStateControl", player.GetComponent<PlayerStateControl>()), ("playerMove", playerMove),
            ("playerEnergy", player.GetComponent<PlayerEnergy>()), ("hotbar", player.GetComponent<Hotbar>()), ("calendar", calendar),
            ("fish", fish.Cast<Object>().ToArray()), ("baits", new Object[] { lure, oldLure, worm }));
        Wire(toolManager, ("fishingRodPrefab", Load<GameObject>(RodPath)));

        // ---- the pond
        GameObject pondWater = Find("WaterSources/Pond Water");
        FishingWater water = pondWater.GetComponent<FishingWater>();
        if (water == null)
        {
            water = Undo.AddComponent<FishingWater>(pondWater);
            log.Add("added FishingWater");
        }
        Vector2[] shore = BakeOutline(Find("FarmScenery/Pond/Water"), pondWater.transform);
        Vector2[] deep = BakeOutline(Find("FarmScenery/Pond/Deep"), pondWater.transform);
        Transform pond = Find("FarmScenery/Pond").transform;
        Object[] pads = pond.Cast<Transform>().Where(t => t.name == "LilyPad").Cast<Object>().ToArray();
        Wire(water, ("shoreOutline", shore), ("deepOutline", deep), ("surfaceHeight", 0.05f), ("lilyPads", pads), ("calendar", calendar),
            ("iceHole", Find("FarmScenery/Pond/Water/Winter Fishing Hole").transform), ("iceHoleRadius", 0.4f));
        log.Add("outlines: shore " + shore.Length + " points, deep " + deep.Length + " points, " + pads.Length + " lily pads");

        // ---- the rig, the HUD and the rod by the pond
        GameObject rig = RootInstance(scene, "Fishing Rig", RigPath, null, log);
        Wire(rig.GetComponent<FishingVisuals>(), ("fishing", control), ("toolManager", toolManager), ("playerMove", playerMove));
        Wire(rig.GetComponent<FishingAudio>(), ("fishing", control));
        Wire(rig.GetComponentInChildren<CastGhost>(true), ("fishing", control), ("toolManager", toolManager));

        Transform canvas = Find("Canvas").transform;
        GameObject hud = RootInstance(scene, "Fishing Hud", HudPath, canvas, log);
        Wire(hud.GetComponent<FishingHud>(), ("fishing", control));

        //the where-text lives in the prefab (the builder writes the same text), so the scene needs no override for it
        if (Wire(Load<GameObject>(RodPickupPath).GetComponent<ToolPickup>(), ("whereText", RodPickupWhere)))
            AssetDatabase.SaveAssets();
        GameObject pickup = RootInstance(scene, "Fishing Rod Pickup", RodPickupPath, null, log);
        SetPose(pickup.transform, RodPickupPosition, Quaternion.Euler(0f, RodPickupYaw, 0f));
        Wire(pickup.GetComponent<ToolPickup>(), ("whereText", RodPickupWhere));

        // ---- the controls window
        ControlsPanel controls = canvas.GetComponent<ControlsPanel>();
        if (AddControls(controls, ("Click", "cast / hook / reel (fishing rod)"), ("Right click", "change bait")))
            log.Add("controls window: fishing lines added");

        // ---- prefab assets
        log.Add(InstallBag());
        log.Add(InstallShop(lure, worm, fish));

        bool changed = scene.isDirty;
        if (changed)
            EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        log.Add(changed ? "scene saved" : "scene already up to date");
        return "Fishing install: " + string.Join(" | ", log);
    }

    private static GameObject Find(string path)
    {
        //from the scene roots (GameObject.Find would skip inactive objects and match same-named children anywhere)
        string[] parts = path.Split('/');
        foreach (GameObject r in EditorSceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (r.name != parts[0])
                continue;
            Transform t = parts.Length > 1 ? r.transform.Find(string.Join("/", parts.Skip(1))) : r.transform;
            if (t != null)
                return t.gameObject;
        }
        throw new Exception("The Farm has no " + path);
    }

    //the one instance of a prefab with this name under the parent (the scene root when null), made when missing
    private static GameObject RootInstance(UnityEngine.SceneManagement.Scene scene, string name, string prefabPath, Transform parent, List<string> log)
    {
        IEnumerable<Transform> candidates = parent != null ? parent.Cast<Transform>() : scene.GetRootGameObjects().Select(g => g.transform);
        Transform found = candidates.FirstOrDefault(t => t.name == name);
        if (found != null)
            return found.gameObject;
        var go = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>(prefabPath), scene);
        go.name = name;
        if (parent != null)
            go.transform.SetParent(parent, false);
        Undo.RegisterCreatedObjectUndo(go, "Install fishing");
        EditorSceneManager.MarkSceneDirty(scene);
        log.Add("added " + name);
        return go;
    }

    private static void SetPose(Transform t, Vector3 position, Quaternion rotation)
    {
        if ((t.position - position).sqrMagnitude < 1e-8f && Quaternion.Angle(t.rotation, rotation) < 0.01f)
            return;
        Undo.RecordObject(t, "Place fishing rod");
        t.SetPositionAndRotation(position, rotation);
        EditorSceneManager.MarkSceneDirty(t.gameObject.scene);
    }

    private static bool AddControls(ControlsPanel panel, params (string keys, string action)[] lines)
    {
        var so = new SerializedObject(panel);
        SerializedProperty list = Prop(so, "controls");
        foreach (var (keys, action) in lines)
        {
            bool present = false;
            for (int i = 0; i < list.arraySize; i++)
                if (list.GetArrayElementAtIndex(i).FindPropertyRelative("keys").stringValue == keys)
                {
                    present = true;
                    SerializedProperty a = list.GetArrayElementAtIndex(i).FindPropertyRelative("action");
                    if (a.stringValue != action)
                        a.stringValue = action;
                }
            if (present)
                continue;
            list.arraySize++;
            SerializedProperty added = list.GetArrayElementAtIndex(list.arraySize - 1);
            added.FindPropertyRelative("keys").stringValue = keys;
            added.FindPropertyRelative("action").stringValue = action;
        }
        return so.ApplyModifiedProperties();
    }

    //the rim of a flat mesh as one loop of points in 'space's local x and z: vertices welded by position, the edges that
    //belong to exactly one triangle kept, and those walked around once
    public static Vector2[] BakeOutline(GameObject meshObject, Transform space)
    {
        Mesh mesh = meshObject.GetComponent<MeshFilter>().sharedMesh;
        Vector3[] vertices = mesh.vertices;
        int[] triangles = mesh.triangles;
        var weldIndex = new Dictionary<Vector3Int, int>();
        var welded = new List<Vector3>();
        var remap = new int[vertices.Length];
        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 world = meshObject.transform.TransformPoint(vertices[i]);
            var key = new Vector3Int(Mathf.RoundToInt(world.x * 1000f), Mathf.RoundToInt(world.y * 1000f), Mathf.RoundToInt(world.z * 1000f));
            if (!weldIndex.TryGetValue(key, out int index))
            {
                index = welded.Count;
                weldIndex[key] = index;
                welded.Add(world);
            }
            remap[i] = index;
        }
        var edgeCount = new Dictionary<(int, int), int>();
        for (int t = 0; t < triangles.Length; t += 3)
            for (int k = 0; k < 3; k++)
            {
                int a = remap[triangles[t + k]], b = remap[triangles[t + (k + 1) % 3]];
                var edge = a < b ? (a, b) : (b, a);
                edgeCount[edge] = edgeCount.TryGetValue(edge, out int n) ? n + 1 : 1;
            }
        var next = new Dictionary<int, List<int>>();
        foreach (var pair in edgeCount)
        {
            if (pair.Value != 1)
                continue;
            var (a, b) = pair.Key;
            if (!next.ContainsKey(a)) next[a] = new List<int>();
            if (!next.ContainsKey(b)) next[b] = new List<int>();
            next[a].Add(b);
            next[b].Add(a);
        }
        if (next.Count < 3 || next.Values.Any(l => l.Count != 2))
            throw new Exception(meshObject.name + ": the rim is not one simple loop (" + next.Count + " rim points)");
        int start = next.Keys.Min();
        var loop = new List<int> { start };
        int prev = start, cur = next[start][0];
        while (cur != start)
        {
            loop.Add(cur);
            int step = next[cur][0] == prev ? next[cur][1] : next[cur][0];
            prev = cur;
            cur = step;
            if (loop.Count > next.Count)
                throw new Exception(meshObject.name + ": the rim has more than one loop");
        }
        if (loop.Count != next.Count)
            throw new Exception(meshObject.name + ": the rim has " + (next.Count - loop.Count) + " points off the main loop");
        return loop.Select(i =>
        {
            Vector3 local = space.InverseTransformPoint(welded[i]);
            return new Vector2((float)Math.Round(local.x, 4), (float)Math.Round(local.z, 4));
        }).ToArray();
    }

    // ---- the bag: the rod has a slot

    private static string InstallBag()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(BagPrefabPath);
        try
        {
            FarmBag bag = root.GetComponentInChildren<FarmBag>(true);
            if (!Wire(bag, ("fishingRodItem", Load<InventoryItemData>(DataDir + "/Items/Fishing Rod.asset"))))
                return "bag already up to date";
            PrefabUtility.SaveAsPrefabAsset(root, BagPrefabPath);
            return "bag: fishing rod item set";
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // ---- the trader's shop: a Fishing tab with bait to buy and fish to sell

    private static string InstallShop(BaitData lure, BaitData worm, FishData[] fish)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(ShopPrefabPath);
        try
        {
            var log = new List<string>();
            ShopTabs tabs = root.GetComponentInChildren<ShopTabs>(true);
            Transform tabBar = tabs.transform;
            Transform fishingTab = tabBar.Find("Tab - Fishing");
            if (fishingTab == null)
            {
                Transform source = tabBar.Find("Tab - Equipment");
                fishingTab = Object.Instantiate(source.gameObject, tabBar).transform;
                fishingTab.name = "Tab - Fishing";
                fishingTab.SetSiblingIndex(source.GetSiblingIndex() + 1);
                log.Add("tab added");
            }
            TMP_Text tabLabel = fishingTab.GetComponentInChildren<TMP_Text>(true);
            tabLabel.gameObject.name = "Tab - Fishing Label";
            tabLabel.text = "Fishing";
            //four tabs evenly across the bar
            string[] order = { "Tab - Buy Seeds", "Tab - Sell Crops", "Tab - Equipment", "Tab - Fishing" };
            float width = 215f, gap = (920f - 4f * width) / 3f;
            for (int i = 0; i < order.Length; i++)
            {
                var r = (RectTransform)tabBar.Find(order[i]);
                r.sizeDelta = new Vector2(width, r.sizeDelta.y);
                r.anchoredPosition = new Vector2(-460f + width * 0.5f + i * (width + gap), r.anchoredPosition.y);
            }
            Wire(tabs, ("fishingTab", fishingTab.GetComponent<Button>()));

            StoreControl store = root.GetComponentInChildren<StoreControl>(true);
            Transform rows = root.transform.Find("Scroll View/Viewport/StoreRows");
            var made = new List<StoreItemRow>
            {
                ShopRow(rows, "Row - Worms", "Row - Sunflower Seeds", StoreItemRow.RowKind.BuyBait, worm, null, "Worms", "Buy Worms", "Buy", Spr("IconWorms"), log),
                ShopRow(rows, "Row - Lure", "Row - Bag", StoreItemRow.RowKind.BuyBait, lure, null, "Lure", "Buy Lure", "Buy", Spr("IconLure"), log),
            };
            foreach (FishData kind in fish)
                made.Add(ShopRow(rows, "Row - Sell " + kind.fishName, "Row - Sell Sunflowers", StoreItemRow.RowKind.SellFish, null, kind,
                    "Sell " + kind.fishName, "Sell " + kind.fishName, "Sell", kind.item.itemSprite, log));

            var so = new SerializedObject(store);
            SerializedProperty list = Prop(so, "rows");
            var current = new List<Object>();
            for (int i = 0; i < list.arraySize; i++)
                current.Add(list.GetArrayElementAtIndex(i).objectReferenceValue);
            foreach (StoreItemRow row in made)
                if (!current.Contains(row))
                    current.Add(row);
            Wire(store, ("rows", current.ToArray()));

            PrefabUtility.SaveAsPrefabAsset(root, ShopPrefabPath);
            return "shop: " + (log.Count > 0 ? string.Join(", ", log) : "rows and tab checked") + ", " + current.Count + " rows";
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    //one shop row, copied from an existing row so its layout and buttons carry over, then made into the new item
    private static StoreItemRow ShopRow(Transform rows, string name, string copyOf, StoreItemRow.RowKind kind, BaitData bait, FishData fish,
        string title, string buttonName, string buttonText, Sprite icon, List<string> log)
    {
        Transform t = rows.Find(name);
        if (t == null)
        {
            Transform source = rows.Find(copyOf);
            t = Object.Instantiate(source.gameObject, rows).transform;
            t.name = name;
            t.SetAsLastSibling();
            log.Add(name + " added");
        }
        StoreItemRow row = t.GetComponent<StoreItemRow>();
        Wire(row, ("kind", (int)kind), ("crop", null), ("bait", bait), ("fish", fish), ("startQuantity", 1));
        t.Find("Icon").GetComponent<Image>().sprite = icon;
        TMP_Text nameLabel = t.Find("Name").GetComponent<TMP_Text>();
        nameLabel.text = title;
        nameLabel.enableAutoSizing = true;//"Sell Largemouth Bass" is the longest name in the shop
        nameLabel.fontSizeMax = 22f;
        nameLabel.fontSizeMin = 16f;

        var so = new SerializedObject(row);
        var action = (Button)Prop(so, "actionButton").objectReferenceValue;
        var minus = (Button)Prop(so, "minusButton").objectReferenceValue;
        var plus = (Button)Prop(so, "plusButton").objectReferenceValue;
        action.gameObject.name = buttonName;
        TMP_Text actionLabel = action.GetComponentInChildren<TMP_Text>(true);
        actionLabel.gameObject.name = buttonName + "Text";
        actionLabel.text = buttonText;
        //every button calls this row, never the row (or the store method) it was copied from
        OnlyCall(action, row, row.Apply, "Apply");
        if (minus != null)
            OnlyCall(minus, row, row.DecreaseQuantity, "DecreaseQuantity");
        if (plus != null)
            OnlyCall(plus, row, row.IncreaseQuantity, "IncreaseQuantity");
        return row;
    }

    private static void OnlyCall(Button button, StoreItemRow row, UnityEngine.Events.UnityAction call, string method)
    {
        var ev = button.onClick;
        if (ev.GetPersistentEventCount() == 1 && ev.GetPersistentTarget(0) == row && ev.GetPersistentMethodName(0) == method)
            return;
        while (ev.GetPersistentEventCount() > 0)
            UnityEventTools.RemovePersistentListener(ev, 0);
        UnityEventTools.AddPersistentListener(ev, call);
        ev.SetPersistentListenerState(0, UnityEngine.Events.UnityEventCallState.RuntimeOnly);
    }

    // ================================================================== a check of everything above

    public static string VerifyInstall()
    {
        var problems = new List<string>();
        void Need(bool ok, string what) { if (!ok) problems.Add(what); }
        void NoNulls(Object target)
        {
            var so = new SerializedObject(target);
            SerializedProperty it = so.GetIterator();
            bool enter = true;
            while (it.NextVisible(enter))
            {
                enter = it.propertyType != SerializedPropertyType.String;
                if (it.propertyType == SerializedPropertyType.ObjectReference && it.objectReferenceValue == null
                    && it.propertyPath != "m_Script" && !it.propertyPath.Contains("refusedSound") && !it.propertyPath.Contains("aimCamera")
                    && !it.propertyPath.Contains("worldCamera"))
                    problems.Add(target.GetType().Name + "." + it.propertyPath + " is empty");
            }
        }
        GameObject player = Find("Player");
        FishingControl control = player.GetComponent<FishingControl>();
        Need(control != null, "FishingControl on the player");
        if (control != null) NoNulls(control);
        Need(new SerializedObject(player.GetComponent<ToolManager>()).FindProperty("fishingRodPrefab").objectReferenceValue != null, "ToolManager.fishingRodPrefab");
        FishingWater water = Find("WaterSources/Pond Water").GetComponent<FishingWater>();
        Need(water != null, "FishingWater on Pond Water");
        if (water != null)
        {
            NoNulls(water);
            var so = new SerializedObject(water);
            Need(so.FindProperty("shoreOutline").arraySize >= 20, "shore outline");
            Need(so.FindProperty("deepOutline").arraySize >= 20, "deep outline");
        }
        var scene = EditorSceneManager.GetActiveScene();
        GameObject rig = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "Fishing Rig");
        Need(rig != null, "Fishing Rig in the scene");
        if (rig != null)
        {
            NoNulls(rig.GetComponent<FishingVisuals>());
            NoNulls(rig.GetComponent<FishingAudio>());
            NoNulls(rig.GetComponentInChildren<CastGhost>(true));
        }
        Transform hud = Find("Canvas").transform.Find("Fishing Hud");
        Need(hud != null, "Fishing Hud under Canvas");
        if (hud != null) NoNulls(hud.GetComponent<FishingHud>());
        Need(scene.GetRootGameObjects().Count(g => g.name == "Fishing Rod Pickup") == 1, "one Fishing Rod Pickup");
        Need(scene.GetRootGameObjects().Count(g => g.name == "Fishing Rig") == 1, "one Fishing Rig");
        var bagRoot = AssetDatabase.LoadAssetAtPath<GameObject>(BagPrefabPath);
        Need(new SerializedObject(bagRoot.GetComponentInChildren<FarmBag>(true)).FindProperty("fishingRodItem").objectReferenceValue != null, "FarmBag.fishingRodItem");

        var shop = AssetDatabase.LoadAssetAtPath<GameObject>(ShopPrefabPath);
        StoreControl store = shop.GetComponentInChildren<StoreControl>(true);
        int rowCount = store.Rows.Count(r => r != null);
        Need(rowCount == 24, "24 shop rows (found " + rowCount + ")");
        ShopTabs tabs = shop.GetComponentInChildren<ShopTabs>(true);
        int tabCount = tabs.GetComponentsInChildren<Button>(true).Length;
        Need(tabCount == 4, "4 tabs (found " + tabCount + ")");
        NoNulls(tabs);
        foreach (StoreItemRow row in store.Rows)
        {
            if (row == null || (row.GetKind() != StoreItemRow.RowKind.BuyBait && row.GetKind() != StoreItemRow.RowKind.SellFish))
                continue;
            Need(row.GetCrop() == null, row.name + " still has a crop");
            Need(row.GetKind() == StoreItemRow.RowKind.BuyBait ? row.GetBait() != null : row.GetFish() != null, row.name + " item");
            foreach (Button b in row.GetComponentsInChildren<Button>(true))
                for (int i = 0; i < b.onClick.GetPersistentEventCount(); i++)
                    Need(b.onClick.GetPersistentTarget(i) == row, row.name + "/" + b.name + " calls " + b.onClick.GetPersistentTarget(i));
        }
        return problems.Count == 0 ? "VERIFY OK: " + rowCount + " rows, " + tabCount + " tabs" : "VERIFY PROBLEMS: " + string.Join("; ", problems);
    }
}
