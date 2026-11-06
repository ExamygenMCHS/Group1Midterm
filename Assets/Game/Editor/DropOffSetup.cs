using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Builds the yellow GTA-style drop-off circle at the end of the mountain road: a see-through beam that fades upward,
// a glowing disc and a bright rim that hug the road, and a trigger. Meshes and materials are baked as assets.
public static class DropOffSetup
{
    const string ScenePath = "Assets/Game/Scenes/Mountain Delivery.unity";
    const string Folder = "Assets/Game/Art/DropOff";
    const string ZoneName = "Drop-off Zone";
    // The road is 10 m wide and the truck is about 2.5 x 4.4 m. The invisible end wall sits 3.5 m past the Finish marker,
    // so the circle is centred a little before it and stays clear of the wall.
    const float Radius = 4f, BeamHeight = 4.5f, BeamSink = 1f, SetBack = 1f, Segments = 48;
    static readonly Color Yellow = new Color(1f, .82f, .12f);

    [MenuItem("Tools/Mountain Delivery/Add Drop-off Zone (keeps hand edits)")]
    public static void AddToScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Create(scene);
        EditorSceneManager.SaveScene(scene);
    }

    // Replaces any existing zone in the scene, so it is safe to run again after tweaking the numbers above.
    public static void Create(Scene scene)
    {
        Transform finish = null, level = null;
        foreach (var r in scene.GetRootGameObjects())
        {
            if (r.name == ZoneName) Object.DestroyImmediate(r);
            else if (r.name.StartsWith("Mountain Delivery")) level = r.transform;
        }
        if (level != null) foreach (Transform t in level) if (t.name.StartsWith("Finish")) finish = t;
        if (finish == null) throw new InvalidOperationException("Missing the Finish marker in " + scene.path);
        var car = FindInScene<BasicCarController>(scene);
        if (car == null) throw new InvalidOperationException("Missing the truck in " + scene.path);

        Physics.SyncTransforms();
        var flatForward = Vector3.ProjectOnPlane(finish.forward, Vector3.up).normalized;
        var centre = finish.position - flatForward * SetBack;
        centre.y = GroundHeight(centre, finish.position.y);

        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Game/Art", "DropOff");
        var beamMaterial = MakeMaterial("DropOffBeam", Yellow, FadeTexture());
        var discMaterial = MakeMaterial("DropOffDisc", new Color(Yellow.r, Yellow.g, Yellow.b, .4f), null);
        var rimMaterial = MakeMaterial("DropOffRim", new Color(1f, .9f, .35f, .95f), null);

        var zone = new GameObject(ZoneName);
        SceneManager.MoveGameObjectToScene(zone, scene);
        zone.transform.position = centre;

        var beamMesh = BeamMesh();
        Part(zone.transform, "Beam", beamMesh, beamMaterial);
        Part(zone.transform, "Disc", GroundMesh("DropOffDisc", centre, 0f, Radius, 3, .06f), discMaterial);
        Part(zone.transform, "Rim", GroundMesh("DropOffRim", centre, Radius - .35f, Radius, 1, .09f), rimMaterial);

        // A convex mesh collider around the beam is a true cylinder, so the trigger matches the drawn circle.
        var trigger = zone.AddComponent<MeshCollider>();
        trigger.sharedMesh = beamMesh; trigger.convex = true; trigger.isTrigger = true;

        var dropOff = zone.AddComponent<DropOffZone>();
        var respawn = zone.AddComponent<TruckRespawn>();
        var so = new SerializedObject(respawn);
        so.FindProperty("truck").objectReferenceValue = car.GetComponent<Rigidbody>();
        so.FindProperty("chaseCamera").objectReferenceValue = FindInScene<CameraControl>(scene);
        so.FindProperty("zone").objectReferenceValue = dropOff;
        so.ApplyModifiedPropertiesWithoutUndo();
        UnityEventTools.AddVoidPersistentListener(dropOff.TruckArrived, respawn.Respawn);

        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("Drop-off zone placed at " + centre + ".");
    }

    static T FindInScene<T>(Scene scene) where T : Component
    {
        foreach (var r in scene.GetRootGameObjects()) { var c = r.GetComponentInChildren<T>(true); if (c) return c; }
        return null;
    }

    // Height of the road surface under a point; falls back to the given height if nothing is hit.
    static float GroundHeight(Vector3 p, float fallback)
    {
        return Physics.Raycast(new Vector3(p.x, fallback + 20f, p.z), Vector3.down, out var hit, 60f, ~0, QueryTriggerInteraction.Ignore) ? hit.point.y : fallback;
    }

    static void Part(Transform parent, string name, Mesh mesh, Material material)
    {
        var go = new GameObject(name); go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
    }

    // An open tube from just under the road up to BeamHeight. Triangles face both ways so it shows from inside and out.
    static Mesh BeamMesh()
    {
        var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var triangles = new List<int>();
        for (int i = 0; i <= Segments; i++)
        {
            float a = i * Mathf.PI * 2f / Segments; var dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * Radius;
            vertices.Add(dir + Vector3.down * BeamSink); uv.Add(new Vector2(i / Segments, 0));
            vertices.Add(dir + Vector3.up * BeamHeight); uv.Add(new Vector2(i / Segments, 1));
        }
        for (int i = 0; i < Segments; i++)
        {
            int b = i * 2;
            triangles.AddRange(new[] { b, b + 1, b + 2, b + 1, b + 3, b + 2, b, b + 2, b + 1, b + 1, b + 2, b + 3 });
        }
        return SaveMesh("DropOffBeam", vertices, uv, triangles);
    }

    // A flat disc or ring that follows the road's slope: every vertex is dropped onto the surface and lifted a hair.
    static Mesh GroundMesh(string name, Vector3 centre, float inner, float outer, int rings, float lift)
    {
        var vertices = new List<Vector3>(); var triangles = new List<int>();
        int perRing = (int)Segments;
        for (int k = 0; k <= rings; k++)
        {
            float r = Mathf.Lerp(inner, outer, k / (float)rings);
            for (int i = 0; i < perRing; i++)
            {
                float a = i * Mathf.PI * 2f / perRing; var p = centre + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * r;
                vertices.Add(new Vector3(p.x - centre.x, GroundHeight(p, centre.y) - centre.y + lift, p.z - centre.z));
            }
        }
        for (int k = 0; k < rings; k++)
            for (int i = 0; i < perRing; i++)
            {
                int a = k * perRing + i, b = k * perRing + (i + 1) % perRing, c = a + perRing, d = b + perRing;
                triangles.AddRange(new[] { a, c, d, a, d, b, a, d, c, a, b, d });
            }
        return SaveMesh(name, vertices, null, triangles);
    }

    static Mesh SaveMesh(string name, List<Vector3> vertices, List<Vector2> uv, List<int> triangles)
    {
        var mesh = new Mesh { name = name };
        mesh.SetVertices(vertices); if (uv != null) mesh.SetUVs(0, uv);
        mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
        string path = Folder + "/" + name + ".asset";
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }

    // 1 x 64 white texture whose alpha is brightest at the bottom and fades to nothing at the top of the beam.
    static Texture2D FadeTexture()
    {
        const string path = Folder + "/DropOffFade.png";
        var pixels = new Color[64];
        for (int y = 0; y < pixels.Length; y++) pixels[y] = new Color(1, 1, 1, .75f * Mathf.Pow(1f - y / 63f, 1.4f));
        var texture = new Texture2D(1, 64, TextureFormat.RGBA32, false);
        texture.SetPixels(pixels);
        File.WriteAllBytes(path, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.alphaIsTransparency = true; importer.mipmapEnabled = false; importer.wrapMode = TextureWrapMode.Clamp;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // Alpha-blended, unlit, no depth write: the same URP set-up as the tyre dust material.
    static Material MakeMaterial(string name, Color color, Texture2D texture)
    {
        string path = Folder + "/" + name + ".mat";
        AssetDatabase.DeleteAsset(path);
        var material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")) { name = name };
        if (texture) material.SetTexture("_BaseMap", texture);
        material.SetColor("_BaseColor", color);
        material.SetFloat("_Surface", 1f); material.SetFloat("_Blend", 0f);
        material.SetFloat("_SrcBlend", 5f); material.SetFloat("_DstBlend", 10f);
        material.SetFloat("_SrcBlendAlpha", 1f); material.SetFloat("_DstBlendAlpha", 10f);
        material.SetFloat("_ZWrite", 0f);
        material.SetOverrideTag("RenderType", "Transparent");
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)RenderQueue.Transparent;
        AssetDatabase.CreateAsset(material, path);
        return material;
    }
}
