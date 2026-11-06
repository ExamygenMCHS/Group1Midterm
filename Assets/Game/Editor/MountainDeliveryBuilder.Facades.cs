using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

// Road-end backdrops ("Wile E. Coyote" facades). At each end of the road, unlit panels stand just outside the map border and
// show the road carrying on into the distance. The picture is rendered in the Editor from a chase-camera eye point with the
// level's own materials and sun, plus an imaginary landscape beyond the border (the road climbing over a crest at the start,
// dropping into a wide valley at the finish, pine ridges, distance haze). So where the real road meets a panel, colours,
// lighting and line positions match.
// Bake Facade Images writes the PNGs and FacadeLayout.json into Facade/, which a rebuild keeps. Facades() places the panels.
public static partial class MountainDeliveryBuilder
{
    const string FacadeFolder = Folder + "/Facade";
    const string FacadeLayoutPath = FacadeFolder + "/FacadeLayout.json";
    // The panels stand just outside the 1 m border walls. The road surface is cut RoadOverhang past the border, behind them.
    const float FacadeOffset = 1.05f;
    // The picture is exact from this point: the chase camera (3.5 m above the truck) a set distance back from the border.
    // Centred on the road, so drivers in either lane see the same small error at the join.
    const float EyeHeight = 3.55f, EyeLateral = 0f;
    const int Supersample = 2, Tile = 1024;
    static readonly Color SkyColor = new Color(.72f, .81f, .82f);

    [Serializable] class FacadePanel { public string end, name, texture; public Vector3 a, b; public float bottom, top; }
    [Serializable] class FacadeLayout { public List<FacadePanel> panels = new List<FacadePanel>(); public List<Vector3> eyes = new List<Vector3>(); }
    class FacadeEnd { public string name; public bool start; public int inside; public Vector3 outward, crossing, eye; public float sx, sz; }
    class Segment { public string name; public Vector3 a, b; public float ppm; }
    class Extension { public List<Vector3> points = new List<Vector3>(); public List<float> distance = new List<float>(); }

    // ---------- Placing the panels (part of Build) ----------

    static void Facades()
    {
        if (!File.Exists(FacadeLayoutPath)) { Debug.LogWarning("Mountain Delivery: no facade images yet. Run Tools > Mountain Delivery > Bake Facade Images."); return; }
        var data = JsonUtility.FromJson<FacadeLayout>(File.ReadAllText(FacadeLayoutPath));
        var group = new GameObject("Facades").transform; group.SetParent(root, false); group.gameObject.isStatic = true;
        foreach (var end in new[] { "Start", "Finish" })
        {
            var parent = new GameObject("Facade " + end).transform; parent.SetParent(group, false); parent.gameObject.isStatic = true;
            foreach (var p in data.panels.Where(p => p.end == end))
            {
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(FacadeFolder + "/" + p.texture);
                if (texture == null) { Debug.LogWarning("Missing facade image " + p.texture); continue; }
                string name = "Facade " + end + " " + p.name;
                var mesh = FacadeMesh(name);
                Vector3 a = new Vector3(p.a.x, p.bottom, p.a.z), b = new Vector3(p.b.x, p.bottom, p.b.z);
                mesh.vertices = new[] { a, b, new Vector3(b.x, p.top, b.z), new Vector3(a.x, p.top, a.z) };
                mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
                mesh.triangles = new[] { 0, 3, 2, 0, 2, 1 }; // a is on the left as seen from the road, so this faces the road
                mesh.RecalculateNormals(); mesh.RecalculateBounds(); EditorUtility.SetDirty(mesh);
                // Static for culling, but not batched: static batching would fold these quads into the road's combined
                // mesh, and the road must keep its own readable mesh (its asphalt UVs are checked in Play Mode).
                var go = new GameObject(p.name); go.transform.SetParent(parent, false);
                GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ReflectionProbeStatic);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = go.AddComponent<MeshRenderer>(); renderer.sharedMaterial = FacadeMaterial(name, texture);
                renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
                renderer.lightProbeUsage = LightProbeUsage.Off; renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            }
        }
    }

    // Reuses the mesh and material of the same name, so the scene and the prefab share them and re-applying does not pile up copies.
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

    [MenuItem("Tools/Mountain Delivery/Apply Facades (keeps hand edits)")]
    public static void ApplyFacades()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var level = scene.GetRootGameObjects().First(r => r.name.StartsWith("Mountain Delivery")).transform;
        ReplaceFacades(level); EditorSceneManager.SaveScene(scene);
        var prefab = PrefabUtility.LoadPrefabContents(PrefabPath);
        try { ReplaceFacades(prefab.transform); PrefabUtility.SaveAsPrefabAsset(prefab, PrefabPath); }
        finally { PrefabUtility.UnloadPrefabContents(prefab); }
        AssetDatabase.SaveAssets();
        Debug.Log("Mountain Delivery facades applied to the scene and prefab.");
    }
    static void ReplaceFacades(Transform level)
    {
        var old = level.Find("Facades"); if (old) Object.DestroyImmediate(old.gameObject);
        root = level; Facades();
    }

    // ---------- Baking the pictures ----------

    [MenuItem("Tools/Mountain Delivery/Bake Facade Images (keeps hand edits)")]
    public static void BakeFacadeImages()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
        layout = JsonUtility.FromJson<Layout>(File.ReadAllText(Folder + "/ReferenceLayout.json"));
        route.Clear(); distances.Clear(); MakeRoute();
        random = new System.Random(4851);
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        materials.Clear();
        foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { Folder + "/Baked" }))
        { var m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid)); materials[m.name] = m; }

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
            world = BuildImaginaryWorld(ends);
            // Distance haze: the far land fades to the sky colour. Near land (inside the shadow and haze-free range) is untouched.
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Linear; RenderSettings.fogColor = SkyColor;
            RenderSettings.fogStartDistance = 160; RenderSettings.fogEndDistance = 3400;
            cameraObject = new GameObject("Facade bake camera") { hideFlags = HideFlags.HideAndDontSave };
            var cam = cameraObject.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = SkyColor; cam.allowHDR = false; cam.allowMSAA = false;
            Directory.CreateDirectory(FacadeFolder);
            foreach (var e in ends)
            {
                data.eyes.Add(e.eye);
                foreach (var s in FacadeSegments(e))
                {
                    string file = "Facade" + e.name + s.name.Replace(" ", "") + ".png";
                    var panel = RenderSegment(cam, e, s, FacadeFolder + "/" + file);
                    panel.end = e.name; panel.name = s.name; panel.texture = file; data.panels.Add(panel);
                }
            }
        }
        finally
        {
            RenderSettings.fog = fog; RenderSettings.fogMode = fogMode; RenderSettings.fogColor = fogColor;
            RenderSettings.fogStartDistance = fogStart; RenderSettings.fogEndDistance = fogEnd;
            if (cameraObject) Object.DestroyImmediate(cameraObject);
            if (world) { foreach (var f in world.GetComponentsInChildren<MeshFilter>()) Object.DestroyImmediate(f.sharedMesh); Object.DestroyImmediate(world); }
            foreach (var g in hidden) if (g) g.SetActive(true);
        }
        File.WriteAllText(FacadeLayoutPath, JsonUtility.ToJson(data, true));
        AssetDatabase.Refresh();
        foreach (var p in data.panels) ImportFacadeTexture(FacadeFolder + "/" + p.texture);
        AssetDatabase.ImportAsset(FacadeLayoutPath);
        Debug.Log("Mountain Delivery facade images baked: " + data.panels.Count + " panels.");
        ApplyFacades();
    }

    static void ImportFacadeTexture(string path)
    {
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Default; importer.sRGBTexture = true; importer.alphaSource = TextureImporterAlphaSource.None;
        importer.mipmapEnabled = true; importer.wrapMode = TextureWrapMode.Clamp; importer.anisoLevel = 8; importer.filterMode = FilterMode.Trilinear;
        importer.npotScale = TextureImporterNPOTScale.None; importer.maxTextureSize = 8192; importer.textureCompression = TextureImporterCompression.Compressed;
        importer.SaveAndReimport();
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
            var e = new FacadeEnd { name = start ? "Start" : "Finish", start = start, inside = i, outward = dir, crossing = p + dir * t, sx = Mathf.Sign(dir.x), sz = Mathf.Sign(dir.z) };
            // At the start the truck can drive up to the cap, so the eye is where its camera sits with the truck stopped there (the
            // view a player studies longest); at the finish the drop-off circle sends the truck back about 20 m short.
            float back = start ? 11f : 34f;
            int k = RouteBack(e, back), truck = RouteBack(e, back - 8); // the chase camera rides 8 m behind the truck
            var travel = Flat(route[k + step] - route[k]).normalized;
            // Its height follows the truck, not the road under the camera, so on a slope the eye is set from the truck's spot.
            e.eye = Flat(route[k]) + Vector3.Cross(Vector3.up, travel) * EyeLateral + Vector3.up * (route[truck].y + EyeHeight);
            ends.Add(e);
        }
        return ends;
    }

    // The route index about this far back along the road from where it crosses the border.
    static int RouteBack(FacadeEnd e, float back)
    {
        int step = e.start ? -1 : 1, k = e.inside; float d = Flat(e.crossing - route[k]).magnitude;
        while (d < back && k - step >= 0 && k - step < route.Count) { d += Flat(route[k] - route[k - step]).magnitude; k -= step; }
        return k;
    }

    // The panels hug the two borders that meet at the corner the road heads for: a sharp panel where the road crosses, wider and
    // coarser ones along the rest of that border, and one along the neighbouring border so turning the camera finds no gap.
    static List<Segment> FacadeSegments(FacadeEnd e)
    {
        bool onX = Mathf.Abs(e.crossing.x - borderLow) < .05f || Mathf.Abs(e.crossing.x - borderHigh) < .05f;
        float Edge(float sign) => sign > 0 ? borderHigh + FacadeOffset : borderLow - FacadeOffset;
        float plane = Edge(onX ? e.sx : e.sz), corner = Edge(onX ? e.sz : e.sx), along = onX ? e.crossing.z : e.crossing.x;
        Vector3 Main(float q) => onX ? new Vector3(plane, 0, q) : new Vector3(q, 0, plane);
        Vector3 Side(float q) => onX ? new Vector3(q, 0, corner) : new Vector3(corner, 0, q);
        float toward = Mathf.Sign(corner - along);
        float nearToward = along + toward * 40, nearAway = along - toward * 40;
        if ((corner - nearToward) * toward < 0) nearToward = corner;
        var list = new List<Segment> { new Segment { name = "Road", a = Main(nearAway), b = Main(nearToward), ppm = 40 } };
        if (Mathf.Abs(corner - nearToward) > 1) list.Add(new Segment { name = "Corner side", a = Main(nearToward), b = Main(corner), ppm = 14 });
        list.Add(new Segment { name = "Far side", a = Main(nearAway - toward * 130), b = Main(nearAway), ppm = 14 });
        list.Add(new Segment { name = "Side wall", a = Side(plane), b = Side(plane - (onX ? e.sx : e.sz) * 170), ppm = 12 });
        foreach (var s in list)
        {
            // Order each panel's ends so a is on the left as seen from the eye.
            var forward = PanelForward(e.eye, s.a, s.b);
            if (Vector3.Dot(s.b - s.a, Vector3.Cross(Vector3.up, forward)) < 0) { var t = s.a; s.a = s.b; s.b = t; }
        }
        return list;
    }
    static Vector3 PanelForward(Vector3 eye, Vector3 a, Vector3 b)
    {
        var normal = Vector3.Cross(Vector3.up, Flat(b - a)).normalized;
        return Vector3.Dot(Flat(a - eye), normal) < 0 ? -normal : normal; // from the eye toward the panel
    }

    // Renders what the eye sees through one panel, in supersampled tiles with off-axis frustums whose near plane is the panel.
    static FacadePanel RenderSegment(Camera cam, FacadeEnd e, Segment s, string path)
    {
        float bottom = float.MaxValue;
        for (float t = 0; t <= 1.0001f; t += 1f / 64)
        {
            var p = Vector3.Lerp(s.a, s.b, t);
            bottom = Mathf.Min(bottom, Height(Mathf.Clamp(p.x, borderLow, borderHigh), Mathf.Clamp(p.z, borderLow, borderHigh)));
        }
        bottom -= 12; float top = e.eye.y + 110;
        float width = Flat(s.b - s.a).magnitude;
        int w = Mathf.Min(8192, Mathf.CeilToInt(width * s.ppm)), h = Mathf.Min(8192, Mathf.CeilToInt((top - bottom) * s.ppm));
        top = bottom + h / s.ppm;
        var forward = PanelForward(e.eye, s.a, s.b); var right = Vector3.Cross(Vector3.up, forward);
        float near = Vector3.Dot(s.a - e.eye, forward);
        float xa = Vector3.Dot(s.a - e.eye, right), xb = Vector3.Dot(s.b - e.eye, right);
        cam.transform.SetPositionAndRotation(e.eye, Quaternion.LookRotation(forward, Vector3.up));
        cam.nearClipPlane = near; cam.farClipPlane = 6000;
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
                    cam.projectionMatrix = Matrix4x4.Frustum(Mathf.Lerp(xa, xb, u0), Mathf.Lerp(xa, xb, u1),
                        Mathf.Lerp(bottom, top, v0) - e.eye.y, Mathf.Lerp(bottom, top, v1) - e.eye.y, near, 6000);
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

    // ---------- The imaginary land beyond the border (exists only while baking) ----------

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
    }
    // Each end owns the land nearer to it; within 150 m of the midline the two landscapes are blended so they meet.
    static float GroundHeight(List<EndFrame> frames, Vector3 w)
    {
        float diff = frames[0].Distance(w) - frames[1].Distance(w);
        if (diff < -150) return frames[0].Land(w);
        if (diff > 150) return frames[1].Land(w);
        return Mathf.Lerp(frames[0].Land(w), frames[1].Land(w), Mathf.SmoothStep(0, 1, Mathf.InverseLerp(-150, 150, diff)));
    }
    static bool Owns(List<EndFrame> frames, EndFrame me, Vector3 w)
    { foreach (var other in frames) if (other != me && other.Distance(w) < me.Distance(w)) return false; return true; }

    static GameObject BuildImaginaryWorld(List<FacadeEnd> ends)
    {
        var batches = new Dictionary<string, Batch>();
        var frames = ends.Select(e => new EndFrame(e, ExtendRoad(e))).ToList();
        foreach (var frame in frames)
        {
            RoadGeometry(frame.road, frame.end.start, batches);
            LandGeometry(frame, frames, batches);
            // A ground-coloured curtain just behind each panel, from the ground down: lines of sight that dip under the ground
            // right behind a panel (hidden from the bake's eye point by the real ground, but not from every camera spot) find ground, not sky.
            foreach (var s in FacadeSegments(frame.end))
            {
                var back = PanelForward(frame.end.eye, s.a, s.b) * .15f; int steps = Mathf.CeilToInt(Flat(s.b - s.a).magnitude / 2);
                for (int k = 0; k < steps; k++)
                {
                    Vector3 p = Vector3.Lerp(s.a, s.b, (float)k / steps) + back, q = Vector3.Lerp(s.a, s.b, (float)(k + 1) / steps) + back;
                    p.y = GroundHeight(frames, p) - .3f; q.y = GroundHeight(frames, q) - .3f;
                    Vector3 pd = new Vector3(p.x, -60, p.z), qd = new Vector3(q.x, -60, q.z);
                    var curtain = Batched(batches, "Meadow"); curtain.Quad(p, q, qd, pd); curtain.Quad(p, pd, qd, q);
                }
            }
        }
        var world = new GameObject("Facade bake world") { hideFlags = HideFlags.HideAndDontSave };
        foreach (var pair in batches)
        {
            var mesh = pair.Value.Mesh(pair.Key); mesh.hideFlags = HideFlags.HideAndDontSave;
            if (pair.Key == "Road") DrivingFeelSetup.ProjectUVs(mesh);
            var go = new GameObject(pair.Key) { hideFlags = HideFlags.HideAndDontSave }; go.transform.SetParent(world.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh; go.AddComponent<MeshRenderer>().sharedMaterial = materials[pair.Key.Split('_')[0]];
        }
        return world;
    }
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
        // Only the part beyond each panel is ever drawn, and it lies on top of the real road.
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

    // The same cross-section as Road(): 10 m asphalt, 1.6 m shoulders, edge lines, 4 m centre dashes, guardrails and posts.
    static void RoadGeometry(Extension x, bool start, Dictionary<string, Batch> batches)
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
        if (outside <= 0) return edge - 2; // inside the map: hidden under the real terrain and in front of the panels anyway
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

    // A low-poly ground mesh in the end's own frame (columns follow the road), coloured like the real terrain, plus pines.
    static void LandGeometry(EndFrame frame, List<EndFrame> frames, Dictionary<string, Batch> batches)
    {
        var e = frame.end; var f = frame.f; var r = frame.r; var o = frame.o;
        void RoadAt(float v, out float u, out float y) { frame.RoadAt(v, out u, out y); }
        var half = new List<float> { 0, 3.3f, 5, 5.8f, 6.6f, 8.5f, 10.5f, 13, 17, 22, 28 };
        for (float u = 28, du = 7; u < 2400;) { du *= 1.1f; u += du; half.Add(u); }
        var U = half.Skip(1).Select(u => -u).Reverse().Concat(half).ToList();
        var V = new List<float>();
        for (float v = -420; v < 0; v += 12) V.Add(v);
        for (float v = 0, dv = 3; v < 3500;) { V.Add(v); v += dv; if (v > 60) dv = Mathf.Min(dv * 1.06f, 140); }
        int nu = U.Count, nv = V.Count; var grid = new Vector3[nu * nv]; var lateral = new float[nu * nv];
        for (int j = 0; j < nv; j++)
        {
            for (int i = 0; i < nu; i++)
            {
                float u = U[i], v = V[j];
                if (Mathf.Abs(u) > 28 && i > 0 && i < nu - 1 && j > 0 && j < nv - 1)
                { u += Rand(-.25f, .25f) * (U[i + 1] - U[i - 1]) * .5f; v += Rand(-.25f, .25f) * (V[j + 1] - V[j - 1]) * .5f; }
                RoadAt(v, out float cu2, out float cy2);
                var w = o + f * v + r * (u + cu2);
                w.y = GroundHeight(frames, w); grid[j * nu + i] = w; lateral[j * nu + i] = u;
            }
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
                bool forest = v > 650 && Fbm(mid.x * .004f, mid.z * .004f, 3) > -.02f && Mathf.Abs(lateral[j * nu + i]) > 40;
                string mat = slope > .9f ? "Cliff" : forest ? (shade > .5f ? "PineLight" : "Pine") : shade < .42f ? "MeadowDark" : shade > .58f ? "MeadowLight" : "Meadow";
                var batch = Batched(batches, mat); batch.Tri(a, b, c); batch.Tri(a, c, d);
            }
        // Pines, denser near the road and on the slopes, sparser on the finish valley floor. Beyond ~1.2 km the forest is ground colour.
        for (float v = -80; v < 1200; v += 9 + Mathf.Max(0, v) * .012f)
        {
            float cell = 9 + Mathf.Max(0, v) * .012f;
            for (float u = -650; u < 650; u += cell)
            {
                float uu = u + Rand(-.4f, .4f) * cell, vv = v + Rand(-.4f, .4f) * cell;
                if (Mathf.Abs(uu) < 16) continue;
                RoadAt(vv, out float cu, out float cy);
                var w = o + f * vv + r * (uu + cu);
                if (Outside(w) < 3 || !Owns(frames, frame, w)) continue;
                float mask = Fbm(w.x * .004f, w.z * .004f, 3) + .25f * Mathf.SmoothStep(0, 1, Mathf.InverseLerp(30, 160, Mathf.Abs(uu)));
                if (!e.start) mask -= .2f * (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(150, 420, Mathf.Abs(uu))));
                if (mask < 0 || random.NextDouble() > .8) continue;
                float y = GroundHeight(frames, w);
                if (Mathf.Abs(GroundHeight(frames, w + f * 3) - y) > 2.6f || Mathf.Abs(GroundHeight(frames, w + r * 3) - y) > 2.6f) continue; // no trees on cliffs
                w.y = y - .4f;
                float h = Rand(TreeMinHeight, TreeMaxHeight), trunk = Mathf.Max(.5f, h * .05f); string mat = random.Next(3) == 0 ? "PineLight" : "Pine";
                Batched(batches, "Wood").Box(w + Vector3.up * h * .2f, new Vector3(trunk, h * .4f, trunk), Quaternion.Euler(0, Rand(0, 90), 0));
                var leaves = Batched(batches, mat);
                leaves.Cone(w + Vector3.up * h * .18f, h * .30f, h * .42f, 7, Rand(0, 6));
                leaves.Cone(w + Vector3.up * h * .42f, h * .23f, h * .34f, 7, Rand(0, 6));
                leaves.Cone(w + Vector3.up * h * .62f, h * .15f, h * .38f, 7, Rand(0, 6));
            }
        }
    }
}
