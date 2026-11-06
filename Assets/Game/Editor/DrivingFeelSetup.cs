using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// One-off setup for the truck's driving feel. Both menu items keep hand edits: they change the existing
// assets in place instead of rebuilding the level.
public static class DrivingFeelSetup
{
    const string PickupPath = "Assets/Game/Art/Pickup Truck/Prefabs/Pickup.prefab";
    const string BakedFolder = "Assets/Game/Art/MountainRoad/Baked";
    const string GeometryPath = BakedFolder + "/Geometry.asset";
    const string RoadMaterialPath = BakedFolder + "/Road.mat";
    const string AlbedoPath = BakedFolder + "/AsphaltAlbedo.png";
    const string NormalPath = BakedFolder + "/AsphaltNormal.png";
    const int TextureSize = 512;
    // The texture repeats every TileMetres along the road (one UV unit).
    const float TileMetres = 4f;

    [MenuItem("Tools/Mountain Delivery/Add Driving Feel (keeps hand edits)")]
    public static void AddDrivingFeel()
    {
        var root = PrefabUtility.LoadPrefabContents(PickupPath);
        try
        {
            var feel = root.GetComponent<VehicleFeel>() ?? root.AddComponent<VehicleFeel>();
            if (!root.GetComponent<RoadFeel>()) root.AddComponent<RoadFeel>(); // also adds the AudioSource
            var model = root.transform.Find("Model");
            Transform body = null;
            if (model)
                for (int i = 0; i < model.childCount; i++)
                    if (model.GetChild(i).name != "WheelsMesh") { body = model.GetChild(i); break; }
            if (body == null) throw new InvalidOperationException("Could not find the chassis under Model in " + PickupPath);
            var so = new SerializedObject(feel);
            so.FindProperty("body").objectReferenceValue = body;
            so.FindProperty("wheelLean").floatValue = .25f; // tyres follow a quarter of the chassis roll
            so.ApplyModifiedPropertiesWithoutUndo();

            var dust = root.GetComponent<TireDust>() ?? root.AddComponent<TireDust>();
            var dustSo = new SerializedObject(dust);
            dustSo.FindProperty("dustMaterial").objectReferenceValue = ParticleMaterial("TireDust", true);
            dustSo.FindProperty("pebbleMaterial").objectReferenceValue = ParticleMaterial("TirePebble", false);
            dustSo.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, PickupPath);
            Debug.Log("Driving feel added to the pickup prefab (chassis: " + body.name + ").");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    [MenuItem("Tools/Mountain Delivery/Add Asphalt Texture (keeps hand edits)")]
    public static void AddAsphalt()
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(RoadMaterialPath);
        if (material == null) throw new InvalidOperationException("Missing " + RoadMaterialPath);
        ApplyAsphalt(material, true);
        var geometry = AssetDatabase.LoadAllAssetsAtPath(GeometryPath).OfType<Mesh>().ToArray();
        int count = 0;
        foreach (var mesh in geometry)
            if (mesh.name.StartsWith("Drivable road")) { ProjectUVs(mesh); count++; }
        AssetDatabase.SaveAssets();
        Debug.Log("Asphalt texture applied to the road material and " + count + " road mesh(es).");
    }

    // Alpha-blended URP particle material with a generated round sprite (soft for dust, hard-edged for pebbles).
    static Material ParticleMaterial(string name, bool soft)
    {
        const string folder = "Assets/Game/Art/Pickup Truck/Effects";
        if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/Game/Art/Pickup Truck", "Effects");
        string materialPath = folder + "/" + name + ".mat", texturePath = folder + "/" + name + ".png";
        var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material) return material;

        const int size = 64;
        var pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + .5f, y + .5f), new Vector2(size / 2f, size / 2f)) / (size / 2f);
                float a = soft ? Mathf.Pow(Mathf.Clamp01(1f - d), 1.6f) : Mathf.Clamp01((.92f - d) * 12f);
                pixels[y * size + x] = new Color(1f, 1f, 1f, a);
            }
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.SetPixels(pixels);
        File.WriteAllBytes(texturePath, texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(texturePath);
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.SaveAndReimport();

        material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")) { name = name };
        material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
        material.SetColor("_BaseColor", Color.white);
        material.SetFloat("_Surface", 1f);
        material.SetFloat("_Blend", 0f);
        material.SetFloat("_SrcBlend", 5f);
        material.SetFloat("_DstBlend", 10f);
        material.SetFloat("_SrcBlendAlpha", 1f);
        material.SetFloat("_DstBlendAlpha", 10f);
        material.SetFloat("_ZWrite", 0f);
        material.SetOverrideTag("RenderType", "Transparent");
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        AssetDatabase.CreateAsset(material, materialPath);
        return material;
    }

    // Planar top-down UVs: one UV unit is TileMetres of road, so the texture keeps its scale on any width.
    public static void ProjectUVs(Mesh mesh)
    {
        var vertices = mesh.vertices;
        var uv = new Vector2[vertices.Length];
        for (int i = 0; i < uv.Length; i++) uv[i] = new Vector2(vertices[i].x, vertices[i].z) / TileMetres;
        mesh.uv = uv;
        EditorUtility.SetDirty(mesh);
    }

    // Creates the albedo and normal maps if they are missing and plugs them into the material.
    public static void ApplyAsphalt(Material material, bool regenerate = false)
    {
        if (regenerate || !File.Exists(AlbedoPath) || !File.Exists(NormalPath)) GenerateTextures();
        var albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(AlbedoPath);
        var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(NormalPath);
        material.SetTexture("_BaseMap", albedo);
        material.SetTexture("_BumpMap", normal);
        material.SetFloat("_BumpScale", .8f);
        material.EnableKeyword("_NORMALMAP");
        EditorUtility.SetDirty(material);
    }

    static void GenerateTextures()
    {
        int n = TextureSize;
        var rng = new System.Random(1485);
        // Height: broad patches, medium mottling and fine aggregate, all tileable.
        var height = new float[n * n];
        var grains = new float[n * n];
        var patches = Periodic(n, 4, rng); var mottle = Periodic(n, 24, rng); var fine = Periodic(n, 128, rng);
        for (int i = 0; i < height.Length; i++)
        {
            grains[i] = (float)rng.NextDouble();
            height[i] = patches[i] * .1f + mottle[i] * .25f + fine[i] * .3f + grains[i] * .35f;
        }
        var albedo = new Color[n * n];
        float sum = 0f;
        for (int i = 0; i < albedo.Length; i++)
        {
            // Mostly fine grain; the broad patches are kept faint so the road does not look stained.
            float v = .72f + (patches[i] - .5f) * .06f + (mottle[i] - .5f) * .1f + (fine[i] - .5f) * .2f + (grains[i] - .5f) * .3f;
            // A few pale stones in the tar.
            if (rng.NextDouble() > .985) v += .22f;
            v = Mathf.Clamp01(v);
            albedo[i] = new Color(v, v, v, 1f);
            sum += v;
        }
        // UV (0,0) is what untextured road pieces (sign backs) sample, so keep that corner at the average.
        float mean = sum / albedo.Length;
        for (int y = 0; y < 3; y++) for (int x = 0; x < 3; x++) albedo[y * n + x] = new Color(mean, mean, mean, 1f);

        var normals = new Color[n * n];
        const float strength = 2.2f;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = height[y * n + (x + 1) % n] - height[y * n + (x + n - 1) % n];
                float dy = height[((y + 1) % n) * n + x] - height[((y + n - 1) % n) * n + x];
                var nrm = new Vector3(-dx * strength, -dy * strength, 1f).normalized;
                normals[y * n + x] = new Color(nrm.x * .5f + .5f, nrm.y * .5f + .5f, nrm.z * .5f + .5f, 1f);
            }

        Save(albedo, AlbedoPath, false);
        Save(normals, NormalPath, true);
    }

    static void Save(Color[] pixels, string path, bool normalMap)
    {
        var texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false, true);
        texture.SetPixels(pixels);
        File.WriteAllBytes(path, texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = normalMap ? TextureImporterType.NormalMap : TextureImporterType.Default;
        importer.sRGBTexture = !normalMap;
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.filterMode = FilterMode.Trilinear;
        importer.anisoLevel = 8;
        importer.mipmapEnabled = true;
        importer.SaveAndReimport();
    }

    // Tileable value noise with `cells` x `cells` lattice points, in 0..1.
    static float[] Periodic(int size, int cells, System.Random rng)
    {
        var lattice = new float[cells * cells];
        for (int i = 0; i < lattice.Length; i++) lattice[i] = (float)rng.NextDouble();
        var result = new float[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float fx = (float)x / size * cells, fy = (float)y / size * cells;
                int x0 = (int)fx, y0 = (int)fy;
                float tx = Mathf.SmoothStep(0f, 1f, fx - x0), ty = Mathf.SmoothStep(0f, 1f, fy - y0);
                int x1 = (x0 + 1) % cells, y1 = (y0 + 1) % cells;
                float a = Mathf.Lerp(lattice[y0 * cells + x0], lattice[y0 * cells + x1], tx);
                float b = Mathf.Lerp(lattice[y1 * cells + x0], lattice[y1 * cells + x1], tx);
                result[y * size + x] = Mathf.Lerp(a, b, ty);
            }
        return result;
    }
}
