// CropGenerator.cs
// Put this file in any folder named "Editor" (e.g. Assets/Editor/CropGenerator.cs).
// Then run:  Tools > Crops > Generate All Crop Prefabs
//
// Generates low-poly crop prefabs (Planted / Growing / Mature) for the Built-in
// render pipeline using the Standard shader. Output goes to:
//   Assets/Game/Prefabs/Plants/<Crop>_<Stage>.prefab
//   Assets/Game/Prefabs/Plants/Generated/Meshes/...
//   Assets/Game/Prefabs/Plants/Generated/Materials/...
//
// Pivot is at ground level (y = 0). The dirt mound is ~0.55 radius, ~0.22 tall.
// Re-running overwrites meshes and prefabs. Materials are only created if missing,
// so you can recolor them by hand without losing changes.

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Rng = System.Random;

public static class CropGenerator
{
    // ------------------------------------------------------------------
    // Settings
    // ------------------------------------------------------------------
    const string OutRoot = "Assets/Game/Prefabs/Plants";
    const string GenRoot = OutRoot + "/Generated";
    const bool IncludeMound = true;     // set false if you add your own dirt separately
    const float GlobalScale = 1f;       // scale everything to match your existing crops
    const float G = 0.18f;              // height where plants emerge from the mound

    enum Stage { Planted, Growing, Mature }

    static readonly Dictionary<string, Color> Palette = new Dictionary<string, Color>
    {
        { "Soil",         new Color(0.29f, 0.19f, 0.13f) },
        { "LeafGreen",    new Color(0.33f, 0.62f, 0.22f) },
        { "LeafDark",     new Color(0.22f, 0.46f, 0.19f) },
        { "LeafLight",    new Color(0.50f, 0.74f, 0.30f) },
        { "Stem",         new Color(0.42f, 0.60f, 0.24f) },
        { "Orange",       new Color(0.95f, 0.49f, 0.10f) },
        { "CornYellow",   new Color(0.97f, 0.80f, 0.25f) },
        { "Husk",         new Color(0.55f, 0.72f, 0.30f) },
        { "WheatStalk",   new Color(0.78f, 0.68f, 0.32f) },
        { "WheatGold",    new Color(0.90f, 0.72f, 0.28f) },
        { "PotatoTan",    new Color(0.76f, 0.58f, 0.34f) },
        { "PotatoFlower", new Color(0.96f, 0.94f, 0.98f) },
        { "FlowerCenter", new Color(0.98f, 0.82f, 0.20f) },
        { "CabbageLight", new Color(0.66f, 0.84f, 0.50f) },
    };

    // To add a crop: write a BuildXxx(Model m, Stage s) method and register it here.
    static readonly Dictionary<string, System.Action<Model, Stage>> Crops =
        new Dictionary<string, System.Action<Model, Stage>>
    {
        { "Carrot",  BuildCarrot  },
        { "Corn",    BuildCorn    },
        { "Wheat",   BuildWheat   },
        { "Potato",  BuildPotato  },
        { "Cabbage", BuildCabbage },
    };

    // ------------------------------------------------------------------
    // Menu entry
    // ------------------------------------------------------------------
    [MenuItem("Tools/Crops/Generate All Crop Prefabs")]
    public static void GenerateAll()
    {
        EnsureFolder(OutRoot);
        EnsureFolder(GenRoot);
        EnsureFolder(GenRoot + "/Meshes");
        EnsureFolder(GenRoot + "/Materials");

        int count = 0;
        foreach (var crop in Crops)
        {
            foreach (Stage stage in System.Enum.GetValues(typeof(Stage)))
            {
                var model = new Model();
                crop.Value(model, stage);
                SavePrefab(crop.Key, stage, model);
                count++;
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("CropGenerator: wrote " + count + " prefabs to " + OutRoot);
    }

    // ------------------------------------------------------------------
    // Crop builders (local space: y up, ground at y = 0)
    // ------------------------------------------------------------------
    static void AddMound(Model m)
    {
        if (!IncludeMound) return;
        m.Add("Soil", Sphere(0.55f, 0.22f, 0.55f, 3, 9));
    }

    static void BuildCarrot(Model m, Stage s)
    {
        AddMound(m);
        var rng = new Rng(11);
        var g = new Vector3(0f, G, 0f);

        switch (s)
        {
            case Stage.Planted:
                LeafRing(m, "LeafLight", 2, g, 0.14f, 0.07f, 0.15f, 40f);
                break;

            case Stage.Growing:
                m.Add("Orange", Sphere(0.06f, 0.04f, 0.06f, 3, 6), Pos(new Vector3(0f, G + 0.01f, 0f)));
                LeafRing(m, "LeafGreen", 6, g + new Vector3(0f, 0.02f, 0f), 0.32f, 0.07f, 0.10f, 58f, 15f, rng, 8f);
                break;

            default: // Mature
                m.Add("Orange", Frustum(0.015f, 0.09f, 0.30f, 7), Pos(new Vector3(0f, 0.06f, 0f)));
                LeafRing(m, "LeafGreen", 9, new Vector3(0f, 0.34f, 0f), 0.50f, 0.09f, 0.18f, 62f, 0f, rng, 8f);
                LeafRing(m, "LeafDark", 5, new Vector3(0f, 0.34f, 0f), 0.38f, 0.08f, 0.10f, 76f, 20f, rng, 8f);
                break;
        }
    }

    static void BuildCorn(Model m, Stage s)
    {
        AddMound(m);
        var rng = new Rng(21);

        float h, len, wid;
        int leaves;
        switch (s)
        {
            case Stage.Planted:
                LeafRing(m, "LeafLight", 2, new Vector3(0f, G, 0f), 0.13f, 0.04f, 0.10f, 68f);
                return;
            case Stage.Growing:
                h = 0.75f; leaves = 5; len = 0.42f; wid = 0.11f;
                break;
            default:
                h = 1.5f; leaves = 8; len = 0.60f; wid = 0.14f;
                break;
        }

        float baseY = G - 0.03f;
        m.Add("Stem", Frustum(0.05f, 0.028f, h, 6), Pos(new Vector3(0f, baseY, 0f)));

        for (int i = 0; i < leaves; i++)
        {
            float t = (i + 0.5f) / leaves;
            float y = G + 0.10f + t * (h - 0.25f);
            float yaw = i * 137.5f;
            float l = len * (1.1f - 0.4f * t);
            m.Add("LeafGreen", Leaf(l, wid, 0.5f), LeafPose(new Vector3(0f, y, 0f), 42f, yaw));
        }

        if (s == Stage.Mature)
        {
            // Cobs
            for (int c = 0; c < 2; c++)
            {
                float y = G + 0.65f + c * 0.30f;
                float yaw = c * 180f + 30f;
                var cm = Lean(new Vector3(0f, y, 0f), 38f, yaw);
                m.Add("Husk", Frustum(0.075f, 0.05f, 0.13f, 6), cm);
                m.Add("CornYellow", Frustum(0.052f, 0.04f, 0.22f, 6), cm * Pos(new Vector3(0f, 0.12f, 0f)));
                m.Add("CornYellow", Sphere(0.04f, 0.035f, 0.04f, 2, 6), cm * Pos(new Vector3(0f, 0.34f, 0f)));
            }
            // Tassel
            LeafRing(m, "WheatGold", 5, new Vector3(0f, baseY + h - 0.02f, 0f), 0.20f, 0.025f, 0.20f, 78f, 0f, rng, 10f);
        }
    }

    static void BuildWheat(Model m, Stage s)
    {
        AddMound(m);
        var rng = new Rng(31);

        switch (s)
        {
            case Stage.Planted:
                LeafRing(m, "LeafLight", 6, new Vector3(0f, G, 0f), 0.10f, 0.03f, 0.10f, 60f, 0f, rng, 10f);
                break;

            case Stage.Growing:
                for (int i = 0; i < 16; i++)
                {
                    float a = (float)rng.NextDouble() * Mathf.PI * 2f;
                    float r = 0.30f * Mathf.Sqrt((float)rng.NextDouble());
                    var p = new Vector3(Mathf.Cos(a) * r, G, Mathf.Sin(a) * r);
                    float yaw = 90f - a * Mathf.Rad2Deg + J(rng, 25f);
                    m.Add("LeafGreen", Leaf(0.38f + J(rng, 0.06f), 0.035f, 0.25f), LeafPose(p, 62f + J(rng, 10f), yaw));
                }
                break;

            default: // Mature
                for (int i = 0; i < 14; i++)
                {
                    float a = (float)rng.NextDouble() * Mathf.PI * 2f;
                    float r = 0.34f * Mathf.Sqrt((float)rng.NextDouble());
                    var p = new Vector3(Mathf.Cos(a) * r, G - 0.02f, Mathf.Sin(a) * r);
                    float yaw = 90f - a * Mathf.Rad2Deg + J(rng, 20f);
                    float lean = 4f + (float)rng.NextDouble() * 10f;
                    float h = 0.70f + (float)rng.NextDouble() * 0.20f;
                    var cm = Lean(p, lean, yaw);

                    m.Add("WheatStalk", Frustum(0.012f, 0.008f, h, 4), cm);
                    m.Add("WheatGold", Sphere(0.035f, 0.12f, 0.035f, 3, 5), cm * Pos(new Vector3(0f, h + 0.05f, 0f)));
                    m.Add("WheatGold", Frustum(0.015f, 0f, 0.12f, 4), cm * Pos(new Vector3(0f, h + 0.15f, 0f)));
                }
                for (int i = 0; i < 6; i++)
                {
                    float a = (float)rng.NextDouble() * Mathf.PI * 2f;
                    float r = 0.25f * Mathf.Sqrt((float)rng.NextDouble());
                    var p = new Vector3(Mathf.Cos(a) * r, G, Mathf.Sin(a) * r);
                    float yaw = 90f - a * Mathf.Rad2Deg;
                    m.Add("LeafGreen", Leaf(0.35f, 0.03f, 0.25f), LeafPose(p, 60f, yaw));
                }
                break;
        }
    }

    static void BuildPotato(Model m, Stage s)
    {
        AddMound(m);
        var rng = new Rng(41);

        switch (s)
        {
            case Stage.Planted:
                LeafRing(m, "LeafGreen", 3, new Vector3(0f, G, 0f), 0.12f, 0.09f, 0.10f, 35f, 0f, rng, 6f);
                break;

            case Stage.Growing:
                PotatoStems(m, rng, 5, 0.18f, 0.75f);
                break;

            default: // Mature
                PotatoStems(m, rng, 6, 0.32f, 1f);

                // Flowers
                for (int f = 0; f < 3; f++)
                {
                    float yaw = f * 120f + 20f;
                    var cm = Lean(new Vector3(0f, G - 0.02f, 0f), 15f, yaw);
                    m.Add("Stem", Frustum(0.010f, 0.007f, 0.42f, 4), cm);
                    m.Add("PotatoFlower", Sphere(0.045f, 0.02f, 0.045f, 2, 6), cm * Pos(new Vector3(0f, 0.42f, 0f)));
                    m.Add("FlowerCenter", Sphere(0.02f, 0.02f, 0.02f, 2, 5), cm * Pos(new Vector3(0f, 0.435f, 0f)));
                }

                // Tubers poking out of the mound
                for (int i = 0; i < 3; i++)
                {
                    float a = (i * 120f + 40f) * Mathf.Deg2Rad;
                    var p = new Vector3(Mathf.Cos(a) * 0.38f, 0.12f, Mathf.Sin(a) * 0.38f);
                    var rot = Quaternion.Euler(0f, J(rng, 60f) + i * 70f, 0f);
                    m.Add("PotatoTan", Sphere(0.10f, 0.07f, 0.08f, 3, 7), Matrix4x4.TRS(p, rot, Vector3.one));
                }
                break;
        }
    }

    static void PotatoStems(Model m, Rng rng, int count, float baseH, float leafScale)
    {
        for (int i = 0; i < count; i++)
        {
            float yaw = i * (360f / count) + J(rng, 10f);
            float lean = 22f + J(rng, 8f);
            float h = baseH + J(rng, 0.04f);
            var cm = Lean(new Vector3(0f, G - 0.02f, 0f), lean, yaw);
            m.Add("Stem", Frustum(0.016f, 0.010f, h, 5), cm);

            var top = new Vector3(0f, h, 0f);
            var mid = new Vector3(0f, h * 0.55f, 0f);
            m.Add("LeafGreen", Leaf(0.20f * leafScale, 0.13f * leafScale, 0.15f), cm * LeafPose(top, 25f, 0f));
            m.Add("LeafGreen", Leaf(0.17f * leafScale, 0.11f * leafScale, 0.15f), cm * LeafPose(top, 35f, 55f));
            m.Add("LeafGreen", Leaf(0.17f * leafScale, 0.11f * leafScale, 0.15f), cm * LeafPose(top, 35f, -55f));
            m.Add("LeafDark", Leaf(0.15f * leafScale, 0.10f * leafScale, 0.15f), cm * LeafPose(mid, 40f, 80f));
            m.Add("LeafDark", Leaf(0.15f * leafScale, 0.10f * leafScale, 0.15f), cm * LeafPose(mid, 40f, -80f));
        }
    }

    static void BuildCabbage(Model m, Stage s)
    {
        AddMound(m);
        var rng = new Rng(51);
        var g = new Vector3(0f, G, 0f);

        switch (s)
        {
            case Stage.Planted:
                LeafRing(m, "LeafLight", 2, g, 0.10f, 0.10f, 0.12f, 30f);
                break;

            case Stage.Growing:
                LeafRing(m, "LeafGreen", 6, g, 0.30f, 0.26f, 0.25f, 40f, 0f, rng, 6f);
                m.Add("CabbageLight", Sphere(0.10f, 0.09f, 0.10f, 3, 7), Pos(new Vector3(0f, G + 0.10f, 0f)));
                break;

            default: // Mature
                m.Add("CabbageLight", Sphere(0.24f, 0.22f, 0.24f, 4, 9), Pos(new Vector3(0f, G + 0.20f, 0f)));
                LeafRing(m, "LeafDark", 7, g, 0.38f, 0.36f, 0.20f, 50f, 0f, rng, 5f);
                LeafRing(m, "LeafGreen", 6, g + new Vector3(0f, 0.04f, 0f), 0.34f, 0.30f, 0.10f, 68f, 25f, rng, 5f);
                break;
        }
    }

    // ------------------------------------------------------------------
    // Helpers for placing parts
    // ------------------------------------------------------------------
    static float J(Rng r, float amt)
    {
        return r == null ? 0f : ((float)r.NextDouble() * 2f - 1f) * amt;
    }

    static Matrix4x4 Pos(Vector3 p)
    {
        return Matrix4x4.TRS(p, Quaternion.identity, Vector3.one);
    }

    // Leans an upward-pointing (+Y) part by 'lean' degrees toward direction 'yaw'.
    static Matrix4x4 Lean(Vector3 p, float lean, float yaw)
    {
        return Matrix4x4.TRS(p, Quaternion.Euler(lean, yaw, 0f), Vector3.one);
    }

    // Leaves are modeled along +Z; this tilts them up by 'pitchUp' degrees.
    static Matrix4x4 LeafPose(Vector3 p, float pitchUp, float yaw)
    {
        return Matrix4x4.TRS(p, Quaternion.Euler(-pitchUp, yaw, 0f), Vector3.one);
    }

    static void LeafRing(Model m, string mat, int count, Vector3 origin,
                         float length, float width, float droop, float pitch,
                         float yawStart = 0f, Rng rng = null, float jitter = 0f)
    {
        for (int i = 0; i < count; i++)
        {
            float yaw = yawStart + i * 360f / count + J(rng, jitter);
            float p = pitch + J(rng, jitter);
            float len = length * (1f + J(rng, jitter) * 0.01f);
            m.Add(mat, Leaf(len, width, droop), LeafPose(origin, p, yaw));
        }
    }

    // ------------------------------------------------------------------
    // Mesh data (flat shaded: every triangle has its own vertices)
    // ------------------------------------------------------------------
    class MeshData
    {
        public readonly List<Vector3> V = new List<Vector3>();
        public readonly List<int> T = new List<int>();

        public void Tri(Vector3 a, Vector3 b, Vector3 c)
        {
            int i = V.Count;
            V.Add(a); V.Add(b); V.Add(c);
            T.Add(i); T.Add(i + 1); T.Add(i + 2);
        }

        // Triangle whose normal points away from 'inside'.
        public void TriOut(Vector3 a, Vector3 b, Vector3 c, Vector3 inside)
        {
            var n = Vector3.Cross(b - a, c - a);
            if (n.sqrMagnitude < 1e-10f) return;
            if (Vector3.Dot(n, (a + b + c) / 3f - inside) < 0f) Tri(a, c, b);
            else Tri(a, b, c);
        }

        // Triangle whose normal points roughly along 'dir'.
        public void TriDir(Vector3 a, Vector3 b, Vector3 c, Vector3 dir)
        {
            var n = Vector3.Cross(b - a, c - a);
            if (n.sqrMagnitude < 1e-10f) return;
            if (Vector3.Dot(n, dir) < 0f) Tri(a, c, b);
            else Tri(a, b, c);
        }

        public void QuadDir(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 dir)
        {
            TriDir(a, b, c, dir);
            TriDir(a, c, d, dir);
        }

        public void Append(MeshData other, Matrix4x4 m)
        {
            int off = V.Count;
            foreach (var p in other.V) V.Add(m.MultiplyPoint3x4(p));
            foreach (var i in other.T) T.Add(i + off);
        }

        public Mesh ToMesh(string name)
        {
            var mesh = new Mesh { name = name };
            mesh.SetVertices(V);
            mesh.SetTriangles(T, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }

    // A crop model = one mesh per material key.
    class Model
    {
        public readonly Dictionary<string, MeshData> Parts = new Dictionary<string, MeshData>();

        public void Add(string mat, MeshData shape)
        {
            Add(mat, shape, Matrix4x4.identity);
        }

        public void Add(string mat, MeshData shape, Matrix4x4 local)
        {
            MeshData md;
            if (!Parts.TryGetValue(mat, out md))
            {
                md = new MeshData();
                Parts[mat] = md;
            }
            var root = Matrix4x4.Scale(Vector3.one * GlobalScale);
            md.Append(shape, root * local);
        }
    }

    // ------------------------------------------------------------------
    // Primitives
    // ------------------------------------------------------------------

    // Cone / cylinder pointing +Y, base at y = 0. Set rt = 0 for a cone.
    static MeshData Frustum(float rb, float rt, float h, int sides)
    {
        var d = new MeshData();
        var inside = new Vector3(0f, h * 0.5f, 0f);
        var bc = Vector3.zero;
        var tc = new Vector3(0f, h, 0f);

        for (int i = 0; i < sides; i++)
        {
            float a0 = Mathf.PI * 2f * i / sides;
            float a1 = Mathf.PI * 2f * (i + 1) / sides;
            var b0 = new Vector3(Mathf.Cos(a0) * rb, 0f, Mathf.Sin(a0) * rb);
            var b1 = new Vector3(Mathf.Cos(a1) * rb, 0f, Mathf.Sin(a1) * rb);
            var t0 = new Vector3(Mathf.Cos(a0) * rt, h, Mathf.Sin(a0) * rt);
            var t1 = new Vector3(Mathf.Cos(a1) * rt, h, Mathf.Sin(a1) * rt);

            d.TriOut(b0, b1, t1, inside);
            d.TriOut(b0, t1, t0, inside);
            d.TriOut(bc, b0, b1, inside);
            if (rt > 0f) d.TriOut(tc, t0, t1, inside);
        }
        return d;
    }

    static Vector3 SpherePt(float rx, float ry, float rz, int r, int s, int rings, int segs)
    {
        float phi = Mathf.PI * r / rings;
        float th = Mathf.PI * 2f * s / segs;
        return new Vector3(
            rx * Mathf.Sin(phi) * Mathf.Cos(th),
            ry * Mathf.Cos(phi),
            rz * Mathf.Sin(phi) * Mathf.Sin(th));
    }

    // Low-poly ellipsoid centered on the origin.
    static MeshData Sphere(float rx, float ry, float rz, int rings, int segs)
    {
        var d = new MeshData();
        for (int r = 0; r < rings; r++)
        {
            for (int s = 0; s < segs; s++)
            {
                var a = SpherePt(rx, ry, rz, r, s, rings, segs);
                var b = SpherePt(rx, ry, rz, r, s + 1, rings, segs);
                var c = SpherePt(rx, ry, rz, r + 1, s + 1, rings, segs);
                var e = SpherePt(rx, ry, rz, r + 1, s, rings, segs);
                d.TriOut(a, b, c, Vector3.zero);
                d.TriOut(a, c, e, Vector3.zero);
            }
        }
        return d;
    }

    // Double-sided leaf along +Z with a center crease and a downward droop.
    static MeshData Leaf(float length, float width, float droop, int segs = 4)
    {
        var d = new MeshData();
        var L = new Vector3[segs + 1];
        var C = new Vector3[segs + 1];
        var R = new Vector3[segs + 1];

        for (int i = 0; i <= segs; i++)
        {
            float t = (float)i / segs;
            float hw = width * 0.5f * Mathf.Sin(Mathf.PI * Mathf.Pow(t, 0.65f));
            float y = -droop * t * t * length;
            float z = t * length;
            L[i] = new Vector3(-hw, y, z);
            C[i] = new Vector3(0f, y + hw * 0.3f, z);
            R[i] = new Vector3(hw, y, z);
        }

        for (int i = 0; i < segs; i++)
        {
            d.QuadDir(L[i], C[i], C[i + 1], L[i + 1], Vector3.up);
            d.QuadDir(C[i], R[i], R[i + 1], C[i + 1], Vector3.up);
            d.QuadDir(L[i], C[i], C[i + 1], L[i + 1], Vector3.down);
            d.QuadDir(C[i], R[i], R[i + 1], C[i + 1], Vector3.down);
        }
        return d;
    }

    // ------------------------------------------------------------------
    // Asset output
    // ------------------------------------------------------------------
    static void SavePrefab(string crop, Stage stage, Model model)
    {
        string baseName = crop + "_" + stage;
        var root = new GameObject(baseName);

        foreach (var part in model.Parts)
        {
            var mesh = part.Value.ToMesh(baseName + "_" + part.Key);
            mesh = SaveMesh(mesh, GenRoot + "/Meshes/" + mesh.name + ".asset");

            var go = new GameObject(part.Key);
            go.transform.SetParent(root.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = GetMaterial(part.Key);
        }

        PrefabUtility.SaveAsPrefabAsset(root, OutRoot + "/" + baseName + ".prefab");
        Object.DestroyImmediate(root);
    }

    // Overwrites in place when the asset exists so existing prefab references stay valid.
    static Mesh SaveMesh(Mesh mesh, string path)
    {
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null)
        {
            EditorUtility.CopySerialized(mesh, existing);
            Object.DestroyImmediate(mesh);
            return existing;
        }
        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }

    static Material GetMaterial(string key)
    {
        string path = GenRoot + "/Materials/Crop_" + key + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat != null) return mat;

        mat = new Material(PickShader());
        ApplyLook(mat, key);
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    static void ApplyLook(Material mat, string key)
    {
        mat.color = Palette[key];
        if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0.08f);   // Standard
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.08f);   // URP / HDRP
        if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);
    }

    // Picks a shader that matches the render pipeline the project is really using.
    static Shader PickShader()
    {
        Shader s = null;
        var rp = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
        if (rp != null)
        {
            string rpName = rp.GetType().Name;
            if (rpName.Contains("Universal")) s = Shader.Find("Universal Render Pipeline/Lit");
            else if (rpName.Contains("HD")) s = Shader.Find("HDRP/Lit");
            if (s == null) s = rp.defaultShader;
        }
        if (s == null) s = Shader.Find("Standard");
        if (s == null) s = Shader.Find("Legacy Shaders/Diffuse");
        return s;
    }

    // Use this on materials that were already generated and show up pink.
    [MenuItem("Tools/Crops/Fix Material Shaders")]
    public static void FixMaterialShaders()
    {
        var shader = PickShader();
        int fixedCount = 0;
        foreach (var key in Palette.Keys)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(GenRoot + "/Materials/Crop_" + key + ".mat");
            if (mat == null) continue;
            mat.shader = shader;
            ApplyLook(mat, key);
            EditorUtility.SetDirty(mat);
            fixedCount++;
        }
        AssetDatabase.SaveAssets();
        Debug.Log("CropGenerator: set " + fixedCount + " materials to shader '" + shader.name + "'");
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
    }
}