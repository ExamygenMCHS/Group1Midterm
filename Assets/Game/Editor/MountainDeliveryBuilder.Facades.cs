using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

// The edge of the world. Three pieces, all outside the map border and none of them drivable (the border walls and road caps keep the truck in):
//  1. Beyond Start / Beyond Finish: real terrain, road, guardrails and pines carrying on from each road end all the way to the ring,
//     in the map's own materials. The road meets the painted road on the ring's panel, and the painting carries it on to the horizon.
//  2. Beyond ground: a coarse low-poly ground ring from the map border out to the backdrop, so the gap on every other side is filled.
//  3. Backdrop: one ring of unlit painted panels (a 24-sided circle) around the whole map, showing distant mountains and haze.
//     The pictures are rendered in the Editor from the point where the two road headings meet (so the painted road is end-on, just
//     as a driver on the last stretch sees it), with the level's own materials and sun, of an imaginary landscape beyond the ring. The ground in pieces 1 and 2 and the imaginary landscape share one height function (GroundHeight),
//     so the real ground meets the paintings at the base of each panel without a seam.
// Bake Facade Images writes the PNGs and FacadeLayout.json into Facade/, which a rebuild keeps. Facades() builds the rest.
public static partial class MountainDeliveryBuilder
{
    const string FacadeFolder = Folder + "/Facade";
    const string FacadeLayoutPath = FacadeFolder + "/FacadeLayout.json";
    const int BackdropSides = 24, Supersample = 2, Tile = 1024, RingSectors = 288, RingRows = 20;
    const float BackdropPpm = 4f, EyeHeight = 3.55f, TruckCameraFar = 1100f;
    // The real terrain beyond a road end runs out to the backdrop: TerrainDepth m of it between the map border and the ring along the
    // road, ApronHalfWidth m of it each side of the road.
    const float TerrainDepth = 220f, ApronHalfWidth = 100f;
    static readonly Color SkyColor = new Color(.72f, .81f, .82f);

    [Serializable] class FacadePanel { public string name, texture; public Vector3 a, b; public float bottom, top; }
    [Serializable] class FacadeLayout { public Vector3 center, eye; public float radius; public List<FacadePanel> panels = new List<FacadePanel>(); }
    class FacadeEnd { public string name; public bool start; public int inside; public Vector3 outward, crossing; public float plane, reach; }
    class Segment { public string name; public Vector3 a, b; public float ppm; }
    class Extension { public List<Vector3> points = new List<Vector3>(); public List<float> distance = new List<float>(); }

    static List<EndFrame> bakeFrames;
    static Vector3 bakeCenter; static float bakeRadius; static int debugSeam = -1;

    // ---------- Putting everything in the level (part of Build, and of Apply Facades) ----------

    static void Facades()
    {
        random = new System.Random(4851);
        var ends = FacadeEnds(); var center = BackdropCenter(); float radius = BackdropRadius(ends);
        FacadeLayout data = File.Exists(FacadeLayoutPath) ? JsonUtility.FromJson<FacadeLayout>(File.ReadAllText(FacadeLayoutPath)) : null;
        if (data == null || data.panels.Count == 0) { Debug.LogWarning("Mountain Delivery: no backdrop images yet. Run Tools > Mountain Delivery > Bake Facade Images."); data = null; }
        else if (Mathf.Abs(data.radius - radius) > .5f) Debug.LogWarning("Mountain Delivery: the backdrop images were baked for a ring of radius " + data.radius + ", but the road ends now need " + radius + ". Run Bake Facade Images.");
        if (data != null) radius = data.radius;
        AssignReach(ends, center, radius);
        var frames = ends.Select(e => new EndFrame(e, ExtendRoad(e))).ToList();

        var group = new GameObject("Facades").transform; group.SetParent(root, false); group.gameObject.isStatic = true;
        if (data != null) Backdrop(group, data);

        var ground = new GameObject("Beyond ground").transform; ground.SetParent(group, false);
        var ring = new Dictionary<string, Batch>();
        RingGeometry(frames, center, radius, ring);
        RingDetails(frames, center, radius, ring);
        Emit(ground, "Beyond ground", ring);

        foreach (var frame in frames)
        {
            string name = "Beyond " + frame.end.name;
            var parent = new GameObject(name).transform; parent.SetParent(group, false);
            var batches = new Dictionary<string, Batch>();
            ApronGeometry(frame, frames, batches);
            ApronRoad(frame, batches);
            ApronTrees(frame, frames, batches, center, radius * Mathf.Cos(Mathf.PI / BackdropSides));
            Emit(parent, name, batches);
        }
    }

    static void Backdrop(Transform group, FacadeLayout data)
    {
        var parent = new GameObject("Backdrop").transform; parent.SetParent(group, false); parent.gameObject.isStatic = true;
        foreach (var p in data.panels)
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(FacadeFolder + "/" + p.texture);
            if (texture == null) { Debug.LogWarning("Missing backdrop image " + p.texture); continue; }
            string name = "Backdrop Panel " + p.name;
            var mesh = FacadeMesh(name);
            Vector3 a = new Vector3(p.a.x, p.bottom, p.a.z), b = new Vector3(p.b.x, p.bottom, p.b.z);
            mesh.vertices = new[] { a, b, new Vector3(b.x, p.top, b.z), new Vector3(a.x, p.top, a.z) };
            mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
            mesh.triangles = new[] { 0, 3, 2, 0, 2, 1 }; // a is on the left as seen from inside the ring, so this faces the map
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); EditorUtility.SetDirty(mesh);
            // Static for culling, but not batched: static batching would fold these quads into the road's combined
            // mesh, and the road must keep its own readable mesh (its asphalt UVs are checked in Play Mode).
            var go = new GameObject("Panel " + p.name); go.transform.SetParent(parent, false);
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ReflectionProbeStatic);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>(); renderer.sharedMaterial = FacadeMaterial(name, texture);
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off; renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }
    }

    // One renderer per material. Meshes are reused by name, so the scene and the prefab share them and re-applying does not pile up copies.
    static void Emit(Transform parent, string prefix, Dictionary<string, Batch> batches)
    {
        foreach (var pair in batches.OrderBy(p => p.Key))
        {
            if (pair.Value.vertices.Count == 0) continue;
            string key = pair.Key.Replace('_', ' ');
            var mesh = FacadeMesh(prefix + " " + key);
            mesh.indexFormat = pair.Value.vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(pair.Value.vertices); mesh.SetTriangles(pair.Value.triangles, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            if (pair.Key.StartsWith("Road")) DrivingFeelSetup.ProjectUVs(mesh); // the same asphalt texture as the drivable road
            EditorUtility.SetDirty(mesh);
            var go = new GameObject(key); go.transform.SetParent(parent, false);
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ReflectionProbeStatic);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>(); renderer.sharedMaterial = materials[pair.Key.Split('_')[0]];
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }
    }

    static Mesh FacadeMesh(string name)
    {
        string path = Folder + "/Baked/Geometry.asset";
        var mesh = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Mesh>().FirstOrDefault(m => m.name == name);
        if (mesh == null) { mesh = new Mesh { name = name }; AssetDatabase.AddObjectToAsset(mesh, path); }
        mesh.Clear(); return mesh;
    }
    static Material FacadeMaterial(string name, Texture2D texture)
    {
        string path = Folder + "/Baked/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null) { material = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = name }; AssetDatabase.CreateAsset(material, path); }
        material.SetTexture("_BaseMap", texture); material.mainTexture = texture; material.SetColor("_BaseColor", Color.white);
        EditorUtility.SetDirty(material); return material;
    }

    // Loads what Build() would have set up, for the menu items that run on an existing level.
    static void PrepareState()
    {
        layout = JsonUtility.FromJson<Layout>(File.ReadAllText(Folder + "/ReferenceLayout.json"));
        route.Clear(); distances.Clear(); MakeRoute();
        random = new System.Random(4851);
        materials.Clear();
        foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { Folder + "/Baked" }))
        { var m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid)); materials[m.name] = m; }
    }

    [MenuItem("Tools/Mountain Delivery/Apply Facades (keeps hand edits)")]
    public static void ApplyFacades()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
        PrepareState();
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var level = scene.GetRootGameObjects().First(r => r.name.StartsWith("Mountain Delivery")).transform;
        ReplaceFacades(level, true);
        // The truck camera must see the far side of the ring from the opposite corner of the map.
        foreach (var camera in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (camera.name == "Truck Camera") camera.farClipPlane = Mathf.Max(camera.farClipPlane, TruckCameraFar);
        EditorSceneManager.SaveScene(scene);
        var prefab = PrefabUtility.LoadPrefabContents(PrefabPath);
        try { ReplaceFacades(prefab.transform, false); PrefabUtility.SaveAsPrefabAsset(prefab, PrefabPath); }
        finally { PrefabUtility.UnloadPrefabContents(prefab); }
        AssetDatabase.SaveAssets();
        Debug.Log("Mountain Delivery facades applied to the scene and prefab.");
    }
    static void ReplaceFacades(Transform level, bool purge)
    {
        var old = level.Find("Facades"); if (old) Object.DestroyImmediate(old.gameObject);
        if (purge) PurgeFacadeAssets();
        root = level; Facades();
    }
    // Drops the meshes and materials of earlier facade layouts (once per apply: the scene pass makes the meshes, the prefab pass reuses them).
    static void PurgeFacadeAssets()
    {
        string geometry = Folder + "/Baked/Geometry.asset";
        var stale = AssetDatabase.LoadAllAssetsAtPath(geometry).OfType<Mesh>()
            .Where(m => m.name.StartsWith("Facade ") || m.name.StartsWith("Beyond ") || m.name.StartsWith("Backdrop ")).ToList();
        foreach (var m in stale) { AssetDatabase.RemoveObjectFromAsset(m); Object.DestroyImmediate(m, true); }
        foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { Folder + "/Baked" }))
        { string path = AssetDatabase.GUIDToAssetPath(guid); if (Path.GetFileName(path).StartsWith("Facade ")) AssetDatabase.DeleteAsset(path); }
    }

    // ---------- The ring: where it stands ----------

    static Vector3 BackdropCenter() { float c = (borderLow + borderHigh) / 2; return new Vector3(c, 0, c); }

    // Vertex radius of the 24-gon: TerrainDepth m of real terrain between each road end and the ring, and room for the map's corners.
    static float BackdropRadius(List<FacadeEnd> ends)
    {
        var c = BackdropCenter(); float far = (borderHigh - borderLow) * .5f * 1.4143f + 60;
        foreach (var e in ends) far = Mathf.Max(far, Flat(e.crossing - c).magnitude + TerrainDepth);
        return Mathf.Ceil(far / Mathf.Cos(Mathf.PI / BackdropSides) / 10) * 10;
    }

    // Distance along a ray from inside the ring to the ring's panel planes.
    static float PlaneDistance(Vector3 from, Vector3 dir, Vector3 center, float radius)
    {
        float best = float.MaxValue;
        for (int k = 0; k < BackdropSides; k++)
        {
            float a0 = k * Mathf.PI * 2 / BackdropSides, a1 = (k + 1) * Mathf.PI * 2 / BackdropSides;
            var a = center + new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)) * radius; var e = center + new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1)) * radius - a;
            float cross = dir.x * e.z - dir.z * e.x; if (Mathf.Abs(cross) < 1e-5f) continue;
            var d = a - from; float t = (d.x * e.z - d.z * e.x) / cross, u = (d.x * dir.z - d.z * dir.x) / cross;
            if (t > 0 && u >= 0 && u <= 1) best = Mathf.Min(best, t);
        }
        return best;
    }
    // plane: along the road to the ring. reach: how far the real terrain must go (the farthest of the road and its two sides), plus a margin.
    static void AssignReach(List<FacadeEnd> ends, Vector3 center, float radius)
    {
        foreach (var e in ends)
        {
            var right = Vector3.Cross(Vector3.up, e.outward);
            e.plane = PlaneDistance(e.crossing, e.outward, center, radius);
            float far = e.plane;
            foreach (float u in new[] { -ApronHalfWidth, ApronHalfWidth }) far = Mathf.Max(far, PlaneDistance(e.crossing + right * u, e.outward, center, radius));
            e.reach = far + 30;
        }
    }

    // The painting is exact from this point: where the two road headings meet behind the caps, so a driver on either last stretch is on
    // the line through the eye and sees the painted road end-on, exactly as painted. At about the height of the chase camera.
    static Vector3 BackdropEye(List<FacadeEnd> ends, Vector3 center)
    {
        var a = ends[0]; var b = ends[1];
        float cross = a.outward.x * b.outward.z - a.outward.z * b.outward.x; var xz = center;
        if (Mathf.Abs(cross) > .05f)
        {
            var d = b.crossing - a.crossing; float t = (d.x * b.outward.z - d.z * b.outward.x) / cross;
            xz = a.crossing + a.outward * t;
            Debug.Log("Mountain Delivery: road headings meet at " + xz + " (" + t + " m along the Start heading).");
        }
        float limit = (borderHigh - borderLow) * .45f;
        xz.x = center.x + Mathf.Clamp(xz.x - center.x, -limit, limit); xz.z = center.z + Mathf.Clamp(xz.z - center.z, -limit, limit);
        return new Vector3(xz.x, (a.crossing.y + b.crossing.y) / 2 + EyeHeight, xz.z);
    }

    static List<FacadeEnd> FacadeEnds()
    {
        var ends = new List<FacadeEnd>();
        foreach (bool start in new[] { true, false })
        {
            int i = start ? firstInside : lastInside, step = start ? -1 : 1;
            var dir = Flat(route[i + step] - route[i]).normalized; var p = route[i];
            float t = 1000;
            if (dir.x > .001f) t = Mathf.Min(t, (borderHigh - p.x) / dir.x); else if (dir.x < -.001f) t = Mathf.Min(t, (borderLow - p.x) / dir.x);
            if (dir.z > .001f) t = Mathf.Min(t, (borderHigh - p.z) / dir.z); else if (dir.z < -.001f) t = Mathf.Min(t, (borderLow - p.z) / dir.z);
            ends.Add(new FacadeEnd { name = start ? "Start" : "Finish", start = start, inside = i, outward = dir, crossing = p + dir * t });
        }
        return ends;
    }

    static List<Segment> BackdropSegments(Vector3 center, float radius, Vector3 eye)
    {
        var list = new List<Segment>();
        for (int k = 0; k < BackdropSides; k++)
        {
            float a0 = k * Mathf.PI * 2 / BackdropSides, a1 = (k + 1) * Mathf.PI * 2 / BackdropSides;
            var s = new Segment
            {
                name = k.ToString("D2"), ppm = BackdropPpm,
                a = center + new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)) * radius, b = center + new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1)) * radius
            };
            // Order each panel's ends so a is on the left as seen from the eye.
            var forward = PanelForward(eye, s.a, s.b);
            if (Vector3.Dot(s.b - s.a, Vector3.Cross(Vector3.up, forward)) < 0) { var t = s.a; s.a = s.b; s.b = t; }
            list.Add(s);
        }
        return list;
    }
    static Vector3 PanelForward(Vector3 eye, Vector3 a, Vector3 b)
    {
        var normal = Vector3.Cross(Vector3.up, Flat(b - a)).normalized;
        return Vector3.Dot(Flat(a - eye), normal) < 0 ? -normal : normal; // from the eye toward the panel
    }

    // ---------- Baking the pictures ----------

    [MenuItem("Tools/Mountain Delivery/Bake Facade Images (keeps hand edits)")]
    public static void BakeFacadeImages()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
        PrepareState();
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        // Only the land beyond each panel is drawn (the near clip plane is the panel), but hide anything that could cast into it.
        var hidden = new List<GameObject>();
        foreach (var r in scene.GetRootGameObjects())
        {
            if (r.name.StartsWith("Pickup") || r.name.StartsWith("Drop-off")) hidden.Add(r);
            var old = r.transform.Find("Facades"); if (old) hidden.Add(old.gameObject);
        }
        hidden.RemoveAll(g => !g.activeSelf); foreach (var g in hidden) g.SetActive(false);

        bool fog = RenderSettings.fog; var fogMode = RenderSettings.fogMode; var fogColor = RenderSettings.fogColor;
        float fogStart = RenderSettings.fogStartDistance, fogEnd = RenderSettings.fogEndDistance;
        GameObject world = null, cameraObject = null;
        var data = new FacadeLayout();
        try
        {
            var ends = FacadeEnds();
            data.center = BackdropCenter(); data.radius = BackdropRadius(ends); data.eye = BackdropEye(ends, data.center);
            AssignReach(ends, data.center, data.radius); bakeCenter = data.center; bakeRadius = data.radius;
            bakeFrames = ends.Select(e => new EndFrame(e, ExtendRoad(e))).ToList();
            var segments = BackdropSegments(data.center, data.radius, data.eye);
            world = BuildImaginaryWorld(bakeFrames, segments, data.eye);
            // Distance haze: the far land fades to the sky colour, starting at the ring (set per panel in RenderSegment).
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Linear; RenderSettings.fogColor = SkyColor;
            cameraObject = new GameObject("Facade bake camera") { hideFlags = HideFlags.HideAndDontSave };
            var cam = cameraObject.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = SkyColor; cam.allowHDR = false; cam.allowMSAA = false;
            Directory.CreateDirectory(FacadeFolder);
            // Overwrite the images in place so rebaking preserves their asset GUIDs.
            foreach (var s in segments)
            {
                string file = "Backdrop" + s.name + ".png";
                var panel = RenderSegment(cam, data.eye, s, FacadeFolder + "/" + file);
                panel.name = s.name; panel.texture = file; data.panels.Add(panel);
            }
            if (debugSeam >= 0) // ground truth at one seam: a normal camera at the eye looking at that polygon vertex
            {
                float ang = debugSeam * Mathf.PI * 2 / BackdropSides;
                var seam = data.center + new Vector3(Mathf.Cos(ang), 0, Mathf.Sin(ang)) * data.radius; var ray = Flat(seam - data.eye);
                var rtt = new RenderTexture(512, 512, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB); var tt = new Texture2D(512, 512, TextureFormat.RGB24, false);
                cam.ResetProjectionMatrix(); cam.fieldOfView = 24; cam.aspect = 1; cam.nearClipPlane = ray.magnitude; cam.farClipPlane = 6000;
                cam.transform.SetPositionAndRotation(data.eye, Quaternion.LookRotation(ray, Vector3.up));
                RenderSettings.fogStartDistance = 0; RenderSettings.fogEndDistance = 3300f * 6000f / (6000f - ray.magnitude);
                cam.targetTexture = rtt; cam.Render(); RenderTexture.active = rtt; tt.ReadPixels(new Rect(0, 0, 512, 512), 0, 0); tt.Apply(); RenderTexture.active = null;
                File.WriteAllBytes(Application.dataPath + "/../seam_truth.png", tt.EncodeToPNG()); cam.targetTexture = null;
            }
        }
        finally
        {
            RenderSettings.fog = fog; RenderSettings.fogMode = fogMode; RenderSettings.fogColor = fogColor;
            RenderSettings.fogStartDistance = fogStart; RenderSettings.fogEndDistance = fogEnd;
            if (cameraObject) Object.DestroyImmediate(cameraObject);
            if (world) { foreach (var f in world.GetComponentsInChildren<MeshFilter>()) Object.DestroyImmediate(f.sharedMesh); Object.DestroyImmediate(world); }
            foreach (var g in hidden) if (g) g.SetActive(true);
            bakeFrames = null;
        }
        File.WriteAllText(FacadeLayoutPath, JsonUtility.ToJson(data, true));
        AssetDatabase.Refresh();
        foreach (var p in data.panels) ImportFacadeTexture(FacadeFolder + "/" + p.texture);
        AssetDatabase.ImportAsset(FacadeLayoutPath);
        Debug.Log("Mountain Delivery backdrop baked: " + data.panels.Count + " panels, ring radius " + data.radius + " m.");
        ApplyFacades();
    }

    static void ImportFacadeTexture(string path)
    {
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Default; importer.sRGBTexture = true; importer.alphaSource = TextureImporterAlphaSource.None;
        importer.mipmapEnabled = true; importer.wrapMode = TextureWrapMode.Clamp; importer.anisoLevel = 8; importer.filterMode = FilterMode.Trilinear;
        importer.npotScale = TextureImporterNPOTScale.None; importer.maxTextureSize = 8192; importer.textureCompression = TextureImporterCompression.Uncompressed; // compression shifts the sky colour, showing a box edge against the camera's sky
        importer.SaveAndReimport();
    }

    // Renders what the eye sees through one panel, in supersampled tiles with off-axis frustums whose near plane is the panel.
    static FacadePanel RenderSegment(Camera cam, Vector3 eye, Segment s, string path)
    {
        float bottom = float.MaxValue, peak = float.MinValue;
        for (float t = 0; t <= 1.0001f; t += 1f / 32)
        {
            float g = GroundHeight(bakeFrames, Vector3.Lerp(s.a, s.b, t));
            bottom = Mathf.Min(bottom, g); peak = Mathf.Max(peak, g);
        }
        bottom -= 25; float top = Mathf.Max(peak, eye.y) + 130;
        float width = Flat(s.b - s.a).magnitude;
        int w = Mathf.Min(8192, Mathf.CeilToInt(width * s.ppm)), h = Mathf.Min(8192, Mathf.CeilToInt((top - bottom) * s.ppm));
        top = bottom + h / s.ppm;
        var forward = PanelForward(eye, s.a, s.b); var right = Vector3.Cross(Vector3.up, forward);
        float near = Vector3.Dot(s.a - eye, forward);
        float xa = Vector3.Dot(s.a - eye, right), xb = Vector3.Dot(s.b - eye, right);
        cam.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(forward, Vector3.up));
        cam.nearClipPlane = near; cam.farClipPlane = 6000;
        // Unity's linear fog reads depth through the camera's near plane (depth' = far*(depth-near)/(far-near)), and every panel's near
        // plane is a different distance, so the same fog settings would haze each panel differently and show steps at the seams.
        // Start the haze at the panel itself (the real ground in front of it has none) and rescale the end to make it the same for all.
        RenderSettings.fogStartDistance = 0; RenderSettings.fogEndDistance = 3300f * 6000f / (6000f - near);
        var pixels = new Color32[w * h];
        var rt = new RenderTexture(Tile * Supersample, Tile * Supersample, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        var read = new Texture2D(Tile * Supersample, Tile * Supersample, TextureFormat.RGBA32, false);
        try
        {
            cam.targetTexture = rt;
            for (int ty = 0; ty < h; ty += Tile)
                for (int tx = 0; tx < w; tx += Tile)
                {
                    float u0 = (float)tx / w, u1 = (float)(tx + Tile) / w, v0 = (float)ty / h, v1 = (float)(ty + Tile) / h;
                    // The last tile extends beyond the image. Render that full tile and crop its pixels;
                    // clamping its frustum would squeeze the remaining landscape into a full-size tile.
                    cam.projectionMatrix = Matrix4x4.Frustum(Mathf.LerpUnclamped(xa, xb, u0), Mathf.LerpUnclamped(xa, xb, u1),
                        Mathf.LerpUnclamped(bottom, top, v0) - eye.y, Mathf.LerpUnclamped(bottom, top, v1) - eye.y, near, 6000);
                    cam.Render();
                    RenderTexture.active = rt; read.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); read.Apply(); RenderTexture.active = null;
                    var src = read.GetPixels32(); int sw = rt.width;
                    for (int y = 0; y < Tile && ty + y < h; y++)
                        for (int x = 0; x < Tile && tx + x < w; x++)
                        {
                            int r = 0, g = 0, b = 0;
                            for (int j = 0; j < Supersample; j++) for (int i = 0; i < Supersample; i++)
                            { var c = src[(y * Supersample + j) * sw + x * Supersample + i]; r += c.r; g += c.g; b += c.b; }
                            int n = Supersample * Supersample;
                            pixels[(ty + y) * w + tx + x] = new Color32((byte)(r / n), (byte)(g / n), (byte)(b / n), 255);
                        }
                }
        }
        finally { cam.targetTexture = null; cam.ResetProjectionMatrix(); rt.Release(); Object.DestroyImmediate(rt); Object.DestroyImmediate(read); }

        // Crop the rows of plain sky off the top: above the panel the camera's sky colour shows anyway.
        var sky = pixels[(h - 1) * w];
        int keep = h;
        for (int y = h - 1; y >= 0; y--)
        {
            bool plain = true;
            for (int x = 0; x < w && plain; x++) { var c = pixels[y * w + x]; plain = Mathf.Abs(c.r - sky.r) <= 3 && Mathf.Abs(c.g - sky.g) <= 3 && Mathf.Abs(c.b - sky.b) <= 3; }
            if (!plain) { keep = Mathf.Min(h, y + 1 + Mathf.CeilToInt(4 * s.ppm)); break; }
        }
        var texture = new Texture2D(w, keep, TextureFormat.RGB24, false);
        var cropped = new Color32[w * keep]; Array.Copy(pixels, cropped, cropped.Length);
        texture.SetPixels32(cropped); texture.Apply();
        File.WriteAllBytes(path, texture.EncodeToPNG()); Object.DestroyImmediate(texture);
        return new FacadePanel { a = s.a, b = s.b, bottom = bottom, top = bottom + keep / s.ppm };
    }

    // ---------- The land past the map border (shared by the real terrain and the bake) ----------

    // One end's frame: f points out past the border, r to its right; the imaginary road's offset and height by distance v past the crossing.
    class EndFrame
    {
        public FacadeEnd end; public Extension road; public Vector3 f, r, o;
        readonly List<float> rv = new List<float>(), ru = new List<float>(), ry = new List<float>();
        public EndFrame(FacadeEnd e, Extension x)
        {
            end = e; road = x; f = e.outward; r = Vector3.Cross(Vector3.up, f); o = e.crossing;
            foreach (var p in x.points) { rv.Add(Vector3.Dot(Flat(p - o), f)); ru.Add(Vector3.Dot(Flat(p - o), r)); ry.Add(p.y); }
        }
        public void RoadAt(float v, out float u, out float y)
        {
            if (v <= rv[0]) { u = ru[0]; y = ry[0]; return; }
            int k = 1; while (k < rv.Count - 1 && rv[k] < v) k++;
            float t = Mathf.InverseLerp(rv[k - 1], rv[k], v); u = Mathf.Lerp(ru[k - 1], ru[k], t); y = Mathf.Lerp(ry[k - 1], ry[k], t);
        }
        public float Land(Vector3 w)
        {
            float v = Vector3.Dot(Flat(w - o), f); RoadAt(v, out float cu, out float cy);
            return LandHeight(end.start, w, Vector3.Dot(Flat(w - o), r) - cu, v, cy);
        }
        public float Distance(Vector3 w) { return Flat(w - o).magnitude; }
        // Is w on this end's real terrain (the road-following patch beyond the map border)? Shrink pulls the edge in.
        public bool InApron(Vector3 w, float shrink)
        {
            float v = Vector3.Dot(Flat(w - o), f); if (v < -13 || v > end.reach - shrink) return false;
            RoadAt(v, out float cu, out float cy);
            return Mathf.Abs(Vector3.Dot(Flat(w - o), r) - cu) <= ApronHalfWidth - shrink;
        }
    }
    // Each end owns the land nearer to it, and the two landscapes (high mountains at the start, a low valley at the finish) are blended
    // where they meet. Near the road ends the blend is narrow (150 m), so the road bench is untouched; further out it widens with distance,
    // so the far landscape rises gently all the way round instead of showing a cliff where the two meet.
    static float GroundHeight(List<EndFrame> frames, Vector3 w)
    {
        float d0 = frames[0].Distance(w), d1 = frames[1].Distance(w), width = 150 + Mathf.Max(0, Mathf.Min(d0, d1) - 300);
        float diff = d0 - d1;
        if (diff < -width) return frames[0].Land(w);
        if (diff > width) return frames[1].Land(w);
        return Mathf.Lerp(frames[0].Land(w), frames[1].Land(w), Mathf.SmoothStep(0, 1, Mathf.InverseLerp(-width, width, diff)));
    }
    static bool Owns(List<EndFrame> frames, EndFrame me, Vector3 w)
    { foreach (var other in frames) if (other != me && other.Distance(w) < me.Distance(w)) return false; return true; }

    static Batch Batched(Dictionary<string, Batch> batches, string key)
    {
        if (!batches.TryGetValue(key, out var b)) batches[key] = b = new Batch();
        if (b.vertices.Count > 3000000) // park the full batch under a numbered key and start a fresh one
        { int n = 1; while (batches.ContainsKey(key + "_" + n)) n++; batches[key + "_" + n] = b; batches[key] = b = new Batch(); }
        return b;
    }

    // The real road's points past the border, then the imaginary road: straight on, then a gentle bend far away.
    // Start: it climbs over a crest and drops out of sight. Finish: it eases down into the valley floor and runs to the horizon.
    static Extension ExtendRoad(FacadeEnd e)
    {
        var x = new Extension(); int step = e.start ? -1 : 1;
        // Start well inside the map: the border cuts the road on a diagonal, so one edge reaches it about 10 m sooner.
        for (int i = Mathf.Clamp(e.inside - step * 10, 0, route.Count - 1); i >= 0 && i < route.Count; i += step) { x.points.Add(route[i]); x.distance.Add(distances[i]); }
        var p = x.points[x.points.Count - 1]; float d = x.distance[x.distance.Count - 1]; var dir = e.outward;
        float s = Vector3.Dot(Flat(p - e.crossing), e.outward);
        while (s < 3400)
        {
            float ds = s < 300 ? 1.35f : Mathf.Min(1.35f + (s - 300) * .01f, 12f);
            if (s > 420 && s < 760) dir = Quaternion.Euler(0, (e.start ? -14f : 12f) / 340f * ds, 0) * dir;
            p += dir * ds; p.y += Grade(e.start, s) * ds; s += ds; d += step * ds;
            x.points.Add(p); x.distance.Add(d);
        }
        return x;
    }
    static float Grade(bool start, float s)
    {
        if (start) return s < 320 ? .07f * Mathf.SmoothStep(0, 1, s / 90) : s < 440 ? Mathf.Lerp(.07f, -.06f, (s - 320) / 120) : s < 900 ? -.06f : 0;
        return s < 240 ? -.045f * Mathf.SmoothStep(0, 1, s / 70) : s < 330 ? Mathf.Lerp(-.045f, 0, (s - 240) / 90) : 0;
    }

    static float Fbm(float x, float z, int octaves)
    {
        float sum = 0, amplitude = .5f, frequency = 1;
        for (int i = 0; i < octaves; i++) { sum += amplitude * (Mathf.PerlinNoise(x * frequency + 37.1f * i + 100, z * frequency - 23.7f * i + 100) * 2 - 1); frequency *= 2.07f; amplitude *= .5f; }
        return sum;
    }
    static float Outside(Vector3 w) { return Mathf.Max(Mathf.Max(borderLow - w.x, w.x - borderHigh), Mathf.Max(borderLow - w.z, w.z - borderHigh)); }

    // Ground height beyond the border. du is the distance from the road's centreline, v the distance past the border crossing,
    // yr the road's height there. It continues the real terrain at the border and hands over to the imaginary landscape.
    static float LandHeight(bool start, Vector3 w, float du, float v, float yr)
    {
        float outside = Outside(w);
        float edge = Height(Mathf.Clamp(w.x, borderLow, borderHigh), Mathf.Clamp(w.z, borderLow, borderHigh));
        if (outside < -.05f) return edge - 1.2f; // inside the map: just under the real terrain
        float a = Mathf.Abs(du), n1 = Fbm(w.x * .006f, w.z * .006f, 4), n2 = Fbm(w.x * .0012f + 50, w.z * .0012f + 50, 4);
        float land;
        if (start)
        {
            float ridge = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(24, 150, a)) * (48 + 30 * n1);
            float mountains = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(520, 1500, v)) * (170 + 150 * n2) * Mathf.Lerp(.55f, 1, Mathf.InverseLerp(0, 400, a));
            land = yr + ridge + mountains + 6 * n1;
        }
        else
        {
            float hills = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(170, 470, a)) * (60 + 45 * n1);
            float far = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(900, 2200, v)) * (100 + 80 * n2) * Mathf.SmoothStep(0, 1, Mathf.InverseLerp(60, 450, a));
            land = yr + hills + far + Mathf.SmoothStep(0, 1, Mathf.InverseLerp(28, 90, a)) * 4 * n1;
        }
        float h = Mathf.Lerp(edge, land, Mathf.SmoothStep(0, 1, outside / 70));
        return Mathf.Lerp(yr - .65f, h, Mathf.SmoothStep(0, 1, Mathf.InverseLerp(13, 28, a))); // the road bench, as in Height()
    }

    // Ground colour for one cell, like the real terrain: cliff when steep, else one of three meadow shades.
    static string CellMaterial(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
    {
        float size = Mathf.Max(1, Mathf.Min(Flat(c - a).magnitude, Flat(d - b).magnitude) * .7f);
        float slope = Mathf.Max(Mathf.Abs(a.y - c.y), Mathf.Abs(b.y - d.y)) / size;
        var mid = (a + c) / 2;
        float shade = Mathf.PerlinNoise(mid.x * .035f + 20, mid.z * .035f + 20) + Rand(-.1f, .1f);
        return slope > .9f ? "Cliff" : shade < .42f ? "MeadowDark" : shade > .58f ? "MeadowLight" : "Meadow";
    }

    // ---------- Real terrain beyond each road end ----------

    // A low-poly ground mesh in the end's own frame (columns follow the road), from just inside the map out to the backdrop.
    static void ApronGeometry(EndFrame frame, List<EndFrame> frames, Dictionary<string, Batch> batches)
    {
        var f = frame.f; var r = frame.r; var o = frame.o;
        var half = new List<float> { 0, 3.3f, 5, 5.8f, 6.6f, 8.5f, 10.5f, 13, 17, 22, 28 };
        for (float u = 28, du = 7; ;) { du *= 1.1f; u += du; if (u >= ApronHalfWidth - 5) break; half.Add(u); }
        half.Add(ApronHalfWidth);
        var U = half.Skip(1).Select(u => -u).Reverse().Concat(half).ToList();
        var V = new List<float> { -13, -6 };
        for (float v = 0, dv = 3; v < frame.end.reach - .5f;) { V.Add(v); v += dv; if (v > 60) dv = Mathf.Min(dv * 1.06f, 16); }
        V.Add(frame.end.reach);
        int nu = U.Count, nv = V.Count; var grid = new Vector3[nu * nv];
        for (int j = 0; j < nv; j++)
            for (int i = 0; i < nu; i++)
            {
                float u = U[i], v = V[j];
                if (Mathf.Abs(u) > 28 && i > 0 && i < nu - 1 && j > 0 && j < nv - 1)
                { u += Rand(-.25f, .25f) * (U[i + 1] - U[i - 1]) * .5f; v += Rand(-.25f, .25f) * (V[j + 1] - V[j - 1]) * .5f; }
                frame.RoadAt(v, out float cu, out float cy);
                var w = o + f * v + r * (u + cu);
                w.y = GroundHeight(frames, w); grid[j * nu + i] = w;
            }
        for (int j = 0; j < nv - 1; j++)
            for (int i = 0; i < nu - 1; i++)
            {
                Vector3 a = grid[j * nu + i], b = grid[(j + 1) * nu + i], c = grid[(j + 1) * nu + i + 1], d = grid[j * nu + i + 1];
                if (Outside(a) <= 0 && Outside(b) <= 0 && Outside(c) <= 0 && Outside(d) <= 0) continue; // entirely under the map
                var batch = Batched(batches, CellMaterial(a, b, c, d)); batch.Tri(a, b, c); batch.Tri(a, c, d);
            }
        // Skirts on the sides and far edge, so the ring beneath (and any gap against it) never shows sky.
        var skirt = Batched(batches, "Meadow");
        void Skirt(Vector3 p, Vector3 q)
        {
            if (Outside(p) < 1 || Outside(q) < 1) return;
            Vector3 pd = new Vector3(p.x, p.y - 12, p.z), qd = new Vector3(q.x, q.y - 12, q.z);
            skirt.Quad(p, q, qd, pd); skirt.Quad(p, pd, qd, q);
        }
        for (int j = 0; j < nv - 1; j++) { Skirt(grid[j * nu], grid[(j + 1) * nu]); Skirt(grid[j * nu + nu - 1], grid[(j + 1) * nu + nu - 1]); }
        for (int i = 0; i < nu - 1; i++) Skirt(grid[(nv - 1) * nu + i], grid[(nv - 1) * nu + i + 1]);
    }

    // The road past the border: the same cross-section as Road() (10 m asphalt, 1.6 m shoulders, edge lines, 4 m centre dashes,
    // guardrails and posts) in coarser 3 m pieces, from just inside the map to the backdrop panel, where the painted road takes over.
    static void ApronRoad(EndFrame frame, Dictionary<string, Batch> batches)
    {
        var pts = new List<Vector3>(); var dist = new List<float>();
        ResampleRoad(frame.road, frame.end, -2f, frame.end.plane + 2f, 3f, pts, dist);
        Vector3 R(int j) => Vector3.Cross(Vector3.up, Flat(pts[Mathf.Min(j + 1, pts.Count - 1)] - pts[Mathf.Max(j - 1, 0)])).normalized;
        var lift = Vector3.up * .015f; // the first metres overlap the real road, so sit a hair above it
        for (int j = 0; j < pts.Count - 1; j++)
        {
            var a = pts[j] + lift; var b = pts[j + 1] + lift; var ra = R(j); var rb = R(j + 1);
            Batched(batches, "Road").Quad(a - ra * 5, b - rb * 5, b + rb * 5, a + ra * 5);
            foreach (int side in new[] { -1, 1 })
            {
                var ai = a + ra * side * 5; var bi = b + rb * side * 5; var ao = a + ra * side * 6.6f; var bo = b + rb * side * 6.6f;
                if (side > 0) Batched(batches, "Shoulder").Quad(ai, bi, bo, ao); else Batched(batches, "Shoulder").Quad(ao, bo, bi, ai);
                var up = Vector3.up * .02f; var pa = a + ra * side * 4.65f + up; var pb = b + rb * side * 4.65f + up;
                Batched(batches, "Paint").Quad(pa - ra * .08f, pb - rb * .08f, pb + rb * .08f, pa + ra * .08f);
                var ga = pts[j] + ra * side * 6.35f + Vector3.up * .9f; var gb = pts[j + 1] + rb * side * 6.35f + Vector3.up * .9f;
                Beam(Batched(batches, "Steel"), ga, gb, .18f, .44f);
                if (j % 2 == 0) Batched(batches, "Wood").Box(ga - Vector3.up * .36f, new Vector3(.18f, 1.2f, .18f), Quaternion.identity);
            }
            // Centre dashes on the same 4 m rule as the real road, whichever way the trip distance runs.
            float d0 = dist[j], d1 = dist[j + 1], lo = Mathf.Min(d0, d1), hi = Mathf.Max(d0, d1);
            for (int k = Mathf.FloorToInt(lo / 4); k <= Mathf.FloorToInt(hi / 4); k++)
            {
                if (((k % 2) + 2) % 2 != 0) continue;
                float ta = Mathf.InverseLerp(d0, d1, Mathf.Max(4 * k, lo)), tb = Mathf.InverseLerp(d0, d1, Mathf.Min(4 * k + 4, hi));
                if (ta > tb) { float t = ta; ta = tb; tb = t; }
                if (tb - ta < .01f) continue;
                Vector3 pa = Vector3.Lerp(a, b, ta), pb = Vector3.Lerp(a, b, tb), qa = Vector3.Lerp(ra, rb, ta), qb = Vector3.Lerp(ra, rb, tb);
                var up = Vector3.up * .025f;
                Batched(batches, "Paint").Quad(pa - qa * .10f + up, pb - qb * .10f + up, pb + qb * .10f + up, pa + qa * .10f + up);
            }
        }
    }

    // Points along the extension every `spacing` m of arc between two distances past the border, with the trip distance at each.
    static void ResampleRoad(Extension x, FacadeEnd e, float s0, float s1, float spacing, List<Vector3> pts, List<float> dist)
    {
        var p = x.points; int n = p.Count; var arc = new float[n];
        for (int i = 1; i < n; i++) arc[i] = arc[i - 1] + Vector3.Distance(p[i - 1], p[i]);
        float S(int i) => Vector3.Dot(Flat(p[i] - e.crossing), e.outward);
        int i0 = 0; while (i0 < n - 1 && S(i0) < s0) i0++;
        int i1 = i0; while (i1 < n - 1 && S(i1) < s1) i1++;
        float a0 = arc[i0], a1 = arc[i1]; int k = i0;
        for (float a = a0; ; a += spacing)
        {
            bool last = a >= a1; if (last) a = a1;
            while (k < i1 - 1 && arc[k + 1] < a) k++;
            float t = Mathf.InverseLerp(arc[k], arc[k + 1], a);
            pts.Add(Vector3.Lerp(p[k], p[k + 1], t)); dist.Add(Mathf.Lerp(x.distance[k], x.distance[k + 1], t));
            if (last) break;
        }
    }

    // Pines like the level's own, but sparser and a little cheaper: only the ones within sight of the road are real.
    static void ApronTrees(EndFrame frame, List<EndFrame> frames, Dictionary<string, Batch> batches, Vector3 ringCenter, float ringApothem)
    {
        var f = frame.f; var r = frame.r; var o = frame.o; var e = frame.end;
        for (float v = 4; v < frame.end.reach; v += 11 + v * .012f)
        {
            float cell = 11 + v * .012f;
            for (float u = -ApronHalfWidth + 8; u < ApronHalfWidth - 8; u += cell)
            {
                float uu = u + Rand(-.4f, .4f) * cell, vv = v + Rand(-.4f, .4f) * cell;
                if (Mathf.Abs(uu) < 16) continue;
                frame.RoadAt(vv, out float cu, out float cy);
                var w = o + f * vv + r * (uu + cu);
                if (Outside(w) < 3 || Flat(w - ringCenter).magnitude > ringApothem - 30) continue; // a tree must not be cut by the panel
                if (Fbm(w.x * .004f, w.z * .004f, 3) < 0 || random.NextDouble() > .8) continue; // same patchiness as the painted forest beyond
                float y = GroundHeight(frames, w);
                if (Mathf.Abs(GroundHeight(frames, w + f * 3) - y) > 2.6f || Mathf.Abs(GroundHeight(frames, w + r * 3) - y) > 2.6f) continue; // no trees on cliffs
                w.y = y - .4f;
                Pine(batches, w);
            }
        }
    }
    static void Pine(Dictionary<string, Batch> batches, Vector3 w)
    {
        float h = Rand(TreeMinHeight, TreeMaxHeight), trunk = Mathf.Max(.5f, h * .05f); string mat = random.Next(3) == 0 ? "PineLight" : "Pine";
        Batched(batches, "Wood").Box(w + Vector3.up * h * .2f, new Vector3(trunk, h * .4f, trunk), Quaternion.Euler(0, Rand(0, 90), 0));
        var leaves = Batched(batches, mat);
        leaves.Cone(w + Vector3.up * h * .18f, h * .30f, h * .42f, 6, Rand(0, 6));
        leaves.Cone(w + Vector3.up * h * .42f, h * .23f, h * .34f, 6, Rand(0, 6));
        leaves.Cone(w + Vector3.up * h * .62f, h * .15f, h * .38f, 6, Rand(0, 6));
    }

    // ---------- Real ground between the map border and the backdrop ----------

    // Continue the forest around the whole level, with open meadows and small rocky outcrops.
    // All detail is baked into the existing material batches and has no gameplay colliders.
    static void RingDetails(List<EndFrame> frames, Vector3 center, float radius, Dictionary<string, Batch> batches)
    {
        float limit = radius * Mathf.Cos(Mathf.PI / BackdropSides) - 24;
        for (float x = center.x - limit; x < center.x + limit; x += 34)
            for (float z = center.z - limit; z < center.z + limit; z += 34)
            {
                var w = new Vector3(x + Rand(-7, 7), 0, z + Rand(-7, 7));
                if (Outside(w) < 7 || Flat(w - center).magnitude > limit || NearRoad(frames, w, 20)) continue;
                if (frames.Any(f => f.InApron(w, -8))) continue;
                float y = GroundHeight(frames, w);
                float slope = Mathf.Max(Mathf.Abs(GroundHeight(frames, w + Vector3.right * 3) - y),
                    Mathf.Abs(GroundHeight(frames, w + Vector3.forward * 3) - y));
                w.y = y - .5f;
                float patch = Fbm(w.x * .009f + 17, w.z * .009f + 17, 3);
                if (slope < 2.6f && patch > -.08f && random.NextDouble() < .72)
                    Pine(batches, w);
                else if (random.NextDouble() < .16)
                {
                    float size = Rand(1.8f, 4.4f);
                    Batched(batches, "Cliff").Cone(w, size, size * Rand(.55f, 1.2f), 5, Rand(0, 6));
                    Batched(batches, "Cliff").Cone(w + new Vector3(size * .65f, -.3f, size * .2f), size * .6f, size * .55f, 5, Rand(0, 6));
                }
            }
    }

    // A polar mesh from the square border out to the 24-gon, coarse (about 10-20 m cells), using the same ground height as everything else.
    // It is skipped where an end's real terrain already covers it; that terrain carries the road bench, which a coarse mesh cannot.
    static void RingGeometry(List<EndFrame> frames, Vector3 center, float radius, Dictionary<string, Batch> batches)
    {
        float half = (borderHigh - borderLow) / 2, apothem = radius * Mathf.Cos(Mathf.PI / BackdropSides);
        float sector = Mathf.PI * 2 / RingSectors, side = Mathf.PI * 2 / BackdropSides;
        var grid = new Vector3[(RingRows + 1) * RingSectors];
        for (int k = 0; k <= RingRows; k++)
            for (int j = 0; j < RingSectors; j++)
            {
                float theta = j * sector;
                bool interior = k > 0 && k < RingRows;
                if (interior) theta += Rand(-.25f, .25f) * sector;
                var dir = new Vector3(Mathf.Cos(theta), 0, Mathf.Sin(theta));
                float rIn = half / Mathf.Max(Mathf.Abs(dir.x), Mathf.Abs(dir.z));
                float middle = (Mathf.Floor(theta / side) + .5f) * side, rOut = apothem / Mathf.Cos(theta - middle);
                float fraction = Mathf.Pow((float)k / RingRows, 1.8f), r = rIn + (rOut - rIn) * fraction;
                if (interior) r += Rand(-.25f, .25f) * (rOut - rIn) * (Mathf.Pow((k + 1f) / RingRows, 1.8f) - Mathf.Pow((k - 1f) / RingRows, 1.8f)) * .5f;
                var w = center + dir * r;
                // The last rows ride a little high, so the dark curtain behind the painting never peeks above the ring.
                w.y = GroundHeight(frames, w) + .8f * Mathf.SmoothStep(0, 1, (k - RingRows + 3f) / 3f);
                grid[k * RingSectors + j] = w;
            }
        // The playable terrain uses a different triangulation. Cover the small differences
        // between its interpolated edge and our height samples instead of exposing sky below it.
        var edgeSkirt = Batched(batches, "Meadow");
        for (int j = 0; j < RingSectors; j++)
        {
            var a = grid[j]; var b = grid[(j + 1) % RingSectors];
            var down = Vector3.down * 12;
            edgeSkirt.Quad(a, b, b + down, a + down);
            edgeSkirt.Quad(b, a, a + down, b + down);
        }
        for (int k = 0; k < RingRows; k++)
            for (int j = 0; j < RingSectors; j++)
            {
                int j1 = (j + 1) % RingSectors;
                Vector3 a = grid[k * RingSectors + j], b = grid[(k + 1) * RingSectors + j], c = grid[(k + 1) * RingSectors + j1], d = grid[k * RingSectors + j1];
                bool covered = true;
                foreach (var p in new[] { a, b, c, d })
                { bool inside = false; foreach (var frame in frames) inside |= frame.InApron(p, 1); covered &= inside; }
                if (covered) continue;
                var batch = Batched(batches, CellMaterial(a, b, c, d)); batch.Tri(a, c, b); batch.Tri(a, d, c); // sectors run against the grain, so flip the winding
            }
    }

    // ---------- The imaginary land beyond the ring (exists only while baking) ----------

    static GameObject BuildImaginaryWorld(List<EndFrame> frames, List<Segment> segments, Vector3 eye)
    {
        var batches = new Dictionary<string, Batch>();
        foreach (var frame in frames) { FarLandGeometry(frame, frames, batches); FarRoadGeometry(frame.road, frame.end.start, batches); }
        // A ground-coloured curtain just behind each panel, from the ground down: lines of sight that dip under the ground
        // right behind a panel (hidden from the bake's eye point by the real ground, but not from every camera spot) find ground, not sky.
        foreach (var s in segments)
        {
            var back = PanelForward(eye, s.a, s.b) * .15f; int steps = Mathf.CeilToInt(Flat(s.b - s.a).magnitude / 2);
            for (int k = 0; k < steps; k++)
            {
                Vector3 p = Vector3.Lerp(s.a, s.b, (float)k / steps) + back, q = Vector3.Lerp(s.a, s.b, (float)(k + 1) / steps) + back;
                p.y = GroundHeight(frames, p) + .5f; q.y = GroundHeight(frames, q) + .5f; // a little above the ground line, so no sky shows between
                Vector3 pd = new Vector3(p.x, -60, p.z), qd = new Vector3(q.x, -60, q.z);
                var curtain = Batched(batches, "Curtain"); curtain.Quad(p, q, qd, pd); curtain.Quad(p, pd, qd, q);
            }
        }
        FarForest(frames, batches);
        var world = new GameObject("Facade bake world") { hideFlags = HideFlags.HideAndDontSave };
        foreach (var pair in batches)
        {
            var mesh = pair.Value.Mesh(pair.Key); mesh.hideFlags = HideFlags.HideAndDontSave;
            if (pair.Key.StartsWith("Road")) DrivingFeelSetup.ProjectUVs(mesh);
            var go = new GameObject(pair.Key) { hideFlags = HideFlags.HideAndDontSave }; go.transform.SetParent(world.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            if (pair.Key.StartsWith("Curtain"))
            {
                // Flat ground colour, not lit meadow: a lit vertical wall would show as a dark band under the painting.
                var flat = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { hideFlags = HideFlags.HideAndDontSave };
                flat.SetColor("_BaseColor", new Color(.58f, .65f, .43f)); renderer.sharedMaterial = flat;
            }
            else renderer.sharedMaterial = materials[pair.Key.Split('_')[0]];
        }
        return world;
    }

    // The painted road: the same cross-section as Road(), from the extension's start on to the horizon (inside the ring it is clipped away).
    static void FarRoadGeometry(Extension x, bool start, Dictionary<string, Batch> batches)
    {
        var pts = x.points;
        Vector3 R(int j) => Vector3.Cross(Vector3.up, Flat(pts[Mathf.Min(j + 1, pts.Count - 1)] - pts[Mathf.Max(j - 1, 0)])).normalized;
        for (int j = 0; j < pts.Count - 1; j++)
        {
            var a = pts[j]; var b = pts[j + 1]; var ra = R(j); var rb = R(j + 1);
            Batched(batches, "Road").Quad(a - ra * 5, b - rb * 5, b + rb * 5, a + ra * 5);
            foreach (int side in new[] { -1, 1 })
            {
                var ai = a + ra * side * 5; var bi = b + rb * side * 5; var ao = a + ra * side * 6.6f; var bo = b + rb * side * 6.6f;
                if (side > 0) Batched(batches, "Shoulder").Quad(ai, bi, bo, ao); else Batched(batches, "Shoulder").Quad(ao, bo, bi, ai);
                var up = Vector3.up * .035f; var pa = a + ra * side * 4.65f + up; var pb = b + rb * side * 4.65f + up;
                Batched(batches, "Paint").Quad(pa - ra * .08f, pb - rb * .08f, pb + rb * .08f, pa + ra * .08f);
                var ga = a + ra * side * 6.35f + Vector3.up * .9f; var gb = b + rb * side * 6.35f + Vector3.up * .9f;
                Beam(Batched(batches, "Steel"), ga, gb, .18f, .44f);
                if (j % 3 == 0) Batched(batches, "Wood").Box(ga - Vector3.up * .36f, new Vector3(.18f, 1.2f, .18f), Quaternion.identity);
            }
            // Same dash rule as Road(), which tests the lower-indexed end of each real segment.
            float dd = start ? x.distance[j + 1] : x.distance[j];
            if (((Mathf.FloorToInt(dd / 4) % 2) + 2) % 2 == 0)
                Batched(batches, "Paint").Quad(a - ra * .10f + Vector3.up * .04f, b - rb * .10f + Vector3.up * .04f, b + rb * .10f + Vector3.up * .04f, a + ra * .10f + Vector3.up * .04f);
        }
    }

    // A big low-poly ground mesh in the end's own frame (columns follow the road), coloured like the real terrain, plus far pine forest.
    static void FarLandGeometry(EndFrame frame, List<EndFrame> frames, Dictionary<string, Batch> batches)
    {
        var e = frame.end; var f = frame.f; var r = frame.r; var o = frame.o;
        var half = new List<float> { 0, 3.3f, 5, 5.8f, 6.6f, 8.5f, 10.5f, 13, 17, 22, 28 };
        for (float u = 28, du = 7; u < 2400;) { du *= 1.1f; u += du; half.Add(u); }
        var U = half.Skip(1).Select(u => -u).Reverse().Concat(half).ToList();
        var V = new List<float>();
        for (float v = -420; v < 0; v += 12) V.Add(v);
        for (float v = 0, dv = 3; v < 3500;) { V.Add(v); v += dv; if (v > 60) dv = Mathf.Min(dv * 1.06f, 140); }
        int nu = U.Count, nv = V.Count; var grid = new Vector3[nu * nv]; var lateral = new float[nu * nv];
        for (int j = 0; j < nv; j++)
            for (int i = 0; i < nu; i++)
            {
                float u = U[i], v = V[j];
                if (Mathf.Abs(u) > 28 && i > 0 && i < nu - 1 && j > 0 && j < nv - 1)
                { u += Rand(-.25f, .25f) * (U[i + 1] - U[i - 1]) * .5f; v += Rand(-.25f, .25f) * (V[j + 1] - V[j - 1]) * .5f; }
                frame.RoadAt(v, out float cu2, out float cy2);
                var w = o + f * v + r * (u + cu2);
                w.y = GroundHeight(frames, w); grid[j * nu + i] = w; lateral[j * nu + i] = u;
            }
        for (int j = 0; j < nv - 1; j++)
            for (int i = 0; i < nu - 1; i++)
            {
                Vector3 a = grid[j * nu + i], b = grid[(j + 1) * nu + i], c = grid[(j + 1) * nu + i + 1], d = grid[j * nu + i + 1];
                if (Outside(a) <= 0 && Outside(b) <= 0 && Outside(c) <= 0 && Outside(d) <= 0) continue;
                if (!Owns(frames, frame, (a + c) / 2)) continue;
                float size = Mathf.Max(1, Mathf.Min(Flat(c - a).magnitude, Flat(d - b).magnitude) * .7f);
                float slope = Mathf.Max(Mathf.Abs(a.y - c.y), Mathf.Abs(b.y - d.y)) / size;
                var mid = (a + c) / 2; float v = V[j];
                float shade = Mathf.PerlinNoise(mid.x * .035f + 20, mid.z * .035f + 20) + Rand(-.1f, .1f);
                bool forest = Flat(mid - bakeCenter).magnitude > bakeRadius + 250 && Fbm(mid.x * .004f, mid.z * .004f, 3) > -.02f && Mathf.Abs(lateral[j * nu + i]) > 40;
                string mat = slope > .9f ? "Cliff" : forest ? (shade > .5f ? "PineLight" : "Pine") : shade < .42f ? "MeadowDark" : shade > .58f ? "MeadowLight" : "Meadow";
                var batch = Batched(batches, mat); batch.Tri(a, b, c); batch.Tri(a, c, d);
            }
    }

    static bool NearRoad(List<EndFrame> frames, Vector3 w, float clearance)
    {
        foreach (var frame in frames)
        {
            float v = Vector3.Dot(Flat(w - frame.o), frame.f); if (v < -20) continue;
            frame.RoadAt(v, out float cu, out float cy);
            if (Mathf.Abs(Vector3.Dot(Flat(w - frame.o), frame.r) - cu) < clearance) return true;
        }
        return false;
    }

    // One forest all the way round the ring, from just outside the panels to ~1.2 km (further out the forest is ground colour), with the same
    // spacing and patchiness as the real pines beyond the road ends, so the density does not jump at the backdrop. Not tied to either road
    // end: a forest that stopped along a line would show as a cut in the painting.
    static void FarForest(List<EndFrame> frames, Dictionary<string, Batch> batches)
    {
        for (float r = bakeRadius + 35; r < 1200;)
        {
            float cell = 12 + (r - bakeRadius) * .012f, step = cell / r;
            for (float theta = 0; theta < Mathf.PI * 2; theta += step)
            {
                float rr = r + Rand(-.4f, .4f) * cell, tt = theta + Rand(-.4f, .4f) * step;
                var w = bakeCenter + new Vector3(Mathf.Cos(tt), 0, Mathf.Sin(tt)) * rr;
                if (NearRoad(frames, w, 16) || Fbm(w.x * .004f, w.z * .004f, 3) < 0 || random.NextDouble() > .8) continue;
                float y = GroundHeight(frames, w);
                if (Mathf.Abs(GroundHeight(frames, w + Vector3.right * 3) - y) > 2.6f || Mathf.Abs(GroundHeight(frames, w + Vector3.forward * 3) - y) > 2.6f) continue; // no trees on cliffs
                w.y = y - .4f;
                Pine(batches, w);
            }
            r += cell;
        }
    }
}
