using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

//the low-poly meshes: flat shaded (every triangle has its own vertices and face normal), coloured through palette UVs
public static partial class FishingBuilder
{
    // ================================================================== mesh builder

    private class MB
    {
        public readonly List<Vector3> verts = new List<Vector3>();
        public readonly List<Vector3> normals = new List<Vector3>();
        public readonly List<Vector2> uvs = new List<Vector2>();
        public readonly List<int> tris = new List<int>();
        private readonly Stack<Matrix4x4> stack = new Stack<Matrix4x4>();
        public Matrix4x4 m = Matrix4x4.identity;
        public float gradLo = -0.1f, gradHi = 0.1f, grad = 0.35f;//local y range of the swatch gradient

        public int TriCount { get { return tris.Count / 3; } }

        public void Push(Vector3 pos, Quaternion rot, Vector3 scale) { stack.Push(m); m = m * Matrix4x4.TRS(pos, rot, scale); }
        public void Push(Vector3 pos, Quaternion rot) { Push(pos, rot, Vector3.one); }
        public void Pop() { m = stack.Pop(); }

        //one flat triangle, turned to face away from 'inside' (or toward 'facing' when given)
        public void Tri(Vector3 la, Vector3 lb, Vector3 lc, string col, Vector3 inside, Vector3? facing = null)
        {
            Vector3 a = m.MultiplyPoint3x4(la), b = m.MultiplyPoint3x4(lb), c = m.MultiplyPoint3x4(lc);
            Vector3 n = Vector3.Cross(b - a, c - a);
            if (n.sqrMagnitude < 1e-16f)
                return;
            bool flip = facing.HasValue
                ? Vector3.Dot(n, m.MultiplyVector(facing.Value)) < 0f
                : Vector3.Dot(n, (a + b + c) / 3f - m.MultiplyPoint3x4(inside)) < 0f;
            if (flip)
            {
                Vector3 t = b; b = c; c = t;
                t = lb; lb = lc; lc = t;
                n = -n;
            }
            n /= Mathf.Sqrt(n.sqrMagnitude);
            int swatch = Pal(col);
            int i = verts.Count;
            Add(a, la, n, swatch);
            Add(b, lb, n, swatch);
            Add(c, lc, n, swatch);
            tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
        }

        private void Add(Vector3 v, Vector3 local, Vector3 n, int swatch)
        {
            verts.Add(v);
            normals.Add(n);
            float t = gradHi > gradLo + 1e-6f ? (local.y - gradLo) / (gradHi - gradLo) : 0.5f;
            uvs.Add(PalUV(swatch, 0.6f + (t - 0.5f) * grad));
        }

        //seen from both sides (fins, the lure's lip)
        public void DTri(Vector3 a, Vector3 b, Vector3 c, string col)
        {
            Vector3 n = Vector3.Cross(b - a, c - a);
            if (n.sqrMagnitude < 1e-16f)
                return;
            Tri(a, b, c, col, Vector3.zero, n);
            Tri(a, b, c, col, Vector3.zero, -n);
        }

        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, string col, Vector3 inside)
        {
            Tri(a, b, c, col, inside);
            Tri(a, c, d, col, inside);
        }

        //frustum along +y from base centre b (r1 = 0: a cone)
        public void Cyl(Vector3 b, float r0, float r1, float h, int sides, string col, bool capBottom = true, bool capTop = true, float rotDeg = 0f)
        {
            Vector3 inside = b + Vector3.up * h * 0.5f;
            for (int i = 0; i < sides; i++)
            {
                float a0 = (i / (float)sides) * Mathf.PI * 2f + rotDeg * Mathf.Deg2Rad;
                float a1 = ((i + 1) / (float)sides) * Mathf.PI * 2f + rotDeg * Mathf.Deg2Rad;
                Vector3 d0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)), d1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));
                Vector3 p0 = b + d0 * r0, p1 = b + d1 * r0, q0 = b + d0 * r1 + Vector3.up * h, q1 = b + d1 * r1 + Vector3.up * h;
                if (r1 > 0.00005f && r0 > 0.00005f)
                    Quad(p0, p1, q1, q0, col, inside);
                else if (r1 <= 0.00005f)
                    Tri(p0, p1, q0, col, inside);
                else
                    Tri(p0, q1, q0, col, inside);
                if (capBottom && r0 > 0.00005f)
                    Tri(b, p0, p1, col, inside);
                if (capTop && r1 > 0.00005f)
                    Tri(b + Vector3.up * h, q0, q1, col, inside);
            }
        }

        //a cylinder from one point to another
        public void Rod(Vector3 from, Vector3 to, float r0, float r1, int sides, string col, bool caps = true)
        {
            Vector3 d = to - from;
            if (d.sqrMagnitude < 1e-12f)
                return;
            Push(from, Quaternion.FromToRotation(Vector3.up, d.normalized));
            Cyl(Vector3.zero, r0, r1, d.magnitude, sides, col, caps, caps);
            Pop();
        }

        //a thin ring around 'axis' (the rod's line guides)
        public void Torus(Vector3 c, Vector3 axis, float radius, float tube, int segs, int tubeSides, string col)
        {
            Quaternion q = Quaternion.FromToRotation(Vector3.up, axis.normalized);
            Vector3 P(int i, int j)
            {
                float a = i / (float)segs * Mathf.PI * 2f, b = j / (float)tubeSides * Mathf.PI * 2f;
                Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                return c + q * (dir * (radius + Mathf.Cos(b) * tube) + Vector3.up * Mathf.Sin(b) * tube);
            }
            for (int i = 0; i < segs; i++)
                for (int j = 0; j < tubeSides; j++)
                {
                    float a = (i + 0.5f) / segs * Mathf.PI * 2f;
                    Vector3 core = c + q * (new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius);
                    Quad(P(i, j), P(i + 1, j), P(i + 1, j + 1), P(i, j + 1), col, core);
                }
        }

        //a UV sphere / ellipsoid; the colour of each face comes from its direction from the centre
        public void Sphere(Vector3 c, Vector3 radii, int lon, int lat, Func<Vector3, string> colour)
        {
            Vector3 P(int i, int j)
            {
                float th = j / (float)lat * Mathf.PI, ph = i / (float)lon * Mathf.PI * 2f;
                return c + Vector3.Scale(new Vector3(Mathf.Sin(th) * Mathf.Cos(ph), Mathf.Cos(th), Mathf.Sin(th) * Mathf.Sin(ph)), radii);
            }
            for (int j = 0; j < lat; j++)
                for (int i = 0; i < lon; i++)
                {
                    Vector3 a = P(i, j), b = P(i + 1, j), cc = P(i + 1, j + 1), d = P(i, j + 1);
                    Vector3 mid = (a + b + cc + d) * 0.25f - c;
                    string col = colour(new Vector3(mid.x / radii.x, mid.y / radii.y, mid.z / radii.z).normalized);
                    if (j == 0)
                        Tri(a, cc, d, col, c);
                    else if (j == lat - 1)
                        Tri(a, b, cc, col, c);
                    else
                        Quad(a, b, cc, d, col, c);
                }
        }

        //a tube through a list of points (the worm); colour per segment
        public void Tube(List<Vector3> pts, List<float> radii, int sides, Func<int, string> colour)
        {
            var rings = new List<Vector3[]>();
            Vector3 prevSide = Vector3.right;
            for (int i = 0; i < pts.Count; i++)
            {
                Vector3 t = (pts[Mathf.Min(i + 1, pts.Count - 1)] - pts[Mathf.Max(i - 1, 0)]).normalized;
                Vector3 side = Vector3.Cross(t, Vector3.Cross(prevSide, t)).normalized;
                if (side.sqrMagnitude < 0.5f)
                    side = Vector3.Cross(t, Vector3.forward).normalized;
                prevSide = side;
                Vector3 up = Vector3.Cross(t, side);
                var ring = new Vector3[sides];
                for (int k = 0; k < sides; k++)
                {
                    float a = k / (float)sides * Mathf.PI * 2f;
                    ring[k] = pts[i] + (side * Mathf.Cos(a) + up * Mathf.Sin(a)) * radii[i];
                }
                rings.Add(ring);
            }
            for (int i = 0; i < pts.Count - 1; i++)
            {
                Vector3 inside = (pts[i] + pts[i + 1]) * 0.5f;
                for (int k = 0; k < sides; k++)
                    Quad(rings[i][k], rings[i][(k + 1) % sides], rings[i + 1][(k + 1) % sides], rings[i + 1][k], colour(i), inside);
            }
            for (int k = 0; k < sides; k++)
            {
                Tri(pts[0], rings[0][k], rings[0][(k + 1) % sides], colour(0), pts[1]);
                int last = pts.Count - 1;
                Tri(pts[last], rings[last][k], rings[last][(k + 1) % sides], colour(last - 1), pts[last - 1]);
            }
        }
    }

    private static Mesh SaveMesh(MB mb, string name)
    {
        string path = MeshDir + "/" + name + ".asset";
        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        bool isNew = mesh == null;
        if (isNew)
            mesh = new Mesh();
        FillMesh(mesh, mb, name);
        if (isNew)
            AssetDatabase.CreateAsset(mesh, path);
        else
            EditorUtility.SetDirty(mesh);
        return mesh;
    }

    private static void FillMesh(Mesh mesh, MB mb, string name)
    {
        mesh.Clear();
        mesh.name = name;
        mesh.indexFormat = mb.verts.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
        mesh.SetVertices(mb.verts);
        mesh.SetNormals(mb.normals);
        mesh.SetUVs(0, mb.uvs);
        var colors = new List<Color>(mb.verts.Count);
        for (int i = 0; i < mb.verts.Count; i++)
            colors.Add(Color.white);
        mesh.SetColors(colors);
        mesh.SetTriangles(mb.tris, 0);
        mesh.RecalculateBounds();
        mesh.RecalculateTangents();
    }

    private static Mesh TempMesh(MB mb, string name)
    {
        var mesh = new Mesh();
        mesh.hideFlags = HideFlags.HideAndDontSave;
        FillMesh(mesh, mb, name);
        return mesh;
    }

    // ================================================================== rod
    //every part is modelled along +y from its own origin. The spinning reel and the guides hang on the +z side, which
    //is "under" the rod once the model is tilted forward in the hand. 'k' thickens everything (for the icon only).

    private const float HandleLength = 0.40f;
    private static readonly float[] SectionLengths = { 0.40f, 0.40f, 0.37f };
    private static readonly float[] SectionRadii = { 0.0095f, 0.0068f, 0.0046f, 0.0022f };

    private static MB RodHandle(float k)
    {
        var mb = new MB { gradLo = 0f, gradHi = HandleLength };
        mb.Cyl(new Vector3(0f, 0f, 0f), 0.017f * k, 0.018f * k, 0.022f, 7, "rubber");
        mb.Cyl(new Vector3(0f, 0.022f, 0f), 0.0175f * k, 0.0165f * k, 0.105f, 7, "cork", false, false);
        mb.Cyl(new Vector3(0f, 0.127f, 0f), 0.0165f * k, 0.0150f * k, 0.105f, 7, "cork", false, false);
        mb.Cyl(new Vector3(0f, 0.232f, 0f), 0.0150f * k, 0.0125f * k, 0.016f, 7, "corkDark", false, true);
        //reel seat with its two metal hoods
        mb.Cyl(new Vector3(0f, 0.248f, 0f), 0.0125f * k, 0.0125f * k, 0.078f, 7, "reel");
        mb.Cyl(new Vector3(0f, 0.256f, 0f), 0.0138f * k, 0.0138f * k, 0.012f, 7, "metal");
        mb.Cyl(new Vector3(0f, 0.312f, 0f), 0.0138f * k, 0.0138f * k, 0.012f, 7, "metal");
        mb.Cyl(new Vector3(0f, 0.326f, 0f), 0.0130f * k, 0.0115f * k, 0.064f, 7, "cork");
        mb.Cyl(new Vector3(0f, 0.390f, 0f), 0.0110f * k, SectionRadii[0] * k, 0.010f, 7, "metalDark");

        //spinning reel under the seat: foot, stem, body, spool, bail and crank
        float z = 0.068f * Mathf.Lerp(1f, k, 0.6f);
        float s = Mathf.Lerp(1f, k, 0.6f);
        mb.Rod(new Vector3(0f, 0.284f, 0.01f * k), new Vector3(0f, 0.290f, z - 0.018f * s), 0.0045f * s, 0.004f * s, 5, "reel");
        mb.Rod(new Vector3(0f, 0.270f, 0.012f * k), new Vector3(0f, 0.300f, 0.012f * k), 0.004f * s, 0.004f * s, 4, "metal");
        mb.Cyl(new Vector3(0f, 0.262f, z), 0.021f * s, 0.024f * s, 0.040f, 8, "reel");
        mb.Cyl(new Vector3(0f, 0.302f, z), 0.024f * s, 0.017f * s, 0.010f, 8, "reelGold");
        mb.Cyl(new Vector3(0f, 0.312f, z), 0.019f * s, 0.019f * s, 0.022f, 8, "metal");
        mb.Cyl(new Vector3(0f, 0.334f, z), 0.021f * s, 0.008f * s, 0.008f, 8, "reel");
        //bail wire: a half hoop in front of the spool
        Vector3 prev = Vector3.zero;
        for (int i = 0; i <= 4; i++)
        {
            float a = Mathf.PI * i / 4f;
            Vector3 p = new Vector3(Mathf.Cos(a) * 0.026f * s, 0.326f, z - Mathf.Sin(a) * 0.026f * s);
            if (i > 0)
                mb.Rod(prev, p, 0.0018f * s, 0.0018f * s, 4, "metal");
            prev = p;
        }
        //crank arm and knob on the left side
        Vector3 crank0 = new Vector3(-0.024f * s, 0.282f, z);
        Vector3 crank1 = new Vector3(-0.040f * s, 0.282f, z);
        Vector3 crank2 = new Vector3(-0.040f * s, 0.282f, z + 0.030f * s);
        mb.Rod(crank0, crank1, 0.003f * s, 0.003f * s, 4, "metal");
        mb.Rod(crank1, crank2, 0.0028f * s, 0.0024f * s, 4, "metal");
        mb.Rod(crank2, crank2 + new Vector3(-0.014f * s, 0f, 0f), 0.0055f * s, 0.0055f * s, 6, "rubber");
        return mb;
    }

    private static MB RodSection(int index, float k)
    {
        float len = SectionLengths[index];
        float r0 = SectionRadii[index] * k, r1 = SectionRadii[index + 1] * k;
        var mb = new MB { gradLo = 0f, gradHi = len };
        if (index > 0)
            mb.Cyl(Vector3.zero, r0 * 1.25f, r0 * 1.15f, 0.025f, 6, "graphiteLight");
        mb.Cyl(Vector3.zero, r0, r1, len, 6, "graphite", index == 0, index < 2);
        float[][] guides = { new[] { 0.26f }, new[] { 0.10f, 0.30f }, new[] { 0.09f, 0.20f, 0.29f } };
        float[] guideSize = { 0.012f, 0.0085f, 0.0058f };
        for (int g = 0; g < guides[index].Length; g++)
        {
            float y = guides[index][g];
            float size = guideSize[index] * (1f - g * 0.12f) * Mathf.Lerp(1f, k, 0.5f);
            float rb = Mathf.Lerp(r0, r1, y / len);
            float centre = rb + size * 1.6f + size;
            mb.Cyl(new Vector3(0f, y - 0.012f, 0f), rb * 1.18f, rb * 1.18f, 0.024f, 6, "graphiteLight", false, false);
            mb.Rod(new Vector3(0f, y - 0.010f, rb), new Vector3(0f, y, centre - size), 0.0012f * k, 0.0012f * k, 3, "metal");
            mb.Rod(new Vector3(0f, y + 0.008f, rb), new Vector3(0f, y, centre - size), 0.0012f * k, 0.0012f * k, 3, "metal");
            mb.Torus(new Vector3(0f, y, centre), Vector3.up, size, 0.0016f * Mathf.Lerp(1f, k, 0.5f), 7, 3, "metal");
        }
        if (index == 2)
        {
            //red wrap just below the tip, and the tip-top ring the line leaves through
            mb.Cyl(new Vector3(0f, 0.300f, 0f), r1 * 1.9f, r1 * 1.8f, 0.035f, 6, "wrapRed", true, true);
            mb.Cyl(new Vector3(0f, len - 0.008f, 0f), r1 * 1.5f, r1 * 1.5f, 0.008f, 6, "metal");
            mb.Torus(new Vector3(0f, len + 0.003f, r1 + 0.0035f * k), Vector3.forward, 0.0035f * k, 0.0012f * k, 6, 3, "metal");
        }
        return mb;
    }

    // ================================================================== bobber and hook models

    private static MB Bobber()
    {
        var mb = new MB { gradLo = -0.06f, gradHi = 0.07f };
        float r = 0.055f;
        Vector3 c = new Vector3(0f, 0.012f, 0f);
        mb.Sphere(c, new Vector3(r, r, r), 10, 6, d => d.y > 0.02f ? "bobberRed" : "bobberWhite");
        mb.Cyl(c + Vector3.up * 0.050f, 0.0062f, 0.0055f, 0.040f, 6, "bobberRed");
        mb.Cyl(c + Vector3.up * 0.090f, 0.0075f, 0.0045f, 0.007f, 6, "bobberWhite");
        mb.Cyl(c + Vector3.down * 0.072f, 0.0028f, 0.0045f, 0.018f, 6, "bobberWhite");
        return mb;
    }

    //a crankbait hanging nose-up from the line tie at the origin: lip, body, eyes and a treble hook at the bottom
    private static MB Lure(bool old)
    {
        var mb = new MB { gradLo = -0.06f, gradHi = 0f };
        string hook = old ? "oldHook" : "hook";
        mb.Torus(new Vector3(0f, -0.0045f, 0f), Vector3.forward, 0.0038f, 0.0009f, 6, 3, hook);
        Vector3 c = new Vector3(0f, -0.031f, 0f);
        Vector3 radii = new Vector3(0.0095f, 0.022f, 0.0125f);
        int face = 0;
        mb.Sphere(c, radii, 8, 6, d =>
        {
            face++;
            if (d.y > 0.55f && Mathf.Abs(d.x) > 0.5f && d.z > -0.3f && d.z < 0.5f)
                return old ? "oldGreen" : "lureBlack";//eye
            if (old)
            {
                if ((face * 7919 % 11) < 3)
                    return "oldScratch";
                if ((face * 104729 % 13) < 2)
                    return "oldRust";
                return d.z > 0.25f ? "oldGreen" : d.z < -0.4f ? "oldBelly" : "oldRust";
            }
            if (d.z > 0.25f)
                return "chartreuse";
            return d.z < -0.45f ? "lureBelly" : "lureOrange";
        });
        //eyes as small bumps
        foreach (int sx in new[] { -1, 1 })
            mb.Sphere(c + new Vector3(sx * 0.0078f, 0.012f, 0.002f), Vector3.one * 0.0035f, 5, 3, d => old ? "oldScratch" : (d.x * sx > 0.5f ? "lureBlack" : "lureBelly"));
        //lip: a plate angled forward at the nose
        string lip = old ? "oldScratch" : "lureLip";
        Vector3 l0 = new Vector3(-0.0075f, -0.010f, -0.004f), l1 = new Vector3(0.0075f, -0.010f, -0.004f);
        Vector3 l2 = new Vector3(0.0085f, -0.002f, -0.018f), l3 = new Vector3(-0.0085f, -0.002f, -0.018f);
        mb.DTri(l0, l1, l2, lip);
        mb.DTri(l0, l2, l3, lip);
        //treble hook
        Vector3 top = new Vector3(0f, -0.051f, 0f), bend = new Vector3(0f, -0.064f, 0f);
        mb.Torus(top + Vector3.down * 0.002f, Vector3.right, 0.0025f, 0.0008f, 5, 3, hook);
        mb.Rod(top, bend, 0.0011f, 0.0011f, 3, hook);
        for (int i = 0; i < 3; i++)
        {
            float a = i * Mathf.PI * 2f / 3f + 0.4f;
            Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            Vector3 low = bend + dir * 0.004f + Vector3.down * 0.004f;
            Vector3 up = bend + dir * 0.0085f + Vector3.up * 0.002f;
            Vector3 point = up - dir * 0.002f + Vector3.up * 0.003f;
            mb.Rod(bend, low, 0.0011f, 0.0011f, 3, hook);
            mb.Rod(low, up, 0.0011f, 0.0010f, 3, hook);
            mb.Rod(up, point, 0.0010f, 0.0002f, 3, hook);
        }
        return mb;
    }

    private static List<Vector3> CatmullRom(Vector3[] ctrl, int steps)
    {
        var pts = new List<Vector3>();
        for (int i = 0; i < ctrl.Length - 1; i++)
        {
            Vector3 p0 = ctrl[Mathf.Max(i - 1, 0)], p1 = ctrl[i], p2 = ctrl[i + 1], p3 = ctrl[Mathf.Min(i + 2, ctrl.Length - 1)];
            for (int s = 0; s < steps; s++)
            {
                float t = s / (float)steps, t2 = t * t, t3 = t2 * t;
                pts.Add(0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3));
            }
        }
        pts.Add(ctrl[ctrl.Length - 1]);
        return pts;
    }

    //a short bent worm along the given control points, with the lighter band (clitellum) a third of the way along
    private static void AddWorm(MB mb, Vector3[] ctrl, float radius)
    {
        List<Vector3> pts = CatmullRom(ctrl, 3);
        var radii = new List<float>();
        for (int i = 0; i < pts.Count; i++)
        {
            float t = i / (float)(pts.Count - 1);
            radii.Add(radius * Mathf.Lerp(0.55f, 1f, Mathf.Sin(Mathf.PI * Mathf.Clamp01(t * 1.15f))) );
        }
        int band = Mathf.RoundToInt((pts.Count - 1) * 0.3f);
        mb.Tube(pts, radii, 5, i => i == band || i == band + 1 ? "wormBand" : (i % 3 == 0 ? "wormDark" : "worm"));
    }

    private static MB WormHook()
    {
        var mb = new MB { gradLo = -0.08f, gradHi = 0f };
        mb.Torus(new Vector3(0f, -0.003f, 0f), Vector3.forward, 0.0028f, 0.0009f, 6, 3, "hook");
        mb.Rod(new Vector3(0f, -0.006f, 0f), new Vector3(0f, -0.016f, 0f), 0.0011f, 0.0011f, 3, "hook");
        AddWorm(mb, new[]
        {
            new Vector3(0f, -0.010f, 0f), new Vector3(0.002f, -0.024f, 0.002f), new Vector3(-0.002f, -0.037f, 0.004f),
            new Vector3(-0.012f, -0.047f, 0.001f), new Vector3(-0.017f, -0.060f, -0.004f), new Vector3(-0.009f, -0.071f, -0.006f),
            new Vector3(0.003f, -0.074f, -0.004f),
        }, 0.0042f);
        return mb;
    }

    // ================================================================== effect meshes

    private static Mesh SaveEffectMesh(string name, List<Vector3> v, List<Vector2> uv, List<int> t)
    {
        string path = MeshDir + "/" + name + ".asset";
        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        bool isNew = mesh == null;
        if (isNew)
            mesh = new Mesh();
        mesh.Clear();
        mesh.name = name;
        mesh.SetVertices(v);
        var n = new List<Vector3>();
        var c = new List<Color>();
        for (int i = 0; i < v.Count; i++)
        {
            n.Add(Vector3.up);
            c.Add(Color.white);
        }
        mesh.SetNormals(n);
        mesh.SetColors(c);
        mesh.SetUVs(0, uv);
        mesh.SetTriangles(t, 0);
        mesh.RecalculateBounds();
        if (isNew)
            AssetDatabase.CreateAsset(mesh, path);
        else
            EditorUtility.SetDirty(mesh);
        return mesh;
    }

    //flat ring (outer radius 0.5) and a faint inner disc in XZ, facing up; the two alphas come from the GhostMarker strip
    private static void BuildGhostMarker()
    {
        const int segs = 48;
        const float outer = 0.5f, inner = 0.43f;
        var v = new List<Vector3>();
        var uv = new List<Vector2>();
        var t = new List<int>();
        v.Add(Vector3.zero);
        uv.Add(new Vector2(0.25f, 0.5f));
        for (int i = 0; i < segs; i++)
        {
            float a = i / (float)segs * Mathf.PI * 2f;
            v.Add(new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * inner);
            uv.Add(new Vector2(0.25f, 0.5f));
        }
        for (int i = 0; i < segs; i++)
        {
            t.Add(0); t.Add(1 + (i + 1) % segs); t.Add(1 + i);
        }
        int ring = v.Count;
        for (int i = 0; i < segs; i++)
        {
            float a = i / (float)segs * Mathf.PI * 2f;
            Vector3 d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            v.Add(d * inner); uv.Add(new Vector2(0.75f, 0.5f));
            v.Add(d * outer); uv.Add(new Vector2(0.75f, 0.5f));
        }
        for (int i = 0; i < segs; i++)
        {
            int a0 = ring + i * 2, a1 = ring + i * 2 + 1, b0 = ring + ((i + 1) % segs) * 2, b1 = b0 + 1;
            t.Add(a0); t.Add(b1); t.Add(a1);
            t.Add(a0); t.Add(b0); t.Add(b1);
        }
        SaveEffectMesh("CastGhostMarker", v, uv, t);
    }

    private static void BuildShadowQuad()
    {
        var v = new List<Vector3> { new Vector3(-0.5f, 0f, -0.5f), new Vector3(0.5f, 0f, -0.5f), new Vector3(0.5f, 0f, 0.5f), new Vector3(-0.5f, 0f, 0.5f) };
        var uv = new List<Vector2> { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) };
        var t = new List<int> { 0, 2, 1, 0, 3, 2 };
        SaveEffectMesh("ShadowQuad", v, uv, t);
    }

    // ================================================================== all meshes

    private static readonly string[] FishNames = { "Largemouth Bass", "Bluegill", "Crappie", "Catfish" };
    private static string FishMeshName(string fish) { return "Fish" + fish.Replace(" ", ""); }

    public static string BuildMeshes()
    {
        EnsureFolder(MeshDir);
        var log = new List<string>();
        void Save(MB mb, string name)
        {
            SaveMesh(mb, name);
            log.Add(name + " " + mb.TriCount);
        }
        Save(RodHandle(1f), "FishingRodHandle");
        for (int i = 0; i < 3; i++)
            Save(RodSection(i, 1f), "FishingRodSection" + (i + 1));
        Save(Bobber(), "Bobber");
        Save(Lure(false), "Lure");
        Save(Lure(true), "OldLure");
        Save(WormHook(), "Worm");
        foreach (string fish in FishNames)
            Save(FishMesh(fish), FishMeshName(fish));
        BuildGhostMarker();
        BuildShadowQuad();
        return "meshes (tris): " + string.Join(", ", log);
    }
}
