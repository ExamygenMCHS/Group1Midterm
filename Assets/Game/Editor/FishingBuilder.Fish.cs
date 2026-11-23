using System;
using System.Collections.Generic;
using UnityEngine;

//the four fish, each exactly 1 unit from nose (+z 0.5) to tail tip (-z 0.5), upright, centred on the origin.
//The body is lofted through cross sections (8 sides, flat shaded); markings are thin "decals" lying just above the
//body faces; fins are double-sided fans.
public static partial class FishingBuilder
{
    private const int FishSides = 8;

    //one cross section: z, centre height, height above the centre, depth below it, half width
    private struct Station
    {
        public float z, c, top, bot, w;
        public Station(float z, float c, float top, float bot, float w) { this.z = z; this.c = c; this.top = top; this.bot = bot; this.w = w; }
    }

    private class FishBody
    {
        public Station[] st;//nose first
        public float noseY;//the snout tip height (z 0.5)

        public Station At(float z)
        {
            if (z >= st[0].z)
                return st[0];
            for (int i = 0; i < st.Length - 1; i++)
                if (z >= st[i + 1].z)
                {
                    float t = (st[i].z - z) / (st[i].z - st[i + 1].z);
                    return new Station(z, Mathf.Lerp(st[i].c, st[i + 1].c, t), Mathf.Lerp(st[i].top, st[i + 1].top, t),
                        Mathf.Lerp(st[i].bot, st[i + 1].bot, t), Mathf.Lerp(st[i].w, st[i + 1].w, t));
                }
            return st[st.Length - 1];
        }

        public static Vector3 Vert(Station s, int k)
        {
            float a = k * Mathf.PI * 2f / FishSides;
            float cos = Mathf.Cos(a), sin = Mathf.Sin(a);
            return new Vector3(s.w * sin, s.c + (cos >= 0f ? s.top : s.bot) * cos, s.z);
        }

        //the point on the flat-shaded body at (z, y) on one side, and that face's outward normal
        public Vector3 Surface(int side, float z, float y, out Vector3 normal)
        {
            Station s = At(z);
            float r = y >= s.c ? s.top : s.bot;
            float q = Mathf.Clamp((y - s.c) / Mathf.Max(1e-4f, r), -0.995f, 0.995f);
            float theta = Mathf.Acos(q);
            if (side < 0)
                theta = Mathf.PI * 2f - theta;
            float step = Mathf.PI * 2f / FishSides;
            int k = Mathf.Clamp(Mathf.FloorToInt(theta / step), 0, FishSides - 1);
            float f = theta / step - k;
            Vector3 p = Vector3.Lerp(Vert(s, k), Vert(s, k + 1), f);
            Station s0 = At(z + 0.01f), s1 = At(z - 0.01f);
            Vector3 along = Vector3.Lerp(Vert(s0, k), Vert(s0, k + 1), f) - Vector3.Lerp(Vert(s1, k), Vert(s1, k + 1), f);
            Vector3 around = Vert(s, k + 1) - Vert(s, k);
            normal = Vector3.Cross(around, along).normalized;
            if (Vector3.Dot(normal, new Vector3(p.x, p.y - s.c, 0f)) < 0f)
                normal = -normal;
            return p;
        }
    }

    //colour of a body face from its z and how far up it faces (cos of its angle from the top: 1 back, -1 belly)
    private delegate string BodyPaint(float z, float up);

    private static void AddBody(MB mb, FishBody body, BodyPaint paint)
    {
        Station[] st = body.st;
        for (int i = 0; i < st.Length - 1; i++)
        {
            Vector3 inside = new Vector3(0f, (st[i].c + st[i + 1].c) * 0.5f, (st[i].z + st[i + 1].z) * 0.5f);
            for (int k = 0; k < FishSides; k++)
            {
                float mid = (k + 0.5f) * Mathf.PI * 2f / FishSides;
                string col = paint((st[i].z + st[i + 1].z) * 0.5f, Mathf.Cos(mid));
                mb.Quad(FishBody.Vert(st[i], k), FishBody.Vert(st[i], k + 1), FishBody.Vert(st[i + 1], k + 1), FishBody.Vert(st[i + 1], k), col, inside);
            }
        }
        //snout point and the closed tail root
        Vector3 nose = new Vector3(0f, body.noseY, 0.5f);
        Vector3 back = new Vector3(0f, st[0].c, st[0].z - 0.02f);
        int last = st.Length - 1;
        Vector3 tailRoot = new Vector3(0f, st[last].c, st[last].z);
        for (int k = 0; k < FishSides; k++)
        {
            float mid = (k + 0.5f) * Mathf.PI * 2f / FishSides;
            mb.Tri(nose, FishBody.Vert(st[0], k), FishBody.Vert(st[0], k + 1), paint(0.5f, Mathf.Cos(mid)), back);
            mb.Tri(tailRoot, FishBody.Vert(st[last], k), FishBody.Vert(st[last], k + 1), paint(st[last].z, Mathf.Cos(mid)), tailRoot + Vector3.forward * 0.02f);
        }
    }

    //a marking on one side of the body: a convex outline in (z, y), split into small triangles that follow the faces
    private static void Decal(MB mb, FishBody body, int side, Vector2[] zy, string col, float eps = 0.0025f)
    {
        Vector2 centre = Vector2.zero;
        foreach (Vector2 p in zy)
            centre += p;
        centre /= zy.Length;
        float size = 0f;
        foreach (Vector2 p in zy)
            size = Mathf.Max(size, (p - centre).magnitude);
        bool split = size > 0.035f;
        Vector3 Project(Vector2 p)
        {
            Vector3 pos = body.Surface(side, p.x, p.y, out Vector3 n);
            return pos + n * eps;
        }
        for (int i = 0; i < zy.Length; i++)
        {
            Vector2 a = centre, b = zy[i], c = zy[(i + 1) % zy.Length];
            Vector3 facing = new Vector3(side, 0f, 0f);
            if (!split)
            {
                mb.Tri(Project(a), Project(b), Project(c), col, Vector3.zero, facing);
                continue;
            }
            Vector2 ab = (a + b) * 0.5f, bc = (b + c) * 0.5f, ca = (c + a) * 0.5f;
            mb.Tri(Project(a), Project(ab), Project(ca), col, Vector3.zero, facing);
            mb.Tri(Project(ab), Project(b), Project(bc), col, Vector3.zero, facing);
            mb.Tri(Project(ca), Project(bc), Project(c), col, Vector3.zero, facing);
            mb.Tri(Project(ab), Project(bc), Project(ca), col, Vector3.zero, facing);
        }
    }

    private static Vector2[] Circle(float z, float y, float r, int n, float squash = 1f)
    {
        var pts = new Vector2[n];
        for (int i = 0; i < n; i++)
        {
            float a = i * Mathf.PI * 2f / n;
            pts[i] = new Vector2(z + Mathf.Cos(a) * r, y + Mathf.Sin(a) * r * squash);
        }
        return pts;
    }

    //a thin curved line on the body (gill cover edge, cheek streak)
    private static void StripDecal(MB mb, FishBody body, int side, Vector2[] pts, float width, string col)
    {
        for (int i = 0; i < pts.Length - 1; i++)
        {
            Vector2 d = (pts[i + 1] - pts[i]).normalized;
            Vector2 n = new Vector2(-d.y, d.x) * width * 0.5f;
            Decal(mb, body, side, new[] { pts[i] - n, pts[i + 1] - n, pts[i + 1] + n, pts[i] + n }, col, 0.003f);
        }
    }

    private static void Eye(MB mb, FishBody body, float z, float y, float r)
    {
        foreach (int side in new[] { -1, 1 })
        {
            Decal(mb, body, side, Circle(z, y, r, 8), "eyeGold", 0.003f);
            Decal(mb, body, side, Circle(z - r * 0.08f, y, r * 0.58f, 7), "eyeBlack", 0.0045f);
            Decal(mb, body, side, Circle(z + r * 0.18f, y + r * 0.22f, r * 0.17f, 5), "eyeWhite", 0.0055f);
        }
    }

    //a fin in the middle plane (x = 0): a fan of quads between base points on the body and the outer edge
    private static void MidFin(MB mb, Vector2[] baseZY, Vector2[] edgeZY, Func<int, string> col)
    {
        for (int i = 0; i < baseZY.Length - 1; i++)
        {
            Vector3 b0 = new Vector3(0f, baseZY[i].y, baseZY[i].x), b1 = new Vector3(0f, baseZY[i + 1].y, baseZY[i + 1].x);
            Vector3 e0 = new Vector3(0f, edgeZY[i].y, edgeZY[i].x), e1 = new Vector3(0f, edgeZY[i + 1].y, edgeZY[i + 1].x);
            mb.DTri(b0, b1, e1, col(i));
            mb.DTri(b0, e1, e0, col(i));
        }
    }

    //dorsal (sign 1) or anal (sign -1) fin: (z, height) pairs of the outer edge; the base follows the body
    private static void BackFin(MB mb, FishBody body, int sign, Vector2[] edge, float rake, Func<int, string> col)
    {
        var baseZY = new Vector2[edge.Length];
        var edgeZY = new Vector2[edge.Length];
        for (int i = 0; i < edge.Length; i++)
        {
            Station s = body.At(edge[i].x);
            float y = s.c + sign * (sign > 0 ? s.top : s.bot) * 0.82f;
            baseZY[i] = new Vector2(edge[i].x, y);
            Station se = body.At(edge[i].x - rake);
            float ye = se.c + sign * ((sign > 0 ? se.top : se.bot) + edge[i].y);
            edgeZY[i] = new Vector2(edge[i].x - rake, ye);
        }
        MidFin(mb, baseZY, edgeZY, col);
    }

    //the tail: a fan from its root through the outline points (top tip ... bottom tip)
    private static void TailFin(MB mb, Vector2 root, Vector2[] outline, Func<int, string> col)
    {
        Vector3 r = new Vector3(0f, root.y, root.x);
        for (int i = 0; i < outline.Length - 1; i++)
            mb.DTri(r, new Vector3(0f, outline[i].y, outline[i].x), new Vector3(0f, outline[i + 1].y, outline[i + 1].x), col(i));
    }

    //a paired fin (pectoral, pelvic) on both sides: two base points on the body and a tip direction
    private static void SideFins(MB mb, FishBody body, Vector2 baseA, Vector2 baseB, Vector3 tipOffset, float spread, string col)
    {
        foreach (int side in new[] { -1, 1 })
        {
            Vector3 a = body.Surface(side, baseA.x, baseA.y, out Vector3 na) - na * 0.004f;
            Vector3 b = body.Surface(side, baseB.x, baseB.y, out Vector3 nb) - nb * 0.004f;
            Vector3 off = new Vector3(tipOffset.x * side, tipOffset.y, tipOffset.z);
            Vector3 tip = (a + b) * 0.5f + off;
            Vector3 tip2 = tip + new Vector3(0f, spread, spread * 0.4f);
            mb.DTri(a, b, tip, col);
            mb.DTri(a, tip, tip2, col);
        }
    }

    private static float Hash01(int a, int b)
    {
        unchecked
        {
            uint h = (uint)a * 374761393u + (uint)b * 668265263u;
            h = (h ^ (h >> 13)) * 1274126177u;
            h ^= h >> 16;
            return (h & 0xFFFFFF) / 16777215f;
        }
    }

    private static Vector2[] Blob(float z, float y, float r, int seed, int n = 5)
    {
        var pts = new Vector2[n];
        for (int i = 0; i < n; i++)
        {
            float a = i * Mathf.PI * 2f / n + Hash01(seed, 99) * 2f;
            float k = 0.7f + Hash01(seed, i) * 0.5f;
            pts[i] = new Vector2(z + Mathf.Cos(a) * r * k * 1.3f, y + Mathf.Sin(a) * r * k);
        }
        return pts;
    }

    private static MB FishMesh(string fish)
    {
        switch (fish)
        {
            case "Largemouth Bass": return Bass();
            case "Bluegill": return Bluegill();
            case "Crappie": return Crappie();
            default: return Catfish();
        }
    }

    // ---------------------------------------------------------------- largemouth bass
    private static MB Bass()
    {
        var mb = new MB { gradLo = -0.15f, gradHi = 0.15f };
        var body = new FishBody
        {
            noseY = 0.0f,
            st = new[]
            {
                new Station(0.475f, 0.004f, 0.026f, 0.030f, 0.024f),
                new Station(0.43f, 0.008f, 0.062f, 0.060f, 0.050f),
                new Station(0.35f, 0.010f, 0.098f, 0.084f, 0.072f),
                new Station(0.24f, 0.010f, 0.124f, 0.098f, 0.083f),
                new Station(0.11f, 0.008f, 0.132f, 0.102f, 0.083f),
                new Station(-0.02f, 0.006f, 0.122f, 0.094f, 0.074f),
                new Station(-0.14f, 0.004f, 0.098f, 0.076f, 0.058f),
                new Station(-0.24f, 0.002f, 0.064f, 0.052f, 0.038f),
                new Station(-0.31f, 0.0f, 0.040f, 0.034f, 0.021f),
            },
        };
        AddBody(mb, body, (z, up) => up > 0.7f ? "bassBack" : up > 0f ? "bassUpper" : up > -0.7f ? (z > 0.36f ? "bassUpper" : "bassLower") : "bassBelly");
        foreach (int side in new[] { -1, 1 })
        {
            //the dark, broken stripe along the side
            for (int i = 0; i < 9; i++)
            {
                float z = 0.27f - i * 0.065f;
                float y = body.At(z).c + 0.004f + (Hash01(i, 3) - 0.5f) * 0.012f;
                float h = 0.016f + Hash01(i, 5) * 0.012f;
                float len = 0.026f + Hash01(i, 7) * 0.012f;
                Decal(mb, body, side, new[]
                {
                    new Vector2(z + len, y), new Vector2(z + len * 0.4f, y + h), new Vector2(z - len * 0.6f, y + h * 0.8f),
                    new Vector2(z - len, y), new Vector2(z - len * 0.4f, y - h), new Vector2(z + len * 0.5f, y - h * 0.8f),
                }, "bassStripe");
            }
            //the big mouth, reaching back past the eye
            Decal(mb, body, side, new[] { new Vector2(0.50f, 0.000f), new Vector2(0.47f, 0.004f), new Vector2(0.345f, -0.012f), new Vector2(0.34f, -0.026f), new Vector2(0.47f, -0.014f) }, "mouth", 0.004f);
            Decal(mb, body, side, new[] { new Vector2(0.347f, -0.006f), new Vector2(0.333f, -0.008f), new Vector2(0.330f, -0.030f), new Vector2(0.345f, -0.032f) }, "bassLower", 0.0045f);
            StripDecal(mb, body, side, new[] { new Vector2(0.335f, 0.075f), new Vector2(0.305f, 0.040f), new Vector2(0.295f, 0.0f), new Vector2(0.305f, -0.045f), new Vector2(0.33f, -0.07f) }, 0.007f, "gillLine");
        }
        Eye(mb, body, 0.405f, 0.035f, 0.021f);
        Func<int, string> fin = i => "bassFin";
        //notched dorsal: spiny front, a dip, then the soft rear part
        BackFin(mb, body, 1, new[]
        {
            new Vector2(0.24f, 0.0f), new Vector2(0.215f, 0.075f), new Vector2(0.18f, 0.052f), new Vector2(0.15f, 0.072f), new Vector2(0.115f, 0.048f),
            new Vector2(0.08f, 0.060f), new Vector2(0.045f, 0.026f), new Vector2(0.02f, 0.012f), new Vector2(-0.005f, 0.060f),
            new Vector2(-0.05f, 0.072f), new Vector2(-0.10f, 0.066f), new Vector2(-0.15f, 0.048f), new Vector2(-0.18f, 0.0f),
        }, 0.02f, fin);
        BackFin(mb, body, -1, new[] { new Vector2(-0.06f, 0.0f), new Vector2(-0.085f, 0.060f), new Vector2(-0.13f, 0.058f), new Vector2(-0.17f, 0.036f), new Vector2(-0.19f, 0.0f) }, 0.02f, fin);
        TailFin(mb, new Vector2(-0.29f, 0.0f), new[]
        {
            new Vector2(-0.30f, 0.036f), new Vector2(-0.40f, 0.100f), new Vector2(-0.50f, 0.128f), new Vector2(-0.475f, 0.06f),
            new Vector2(-0.455f, 0.0f), new Vector2(-0.475f, -0.06f), new Vector2(-0.50f, -0.128f), new Vector2(-0.40f, -0.100f), new Vector2(-0.30f, -0.036f),
        }, fin);
        SideFins(mb, body, new Vector2(0.29f, -0.02f), new Vector2(0.27f, -0.045f), new Vector3(0.03f, -0.025f, -0.085f), 0.02f, "bassFin");
        SideFins(mb, body, new Vector2(0.22f, -0.080f), new Vector2(0.18f, -0.085f), new Vector3(0.018f, -0.05f, -0.06f), 0.0f, "bassFin");
        return mb;
    }

    // ---------------------------------------------------------------- bluegill
    private static MB Bluegill()
    {
        var mb = new MB { gradLo = -0.2f, gradHi = 0.2f };
        var body = new FishBody
        {
            noseY = 0.004f,
            st = new[]
            {
                new Station(0.48f, 0.004f, 0.022f, 0.022f, 0.016f),
                new Station(0.44f, 0.006f, 0.070f, 0.058f, 0.036f),
                new Station(0.37f, 0.008f, 0.130f, 0.110f, 0.050f),
                new Station(0.27f, 0.010f, 0.178f, 0.158f, 0.058f),
                new Station(0.15f, 0.010f, 0.198f, 0.178f, 0.060f),
                new Station(0.03f, 0.008f, 0.192f, 0.172f, 0.057f),
                new Station(-0.09f, 0.006f, 0.160f, 0.146f, 0.050f),
                new Station(-0.19f, 0.004f, 0.106f, 0.098f, 0.038f),
                new Station(-0.27f, 0.002f, 0.052f, 0.048f, 0.022f),
                new Station(-0.30f, 0.0f, 0.040f, 0.036f, 0.017f),
            },
        };
        AddBody(mb, body, (z, up) =>
        {
            if (up > 0.7f)
                return "gillBack";
            if (up > 0f)
                return "gillUpper";
            if (up > -0.7f)
                return z > -0.02f && z < 0.40f ? "gillBreast" : "gillLower";
            return z > -0.12f ? "gillBreast" : "gillBelly";
        });
        foreach (int side in new[] { -1, 1 })
        {
            //faint dark vertical bars
            for (int i = 0; i < 6; i++)
            {
                float z = 0.23f - i * 0.085f;
                Station s = body.At(z);
                float yTop = s.c + s.top * 0.78f, yLow = s.c - s.bot * 0.15f;
                Decal(mb, body, side, new[] { new Vector2(z + 0.016f, yTop), new Vector2(z - 0.016f, yTop), new Vector2(z - 0.020f, yLow), new Vector2(z + 0.012f, yLow) }, "gillBar");
            }
            //the black "ear" flap on the gill cover, and the blue streak on the cheek
            Decal(mb, body, side, new[] { new Vector2(0.345f, 0.044f), new Vector2(0.300f, 0.058f), new Vector2(0.250f, 0.042f), new Vector2(0.240f, 0.012f), new Vector2(0.280f, -0.006f), new Vector2(0.340f, 0.008f) }, "gillEar", 0.0045f);
            StripDecal(mb, body, side, new[] { new Vector2(0.47f, -0.006f), new Vector2(0.41f, -0.022f), new Vector2(0.36f, -0.040f) }, 0.010f, "gillBlue");
            StripDecal(mb, body, side, new[] { new Vector2(0.36f, 0.09f), new Vector2(0.335f, 0.05f), new Vector2(0.33f, 0.0f), new Vector2(0.345f, -0.06f), new Vector2(0.37f, -0.10f) }, 0.007f, "gillLine");
            //a small mouth
            Decal(mb, body, side, new[] { new Vector2(0.50f, 0.004f), new Vector2(0.47f, 0.008f), new Vector2(0.462f, -0.002f), new Vector2(0.49f, -0.004f) }, "mouth", 0.004f);
        }
        Eye(mb, body, 0.425f, 0.036f, 0.020f);
        Func<int, string> fin = i => "gillFin";
        BackFin(mb, body, 1, new[]
        {
            new Vector2(0.26f, 0.0f), new Vector2(0.24f, 0.050f), new Vector2(0.20f, 0.056f), new Vector2(0.15f, 0.058f), new Vector2(0.10f, 0.060f),
            new Vector2(0.05f, 0.064f), new Vector2(0.0f, 0.082f), new Vector2(-0.06f, 0.090f), new Vector2(-0.12f, 0.070f), new Vector2(-0.17f, 0.030f), new Vector2(-0.19f, 0.0f),
        }, 0.015f, i => i >= 6 ? "gillFin" : "gillBack");
        BackFin(mb, body, -1, new[] { new Vector2(0.02f, 0.0f), new Vector2(-0.01f, 0.060f), new Vector2(-0.07f, 0.082f), new Vector2(-0.13f, 0.064f), new Vector2(-0.18f, 0.025f), new Vector2(-0.20f, 0.0f) }, 0.015f, fin);
        TailFin(mb, new Vector2(-0.28f, 0.0f), new[]
        {
            new Vector2(-0.29f, 0.038f), new Vector2(-0.40f, 0.098f), new Vector2(-0.50f, 0.118f), new Vector2(-0.48f, 0.05f),
            new Vector2(-0.465f, 0.0f), new Vector2(-0.48f, -0.05f), new Vector2(-0.50f, -0.118f), new Vector2(-0.40f, -0.098f), new Vector2(-0.29f, -0.038f),
        }, fin);
        SideFins(mb, body, new Vector2(0.30f, -0.03f), new Vector2(0.28f, -0.06f), new Vector3(0.03f, 0.0f, -0.12f), 0.02f, "gillFin");
        SideFins(mb, body, new Vector2(0.24f, -0.150f), new Vector2(0.20f, -0.160f), new Vector3(0.015f, -0.06f, -0.05f), 0.0f, "gillFin");
        return mb;
    }

    // ---------------------------------------------------------------- black crappie
    private static MB Crappie()
    {
        var mb = new MB { gradLo = -0.2f, gradHi = 0.2f };
        var body = new FishBody
        {
            noseY = 0.016f,
            st = new[]
            {
                new Station(0.48f, 0.008f, 0.020f, 0.028f, 0.016f),
                new Station(0.44f, 0.004f, 0.048f, 0.068f, 0.032f),
                new Station(0.36f, 0.004f, 0.088f, 0.118f, 0.046f),
                new Station(0.26f, 0.006f, 0.138f, 0.152f, 0.054f),
                new Station(0.14f, 0.006f, 0.166f, 0.168f, 0.057f),
                new Station(0.02f, 0.006f, 0.162f, 0.164f, 0.054f),
                new Station(-0.10f, 0.004f, 0.132f, 0.132f, 0.046f),
                new Station(-0.20f, 0.002f, 0.082f, 0.082f, 0.034f),
                new Station(-0.27f, 0.0f, 0.046f, 0.046f, 0.021f),
                new Station(-0.30f, 0.0f, 0.038f, 0.038f, 0.017f),
            },
        };
        AddBody(mb, body, (z, up) => up > 0.7f ? "crapBack" : up > 0f ? "crapUpper" : up > -0.7f ? "crapLower" : "crapBelly");
        foreach (int side in new[] { -1, 1 })
        {
            //dark speckles, densest on the upper sides
            for (int i = 0; i < 22; i++)
            {
                int seed = i * 3 + (side > 0 ? 1 : 2);
                float z = Mathf.Lerp(0.30f, -0.24f, Hash01(seed, 11));
                Station s = body.At(z);
                float v = Mathf.Pow(Hash01(seed, 13), 0.7f);
                float y = Mathf.Lerp(s.c + s.top * 0.8f, s.c - s.bot * 0.55f, v);
                float r = 0.010f + Hash01(seed, 17) * 0.012f;
                Decal(mb, body, side, Blob(z, y, r, seed), "crapSpot");
            }
            //upturned mouth: the gape runs from the snout tip down and back
            Decal(mb, body, side, new[] { new Vector2(0.50f, 0.018f), new Vector2(0.485f, 0.020f), new Vector2(0.425f, -0.022f), new Vector2(0.42f, -0.036f), new Vector2(0.49f, 0.004f) }, "mouth", 0.004f);
            StripDecal(mb, body, side, new[] { new Vector2(0.36f, 0.075f), new Vector2(0.335f, 0.035f), new Vector2(0.33f, -0.01f), new Vector2(0.345f, -0.07f), new Vector2(0.375f, -0.11f) }, 0.007f, "gillLine");
        }
        Eye(mb, body, 0.415f, 0.030f, 0.026f);
        Func<int, string> mottled = i => Hash01(i, 41) > 0.55f ? "crapFinSpot" : "crapFin";
        //big fan-like dorsal and anal fins of about the same size, set far back
        BackFin(mb, body, 1, new[]
        {
            new Vector2(0.03f, 0.0f), new Vector2(0.01f, 0.070f), new Vector2(-0.03f, 0.110f), new Vector2(-0.075f, 0.135f),
            new Vector2(-0.12f, 0.132f), new Vector2(-0.165f, 0.105f), new Vector2(-0.21f, 0.060f), new Vector2(-0.235f, 0.0f),
        }, 0.03f, mottled);
        BackFin(mb, body, -1, new[]
        {
            new Vector2(0.05f, 0.0f), new Vector2(0.03f, 0.065f), new Vector2(-0.01f, 0.105f), new Vector2(-0.06f, 0.128f),
            new Vector2(-0.11f, 0.124f), new Vector2(-0.16f, 0.098f), new Vector2(-0.205f, 0.055f), new Vector2(-0.235f, 0.0f),
        }, 0.03f, mottled);
        TailFin(mb, new Vector2(-0.28f, 0.0f), new[]
        {
            new Vector2(-0.29f, 0.036f), new Vector2(-0.40f, 0.100f), new Vector2(-0.50f, 0.122f), new Vector2(-0.47f, 0.05f),
            new Vector2(-0.45f, 0.0f), new Vector2(-0.47f, -0.05f), new Vector2(-0.50f, -0.122f), new Vector2(-0.40f, -0.100f), new Vector2(-0.29f, -0.036f),
        }, mottled);
        SideFins(mb, body, new Vector2(0.31f, -0.03f), new Vector2(0.29f, -0.06f), new Vector3(0.028f, 0.0f, -0.10f), 0.02f, "crapFin");
        SideFins(mb, body, new Vector2(0.25f, -0.135f), new Vector2(0.21f, -0.150f), new Vector3(0.015f, -0.06f, -0.05f), 0.0f, "crapFin");
        return mb;
    }

    // ---------------------------------------------------------------- channel catfish
    private static MB Catfish()
    {
        var mb = new MB { gradLo = -0.12f, gradHi = 0.12f };
        var body = new FishBody
        {
            noseY = -0.004f,
            st = new[]
            {
                new Station(0.48f, -0.004f, 0.026f, 0.020f, 0.058f),
                new Station(0.44f, -0.002f, 0.048f, 0.036f, 0.084f),
                new Station(0.37f, 0.002f, 0.070f, 0.050f, 0.098f),
                new Station(0.29f, 0.006f, 0.088f, 0.060f, 0.094f),
                new Station(0.19f, 0.008f, 0.098f, 0.068f, 0.084f),
                new Station(0.07f, 0.008f, 0.098f, 0.072f, 0.074f),
                new Station(-0.05f, 0.006f, 0.088f, 0.068f, 0.062f),
                new Station(-0.15f, 0.004f, 0.072f, 0.058f, 0.048f),
                new Station(-0.23f, 0.002f, 0.050f, 0.042f, 0.032f),
                new Station(-0.29f, 0.0f, 0.032f, 0.028f, 0.018f),
            },
        };
        AddBody(mb, body, (z, up) => up > 0.7f ? "catBack" : up > 0f ? "catUpper" : up > -0.7f ? (z > 0.40f ? "catUpper" : "catLower") : "catBelly");
        foreach (int side in new[] { -1, 1 })
        {
            //a few small dark spots, like a young channel cat
            for (int i = 0; i < 9; i++)
            {
                int seed = i * 5 + (side > 0 ? 3 : 4);
                float z = Mathf.Lerp(0.22f, -0.22f, Hash01(seed, 21));
                Station s = body.At(z);
                float y = Mathf.Lerp(s.c + s.top * 0.55f, s.c - s.bot * 0.1f, Hash01(seed, 23));
                Decal(mb, body, side, Circle(z, y, 0.0075f, 5), "catSpot");
            }
            //wide mouth across the flat snout
            Decal(mb, body, side, new[] { new Vector2(0.50f, -0.004f), new Vector2(0.475f, -0.006f), new Vector2(0.445f, -0.020f), new Vector2(0.45f, -0.030f), new Vector2(0.49f, -0.016f) }, "mouth", 0.004f);
            StripDecal(mb, body, side, new[] { new Vector2(0.33f, 0.05f), new Vector2(0.315f, 0.02f), new Vector2(0.31f, -0.02f), new Vector2(0.325f, -0.05f) }, 0.006f, "catBack");
        }
        Eye(mb, body, 0.425f, 0.026f, 0.012f);
        //four pairs of barbels: long ones at the mouth corners, short ones by the nostrils, two pairs under the chin
        foreach (int s in new[] { -1, 1 })
        {
            Vector3 corner = new Vector3(s * 0.066f, -0.010f, 0.462f);
            Vector3 mid = corner + new Vector3(s * 0.045f, -0.006f, -0.050f);
            mb.Rod(corner, mid, 0.0075f, 0.0060f, 4, "catBarbel");
            mb.Rod(mid, mid + new Vector3(s * 0.040f, -0.026f, -0.090f), 0.0060f, 0.0022f, 4, "catBarbel");
            Vector3 nostril = new Vector3(s * 0.022f, 0.016f, 0.470f);
            mb.Rod(nostril, nostril + new Vector3(s * 0.012f, 0.030f, -0.012f), 0.0045f, 0.0018f, 3, "catBarbel");
            Vector3 chinA = new Vector3(s * 0.012f, -0.022f, 0.455f);
            mb.Rod(chinA, chinA + new Vector3(s * 0.006f, -0.045f, -0.030f), 0.0045f, 0.0018f, 3, "catBarbel");
            Vector3 chinB = new Vector3(s * 0.032f, -0.020f, 0.450f);
            mb.Rod(chinB, chinB + new Vector3(s * 0.020f, -0.040f, -0.040f), 0.0045f, 0.0018f, 3, "catBarbel");
        }
        Func<int, string> fin = i => "catFin";
        BackFin(mb, body, 1, new[] { new Vector2(0.25f, 0.0f), new Vector2(0.235f, 0.105f), new Vector2(0.205f, 0.092f), new Vector2(0.17f, 0.050f), new Vector2(0.14f, 0.0f) }, 0.035f, fin);
        //adipose fin: the small fleshy bump near the tail
        BackFin(mb, body, 1, new[] { new Vector2(-0.12f, 0.0f), new Vector2(-0.135f, 0.026f), new Vector2(-0.165f, 0.030f), new Vector2(-0.19f, 0.0f) }, 0.01f, fin);
        BackFin(mb, body, -1, new[] { new Vector2(-0.02f, 0.0f), new Vector2(-0.04f, 0.048f), new Vector2(-0.10f, 0.056f), new Vector2(-0.17f, 0.050f), new Vector2(-0.22f, 0.028f), new Vector2(-0.235f, 0.0f) }, 0.015f, fin);
        //deeply forked tail
        TailFin(mb, new Vector2(-0.27f, 0.0f), new[]
        {
            new Vector2(-0.28f, 0.030f), new Vector2(-0.38f, 0.085f), new Vector2(-0.50f, 0.138f), new Vector2(-0.43f, 0.055f),
            new Vector2(-0.37f, 0.0f), new Vector2(-0.43f, -0.055f), new Vector2(-0.50f, -0.138f), new Vector2(-0.38f, -0.085f), new Vector2(-0.28f, -0.030f),
        }, fin);
        SideFins(mb, body, new Vector2(0.33f, -0.03f), new Vector2(0.30f, -0.04f), new Vector3(0.07f, -0.012f, -0.07f), 0.012f, "catFin");
        SideFins(mb, body, new Vector2(0.08f, -0.062f), new Vector2(0.03f, -0.060f), new Vector3(0.035f, -0.03f, -0.05f), 0.0f, "catFin");
        return mb;
    }
}
