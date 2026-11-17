using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Builds the farm's log cabin (Prefabs/Farm/Farm Cabin.prefab) out of generated low-poly logs and planks, and puts it
// in the farm scenes where the storage house and the open-air sleep area used to stand. The bed and rug (the
// SleepArea object, which the camera, the tests and the farmer's animator already know) move inside it.
// Run it again after changing the numbers here: the prefab and the scene placement are rebuilt from scratch.
public static class FarmCabinBuilder
{
    const string ArtDir = "Assets/Game/Art/Cabin";
    const string PrefabPath = "Assets/Game/Prefabs/Farm/Farm Cabin.prefab";
    public const string CabinName = "Farm Cabin";
    const string FarmScene = "Assets/Game/Scenes/The Farm.unity";

    // ---- placement in the farm (world space)
    public static readonly Vector3 CabinPosition = new Vector3(-3.75f, 0f, 12.8f);
    static readonly Vector3 BedLocal = new Vector3(3.0f, 0.03f, 1.6f);
    static readonly Vector3 HayStackPosition = new Vector3(1.95f, 0f, 11.3f);

    // ---- cabin shape (local space: +z is the back wall, the door faces -z toward the farm and the camera)
    const float HalfW = 4.5f;      // wall centre lines at x = +-HalfW
    const float HalfD = 2.8f;      // wall centre lines at z = +-HalfD
    const float LogR = 0.17f;      // log radius
    const float RowStep = 0.3f;    // logs overlap a little, so no gaps show
    const float CornerOut = 0.35f; // log ends stick out past the corners
    const float Plate = 3.15f;     // underside of the roof above the long walls
    const float Pitch = 34f;       // roof angle
    const float Overhang = 0.65f;  // eaves past the long walls
    const float RoofHalfX = 5.25f; // the roof runs past the gables
    const float DoorX0 = -0.05f, DoorX1 = 1.25f, DoorTop = 2.15f;

    static Mesh logMesh;
    static readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();
    static System.Random rng;

    [MenuItem("Tools/Farm/Build Cabin Prefab")]
    public static GameObject BuildPrefab()
    {
        rng = new System.Random(485);
        EnsureFolder(ArtDir);
        logMesh = LogMesh();
        mats.Clear();

        var root = new GameObject(CabinName);
        try
        {
            BuildCabin(root.transform);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Debug.Log("Saved " + PrefabPath);
            return prefab;
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    [MenuItem("Tools/Farm/Place Cabin In Open Scene")]
    public static void PlaceInActiveScene()
    {
        if (Application.isPlaying)
            throw new System.InvalidOperationException("Leave Play Mode first.");
        PlaceIn(SceneManager.GetActiveScene());
    }

    // builds the prefab and puts the cabin in The Farm, saving it
    [MenuItem("Tools/Farm/Build Cabin And Place In The Farm")]
    public static string BuildAndPlaceInFarm()
    {
        if (Application.isPlaying)
            throw new System.InvalidOperationException("Leave Play Mode first.");
        if (SceneManager.GetActiveScene().path != FarmScene && SceneManager.GetActiveScene().isDirty)
            throw new System.InvalidOperationException("Save or discard the open scene first.");
        BuildPrefab();
        var scene = SceneManager.GetActiveScene().path == FarmScene ? SceneManager.GetActiveScene()
                                                                    : EditorSceneManager.OpenScene(FarmScene, OpenSceneMode.Single);
        string result = PlaceIn(scene);
        EditorSceneManager.SaveScene(scene);
        return FarmScene + ": " + result;
    }

    public static string PlaceIn(Scene scene)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null)
            prefab = BuildPrefab();
        var notes = new List<string>();
        var roots = scene.GetRootGameObjects();

        foreach (var old in roots.Where(g => g.name == CabinName))
            Object.DestroyImmediate(old);
        var cabin = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        cabin.transform.SetPositionAndRotation(CabinPosition, Quaternion.identity);
        notes.Add("cabin placed");

        var storage = roots.FirstOrDefault(g => g != null && g.name == "Storage_House");
        if (storage != null)
        {
            Object.DestroyImmediate(storage);
            notes.Add("storage house removed");
        }

        var sleep = roots.FirstOrDefault(g => g != null && g.name == "SleepArea");
        if (sleep != null)
        {
            sleep.transform.SetPositionAndRotation(cabin.transform.TransformPoint(BedLocal), Quaternion.identity);
            foreach (string part in new[] { "Canopy", "SleepLabel" })
            {
                var child = sleep.transform.Find(part);
                if (child != null)
                    child.gameObject.SetActive(false);
            }
            notes.Add("bed and rug moved inside");
        }

        var scenery = roots.FirstOrDefault(g => g != null && g.name == "FarmScenery");
        var hay = scenery != null ? scenery.transform.Find("Props/HayStack") : null;
        if (hay != null)
        {
            hay.position = HayStackPosition;
            notes.Add("hay stack moved");
        }

        var meadow = scenery != null ? scenery.transform.Find("MeadowDetails") : null;
        if (meadow != null)
            notes.Add(ClearMeadowUnder(cabin.transform, meadow) + " grass and flower triangles cleared from the floor");

        EditorSceneManager.MarkSceneDirty(scene);
        return string.Join(", ", notes);
    }

    // the meadow's grass and flowers are a few big combined meshes: drop the blades that would grow through the floor
    static int ClearMeadowUnder(Transform cabin, Transform meadow)
    {
        int removed = 0;
        foreach (var filter in meadow.GetComponentsInChildren<MeshFilter>())
        {
            var mesh = filter.sharedMesh;
            if (mesh == null)
                continue;
            var verts = mesh.vertices;
            var under = new bool[verts.Length];
            bool any = false;
            for (int i = 0; i < verts.Length; i++)
            {
                var local = cabin.InverseTransformPoint(filter.transform.TransformPoint(verts[i]));
                bool inWalls = Mathf.Abs(local.x) < HalfW + 0.3f && Mathf.Abs(local.z) < HalfD + 0.3f;
                bool onStep = local.x > DoorX0 - 0.45f && local.x < DoorX1 + 0.45f && local.z < 0f && local.z > -HalfD - 0.75f;
                under[i] = inWalls || onStep;
                any |= under[i];
            }
            if (!any)
                continue;
            for (int sub = 0; sub < mesh.subMeshCount; sub++)
            {
                var tris = mesh.GetTriangles(sub);
                var kept = new List<int>(tris.Length);
                for (int t = 0; t < tris.Length; t += 3)
                {
                    if (under[tris[t]] || under[tris[t + 1]] || under[tris[t + 2]])
                    {
                        removed++;
                        continue;
                    }
                    kept.Add(tris[t]); kept.Add(tris[t + 1]); kept.Add(tris[t + 2]);
                }
                mesh.SetTriangles(kept, sub, false);
            }
            EditorUtility.SetDirty(mesh);
        }
        AssetDatabase.SaveAssets();
        return removed;
    }

    // ------------------------------------------------------------------ the cabin

    static void BuildCabin(Transform root)
    {
        var walls = Group(root, "Walls");          // north, east and west walls up to the eaves
        var front = Group(root, "Front Wall");     // the south wall, its windows and the door: hidden indoors
        var upper = Group(root, "Upper");          // gables and roof: hidden indoors
        var floor = Group(root, "Floor");
        var furniture = Group(root, "Furniture");
        var colliders = Group(root, "Colliders");
        var markers = Group(root, "Markers");

        float tan = Mathf.Tan(Pitch * Mathf.Deg2Rad);
        float ridge = Plate + HalfD * tan;
        var longRows = Enumerable.Range(0, 10).Select(i => 0.12f + i * RowStep).ToArray();
        var shortRows = Enumerable.Range(0, 10).Select(i => -0.03f + i * RowStep).ToArray();

        // ---- the long walls: the south one has the door and two windows, the north one a window
        var southOpenings = new List<Opening>
        {
            new Opening(DoorX0, DoorX1, 0f, DoorTop, true),
            new Opening(-3.25f, -1.95f, 0.95f, 2.05f, false),
            new Opening(2.55f, 3.85f, 0.95f, 2.05f, false),
        };
        var northOpenings = new List<Opening> { new Opening(-0.5f, 0.8f, 0.95f, 2.05f, false) };
        var sideOpenings = new List<Opening> { new Opening(-0.55f, 0.55f, 0.95f, 2.05f, false) };

        LogWall(front, "South Logs", true, -HalfD, HalfW, longRows, southOpenings, true);
        LogWall(walls, "North Logs", true, HalfD, HalfW, longRows, northOpenings, true);
        LogWall(walls, "East Logs", false, HalfW, HalfD, shortRows, sideOpenings, true);
        LogWall(walls, "West Logs", false, -HalfW, HalfD, shortRows, sideOpenings, true);

        foreach (var o in southOpenings.Where(o => !o.door))
            Window(front, true, -HalfD, o, longRows, -1);
        foreach (var o in northOpenings)
            Window(walls, true, HalfD, o, longRows, 1);
        Window(walls, false, HalfW, sideOpenings[0], shortRows, 1);
        Window(walls, false, -HalfW, sideOpenings[0], shortRows, -1);
        Door(front, longRows);

        // ---- gables: shorter and shorter logs up to the ridge
        var gable = Group(upper, "Gables");
        foreach (float x in new[] { -HalfW, HalfW })
            for (float y = shortRows.Last() + RowStep; ; y += RowStep)
            {
                float half = HalfD - (y + LogR - Plate) / tan;
                if (half < 0.25f)
                    break;
                Log(gable, "Gable Log", new Vector3(x, y, -half), new Vector3(x, y, half), Var("Log"));
            }

        // ---- roof: planks down both slopes, a ridge cap, dark barge boards and purlin log ends under the eaves
        var roof = Group(upper, "Roof");
        float run = HalfD + Overhang;
        float slope = run / Mathf.Cos(Pitch * Mathf.Deg2Rad);
        const float plank = 0.42f, thick = 0.1f;
        int planks = Mathf.CeilToInt(RoofHalfX * 2f / plank);
        foreach (int side in new[] { -1, 1 })
        {
            float s = Mathf.Sin(Pitch * Mathf.Deg2Rad), c = Mathf.Cos(Pitch * Mathf.Deg2Rad);
            var down = new Vector3(0f, -s, side * c);
            var normal = new Vector3(0f, c, side * s);
            var rot = Quaternion.LookRotation(down, normal);
            var top = new Vector3(0f, ridge, 0f);
            for (int i = 0; i < planks; i++)
            {
                float x = -RoofHalfX + plank * (i + 0.5f);
                float len = slope + (float)(rng.NextDouble() * 0.12 - 0.04);
                float lift = (i % 2 == 0 ? 0f : 0.018f) + thick / 2f;
                var centre = top + new Vector3(x, 0f, 0f) + down * (len / 2f - 0.04f) + normal * lift;
                Box(roof, "Roof Plank", centre, new Vector3(plank - 0.025f, thick, len), Var("Roof"), rot);
            }
            foreach (float x in new[] { -RoofHalfX - 0.03f, RoofHalfX + 0.03f })
                Box(roof, "Barge Board", top + new Vector3(x, 0f, 0f) + down * (slope / 2f) + normal * 0.02f,
                    new Vector3(0.09f, 0.3f, slope + 0.05f), M("RoofTrim", "#6A4630"), rot);
            foreach (float f in new[] { 0.42f, 0.8f })
            {
                float z = side * HalfD * f;
                // the log's corners must stay below the sloping underside: measure square to the roof, not straight up
                float y = Plate + (HalfD - Mathf.Abs(z)) * tan - (LogR * 1.1f + 0.02f) / Mathf.Cos(Pitch * Mathf.Deg2Rad);
                Log(roof, "Purlin", new Vector3(-RoofHalfX + 0.12f, y, z), new Vector3(RoofHalfX - 0.12f, y, z), Var("Log"));
            }
        }
        Box(roof, "Ridge Cap", new Vector3(0f, ridge + 0.1f, 0f), new Vector3(RoofHalfX * 2f + 0.16f, 0.26f, 0.26f),
            M("RoofTrim", "#6A4630"), Quaternion.Euler(45f, 0f, 0f));

        // ---- floor boards and a door step
        float inX = HalfW - LogR, inZ = HalfD - LogR;
        int boards = 16;
        float board = HalfD * 2f / boards; // the boards run on under the walls, so no grass shows along them
        for (int i = 0; i < boards; i++)
            Box(floor, "Floor Board", new Vector3(0f, 0.0125f, -HalfD + board * (i + 0.5f)),
                new Vector3(HalfW * 2f, 0.025f, board - 0.015f), Var("Floor"));
        // the dark gap layer tops out above the farm path mesh (0.018) that runs under the cabin, so the path never shows between boards
        Box(floor, "Floor Gap", new Vector3(0f, 0.0105f, 0f), new Vector3(HalfW * 2f, 0.019f, HalfD * 2f), M("FloorGap", "#4A3220"));
        Box(floor, "Door Step", new Vector3((DoorX0 + DoorX1) / 2f, 0.03f, -HalfD - 0.42f), new Vector3(1.7f, 0.06f, 0.5f), Var("Floor"));
        Box(floor, "Door Sill", new Vector3((DoorX0 + DoorX1) / 2f, 0.02f, -HalfD), new Vector3(DoorX1 - DoorX0, 0.04f, 0.36f), M("RoofTrim", "#6A4630"));

        // ---- furniture: a table with a lantern under the back window, and the storage chest
        Table(furniture, new Vector3(0.15f, 0f, 2.1f), colliders);
        Chest(root, new Vector3(-3.0f, 0.025f, 2.12f));

        // ---- colliders: solid walls, an open doorway
        float t = LogR * 2f + 0.04f, h = Plate;
        float longHalf = HalfW + CornerOut;
        WallCollider(colliders, "South West", new Vector3((-longHalf + DoorX0) / 2f, h / 2f, -HalfD), new Vector3(DoorX0 + longHalf, h, t));
        WallCollider(colliders, "South East", new Vector3((DoorX1 + longHalf) / 2f, h / 2f, -HalfD), new Vector3(longHalf - DoorX1, h, t));
        WallCollider(colliders, "North", new Vector3(0f, h / 2f, HalfD), new Vector3(longHalf * 2f, h, t));
        WallCollider(colliders, "East", new Vector3(HalfW, h / 2f, 0f), new Vector3(t, h, (HalfD + CornerOut) * 2f));
        WallCollider(colliders, "West", new Vector3(-HalfW, h / 2f, 0f), new Vector3(t, h, (HalfD + CornerOut) * 2f));

        // ---- markers the tests (and anyone placing things) can use
        Group(markers, "Door Outside").localPosition = new Vector3((DoorX0 + DoorX1) / 2f, 0f, -HalfD - 0.9f);
        Group(markers, "Door Inside").localPosition = new Vector3((DoorX0 + DoorX1) / 2f, 0f, -HalfD + 0.9f);

        foreach (var r in root.GetComponentsInChildren<Transform>())
            if (r.GetComponent<MeshRenderer>() != null && r.GetComponentInParent<StorageChest>() == null && !r.name.StartsWith("Door"))
                GameObjectUtility.SetStaticEditorFlags(r.gameObject, StaticEditorFlags.BatchingStatic);

        var cabin = root.gameObject.AddComponent<FarmCabin>();
        var so = new SerializedObject(cabin);
        var hide = so.FindProperty("hideWhenInside");
        hide.arraySize = 2;
        hide.GetArrayElementAtIndex(0).objectReferenceValue = front;
        hide.GetArrayElementAtIndex(1).objectReferenceValue = upper;
        so.FindProperty("interior").boundsValue = new Bounds(new Vector3(0f, 1f, 0f), new Vector3(inX * 2f, 2f, inZ * 2f));
        so.FindProperty("doorHinge").objectReferenceValue = front.Find("Door/Door Hinge");
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    struct Opening
    {
        public float a0, a1, y0, y1;
        public bool door;
        public Opening(float a0, float a1, float y0, float y1, bool door)
        {
            this.a0 = a0; this.a1 = a1; this.y0 = y0; this.y1 = y1; this.door = door;
        }
        public bool Cuts(float y) { return y + LogR > y0 + 0.01f && y - LogR < y1 - 0.01f; }
    }

    // a wall of stacked logs along X (at z = at) or along Z (at x = at), split around its openings
    static void LogWall(Transform parent, string name, bool alongX, float at, float half, float[] rows, List<Opening> openings, bool corners)
    {
        var wall = Group(parent, name);
        foreach (float y in rows)
        {
            var segments = new List<Vector2> { new Vector2(-half - (corners ? CornerOut : 0f), half + (corners ? CornerOut : 0f)) };
            foreach (var o in openings.Where(o => o.Cuts(y)))
            {
                var next = new List<Vector2>();
                foreach (var seg in segments)
                {
                    if (o.a1 <= seg.x || o.a0 >= seg.y) { next.Add(seg); continue; }
                    if (o.a0 > seg.x) next.Add(new Vector2(seg.x, o.a0));
                    if (o.a1 < seg.y) next.Add(new Vector2(o.a1, seg.y));
                }
                segments = next;
            }
            foreach (var seg in segments.Where(s => s.y - s.x > 0.12f))
                Log(wall, "Log", P(alongX, seg.x, y, at), P(alongX, seg.y, y, at), Var("Log"));
        }
    }

    static Vector3 P(bool alongX, float a, float y, float at)
    {
        return alongX ? new Vector3(a, y, at) : new Vector3(at, y, a);
    }

    // window frame, cross bars and glass; 'outward' is +1 when the outside of this wall faces +x / +z
    static void Window(Transform parent, bool alongX, float at, Opening o, float[] rows, int outward)
    {
        float bottom = rows.Where(y => y + LogR <= o.y0 + 0.01f).Select(y => y + LogR).DefaultIfEmpty(o.y0).Max();
        float top = rows.Where(y => y - LogR >= o.y1 - 0.01f).Select(y => y - LogR).DefaultIfEmpty(o.y1).Min();
        float w = o.a1 - o.a0, mid = (o.a0 + o.a1) / 2f;
        var win = Group(parent, "Window");
        win.localPosition = P(alongX, mid, 0f, at);
        win.localRotation = alongX ? Quaternion.identity : Quaternion.Euler(0f, 90f, 0f);
        var frame = M("WindowFrame", "#7A4A2A");
        float depth = LogR * 2f + 0.04f;
        float glassH = o.y1 - o.y0;
        Box(win, "Sill", new Vector3(0f, (bottom + o.y0) / 2f - 0.02f, 0f), new Vector3(w + 0.22f, o.y0 - bottom + 0.08f, depth + 0.14f), frame);
        Box(win, "Head", new Vector3(0f, (o.y1 + top) / 2f, 0f), new Vector3(w + 0.14f, top - o.y1 + 0.04f, depth + 0.04f), frame);
        foreach (int side in new[] { -1, 1 })
            Box(win, "Jamb", new Vector3(side * (w / 2f - 0.05f), (o.y0 + o.y1) / 2f, 0f), new Vector3(0.1f, glassH, depth + 0.04f), frame);
        Box(win, "Bar", new Vector3(0f, (o.y0 + o.y1) / 2f, 0f), new Vector3(0.07f, glassH, 0.08f), frame);
        Box(win, "Bar", new Vector3(0f, (o.y0 + o.y1) / 2f, 0f), new Vector3(w - 0.1f, 0.07f, 0.08f), frame);
        Box(win, "Glass", new Vector3(0f, (o.y0 + o.y1) / 2f, 0f), new Vector3(w - 0.1f, glassH, 0.02f), M("Glass", "#5C6C78", 0.85f));
    }

    static void Door(Transform front, float[] rows)
    {
        var door = Group(front, "Door");
        float w = DoorX1 - DoorX0, depth = LogR * 2f + 0.04f;
        float top = rows.Where(y => y - LogR >= DoorTop - 0.01f).Select(y => y - LogR).DefaultIfEmpty(DoorTop).Min();
        var frame = M("WindowFrame", "#7A4A2A");
        foreach (float x in new[] { DoorX0 + 0.05f, DoorX1 - 0.05f })
            Box(door, "Door Post", new Vector3(x, DoorTop / 2f, -HalfD), new Vector3(0.1f, DoorTop, depth + 0.04f), frame);
        Box(door, "Door Head", new Vector3((DoorX0 + DoorX1) / 2f, (DoorTop + top) / 2f, -HalfD), new Vector3(w + 0.12f, top - DoorTop + 0.04f, depth + 0.06f), frame);

        // the door leaf hangs on its west post and swings inward (FarmCabin opens it when the farmer is close)
        var hinge = Group(door, "Door Hinge");
        hinge.localPosition = new Vector3(DoorX0 + 0.1f, 0f, -HalfD + 0.06f);
        float leafW = w - 0.2f, leafH = DoorTop - 0.04f;
        int boards = 4;
        for (int i = 0; i < boards; i++)
            Box(hinge, "Door Board", new Vector3(leafW / boards * (i + 0.5f), leafH / 2f + 0.02f, 0f),
                new Vector3(leafW / boards - 0.012f, leafH, 0.07f), Var("Door"));
        foreach (float y in new[] { 0.45f, 1.7f })
            Box(hinge, "Door Batten", new Vector3(leafW / 2f, y, 0.06f), new Vector3(leafW - 0.06f, 0.14f, 0.05f), M("RoofTrim", "#6A4630"));
        Box(hinge, "Door Knob", new Vector3(leafW - 0.14f, 1.0f, -0.07f), new Vector3(0.07f, 0.07f, 0.07f), M("Iron", "#3A3A3C", 0.5f));
        Box(hinge, "Door Knob", new Vector3(leafW - 0.14f, 1.0f, 0.1f), new Vector3(0.07f, 0.07f, 0.07f), M("Iron", "#3A3A3C", 0.5f));
    }

    static void Table(Transform parent, Vector3 at, Transform colliders)
    {
        var table = Group(parent, "Table");
        table.localPosition = at;
        var wood = M("TableWood", "#8A5A34");
        Box(table, "Table Top", new Vector3(0f, 0.78f, 0f), new Vector3(1.3f, 0.08f, 0.72f), wood);
        foreach (var c in new[] { new Vector2(-0.56f, -0.27f), new Vector2(0.56f, -0.27f), new Vector2(-0.56f, 0.27f), new Vector2(0.56f, 0.27f) })
            Box(table, "Table Leg", new Vector3(c.x, 0.37f, c.y), new Vector3(0.09f, 0.74f, 0.09f), wood);
        foreach (int side in new[] { -1, 1 })
        {
            var stool = Group(table, "Stool");
            stool.localPosition = new Vector3(side * 0.95f, 0f, -0.15f);
            Box(stool, "Stool Seat", new Vector3(0f, 0.46f, 0f), new Vector3(0.42f, 0.07f, 0.42f), wood);
            foreach (var c in new[] { new Vector2(-0.15f, -0.15f), new Vector2(0.15f, -0.15f), new Vector2(-0.15f, 0.15f), new Vector2(0.15f, 0.15f) })
                Box(stool, "Stool Leg", new Vector3(c.x, 0.21f, c.y), new Vector3(0.06f, 0.43f, 0.06f), wood);
        }
        // lantern on the table: a warm light so the cabin is not dark at night
        var glow = AssetDatabase.LoadAssetAtPath<Material>("Assets/Game/Art/Materials/LowPolyGlow.mat");
        Box(table, "Lantern Base", new Vector3(0.35f, 0.85f, 0.1f), new Vector3(0.18f, 0.05f, 0.18f), M("Iron", "#3A3A3C", 0.5f));
        Box(table, "Lantern Glass", new Vector3(0.35f, 0.97f, 0.1f), new Vector3(0.13f, 0.2f, 0.13f), glow != null ? glow : M("Glow", "#FFD98C"));
        Box(table, "Lantern Cap", new Vector3(0.35f, 1.1f, 0.1f), new Vector3(0.17f, 0.05f, 0.17f), M("Iron", "#3A3A3C", 0.5f));
        var lightGo = Group(table, "Lantern Light");
        lightGo.localPosition = new Vector3(0.35f, 1.0f, 0.1f);
        var light = lightGo.gameObject.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, 0.76f, 0.45f);
        light.range = 3.2f;
        light.intensity = 1.3f;
        light.shadows = LightShadows.None;

        var box = Group(colliders, "Table").gameObject.AddComponent<BoxCollider>();
        box.transform.localPosition = at + new Vector3(0f, 0.4f, 0f);
        box.size = new Vector3(1.3f, 0.8f, 0.72f);
    }

    // a chunky double chest: golden planks, dark edges, a silver latch, and a lid hinged at the back
    static void Chest(Transform root, Vector3 at)
    {
        var chest = Group(root, "Storage Chest");
        chest.localPosition = at;
        const float W = 1.36f, D = 0.7f, body = 0.5f, lidH = 0.26f, e = 0.075f;
        var wood = M("ChestWood", "#C08A2E");
        var grain = M("ChestGrain", "#93621F");
        var trim = M("ChestTrim", "#3B2814");
        var latch = M("ChestLatch", "#D2D2D6", 0.6f, 0.5f);

        Box(chest, "Chest Body", new Vector3(0f, body / 2f, 0f), new Vector3(W, body, D), wood);
        // dark edge bands round the body (a frame on every face, as on the block-style chest)
        foreach (int sx in new[] { -1, 1 })
            foreach (int sz in new[] { -1, 1 })
                Box(chest, "Chest Edge", new Vector3(sx * (W / 2f - e / 2f + 0.005f), body / 2f, sz * (D / 2f - e / 2f + 0.005f)), new Vector3(e, body, e), trim);
        foreach (int sz in new[] { -1, 1 })
            Box(chest, "Chest Edge", new Vector3(0f, e / 2f, sz * (D / 2f - e / 2f + 0.005f)), new Vector3(W + 0.01f, e, e), trim);
        foreach (int sx in new[] { -1, 1 })
            Box(chest, "Chest Edge", new Vector3(sx * (W / 2f - e / 2f + 0.005f), e / 2f, 0f), new Vector3(e, e, D + 0.01f), trim);
        Box(chest, "Chest Rim", new Vector3(0f, body - 0.03f, 0f), new Vector3(W + 0.02f, 0.06f, D + 0.02f), trim);
        // plank lines and the seam down the middle of the double chest
        foreach (float y in new[] { 0.19f, 0.34f })
        {
            Box(chest, "Chest Grain", new Vector3(0f, y, -D / 2f - 0.004f), new Vector3(W - 2f * e, 0.025f, 0.012f), grain);
            Box(chest, "Chest Grain", new Vector3(0f, y, D / 2f + 0.004f), new Vector3(W - 2f * e, 0.025f, 0.012f), grain);
            foreach (int sx in new[] { -1, 1 })
                Box(chest, "Chest Grain", new Vector3(sx * (W / 2f + 0.004f), y, 0f), new Vector3(0.012f, 0.025f, D - 2f * e), grain);
        }
        Box(chest, "Chest Seam", new Vector3(0f, body / 2f, -D / 2f - 0.006f), new Vector3(0.035f, body - 0.1f, 0.012f), trim);

        // lid: its pivot is the hinge along the back top edge, so turning it about X opens it
        var lid = Group(chest, "Lid");
        lid.localPosition = new Vector3(0f, body, D / 2f);
        Box(lid, "Lid Top", new Vector3(0f, lidH / 2f, -D / 2f), new Vector3(W, lidH, D), wood);
        foreach (int sx in new[] { -1, 1 })
            foreach (int sz in new[] { -1, 1 })
                Box(lid, "Lid Edge", new Vector3(sx * (W / 2f - e / 2f + 0.005f), lidH / 2f, -D / 2f + sz * (D / 2f - e / 2f + 0.005f)), new Vector3(e, lidH, e), trim);
        foreach (int sz in new[] { -1, 1 })
            Box(lid, "Lid Edge", new Vector3(0f, lidH - e / 2f + 0.005f, -D / 2f + sz * (D / 2f - e / 2f + 0.005f)), new Vector3(W + 0.01f, e, e), trim);
        foreach (int sx in new[] { -1, 1 })
            Box(lid, "Lid Edge", new Vector3(sx * (W / 2f - e / 2f + 0.005f), lidH - e / 2f + 0.005f, -D / 2f), new Vector3(e, e, D + 0.01f), trim);
        Box(lid, "Lid Grain", new Vector3(0f, lidH + 0.004f, -D / 2f), new Vector3(W - 2f * e, 0.012f, 0.025f), grain);
        Box(lid, "Lid Seam", new Vector3(0f, lidH / 2f, -D - 0.006f), new Vector3(0.035f, lidH - 0.06f, 0.012f), trim);
        Box(lid, "Latch", new Vector3(0f, 0.0f, -D - 0.03f), new Vector3(0.13f, 0.2f, 0.05f), latch);

        var collider = chest.gameObject.AddComponent<BoxCollider>();
        collider.center = new Vector3(0f, (body + lidH) / 2f, 0f);
        collider.size = new Vector3(W, body + lidH, D);

        var audio = chest.gameObject.AddComponent<AudioSource>();
        audio.playOnAwake = false;
        audio.spatialBlend = 0f;
        audio.volume = 0.8f;

        var storage = chest.gameObject.AddComponent<StorageChest>();
        var so = new SerializedObject(storage);
        so.FindProperty("lid").objectReferenceValue = lid;
        so.FindProperty("chestAudio").objectReferenceValue = audio;
        so.FindProperty("openSound").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Game/Audio/Bag Open.wav");
        so.FindProperty("closeSound").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Game/Audio/Bag Close.wav");
        so.FindProperty("windowStyle.font").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
        so.FindProperty("windowStyle.rounded").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Game/Art/Sprites/UIRounded.png");
        so.FindProperty("windowStyle.circle").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Game/Art/Sprites/UICircle.png");
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ------------------------------------------------------------------ pieces

    static Transform Group(Transform parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go.transform;
    }

    static void WallCollider(Transform parent, string name, Vector3 centre, Vector3 size)
    {
        var go = Group(parent, name);
        go.localPosition = centre;
        go.gameObject.AddComponent<BoxCollider>().size = size;
    }

    static GameObject Log(Transform parent, string name, Vector3 from, Vector3 to, Material side)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        Vector3 dir = to - from;
        go.transform.localPosition = (from + to) / 2f;
        go.transform.localRotation = Quaternion.FromToRotation(Vector3.right, dir.normalized);
        go.transform.localScale = new Vector3(dir.magnitude, LogR * 2f, LogR * 2f);
        go.AddComponent<MeshFilter>().sharedMesh = logMesh;
        go.AddComponent<MeshRenderer>().sharedMaterials = new[] { side, M("LogEnd", "#B99A6C") };
        return go;
    }

    static GameObject Box(Transform parent, string name, Vector3 pos, Vector3 size, Material mat, Quaternion? rot = null)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = rot ?? Quaternion.identity;
        go.transform.localScale = size;
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        return go;
    }

    // ------------------------------------------------------------------ assets

    // three shades of each wood, picked at random, so the logs and planks do not look copy-pasted
    static Material Var(string kind)
    {
        string[] hex;
        switch (kind)
        {
            case "Log": hex = new[] { "#9A7448", "#8C6740", "#A57D50" }; break;
            case "Roof": hex = new[] { "#A07450", "#AB7F58", "#946946" }; break;
            case "Floor": hex = new[] { "#B07F52", "#A57549", "#B98A5C" }; break;
            default: hex = new[] { "#6E4432", "#7A4B37", "#653E2D" }; break; // door
        }
        int i = rng.Next(hex.Length);
        return M(kind + (char)('A' + i), hex[i]);
    }

    static Material M(string name, string hex, float smoothness = 0.12f, float metallic = 0f)
    {
        if (mats.TryGetValue(name, out var cached))
            return cached;
        string path = ArtDir + "/Cabin" + name + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(mat, path);
        }
        ColorUtility.TryParseHtmlString(hex, out var color);
        mat.SetColor("_BaseColor", color);
        mat.SetFloat("_Smoothness", smoothness);
        mat.SetFloat("_Metallic", metallic);
        EditorUtility.SetDirty(mat);
        mats[name] = mat;
        return mat;
    }

    // an eight-sided log one unit long (along X) and one unit across, flat shaded; submesh 1 is the end grain
    static Mesh LogMesh()
    {
        string path = ArtDir + "/CabinLog.asset";
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (mesh == null)
        {
            mesh = new Mesh { name = "CabinLog" };
            AssetDatabase.CreateAsset(mesh, path);
        }
        mesh.Clear();
        const int n = 8;
        float R = 0.5f / Mathf.Cos(Mathf.PI / n);
        var ring = new Vector2[n];
        for (int k = 0; k < n; k++)
        {
            float a = (k + 0.5f) * 2f * Mathf.PI / n;
            ring[k] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * R; // (y, z)
        }
        var v = new List<Vector3>();
        var nor = new List<Vector3>();
        var sides = new List<int>();
        var ends = new List<int>();
        for (int k = 0; k < n; k++)
        {
            var p = ring[k];
            var q = ring[(k + 1) % n];
            var mid = (p + q).normalized;
            int b = v.Count;
            v.Add(new Vector3(-0.5f, p.x, p.y)); v.Add(new Vector3(0.5f, p.x, p.y));
            v.Add(new Vector3(0.5f, q.x, q.y)); v.Add(new Vector3(-0.5f, q.x, q.y));
            for (int j = 0; j < 4; j++) nor.Add(new Vector3(0f, mid.x, mid.y));
            sides.AddRange(new[] { b, b + 2, b + 1, b, b + 3, b + 2 });
        }
        foreach (int s in new[] { -1, 1 })
        {
            int c = v.Count;
            v.Add(new Vector3(0.5f * s, 0f, 0f));
            nor.Add(new Vector3(s, 0f, 0f));
            for (int k = 0; k < n; k++)
            {
                v.Add(new Vector3(0.5f * s, ring[k].x, ring[k].y));
                nor.Add(new Vector3(s, 0f, 0f));
            }
            for (int k = 0; k < n; k++)
            {
                int a = c + 1 + k, b = c + 1 + (k + 1) % n;
                if (s > 0) ends.AddRange(new[] { c, a, b });
                else ends.AddRange(new[] { c, b, a });
            }
        }
        mesh.SetVertices(v);
        mesh.SetNormals(nor);
        mesh.subMeshCount = 2;
        mesh.SetTriangles(sides, 0);
        mesh.SetTriangles(ends, 1);
        mesh.RecalculateBounds();
        EditorUtility.SetDirty(mesh);
        return mesh;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;
        string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
    }
}
