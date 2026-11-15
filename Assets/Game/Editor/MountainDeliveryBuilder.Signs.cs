using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static partial class MountainDeliveryBuilder
{
    const string SignFolder = "Assets/Game/Art/RoadSigns/";
    const string SignRoot = "California route signs";

    [MenuItem("Tools/Mountain Delivery/Improve Road Signs (keeps terrain and gameplay)")]
    public static void ImproveRoadSigns()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
        for (int n = 0; n < SceneManager.sceneCount; n++)
            if (SceneManager.GetSceneAt(n).isDirty) throw new InvalidOperationException("Save scene edits first.");
        layout = JsonUtility.FromJson<Layout>(File.ReadAllText(Folder + "/ReferenceLayout.json"));
        route.Clear(); distances.Clear(); MakeRoute();
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var level = scene.GetRootGameObjects().Single(g => g.name.StartsWith("Mountain Delivery")).transform;
        RemoveLegacyChevrons(level);
        RoadSigns(level);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        var prefab = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            RoadSigns(prefab.transform);
            PrefabUtility.SaveAsPrefabAsset(prefab, PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(prefab); }
        AssetDatabase.SaveAssets();
    }

    static int AtDistance(float distance)
    {
        int i = distances.BinarySearch(distance);
        return Mathf.Clamp(i < 0 ? ~i : i, firstInside + 1, lastInside - 1);
    }

    static void RoadSigns(Transform level)
    {
        var old = level.Find(SignRoot);
        if (old != null) Object.DestroyImmediate(old.gameObject);
        var parent = new GameObject(SignRoot).transform;
        parent.SetParent(level, false);
        // Every panel's local +X is the driver's right, and local -Z faces the approaching driver.
        int start = AtDistance(distances[firstInside] + 24);
        RoadPanel(parent, "Scenic route - summit", "ScenicRoute", start, 1, 7.4f, new Vector2(2.8f, 2.8f * 26 / 48));
        RoadPanel(parent, "Please don't drink and drive", "DontDrinkAndDrive", AtDistance(startExtension + coreLength * .53f), 1, 7.4f, new Vector2(2.1f, 1.75f));

        // Measure along the road, not across the chord of a bend (which exaggerates its grade).
        int steep = start; float grade = 0;
        for (int i = start; i < lastInside - 1; i++)
        {
            float g = (route[i].y - route[i + 1].y) / Mathf.Max(.01f, Flat(route[i + 1] - route[i]).magnitude);
            if (g > grade) { grade = g; steep = i; }
        }
        int warning = AtDistance(Mathf.Max(distances[steep] + 8, startExtension + coreLength * .19f + 50));
        RoadPanel(parent, "Watch downhill speed - upper descent", "WatchDownhillSpeed", warning, 1, 7.5f, Vector2.one * 2.25f, true);

        foreach (float fraction in new[] { .19f, .77f })
        {
            int bay = AtDistance(startExtension + coreLength * fraction);
            // One tall post: parking instruction above a smaller supplementary information panel.
            var parking = RoadPanel(parent, "Park off pavement - turnout " + fraction, "ParkOffPavement", bay, 1, 14, new Vector2(2.1f, 1.3125f), mountingHeight: 3.45f);
            RoadPanel(parking, "No loitering - turnout " + fraction, "NoLoitering", bay, 1, 14, new Vector2(1.4f, .875f), addPost: false, mountingHeight: 2.25f);
        }
        foreach (float fraction in new[] { .10f, .46f, .85f })
        {
            int i = AtDistance(startExtension + coreLength * fraction);
            RoadPanel(parent, "Do not pass - downhill " + fraction, "DoNotPass", i, 1, 7.4f, new Vector2(1.8f, 1.8f * 30 / 42));
            int uphill = fraction == .10f ? AtDistance(distances[i] - 32) : i;
            RoadPanel(parent, "Do not pass - uphill " + fraction, "DoNotPass", uphill, -1, -7.4f, new Vector2(1.8f, 1.8f * 30 / 42));
        }
        for (int i = 16; i < route.Count - 16; i += 12)
        {
            var incoming = Flat(route[i] - route[i - 8]);
            var outgoing = Flat(route[i + 8] - route[i]);
            if (Vector3.Angle(incoming, outgoing) < 18 || !Inside(route[i])) continue;
            bool right = Vector3.Cross(incoming, outgoing).y > 0;
            float outside = right ? -7.5f : 7.5f;
            // Opposite approaches see opposite turns. Separate faces prevent mirrored arrows.
            RoadPanel(parent, "Chevron downhill " + i + (right ? " right" : " left"), right ? "ChevronRight" : "ChevronLeft", i, 1, outside, new Vector2(.95f, 1.425f));
            RoadPanel(parent, "Chevron uphill " + i + (right ? " left" : " right"), right ? "ChevronLeft" : "ChevronRight", i, -1, outside, new Vector2(.95f, 1.425f), addPost: false);
        }
    }

    static Material SignMaterial(string name, bool face)
    {
        string path = SignFolder + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m != null) return m;
        m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name, color = face ? Color.white : new Color(.48f, .53f, .53f) };
        m.SetFloat("_Smoothness", .18f);
        if (face)
        {
            string imagePath = SignFolder + name + ".png";
            var importer = (TextureImporter)AssetImporter.GetAtPath(imagePath);
            importer.textureType = TextureImporterType.Default;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.anisoLevel = 4;
            importer.SaveAndReimport();
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(imagePath));
        }
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    static Mesh PlateMesh(bool diamond)
    {
        string name = diamond ? "DiamondPlate" : "RoundedPlate";
        // All panels share a unit mesh; dimensions are set on the transform.
        string path = SignFolder + name + ".asset";
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (mesh != null && mesh.vertexCount == (diamond ? 5 : 13)) return mesh;
        var points = new List<Vector3>();
        if (diamond) points.AddRange(new[] { new Vector3(0,.5f,0), new Vector3(.5f,0,0), new Vector3(0,-.5f,0), new Vector3(-.5f,0,0) });
        else
        {
            for (int corner = 0; corner < 4; corner++)
            {
                float cx = corner == 0 || corner == 3 ? .45f : -.45f;
                float cy = corner < 2 ? .45f : -.45f;
                for (int s = 0; s <= 2; s++)
                {
                    float angle = (corner * 90 + s * 45) * Mathf.Deg2Rad;
                    points.Add(new Vector3(cx + .05f * Mathf.Cos(angle), cy + .05f * Mathf.Sin(angle), 0));
                }
            }
        }
        var vertices = new List<Vector3> { Vector3.zero }; vertices.AddRange(points);
        var triangles = new List<int>();
        for (int i = 0; i < points.Count; i++)
        {
            int a = i + 1, b = (i + 1) % points.Count + 1;
            // Diamond is clockwise; rounded outline is counterclockwise. Front normal is -Z.
            triangles.Add(0); triangles.Add(diamond ? a : b); triangles.Add(diamond ? b : a);
        }
        bool create = mesh == null;
        if (create) mesh = new Mesh { name = name }; else mesh.Clear();
        mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0);
        mesh.SetUVs(0, vertices.Select(v => new Vector2(v.x + .5f, v.y + .5f)).ToList());
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        if (create) AssetDatabase.CreateAsset(mesh, path); else EditorUtility.SetDirty(mesh);
        return mesh;
    }

    static Transform RoadPanel(Transform parent, string name, string face, int i, int travel, float lateral, Vector2 size, bool diamond = false, bool addPost = true, float mountingHeight = 0)
    {
        var go = new GameObject(name).transform; go.SetParent(parent, false);
        Vector3 heading = Flat(route[Mathf.Min(i + 5, route.Count - 1)] - route[Mathf.Max(i - 5, 0)]).normalized * travel;
        // The guardrail follows the widening turnout, not the asphalt edge.
        float railEdge = 5 + (lateral > 0 ? Bay(T(i)) : 0) + 1.35f;
        lateral = Mathf.Sign(lateral) * Mathf.Max(Mathf.Abs(lateral), railEdge + .65f);
        var p = route[i] + Right(i) * lateral;
        p.y = Height(p.x, p.z);
        go.SetPositionAndRotation(p, Quaternion.LookRotation(heading));
        float center = mountingHeight > 0 ? mountingHeight : 2.35f + size.y * .15f;
        var steel = SignMaterial("GalvanizedSteel", false);
        if (addPost)
        {
            var post = GameObject.CreatePrimitive(PrimitiveType.Cube);
            post.name = "Galvanized post"; Object.DestroyImmediate(post.GetComponent<Collider>());
            post.transform.SetParent(go, false); post.transform.localPosition = new Vector3(0, center / 2, 0);
            post.transform.localScale = new Vector3(.085f, center, .055f); post.GetComponent<Renderer>().sharedMaterial = steel;
        }
        var plate = new GameObject("Sign face").transform; plate.SetParent(go, false);
        plate.localPosition = new Vector3(0, center, -.06f);
        plate.localScale = new Vector3(size.x, size.y, 1);
        plate.gameObject.AddComponent<MeshFilter>().sharedMesh = PlateMesh(diamond);
        var renderer = plate.gameObject.AddComponent<MeshRenderer>(); renderer.sharedMaterial = SignMaterial(face, true);
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        // A separate reverse mesh has the metal material; ink never shows through the back.
        var back = Object.Instantiate(plate.gameObject, go); back.name = "Metal back";
        back.transform.localPosition += Vector3.forward * .025f;
        back.transform.localRotation = Quaternion.Euler(0,180,0);
        back.GetComponent<MeshRenderer>().sharedMaterial = steel;
        // As with the backdrop panels, keep the road's readable mesh out of sign batching.
        foreach (Transform t in go.GetComponentsInChildren<Transform>())
            GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ReflectionProbeStatic);
        return go;
    }

    // Older levels bake signs into the tree/asphalt batches. Match their exact generated triangles,
    // preserving the remaining scenery, mesh IDs, colliders, and every hand edit in the level.
    static void RemoveLegacyChevrons(Transform level)
    {
        var legacy = new Batch();
        for (int i = 16; i < route.Count - 16; i += 12)
        {
            if (Vector3.Angle(Flat(route[i] - route[i - 8]), Flat(route[i + 8] - route[i])) < 18) continue;
            float turn = Vector3.Cross(route[i] - route[i - 8], route[i + 8] - route[i]).y;
            int side = turn > 0 ? -1 : 1;
            var p = route[i] + Right(i) * side * 7.2f;
            var q = Quaternion.LookRotation(-Flat(route[i] - route[i - 8]));
            legacy.Box(p + Vector3.up * 1.5f, new Vector3(.18f, 3, .18f), q);
            legacy.Box(p + Vector3.up * 2.65f, new Vector3(2.1f, 1.3f, .14f), q);
            var front = p + Vector3.up * 2.65f + q * new Vector3(0, 0, .09f);
            float direction = turn > 0 ? 1 : -1;
            foreach (int half in new[] { -1, 1 })
                Beam(legacy, front + q * new Vector3(-direction * .35f, half * .43f, 0), front + q * new Vector3(direction * .3f, 0, 0), .20f, .20f);
        }
        string Vertex(Vector3 v) => Mathf.RoundToInt(v.x * 1000) + "," + Mathf.RoundToInt(v.y * 1000) + "," + Mathf.RoundToInt(v.z * 1000);
        string Key(Vector3 a, Vector3 b, Vector3 c) => Vertex(a) + "/" + Vertex(b) + "/" + Vertex(c);
        var keys = new HashSet<string>();
        for (int n = 0; n < legacy.triangles.Count; n += 3)
            keys.Add(Key(legacy.vertices[legacy.triangles[n]], legacy.vertices[legacy.triangles[n + 1]], legacy.vertices[legacy.triangles[n + 2]]));
        int removed = 0;
        foreach (var filter in level.GetComponentsInChildren<MeshFilter>())
        {
            if (!(filter.name.StartsWith("Wood_") || filter.name.StartsWith("Yellow_") || filter.name.StartsWith("Road_"))) continue;
            var mesh = filter.sharedMesh; var vertices = mesh.vertices; var tris = mesh.triangles;
            var kept = new List<int>();
            for (int n = 0; n < tris.Length; n += 3)
                if (keys.Contains(Key(vertices[tris[n]], vertices[tris[n + 1]], vertices[tris[n + 2]]))) removed++;
                else { kept.Add(tris[n]); kept.Add(tris[n + 1]); kept.Add(tris[n + 2]); }
            if (kept.Count == tris.Length) continue;
            mesh.SetTriangles(kept, 0); EditorUtility.SetDirty(mesh);
        }
        Debug.Log("Removed " + removed + " legacy chevron triangles; expected " + legacy.triangles.Count / 3 + " on first upgrade.");
    }
}
