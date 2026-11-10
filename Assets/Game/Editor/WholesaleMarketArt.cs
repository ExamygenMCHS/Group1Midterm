using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Scene-only dressing: the market's transactions and screen-space controls stay owned by WholesaleMarket.
public static class WholesaleMarketArt
{
    const string Pack = "Assets/Gridness Studios/Lite Farm Pack/Prefabs/";
    const string Art = "Assets/Game/Art/Market/";
    static Transform dressing;
    static Material palette;

    [MenuItem("Tools/Mountain Delivery/Dress Wholesale Market (keeps GUI wiring)")]
    public static void Apply()
    {
        var scene = SceneManager.GetActiveScene();
        if (Application.isPlaying || scene.path != "Assets/Game/Scenes/Wholesale Market.unity")
            throw new System.InvalidOperationException("Open Wholesale Market in Edit Mode first.");
        var market = scene.GetRootGameObjects().Single(g => g.name == "Market").transform;
        var old = market.Find("Market dressing");
        if (old != null) Object.DestroyImmediate(old.gameObject);
        dressing = new GameObject("Market dressing").transform;
        dressing.SetParent(market, false);
        palette = AssetDatabase.LoadAssetAtPath<Material>(Art + "GridnessMarketURP.mat");
        if (palette == null)
        {
            var source = AssetDatabase.LoadAssetAtPath<Material>("Assets/Gridness Studios/Lite Farm Pack/Materials/Mat_Farm.mat");
            palette = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            palette.SetTexture("_BaseMap", source.GetTexture("_MainTex"));
            palette.SetColor("_BaseColor", Color.white);
            palette.SetFloat("_Smoothness", .05f);
            AssetDatabase.CreateAsset(palette, Art + "GridnessMarketURP.mat");
        }

        foreach (Transform child in market.Cast<Transform>().ToArray())
            if (child.name == "Crate" || child.name == "Stacked crate") Object.DestroyImmediate(child.gameObject);
        // Keep the original stall and truck. A smaller yard reveals a meadow rather than an endless beige plane.
        market.Find("Yard").localScale = new Vector3(24, .2f, 19);
        market.Find("Yard").localPosition = new Vector3(1, -.12f, 2);
        market.Find("Meadow").localPosition = new Vector3(0, -.22f, 0);
        market.Find("Meadow").GetComponent<Renderer>().sharedMaterial = Mat("MarketGrass", "#7B9560");
        market.Find("Counter").GetComponent<Renderer>().sharedMaterial = Mat("CounterSage", "#71846A");
        market.Find("Sign").GetComponent<Renderer>().sharedMaterial = Mat("SignWood", "#473B30");

        // Slatted counter, a striped canvas valance, and a shelf visibly stocked with real produce.
        for (int i = 0; i < 13; i++)
            Cube("Counter board", new Vector3(1.18f + i * .47f, .7f, 1.185f), new Vector3(.025f, .94f, .025f), Mat("MarketWood", "#765440"));
        for (int i = 0; i < 12; i++)
            Cube("Canvas valance", new Vector3(.43f + i * .65f, 3.03f, .63f), new Vector3(.65f, .32f, .09f), Mat(i % 2 == 0 ? "AwningCream" : "AwningRust", i % 2 == 0 ? "#F2DFAB" : "#B75D44"));
        Cube("Back shelf", new Vector3(4, 1.04f, 4.25f), new Vector3(5.8f, .14f, .85f), Mat("MarketWood", "#765440"));
        for (int i = 0; i < 5; i++)
        {
            Prop("Tomato_Crate", new Vector3(1.5f + i * 1.15f, 1.21f, 1.55f), 1, i * 3 - 6);
            Prop(i % 2 == 0 ? "Tomato_Crate" : "Crate", new Vector3(1.6f + i * 1.15f, 1.12f, 4.2f), .9f, -4 + i * 2);
        }
        Prop("Tomato_Crate_Pack", new Vector3(8.25f, .02f, 2.5f), 1.25f, -12);
        Prop("Crate", new Vector3(8.7f, .02f, .75f), 1.1f, 14);
        Prop("Crate", new Vector3(8.7f, .55f, .75f), 1.1f, 5);
        Prop("Tomato_Fallen", new Vector3(7.9f, .03f, -.05f), 1, 35);
        Prop("WaterCan", new Vector3(.25f, .02f, 3.1f), 1.2f, -40);
        Prop("Harrow", new Vector3(7.22f, .87f, 3.8f), 1, 15);
        Prop("Crate", new Vector3(-3.5f, .02f, 2.9f), 1.2f, -8);
        Prop("Tomato_Crate", new Vector3(-3.5f, .6f, 2.9f), 1.2f, 4);

        // A loading trolley and a chalkboard make the yard feel like an active working stop.
        var wood = Mat("MarketWood", "#765440");
        Cube("Loading trolley deck", new Vector3(-3.1f, .38f, -.2f), new Vector3(1.3f, .15f, 1.8f), wood);
        for (int i = 0; i < 2; i++)
        {
            Cube("Trolley handle", new Vector3(-3.65f + i * 1.1f, .85f, .65f), new Vector3(.07f, 1, .07f), wood);
            for (int j = 0; j < 2; j++)
                Cube("Trolley wheel", new Vector3(-3.65f + i * 1.1f, .2f, -.8f + j * 1.3f), new Vector3(.15f, .32f, .32f), Mat("WheelRubber", "#343B34"));
        }
        Prop("Tomato_Crate_Pack", new Vector3(-3.1f, .46f, -.2f), 1, 0);
        Cube("Chalkboard frame", new Vector3(.2f, .88f, -.6f), new Vector3(1.18f, 1.65f, .12f), wood);
        Cube("Chalkboard face", new Vector3(.2f, .94f, -.68f), new Vector3(1.02f, 1.3f, .03f), Mat("Chalkboard", "#303F38"));
        Label("Chalkboard writing", "TODAY\nFRESH PICKED\nLOCAL PRODUCE\n---\nDELIVERIES\nWELCOME", new Vector3(.2f, .96f, -.705f), .16f, new Color(.96f, .93f, .79f));
        ImproveSigns();

        for (int i = 0; i < 11; i++) Prop("Fence_Middle", new Vector3(-9 + i * 1.8f, 0, 8.5f), 1, 0);
        for (int i = 0; i < 5; i++) Prop("Fence_Middle", new Vector3(-9.8f, 0, -1 + i * 1.8f), 1, 90);
        var random = new System.Random(485);
        for (int i = 0; i < 20; i++)
        {
            float x = -18 + i * 2.2f;
            Prop("Tree", new Vector3(x, -.1f, 11 + (float)random.NextDouble() * 8), 1.3f + (float)random.NextDouble(), i * 51);
        }
        Prop("Tree", new Vector3(-7.4f, 0, 4), 1.55f, 10);
        Prop("Tree", new Vector3(10.8f, 0, 6), 1.8f, 70);
        for (int i = 0; i < 7; i++) Prop("Mountain", new Vector3(-36 + i * 13, -.4f, 43 + i % 2 * 9), 4 + i % 3, i * 42);
        for (int i = 0; i < 70; i++)
        {
            float x = -11 + (float)random.NextDouble() * 22;
            float z = i < 45 ? 7 + (float)random.NextDouble() * 3 : -4 + (float)random.NextDouble() * 11;
            if (i >= 45) x = i % 2 == 0 ? -8.5f : 10.5f;
            Prop(i % 4 == 0 ? "Flower" : "Grass", new Vector3(x, 0, z), .7f + (float)random.NextDouble() * .5f, i * 137);
        }
        for (int i = 0; i < 4; i++)
        {
            Cube("Herb planter", new Vector3(1.4f + i * 1.45f, .24f, 5.6f), new Vector3(1.15f, .48f, .9f), wood);
            Prop(i % 2 == 0 ? "Plant_Tomato_Large" : "Plant_Large", new Vector3(1.4f + i * 1.45f, .48f, 5.6f), .8f, i * 25);
        }

        Cube("Clerk platform", new Vector3(3.2f, .425f, 2.9f), new Vector3(1.4f, .45f, 1.1f), wood);
        ImproveTrader();

        var motes = new GameObject("Drifting pollen").AddComponent<ParticleSystem>();
        motes.transform.SetParent(dressing, false);
        motes.transform.position = new Vector3(1, 1.7f, 2);
        motes.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = motes.main;
        main.startLifetime = 9;
        main.startSpeed = .06f;
        main.startSize = new ParticleSystem.MinMaxCurve(.025f, .05f);
        main.startColor = new Color(1, .91f, .66f, .32f);
        main.maxParticles = 45;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.prewarm = true;
        var emission = motes.emission;
        emission.rateOverTime = 4;
        var shape = motes.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(13, 2.5f, 7);
        var velocity = motes.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = .12f;
        velocity.y = .025f;
        velocity.z = .035f;
        motes.GetComponent<ParticleSystemRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Game/Art/Materials/ParticleDust.mat");
        motes.Play();

        var cam = Camera.main;
        cam.transform.position = new Vector3(-9.5f, 5.4f, -13.5f);
        cam.transform.LookAt(new Vector3(1.8f, 1.6f, 2));
        cam.transform.position += cam.transform.right * 3.7f;
        cam.fieldOfView = 45;
        cam.lensShift = new Vector2(.16f, 0);
        cam.backgroundColor = new Color(.72f, .81f, .82f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogStartDistance = 32;
        RenderSettings.fogEndDistance = 115;
        RenderSettings.fogColor = cam.backgroundColor;

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("Wholesale market dressed with Lite Farm Pack props; GUI and sale wiring preserved.");
    }

    static GameObject Prop(string name, Vector3 position, float scale, float yaw)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Pack + name + ".prefab");
        if (prefab == null) throw new System.InvalidOperationException("Missing Lite Farm Pack prop: " + name);
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, dressing);
        go.transform.SetPositionAndRotation(position, Quaternion.Euler(0, yaw, 0));
        go.transform.localScale = Vector3.one * scale;
        foreach (var renderer in go.GetComponentsInChildren<Renderer>()) renderer.sharedMaterials = renderer.sharedMaterials.Select(m => palette).ToArray();
        return go;
    }

    static Material Mat(string name, string hex)
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(Art + name + ".mat");
        if (material != null) return material;
        ColorUtility.TryParseHtmlString(hex, out var color);
        material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name, color = color };
        material.SetFloat("_Smoothness", .05f);
        AssetDatabase.CreateAsset(material, Art + name + ".mat");
        return material;
    }

    static void Cube(string name, Vector3 position, Vector3 size, Material material)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.name = name;
        go.transform.SetParent(dressing, false);
        go.transform.position = position;
        go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = material;
    }

    static TextMeshPro Label(string name, string text, Vector3 position, float size, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(dressing, false);
        go.transform.position = position;
        var label = go.AddComponent<TextMeshPro>();
        label.text = text;
        label.fontSize = size * 10;
        label.alignment = TextAlignmentOptions.Center;
        label.color = color;
        label.rectTransform.sizeDelta = new Vector2(4, 1.4f);
        return label;
    }

    [MenuItem("Tools/Mountain Delivery/Improve Wholesale Signs (keeps dressing)")]
    public static void ImproveSigns()
    {
        var scene = SceneManager.GetActiveScene();
        if (Application.isPlaying || scene.path != "Assets/Game/Scenes/Wholesale Market.unity")
            throw new System.InvalidOperationException("Open Wholesale Market in Edit Mode first.");
        dressing = scene.GetRootGameObjects().Single(g => g.name == "Market").transform.Find("Market dressing");
        foreach (string name in new[] { "Market sign", "Counter lettering", "Counter sign rim", "Counter sign face" })
        {
            var old = dressing.Find(name);
            if (old != null) Object.DestroyImmediate(old.gameObject);
        }
        // The shop faces -Z. Both the plaque and its ink sit in front of the slats.
        Cube("Counter sign rim", new Vector3(4, .73f, 1.10f), new Vector3(5.45f, .60f, .07f), Mat("MarketWood", "#765440"));
        Cube("Counter sign face", new Vector3(4, .73f, 1.055f), new Vector3(5.25f, .43f, .025f), Mat("SignWood", "#473B30"));
        var cream = new Color(1, .88f, .61f);
        Fit(Label("Market sign", "HARVEST CO-OP", new Vector3(4, 3.75f, 1.125f), .43f, cream), new Vector2(3.78f, .55f));
        Fit(Label("Counter lettering", "FRESH FROM THE VALLEY", new Vector3(4, .73f, 1.035f), .24f, cream), new Vector2(4.95f, .31f));
        var chalk = dressing.Find("Chalkboard writing").GetComponent<TextMeshPro>();
        chalk.text = "FRESH\nDAILY";
        chalk.transform.position = new Vector3(.2f, .94f, -.71f);
        chalk.fontSize = 5;
        Fit(chalk, new Vector2(.88f, 1.12f));
        EditorSceneManager.MarkSceneDirty(scene);
    }

    [MenuItem("Tools/Mountain Delivery/Improve Wholesale Trader (keeps dressing)")]
    public static void ImproveTrader()
    {
        var scene = SceneManager.GetActiveScene();
        if (Application.isPlaying || scene.path != "Assets/Game/Scenes/Wholesale Market.unity")
            throw new System.InvalidOperationException("Open Wholesale Market in Edit Mode first.");
        dressing = scene.GetRootGameObjects().Single(g => g.name == "Market").transform.Find("Market dressing");
        var material = AssetDatabase.LoadAssetAtPath<Material>(Art + "MarketTrader.mat");
        if (material == null)
        {
            // A separate palette keeps skin, equipment and the farm trader's clothes intact.
            var texture = new Texture2D(2, 2);
            ImageConversion.LoadImage(texture, System.IO.File.ReadAllBytes("Assets/Game/Art/Trader/Textures/TraderPalette.png"));
            string[] robes = { "#813D51", "#A35468", "#602C40", "#421E30" };
            for (int y = texture.height * 3 / 4; y < texture.height; y++)
                for (int x = 0; x < texture.width; x++)
                {
                    ColorUtility.TryParseHtmlString(robes[x * 4 / texture.width], out var color);
                    texture.SetPixel(x, y, color);
                }
            System.IO.File.WriteAllBytes(Art + "MarketTraderPalette.png", texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(Art + "MarketTraderPalette.png");
            var importer = (TextureImporter)AssetImporter.GetAtPath(Art + "MarketTraderPalette.png");
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            material = new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/Game/Art/Trader/Materials/TraderPalette.mat"));
            var paletteTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(Art + "MarketTraderPalette.png");
            material.SetTexture("_BaseMap", paletteTexture);
            material.SetTexture("_MainTex", paletteTexture);
            AssetDatabase.CreateAsset(material, Art + "MarketTrader.mat");
        }
        var old = dressing.Find("Co-op clerk");
        if (old != null) Object.DestroyImmediate(old.gameObject);
        var clerk = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Prefabs/Trader/WanderingTraderShop.prefab"), dressing);
        PrefabUtility.UnpackPrefabInstance(clerk, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        clerk.name = "Co-op clerk";
        foreach (var behaviour in clerk.GetComponentsInChildren<MonoBehaviour>(true)) Object.DestroyImmediate(behaviour);
        foreach (var collider in clerk.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
        foreach (var renderer in clerk.GetComponentsInChildren<Renderer>(true)) renderer.sharedMaterial = material;
        clerk.transform.SetPositionAndRotation(new Vector3(3.2f, .65f, 2.9f), Quaternion.Euler(0, 200, 0));
        clerk.transform.localScale = Vector3.one * 1.25f;
        clerk.GetComponent<Animator>().cullingMode = AnimatorCullingMode.AlwaysAnimate;
        EditorSceneManager.MarkSceneDirty(scene);
    }

    static void Fit(TextMeshPro label, Vector2 size)
    {
        label.rectTransform.sizeDelta = size;
        label.enableAutoSizing = true;
        label.fontSizeMin = .5f;
        label.fontSizeMax = label.fontSize;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Overflow;
        label.fontStyle = FontStyles.Bold;
        label.margin = Vector4.zero;
        label.ForceMeshUpdate();
    }
}
