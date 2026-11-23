using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

//three small UI glyphs drawn from signed distances, placeholders for the hand-painted item icons, and model previews
//rendered from the meshes (clean alpha from a black and a white render) for checking the models by eye
public static partial class FishingBuilder
{
    private const int IconSize = 512;
    private static readonly Color OutlineColor = new Color(0.19f, 0.11f, 0.06f, 1f);
    private static readonly Color Gold = new Color(1f, 0.831f, 0.369f, 1f);//#FFD45E, the HUD's key-cap gold

    // ================================================================== rendering a model

    //a camera looking along 'viewRotation', fitted around every mesh under 'root' so it fills 'fill' of the frame
    private static Color[] RenderModel(GameObject root, Quaternion viewRotation, int size, float fill, out int outSize)
    {
        var pru = new PreviewRenderUtility();
        try
        {
            Camera cam = pru.camera;
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.transform.rotation = viewRotation;
            cam.nearClipPlane = 0.01f;
            cam.farClipPlane = 50f;
            cam.allowMSAA = true;
            pru.lights[0].intensity = 1.25f;
            pru.lights[0].color = new Color(1f, 0.96f, 0.88f);
            pru.lights[0].transform.rotation = viewRotation * Quaternion.Euler(28f, -32f, 0f);
            pru.lights[1].intensity = 0.55f;
            pru.lights[1].color = new Color(0.78f, 0.86f, 1f);
            pru.lights[1].transform.rotation = viewRotation * Quaternion.Euler(-20f, 140f, 0f);
            pru.ambientColor = new Color(0.42f, 0.42f, 0.46f);

            GameObject go = UnityEngine.Object.Instantiate(root);
            pru.AddSingleGO(go);
            //fit: every vertex in camera space
            Vector3 min = Vector3.one * 1e9f, max = Vector3.one * -1e9f;
            Matrix4x4 toCam = Matrix4x4.Rotate(Quaternion.Inverse(viewRotation));
            foreach (MeshFilter mf in go.GetComponentsInChildren<MeshFilter>())
            {
                if (mf.sharedMesh == null)
                    continue;
                Matrix4x4 m = toCam * mf.transform.localToWorldMatrix;
                foreach (Vector3 v in mf.sharedMesh.vertices)
                {
                    Vector3 p = m.MultiplyPoint3x4(v);
                    min = Vector3.Min(min, p);
                    max = Vector3.Max(max, p);
                }
            }
            Vector3 centre = (min + max) * 0.5f;
            float half = Mathf.Max(max.x - min.x, max.y - min.y) * 0.5f / fill;
            cam.orthographicSize = half;
            cam.transform.position = viewRotation * new Vector3(centre.x, centre.y, min.z - 5f);

            Color[] black = RenderOnce(pru, Color.black, size, out int w1);
            Color[] white = RenderOnce(pru, Color.white, size, out int w2);
            outSize = Mathf.Min(w1, w2);
            int n = outSize * outSize;
            var result = new Color[n];
            for (int i = 0; i < n; i++)
            {
                Color b = black[i], wh = white[i];
                float a = 1f - ((wh.r - b.r) + (wh.g - b.g) + (wh.b - b.b)) / 3f;
                a = Mathf.Clamp01(a);
                result[i] = a > 0.004f ? new Color(Mathf.Clamp01(b.r / a), Mathf.Clamp01(b.g / a), Mathf.Clamp01(b.b / a), a) : new Color(0f, 0f, 0f, 0f);
            }
            return result;
        }
        finally
        {
            pru.Cleanup();
        }
    }

    private static Color[] RenderOnce(PreviewRenderUtility pru, Color background, int size, out int width)
    {
        pru.camera.backgroundColor = background;
        pru.BeginStaticPreview(new Rect(0, 0, size, size));
        pru.Render(true);
        Texture2D tex = pru.EndStaticPreview();
        width = tex.width;
        //crop to a square from the bottom-left if the preview was scaled
        int w = Mathf.Min(tex.width, tex.height);
        Color[] px = tex.GetPixels(0, 0, w, w);
        width = w;
        UnityEngine.Object.DestroyImmediate(tex);
        return px;
    }

    //a dark rim around the shape: the alpha dilated by 'radius' pixels, drawn under the picture
    private static Color[] AddOutline(Color[] px, int size, float radius, Color color)
    {
        int r = Mathf.CeilToInt(radius) + 1;
        var solid = new bool[px.Length];
        for (int i = 0; i < px.Length; i++)
            solid[i] = px[i].a >= 0.5f;
        var result = new Color[px.Length];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                int i = y * size + x;
                Color top = px[i];
                float best = 1e9f;
                if (top.a < 0.999f)
                {
                    for (int dy = -r; dy <= r; dy++)
                    {
                        int yy = y + dy;
                        if (yy < 0 || yy >= size)
                            continue;
                        for (int dx = -r; dx <= r; dx++)
                        {
                            int xx = x + dx;
                            if (xx < 0 || xx >= size || !solid[yy * size + xx])
                                continue;
                            float d = dx * dx + dy * dy;
                            if (d < best)
                                best = d;
                        }
                    }
                }
                float rim = top.a >= 0.999f ? 1f : Mathf.Clamp01(radius + 0.5f - Mathf.Sqrt(best));
                //picture over rim
                float oa = top.a + rim * (1f - top.a);
                if (oa < 1e-4f)
                {
                    result[i] = new Color(0, 0, 0, 0);
                    continue;
                }
                float k = rim * (1f - top.a);
                result[i] = new Color((top.r * top.a + color.r * k) / oa, (top.g * top.a + color.g * k) / oa, (top.b * top.a + color.b * k) / oa, oa);
            }
        return result;
    }

    private static Sprite SaveSprite(string name, Color[] px, int size)
    {
        string path = SpriteDir + "/" + name + ".png";
        var c32 = new Color32[px.Length];
        for (int i = 0; i < px.Length; i++)
            c32[i] = px[i];
        WritePng(path, size, size, c32);
        //same import as IconHoe.png
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.textureType = TextureImporterType.Sprite;
        imp.spriteImportMode = SpriteImportMode.Single;
        imp.spritePixelsPerUnit = 100f;
        imp.alphaSource = TextureImporterAlphaSource.FromInput;
        imp.alphaIsTransparency = true;
        imp.mipmapEnabled = false;
        imp.sRGBTexture = true;
        imp.wrapMode = TextureWrapMode.Clamp;
        imp.filterMode = FilterMode.Bilinear;
        imp.textureCompression = TextureImporterCompression.Uncompressed;
        imp.maxTextureSize = 512;
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    // ================================================================== temporary models for the icons

    private static GameObject MeshGO(string name, Mesh mesh, Transform parent, Vector3 pos, Quaternion rot, float scale = 1f)
    {
        var go = new GameObject(name);
        go.hideFlags = HideFlags.HideAndDontSave;
        if (parent != null)
            go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = rot;
        go.transform.localScale = Vector3.one * scale;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = Mat("FishingLowPoly");
        return go;
    }

    //the whole rod, straight, with the handle at the origin ('k' thickens it)
    private static GameObject RodModel(float k, List<Mesh> temp)
    {
        var root = new GameObject("Rod");
        root.hideFlags = HideFlags.HideAndDontSave;
        Mesh handle = k == 1f ? MeshAsset("FishingRodHandle") : TempMesh(RodHandle(k), "h");
        if (k != 1f)
            temp.Add(handle);
        Transform parent = MeshGO("Handle", handle, root.transform, Vector3.zero, Quaternion.identity).transform;
        float y = HandleLength;
        for (int i = 0; i < 3; i++)
        {
            Mesh m = k == 1f ? MeshAsset("FishingRodSection" + (i + 1)) : TempMesh(RodSection(i, k), "s" + i);
            if (k != 1f)
                temp.Add(m);
            parent = MeshGO("Section" + (i + 1), m, parent, new Vector3(0f, y, 0f), Quaternion.identity).transform;
            y = SectionLengths[i];
        }
        return root;
    }

    private static void DestroyTemp(GameObject go, List<Mesh> temp)
    {
        UnityEngine.Object.DestroyImmediate(go);
        foreach (Mesh m in temp)
            UnityEngine.Object.DestroyImmediate(m);
        temp.Clear();
    }

    // ================================================================== glyphs

    private static float SdRoundBox(Vector2 p, Vector2 c, Vector2 half, float r)
    {
        Vector2 q = new Vector2(Mathf.Abs(p.x - c.x), Mathf.Abs(p.y - c.y)) - half + new Vector2(r, r);
        return new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - r;
    }
    private static float SdSegment(Vector2 p, Vector2 a, Vector2 b, float r)
    {
        Vector2 pa = p - a, ba = b - a;
        float k = Mathf.Clamp01(Vector2.Dot(pa, ba) / Vector2.Dot(ba, ba));
        return (pa - ba * k).magnitude - r;
    }
    private static float SdCircle(Vector2 p, Vector2 c, float r) { return (p - c).magnitude - r; }
    private static float SdTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        //distance to a triangle (negative inside)
        float d = Mathf.Min(SdSegment(p, a, b, 0f), Mathf.Min(SdSegment(p, b, c, 0f), SdSegment(p, c, a, 0f)));
        float s1 = Cross(b - a, p - a), s2 = Cross(c - b, p - b), s3 = Cross(a - c, p - c);
        bool inside = (s1 >= 0 && s2 >= 0 && s3 >= 0) || (s1 <= 0 && s2 <= 0 && s3 <= 0);
        return inside ? -d : d;
    }
    private static float Cross(Vector2 a, Vector2 b) { return a.x * b.y - a.y * b.x; }
    private static float Cov(float d) { return Mathf.Clamp01(0.5f - d); }
    private static Color Over(Color dst, Color src, float a)
    {
        float sa = src.a * a;
        float oa = sa + dst.a * (1f - sa);
        if (oa <= 0.0001f)
            return new Color(0, 0, 0, 0);
        float k = dst.a * (1f - sa);
        return new Color((src.r * sa + dst.r * k) / oa, (src.g * sa + dst.g * k) / oa, (src.b * sa + dst.b * k) / oa, oa);
    }

    private static Color[] Draw(Func<Vector2, Color> pixel)
    {
        var px = new Color[IconSize * IconSize];
        for (int y = 0; y < IconSize; y++)
            for (int x = 0; x < IconSize; x++)
                px[y * IconSize + x] = pixel(new Vector2(x + 0.5f, y + 0.5f));
        return px;
    }

    //a mouse seen from above: light outline, dark glass body, one button filled gold
    private static Color[] MouseGlyph(bool left)
    {
        Color cream = new Color(0.97f, 0.95f, 0.89f, 1f);
        Color glass = new Color(0.07f, 0.09f, 0.06f, 0.6f);
        Vector2 c = new Vector2(256f, 250f);
        Vector2 half = new Vector2(132f, 196f);
        const float radius = 128f, stroke = 24f;
        float splitY = c.y + 34f;
        Color[] px = Draw(p =>
        {
            float body = SdRoundBox(p, c, half, radius);
            Color col = new Color(0, 0, 0, 0);
            col = Over(col, glass, Cov(body));
            float inner = body + stroke;
            //the pressed button: the top half on one side, inside the outline
            bool sideOk = left ? p.x < c.x : p.x > c.x;
            if (sideOk && p.y > splitY)
                col = Over(col, Gold, Cov(Mathf.Max(inner + 1f, Mathf.Max(splitY + 6f - p.y, (left ? p.x - (c.x - 6f) : (c.x + 6f) - p.x)))));
            //outline, the split between the buttons, and the wheel
            float ring = Mathf.Abs(body + stroke * 0.5f) - stroke * 0.5f;
            float split = Mathf.Max(SdSegment(p, new Vector2(c.x - half.x, splitY), new Vector2(c.x + half.x, splitY), stroke * 0.42f), inner + stroke * 0.4f);
            float mid = Mathf.Max(SdSegment(p, new Vector2(c.x, splitY), new Vector2(c.x, c.y + half.y), stroke * 0.42f), inner + stroke * 0.4f);
            float wheel = SdRoundBox(p, new Vector2(c.x, splitY + 62f), new Vector2(15f, 34f), 15f);
            float lines = Mathf.Min(ring, Mathf.Min(split, mid));
            col = Over(col, cream, Cov(lines));
            col = Over(col, glass, Cov(wheel - 10f));
            col = Over(col, cream, Cov(wheel));
            return col;
        });
        return AddOutline(px, IconSize, 7f, OutlineColor);
    }

    //a bold dark "!" on a gold speech bubble
    private static Color[] BiteGlyph()
    {
        Color dark = new Color(0.13f, 0.09f, 0.05f, 1f);
        Color shade = new Color(0.90f, 0.64f, 0.20f, 1f);
        Vector2 c = new Vector2(256f, 280f);
        Color[] px = Draw(p =>
        {
            float bubble = SdRoundBox(p, c, new Vector2(176f, 166f), 86f);
            float tail = SdTriangle(p, new Vector2(150f, 140f), new Vector2(232f, 130f), new Vector2(118f, 46f));
            float shape = Mathf.Min(bubble, tail - 6f);
            Color col = new Color(0, 0, 0, 0);
            col = Over(col, shade, Cov(shape));
            //a lighter top face gives it a little depth
            col = Over(col, Gold, Cov(Mathf.Max(shape + 14f, -(p.y - (c.y - 120f)) )));
            col = Over(col, Gold, Cov(shape + 16f));
            float bar = SdSegment(p, new Vector2(256f, 392f), new Vector2(256f, 296f), 38f);
            //the bar narrows toward the bottom like a hand-lettered "!"
            bar = Mathf.Max(bar, Mathf.Abs(p.x - 256f) - Mathf.Lerp(24f, 40f, Mathf.InverseLerp(270f, 400f, p.y)));
            float dot = SdCircle(p, new Vector2(256f, 192f), 36f);
            col = Over(col, dark, Cov(Mathf.Min(bar, dot)));
            return col;
        });
        return AddOutline(px, IconSize, 9f, OutlineColor);
    }

    // ================================================================== all icons

    //item icons are painted by hand and dropped over these paths; the builder only writes a flat placeholder when one is
    //missing (so the data assets have a sprite), and never overwrites an existing PNG
    private static readonly (string name, Color color)[] ItemIcons =
    {
        ("IconFishingRod", new Color(0.80f, 0.62f, 0.42f)), ("IconWorms", new Color(0.80f, 0.46f, 0.44f)),
        ("IconLure", new Color(0.74f, 0.92f, 0.16f)), ("IconOldLure", new Color(0.42f, 0.45f, 0.31f)),
        ("IconBass", new Color(0.40f, 0.50f, 0.24f)), ("IconBluegill", new Color(0.32f, 0.42f, 0.40f)),
        ("IconCrappie", new Color(0.62f, 0.68f, 0.58f)), ("IconCatfish", new Color(0.48f, 0.49f, 0.50f)),
    };

    private static Color[] PlaceholderIcon(Color fill)
    {
        Vector2 c = new Vector2(256f, 256f);
        Color[] px = Draw(p => Over(new Color(0, 0, 0, 0), fill, Cov(SdRoundBox(p, c, new Vector2(200f, 200f), 70f))));
        return AddOutline(px, IconSize, 6f, OutlineColor);
    }

    public static string BuildIcons()
    {
        EnsureFolder(SpriteDir);
        int placeholders = 0;
        foreach (var icon in ItemIcons)
        {
            string path = SpriteDir + "/" + icon.name + ".png";
            if (File.Exists(Path.Combine(Path.GetDirectoryName(Application.dataPath), path)))
                continue;
            SaveSprite(icon.name, PlaceholderIcon(icon.color), IconSize);
            placeholders++;
        }
        SaveSprite("IconMouseLeft", MouseGlyph(true), IconSize);
        SaveSprite("IconMouseRight", MouseGlyph(false), IconSize);
        SaveSprite("IconBite", BiteGlyph(), IconSize);
        return "icons: 3 glyphs, " + placeholders + " item placeholders written";
    }

    // ================================================================== previews for checking the models by eye

    [MenuItem("Tools/Fishing/Parts/Render Model Previews (LocalDocs)")]
    public static void RenderPreviewsMenu() { Debug.Log(RenderPreviews()); }

    public static string RenderPreviews()
    {
        string dir = Path.Combine(Path.GetDirectoryName(Application.dataPath), "LocalDocs/screens/fishing");
        Directory.CreateDirectory(dir);
        var temp = new List<Mesh>();
        void Shot(GameObject root, Quaternion view, string file)
        {
            Color[] px = RenderModel(root, view, 768, 0.9f, out int size);
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int i = 0; i < px.Length; i++)
            {
                //on a mid grey-blue background, like water
                Color bg = new Color(0.36f, 0.48f, 0.58f);
                px[i] = Color.Lerp(bg, px[i], px[i].a);
                px[i].a = 1f;
            }
            tex.SetPixels(px);
            tex.Apply();
            File.WriteAllBytes(Path.Combine(dir, file + ".png"), tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
        }
        GameObject Single(string mesh, Quaternion rot)
        {
            var root = new GameObject(mesh);
            root.hideFlags = HideFlags.HideAndDontSave;
            MeshGO("Mesh", MeshAsset(mesh), root.transform, Vector3.zero, rot);
            return root;
        }
        foreach (string fish in FishNames)
        {
            GameObject side = Single(FishMeshName(fish), Quaternion.Euler(0f, 90f, 0f));
            Shot(side, Quaternion.identity, FishMeshName(fish) + "-side");
            Shot(side, Quaternion.Euler(30f, -35f, 0f), FishMeshName(fish) + "-34");
            Shot(side, Quaternion.Euler(90f, 0f, 0f), FishMeshName(fish) + "-top");
            DestroyTemp(side, temp);
        }
        GameObject rod = RodModel(1f, temp);
        rod.transform.rotation = Quaternion.Euler(0f, 0f, -90f);
        Shot(rod, Quaternion.Euler(20f, 0f, 0f), "Rod-side");
        DestroyTemp(rod, temp);
        GameObject handle = Single("FishingRodHandle", Quaternion.Euler(0f, 0f, -90f));
        Shot(handle, Quaternion.Euler(20f, -30f, 0f), "RodHandle");
        DestroyTemp(handle, temp);
        GameObject tip = Single("FishingRodSection3", Quaternion.Euler(0f, 0f, -90f));
        Shot(tip, Quaternion.Euler(20f, -30f, 0f), "RodTip");
        DestroyTemp(tip, temp);
        foreach (string m in new[] { "Bobber", "Lure", "OldLure", "Worm" })
        {
            GameObject g = Single(m, Quaternion.identity);
            Shot(g, Quaternion.Euler(20f, -30f, 0f), m);
            DestroyTemp(g, temp);
        }
        return "previews in " + dir;
    }
}
