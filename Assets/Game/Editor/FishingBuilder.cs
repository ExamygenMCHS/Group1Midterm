using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

//builds every asset of the fishing feature from code: palette, materials, low-poly meshes, prefabs, icons, sounds and
//the fish / bait data. Nothing is downloaded. Re-running it overwrites the assets in place (load or create, same paths,
//same GUIDs), so prefabs and scenes that use them keep their links. Scenes are never touched.
public static partial class FishingBuilder
{
    private const string ArtDir = "Assets/Game/Art/Fishing";
    private const string TexDir = ArtDir + "/Textures";
    private const string MatDir = ArtDir + "/Materials";
    private const string MeshDir = ArtDir + "/Meshes";
    private const string SpriteDir = ArtDir + "/Sprites";
    private const string PrefabDir = "Assets/Game/Prefabs/Fishing";
    private const string DataDir = "Assets/Game/Data/Fishing";
    private const string AudioDir = "Assets/Game/Audio/Fishing";

    [MenuItem("Tools/Fishing/Build Assets")]
    public static void BuildAllMenu()
    {
        Debug.Log(BuildAll());
    }

    public static string BuildAll()
    {
        var log = new List<string>();
        log.Add(BuildMaterials());
        log.Add(BuildMeshes());
        log.Add(BuildPrefabs());
        log.Add(BuildIcons());
        log.Add(BuildAudio());
        log.Add(BuildData());
        AssetDatabase.SaveAssets();
        return "Fishing: " + string.Join(" | ", log);
    }

    [MenuItem("Tools/Fishing/Parts/1 Palette, Textures and Materials")]
    public static void BuildMaterialsMenu() { Debug.Log(BuildMaterials()); AssetDatabase.SaveAssets(); }
    [MenuItem("Tools/Fishing/Parts/2 Meshes")]
    public static void BuildMeshesMenu() { Debug.Log(BuildMeshes()); AssetDatabase.SaveAssets(); }
    [MenuItem("Tools/Fishing/Parts/3 Prefabs")]
    public static void BuildPrefabsMenu() { Debug.Log(BuildPrefabs()); AssetDatabase.SaveAssets(); }
    [MenuItem("Tools/Fishing/Parts/4 Icons")]
    public static void BuildIconsMenu() { Debug.Log(BuildIcons()); AssetDatabase.SaveAssets(); }
    [MenuItem("Tools/Fishing/Parts/5 Audio")]
    public static void BuildAudioMenu() { Debug.Log(BuildAudio()); AssetDatabase.SaveAssets(); }
    [MenuItem("Tools/Fishing/Parts/6 Data")]
    public static void BuildDataMenu() { Debug.Log(BuildData()); AssetDatabase.SaveAssets(); }

    // ================================================================== folders and loading

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    private static T Load<T>(string path) where T : UnityEngine.Object
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null)
            throw new Exception("missing asset " + path + " (run the earlier fishing build steps first)");
        return asset;
    }

    private static Texture2D Tex(string name) { return Load<Texture2D>(TexDir + "/" + name + ".png"); }
    private static Material Mat(string name) { return Load<Material>(MatDir + "/" + name + ".mat"); }
    private static Mesh MeshAsset(string name) { return Load<Mesh>(MeshDir + "/" + name + ".asset"); }
    private static Sprite Spr(string name) { return Load<Sprite>(SpriteDir + "/" + name + ".png"); }

    // ================================================================== palette
    //same scheme as the game's Palette.png: a 16 x 16 swatch per colour with a soft vertical gradient, so a face can be a
    //little darker at the bottom and lighter at the top. Own texture so the fish get their true colours.

    private static readonly (string name, Color color)[] Palette =
    {
        //rod and reel
        ("cork", C(0.80f, 0.62f, 0.42f)), ("corkDark", C(0.62f, 0.45f, 0.29f)), ("graphite", C(0.15f, 0.16f, 0.19f)),
        ("graphiteLight", C(0.26f, 0.28f, 0.33f)), ("metal", C(0.74f, 0.76f, 0.80f)), ("metalDark", C(0.36f, 0.38f, 0.42f)),
        ("wrapRed", C(0.82f, 0.14f, 0.13f)), ("reel", C(0.22f, 0.24f, 0.28f)), ("reelGold", C(0.92f, 0.70f, 0.26f)),
        ("rubber", C(0.10f, 0.10f, 0.11f)),
        //bobber, lures, worm
        ("bobberRed", C(0.92f, 0.16f, 0.12f)), ("bobberWhite", C(0.97f, 0.96f, 0.93f)),
        ("chartreuse", C(0.74f, 0.92f, 0.16f)), ("lureOrange", C(1.00f, 0.52f, 0.10f)), ("lureBelly", C(1.00f, 0.86f, 0.30f)),
        ("lureBlack", C(0.06f, 0.06f, 0.07f)), ("lureLip", C(0.85f, 0.92f, 0.95f)), ("hook", C(0.62f, 0.64f, 0.68f)),
        ("oldGreen", C(0.42f, 0.45f, 0.31f)), ("oldRust", C(0.52f, 0.30f, 0.18f)), ("oldScratch", C(0.66f, 0.64f, 0.56f)),
        ("oldHook", C(0.40f, 0.32f, 0.26f)), ("oldBelly", C(0.62f, 0.56f, 0.40f)),
        ("worm", C(0.80f, 0.46f, 0.44f)), ("wormDark", C(0.60f, 0.32f, 0.31f)), ("wormBand", C(0.90f, 0.60f, 0.52f)),
        //largemouth bass
        ("bassBack", C(0.25f, 0.34f, 0.15f)), ("bassUpper", C(0.40f, 0.50f, 0.24f)), ("bassLower", C(0.66f, 0.70f, 0.46f)),
        ("bassBelly", C(0.92f, 0.91f, 0.80f)), ("bassStripe", C(0.13f, 0.17f, 0.09f)), ("bassFin", C(0.42f, 0.47f, 0.27f)),
        //bluegill
        ("gillBack", C(0.20f, 0.27f, 0.28f)), ("gillUpper", C(0.32f, 0.42f, 0.40f)), ("gillLower", C(0.50f, 0.56f, 0.46f)),
        ("gillBar", C(0.25f, 0.33f, 0.32f)), ("gillBreast", C(0.98f, 0.60f, 0.16f)), ("gillEar", C(0.04f, 0.05f, 0.09f)),
        ("gillFin", C(0.30f, 0.38f, 0.38f)), ("gillBlue", C(0.34f, 0.48f, 0.70f)), ("gillBelly", C(0.95f, 0.82f, 0.50f)),
        //black crappie
        ("crapBack", C(0.30f, 0.38f, 0.30f)), ("crapUpper", C(0.62f, 0.68f, 0.58f)), ("crapLower", C(0.82f, 0.85f, 0.78f)),
        ("crapBelly", C(0.94f, 0.94f, 0.90f)), ("crapSpot", C(0.12f, 0.15f, 0.12f)), ("crapFin", C(0.56f, 0.60f, 0.52f)),
        ("crapFinSpot", C(0.22f, 0.25f, 0.21f)),
        //channel catfish
        ("catBack", C(0.34f, 0.34f, 0.33f)), ("catUpper", C(0.48f, 0.49f, 0.50f)), ("catLower", C(0.66f, 0.67f, 0.66f)),
        ("catBelly", C(0.92f, 0.90f, 0.84f)), ("catFin", C(0.30f, 0.29f, 0.28f)), ("catBarbel", C(0.20f, 0.19f, 0.18f)),
        ("catSpot", C(0.18f, 0.18f, 0.18f)),
        //shared fish parts
        ("eyeBlack", C(0.04f, 0.04f, 0.05f)), ("eyeGold", C(0.90f, 0.70f, 0.25f)), ("eyeWhite", C(0.93f, 0.92f, 0.88f)),
        ("mouth", C(0.20f, 0.10f, 0.09f)), ("gillLine", C(0.18f, 0.16f, 0.12f)),
    };

    private static Color C(float r, float g, float b) { return new Color(r, g, b, 1f); }

    private static Dictionary<string, int> paletteIndex;
    private static int Pal(string name)
    {
        if (paletteIndex == null)
        {
            paletteIndex = new Dictionary<string, int>();
            for (int i = 0; i < Palette.Length; i++)
                paletteIndex[Palette[i].name] = i;
        }
        if (!paletteIndex.TryGetValue(name, out int index))
            throw new Exception("no fishing palette colour '" + name + "'");
        return index;
    }

    private static Vector2 PalUV(int index, float t)
    {
        int col = index % 16, row = index / 16;
        t = Mathf.Clamp01(t);
        return new Vector2((col * 16 + 8) / 256f, (row * 16 + 2.5f + t * 11f) / 256f);
    }

    // ================================================================== textures

    private static void WritePng(string path, int w, int h, Color32[] px)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false, false);
        tex.SetPixels32(px);
        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
    }

    private static Texture2D SaveTexture(string name, int w, int h, Func<float, float, Color> pixel, bool alpha, TextureWrapMode wrap, bool mipmaps)
    {
        string path = TexDir + "/" + name + ".png";
        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                px[y * w + x] = (Color32)pixel((x + 0.5f) / w, (y + 0.5f) / h);
        WritePng(path, w, h, px);
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.textureType = TextureImporterType.Default;
        imp.sRGBTexture = true;
        imp.alphaSource = alpha ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
        imp.alphaIsTransparency = alpha;
        imp.mipmapEnabled = mipmaps;
        imp.wrapMode = wrap;
        imp.filterMode = FilterMode.Bilinear;
        imp.textureCompression = TextureImporterCompression.Uncompressed;
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    private static float Smooth(float a, float b, float x)
    {
        float t = Mathf.Clamp01((x - a) / (b - a));
        return t * t * (3f - 2f * t);
    }

    private static Color Mul(Color c, float k) { return new Color(c.r * k, c.g * k, c.b * k, c.a); }

    private static void BuildTextures()
    {
        //the low-poly palette
        SaveTexture("FishingPalette", 256, 256, (u, v) =>
        {
            int x = Mathf.FloorToInt(u * 256), y = Mathf.FloorToInt(v * 256);
            int index = (y / 16) * 16 + x / 16;
            if (index >= Palette.Length)
                return Color.magenta;
            float t = Mathf.Clamp01(((y % 16) - 2.5f) / 11f);
            Color c = Mul(Palette[index].color, Mathf.Lerp(0.8f, 1.13f, t));
            c.a = 1f;
            return c;
        }, false, TextureWrapMode.Clamp, false);

        //soft ring for the ripples: brightest at 80% of the radius, fading inward more slowly than outward
        SaveTexture("Ring", 128, 128, (u, v) =>
        {
            float r = new Vector2(u - 0.5f, v - 0.5f).magnitude * 2f;
            float d = r - 0.8f;
            float a = d < 0f ? Mathf.Exp(-d * d / 0.018f) : Mathf.Exp(-d * d / 0.004f);
            a *= 1f - Smooth(0.96f, 1f, r);
            return new Color(1f, 1f, 1f, a);
        }, true, TextureWrapMode.Clamp, true);

        //a fish seen from above, nose toward +v, blurred: the dark shape under the water
        SaveTexture("FishShadow", 128, 128, (u, v) =>
        {
            float x = (u - 0.5f) * 2f, y = (v - 0.5f) * 2f;//-1..1, nose at +1
            //body: a spindle from y 0.92 (nose) to y -0.5 (tail root), widest at y 0.35
            float half = 0f;
            if (y < 0.92f && y > -0.55f)
            {
                float t = (y + 0.55f) / 1.47f;//0 tail root, 1 nose
                half = 0.24f * Mathf.Pow(Mathf.Sin(Mathf.PI * Mathf.Pow(t, 0.8f)), 0.75f) + 0.035f;
            }
            float body = half - Mathf.Abs(x);
            //tail: a fan from the root to y -0.92
            float tail = -1f;
            if (y <= -0.45f && y > -0.95f)
            {
                float k = (-0.45f - y) / 0.5f;
                tail = (0.05f + 0.2f * k) - Mathf.Abs(x);
            }
            float dist = Mathf.Max(body, tail);
            float a = Smooth(-0.05f, 0.09f, dist) * 0.78f;
            return new Color(0.03f, 0.06f, 0.08f, a);
        }, true, TextureWrapMode.Clamp, true);

        //one rounded dash, repeated along the aim arc (wrap in u)
        SaveTexture("Dash", 64, 16, (u, v) =>
        {
            float x = u * 64f, y = v * 16f;
            float dx = Mathf.Max(0f, Mathf.Abs(x - 32f) - 14f);
            float dy = Mathf.Abs(y - 8f);
            float d = Mathf.Sqrt(dx * dx + dy * dy) - 5f;
            return new Color(1f, 1f, 1f, Mathf.Clamp01(0.5f - d));
        }, true, TextureWrapMode.Repeat, true);

        //the ghost cast marker picks two alphas from this strip: u < 0.5 the faint inner disc, u > 0.5 the ring
        SaveTexture("GhostMarker", 32, 4, (u, v) => new Color(1f, 1f, 1f, u < 0.5f ? 0.2f : 0.9f), true, TextureWrapMode.Clamp, false);
    }

    // ================================================================== materials

    private static Material GetOrCreateMat(string name, Shader shader)
    {
        string path = MatDir + "/" + name + ".mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }
        else if (mat.shader != shader)
        {
            mat.shader = shader;
        }
        return mat;
    }

    private static void Keyword(Material m, string keyword, bool on)
    {
        if (on)
            m.EnableKeyword(keyword);
        else
            m.DisableKeyword(keyword);
    }

    //opaque URP Lit with the keywords the material inspector would set
    private static Material Lit(string name, Texture2D baseMap, float smoothness)
    {
        Material m = GetOrCreateMat(name, Shader.Find("Universal Render Pipeline/Lit"));
        m.SetColor("_BaseColor", Color.white);
        m.SetTexture("_BaseMap", baseMap);
        m.SetFloat("_Smoothness", smoothness);
        m.SetFloat("_Metallic", 0f);
        m.SetFloat("_SmoothnessTextureChannel", 0f);
        Keyword(m, "_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A", false);
        Keyword(m, "_NORMALMAP", false);
        m.SetFloat("_Cull", 2f);
        m.SetFloat("_AlphaClip", 0f);
        Keyword(m, "_ALPHATEST_ON", false);
        m.SetFloat("_ReceiveShadows", 1f);
        Keyword(m, "_RECEIVE_SHADOWS_OFF", false);
        m.SetFloat("_Surface", 0f);
        m.SetOverrideTag("RenderType", "Opaque");
        m.SetFloat("_SrcBlend", (float)BlendMode.One);
        m.SetFloat("_DstBlend", (float)BlendMode.Zero);
        m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
        m.SetFloat("_DstBlendAlpha", (float)BlendMode.Zero);
        m.SetFloat("_ZWrite", 1f);
        Keyword(m, "_SURFACE_TYPE_TRANSPARENT", false);
        m.SetShaderPassEnabled("DepthOnly", true);
        m.SetShaderPassEnabled("ShadowCaster", true);
        m.renderQueue = (int)RenderQueue.Geometry;
        m.SetColor("_EmissionColor", Color.black);
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
        Keyword(m, "_EMISSION", false);
        m.enableInstancing = true;
        EditorUtility.SetDirty(m);
        return m;
    }

    //transparent unlit, configured like the game's particle materials
    private static Material ParticleMat(string name, Texture2D tex, Color tint)
    {
        Material m = GetOrCreateMat(name, Shader.Find("Universal Render Pipeline/Particles/Unlit"));
        m.SetTexture("_BaseMap", tex);
        m.SetColor("_BaseColor", tint);
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", 0f);
        m.SetOverrideTag("RenderType", "Transparent");
        m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_ZWrite", 0f);
        m.SetFloat("_Cull", 0f);
        Keyword(m, "_SURFACE_TYPE_TRANSPARENT", true);
        m.renderQueue = (int)RenderQueue.Transparent;
        EditorUtility.SetDirty(m);
        return m;
    }

    public static string BuildMaterials()
    {
        foreach (string dir in new[] { TexDir, MatDir })
            EnsureFolder(dir);
        BuildTextures();
        Lit("FishingLowPoly", Tex("FishingPalette"), 0.25f);
        ParticleMat("FishingLine", null, new Color(1f, 1f, 1f, 0.85f));
        ParticleMat("CastGhost", Tex("GhostMarker"), new Color(0.55f, 0.85f, 1f, 0.75f));
        ParticleMat("Ripple", Tex("Ring"), new Color(1f, 1f, 1f, 0.6f));
        ParticleMat("FishShadow", Tex("FishShadow"), Color.white);
        ParticleMat("AimDash", Tex("Dash"), new Color(1f, 1f, 1f, 0.7f));
        return "textures + 6 materials";
    }
}
