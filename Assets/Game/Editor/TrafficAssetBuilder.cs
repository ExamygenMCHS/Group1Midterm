using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

// Original, texture-free low-poly vehicles. All geometry is baked, never generated in a build.
public static class TrafficAssetBuilder
{
    public const string Folder = "Assets/Game/Art/Traffic";
    public const string Prefabs = "Assets/Game/Prefabs/Traffic";
    private const string ScenePath = "Assets/Game/Scenes/Mountain Delivery.unity";
    private static readonly Dictionary<string, Material> palette = new Dictionary<string, Material>();
    private static UnityEngine.Object geometry;

    private sealed class Shape
    {
        public readonly List<Vector3> vertices = new List<Vector3>();
        public readonly List<int> triangles = new List<int>();
        public void Tri(Vector3 a, Vector3 b, Vector3 c)
        { int i = vertices.Count; vertices.Add(a); vertices.Add(b); vertices.Add(c); triangles.Add(i); triangles.Add(i + 1); triangles.Add(i + 2); }
        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d) { Tri(a, b, c); Tri(a, c, d); }
        public void Box(Vector3 p, Vector3 s)
        {
            var v = new Vector3[8];
            for (int i = 0; i < 8; i++) v[i] = p + Vector3.Scale(s, new Vector3((i & 1) == 0 ? -.5f : .5f, (i & 2) == 0 ? -.5f : .5f, (i & 4) == 0 ? -.5f : .5f));
            Quad(v[0], v[2], v[3], v[1]); Quad(v[4], v[5], v[7], v[6]);
            Quad(v[0], v[4], v[6], v[2]); Quad(v[1], v[3], v[7], v[5]);
            Quad(v[2], v[6], v[7], v[3]); Quad(v[0], v[1], v[5], v[4]);
        }
        // Each section is (half-width, bottom, top, z); chamfered corners catch the low-poly lighting.
        public void Loft(Vector4[] sections, float chamfer)
        {
            var rings = new List<Vector3[]>();
            foreach (var s in sections)
            {
                float c = Mathf.Min(chamfer, (s.z - s.y) * .35f);
                rings.Add(new[] { new Vector3(-s.x+c,s.y,s.w),new Vector3(s.x-c,s.y,s.w),
                    new Vector3(s.x,s.y+c,s.w),new Vector3(s.x,s.z-c,s.w),new Vector3(s.x-c,s.z,s.w),
                    new Vector3(-s.x+c,s.z,s.w),new Vector3(-s.x,s.z-c,s.w),new Vector3(-s.x,s.y+c,s.w) });
            }
            for (int i = 0; i < rings.Count - 1; i++)
                for (int j = 0; j < 8; j++) Quad(rings[i][j], rings[i][(j+1)%8], rings[i+1][(j+1)%8], rings[i+1][j]);
            for (int j = 1; j < 7; j++) { Tri(rings[0][0], rings[0][j+1], rings[0][j]); Tri(rings[rings.Count-1][0], rings[rings.Count-1][j], rings[rings.Count-1][j+1]); }
        }
        public void CylinderX(float radius, float width, float offset = 0, int sides = 12)
        {
            for (int i = 0; i < sides; i++)
            {
                float a = i * 2 * Mathf.PI / sides, b = (i+1) * 2 * Mathf.PI / sides;
                var p = new Vector3(offset-width/2, Mathf.Cos(a)*radius, Mathf.Sin(a)*radius);
                var q = new Vector3(offset-width/2, Mathf.Cos(b)*radius, Mathf.Sin(b)*radius);
                var r = p + Vector3.right * width; var s = q + Vector3.right * width;
                Quad(p, q, s, r); Tri(new Vector3(offset-width/2,0,0),q,p); Tri(new Vector3(offset+width/2,0,0),r,s);
            }
        }
    }

    private sealed class Model
    {
        public readonly Dictionary<string, Shape> parts = new Dictionary<string, Shape>();
        public Shape Part(string mat) { if (!parts.TryGetValue(mat, out var s)) parts[mat] = s = new Shape(); return s; }
        public void Box(string mat, float x, float y, float z, float w, float h, float l) => Part(mat).Box(new Vector3(x,y,z),new Vector3(w,h,l));
        public GameObject Bake(string name, Transform parent)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false);
            var mesh = new Mesh { name = name };
            var verts = new List<Vector3>(); var indices = new List<int[]>(); var materials = new List<Material>();
            foreach (var part in parts)
            {
                int offset = verts.Count; verts.AddRange(part.Value.vertices);
                indices.Add(part.Value.triangles.Select(i => i + offset).ToArray()); materials.Add(palette[part.Key]);
            }
            mesh.SetVertices(verts); mesh.subMeshCount = indices.Count;
            for (int i = 0; i < indices.Count; i++) mesh.SetTriangles(indices[i], i);
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); AssetDatabase.AddObjectToAsset(mesh, geometry);
            go.AddComponent<MeshFilter>().sharedMesh = mesh; go.AddComponent<MeshRenderer>().sharedMaterials = materials.ToArray();
            return go;
        }
    }

    private static Material Mat(string name, string hex, float smoothness = .1f, bool emission = false)
    {
        string path = Folder + "/Materials/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m) { palette[name] = m; return m; }
        ColorUtility.TryParseHtmlString(hex, out var color);
        m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name, color = color, enableInstancing = true };
        m.SetFloat("_Smoothness", smoothness);
        if (emission) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", color * 1.2f); }
        AssetDatabase.CreateAsset(m, path); palette[name] = m; return m;
    }

    [MenuItem("Tools/Mountain Delivery/Create Traffic Assets")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode before creating traffic.");
        if (AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "/Commuter Hatchback.prefab"))
            throw new InvalidOperationException("Traffic assets already exist. Edit the prefabs directly; creation never overwrites hand edits.");
        Directory.CreateDirectory(Folder + "/Materials"); Directory.CreateDirectory(Prefabs); AssetDatabase.Refresh();
        Mat("Sage", "#72948B"); Mat("Ochre", "#C99B58"); Mat("Terracotta", "#B86C56"); Mat("Lake", "#688898");
        Mat("Enamel", "#E3D7B7"); Mat("Glass", "#334D5B", .3f); Mat("Glass Glint", "#79969B", .25f);
        Mat("Rubber", "#292F32"); Mat("Trim", "#455456"); Mat("Alloy", "#B8BBB0", .25f);
        Mat("Headlamp", "#FAE7B6", .25f); Mat("Tail", "#8F3E36"); Mat("Brake", "#F46B49", .2f, true); Mat("Amber", "#D9A14B");
        geometry = new Mesh { name = "Traffic Geometry" }; AssetDatabase.CreateAsset(geometry, Folder + "/Traffic Geometry.asset");
        CreateVehicle("Commuter Hatchback", "Sage", 0);
        CreateVehicle("Commuter Sedan", "Ochre", 1);
        CreateVehicle("Utility Pickup", "Terracotta", 2);
        CreateVehicle("Valley Box Truck", "Lake", 3);
        AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
    }

    private static Transform Pivot(string name, Transform parent, Vector3 p)
    { var t = new GameObject(name).transform; t.SetParent(parent, false); t.localPosition = p; return t; }

    private static void CreateVehicle(string name, string paint, int kind)
    {
        bool truck = kind >= 2, box = kind == 3;
        float width = truck ? 2f : 1.82f, length = box ? 5.4f : kind == 2 ? 4.8f : kind == 1 ? 4.5f : 3.8f;
        float radius = truck ? .4f : .34f, wheelbase = box ? 3.15f : kind == 2 ? 2.9f : kind == 1 ? 2.65f : 2.35f;
        float frontZ = wheelbase / 2, rearZ = -wheelbase / 2, half = width / 2, nose = length / 2;
        var root = new GameObject(name);
        var chassis = Pivot("Chassis — body roll", root.transform, Vector3.zero);
        var m = new Model();
        m.Part(paint).Loft(new[] { new Vector4(half-.12f,.57f,.88f,-nose),new Vector4(half,.51f,1.04f,-nose+.28f),
            new Vector4(half,.51f,1.06f,nose-.42f),new Vector4(half-.13f,.61f,.93f,nose) }, .09f);
        m.Box("Trim",0,.49f,0,width-.18f,.15f,length-.4f);
        float roofY = truck ? 2.01f : 1.68f, bottomY = 1.035f;
        float cabinBack = truck ? -.18f : kind == 0 ? -1.43f : -1.35f;
        float cabinFront = truck ? 1.18f : .94f;
        float roofBack = truck ? -.10f : kind == 0 ? -1.13f : -.91f;
        float roofFront = truck ? .8f : .43f;
        float cabinWidth = half-.06f, roofWidth = half-.23f;
        // Cabin shell has a sloped windscreen and rear screen, with solid-color inset glazing.
        var shell = m.Part(paint);
        var lb = new Vector3(-cabinWidth,bottomY,cabinBack); var rb = new Vector3(cabinWidth,bottomY,cabinBack);
        var lf = new Vector3(-cabinWidth,bottomY,cabinFront); var rf = new Vector3(cabinWidth,bottomY,cabinFront);
        var lbt = new Vector3(-roofWidth,roofY,roofBack); var rbt = new Vector3(roofWidth,roofY,roofBack);
        var lft = new Vector3(-roofWidth,roofY,roofFront); var rft = new Vector3(roofWidth,roofY,roofFront);
        shell.Quad(lb,lf,lft,lbt); shell.Quad(rf,rb,rbt,rft); shell.Quad(lbt,lft,rft,rbt);
        shell.Quad(lf,rf,rft,lft); shell.Quad(rb,lb,lbt,rbt);
        var glass = m.Part("Glass");
        glass.Quad(Vector3.Lerp(lf,lft,.14f)+new Vector3(.10f,0,.012f),Vector3.Lerp(rf,rft,.14f)+new Vector3(-.10f,0,.012f),
            Vector3.Lerp(rf,rft,.87f)+new Vector3(-.09f,0,.012f),Vector3.Lerp(lf,lft,.87f)+new Vector3(.09f,0,.012f));
        glass.Quad(Vector3.Lerp(rb,rbt,.15f)+new Vector3(-.1f,0,-.012f),Vector3.Lerp(lb,lbt,.15f)+new Vector3(.1f,0,-.012f),
            Vector3.Lerp(lb,lbt,.85f)+new Vector3(.09f,0,-.012f),Vector3.Lerp(rb,rbt,.85f)+new Vector3(-.09f,0,-.012f));
        // Two side windows separated by a painted B pillar; split glazing makes the silhouette read at road distance.
        foreach (float side in new[] {-1f,1f})
        {
            Vector3 back = new Vector3(side*cabinWidth,bottomY,cabinBack), front = new Vector3(side*cabinWidth,bottomY,cabinFront);
            Vector3 backTop = new Vector3(side*roofWidth,roofY,roofBack), frontTop = new Vector3(side*roofWidth,roofY,roofFront);
            for (int window = 0; window < (truck ? 1 : 2); window++)
            {
                float a = truck ? .1f : window == 0 ? .08f : .55f;
                float b = truck ? .9f : window == 0 ? .49f : .93f;
                Vector3 p = Vector3.Lerp(Vector3.Lerp(back,backTop,.14f),Vector3.Lerp(front,frontTop,.14f),a)+Vector3.right*side*.012f;
                Vector3 q = Vector3.Lerp(Vector3.Lerp(back,backTop,.14f),Vector3.Lerp(front,frontTop,.14f),b)+Vector3.right*side*.012f;
                Vector3 r = Vector3.Lerp(Vector3.Lerp(back,backTop,.87f),Vector3.Lerp(front,frontTop,.87f),b)+Vector3.right*side*.012f;
                Vector3 s = Vector3.Lerp(Vector3.Lerp(back,backTop,.87f),Vector3.Lerp(front,frontTop,.87f),a)+Vector3.right*side*.012f;
                if (side < 0) glass.Quad(p,q,r,s); else glass.Quad(q,p,s,r);
            }
            m.Box("Alloy",side*(half+.008f),.995f,truck ? .1f : -.24f,.035f,.045f,.21f);
            m.Box("Trim",side*(half+.08f),1.19f,cabinFront-.13f,.19f,.055f,.09f);
            m.Box(paint,side*(half+.16f),1.24f,cabinFront-.13f,.16f,.17f,.23f);
            m.Box("Glass",side*(half+.165f),1.25f,cabinFront-.253f,.12f,.11f,.012f);
            m.Box("Trim",side*(half+.009f),.7f,0,.03f,.065f,length-.55f);
        }
        // Windscreen reflection stripe is a small opaque inset, avoiding sorting artifacts.
        var glint = m.Part("Glass Glint");
        Vector3 g0=Vector3.Lerp(lf,lft,.72f)+new Vector3(.15f,0,.016f), g1=Vector3.Lerp(rf,rft,.72f)+new Vector3(-.15f,0,.016f);
        glint.Quad(g0,g1,g1+new Vector3(0,.025f,-.021f),g0+new Vector3(0,.025f,-.021f));
        m.Box("Trim",0,.67f,nose+.015f,width-.22f,.17f,.15f);
        m.Box("Alloy",0,.72f,nose+.095f,width-.45f,.055f,.035f);
        m.Box("Trim",0,.68f,-nose-.025f,width-.18f,.16f,.14f);
        m.Box("Trim",0,.9f,nose-.005f,.64f,.16f,.025f);
        for(int i=0;i<3;i++) m.Box("Alloy",0,.855f+i*.045f,nose+.014f,.53f,.012f,.015f);
        m.Box("Enamel",0,.64f,nose+.102f,.33f,.10f,.016f);
        m.Box("Enamel",0,.77f,-nose-.105f,.34f,.12f,.018f);
        foreach(float side in new[]{-1f,1f})
        {
            m.Box("Headlamp",side*(half-.3f),.9f,nose-.005f,.32f,.17f,.045f);
            m.Box("Amber",side*(half-.09f),.87f,nose-.012f,.08f,.12f,.04f);
        }
        if (kind == 2)
        {
            m.Box("Trim",0,1.065f,-1.17f,width-.26f,.07f,1.78f);
            m.Box(paint,-half+.055f,1.22f,-1.21f,.11f,.38f,2.02f);
            m.Box(paint,half-.055f,1.22f,-1.21f,.11f,.38f,2.02f);
            m.Box(paint,0,1.22f,-nose+.09f,width-.1f,.38f,.12f);
            m.Box("Alloy",0,1.3f,-nose+.018f,.31f,.045f,.03f);
            for(int i=0;i<5;i++) m.Box("Alloy",-.68f+i*.34f,1.108f,-1.23f,.025f,.015f,1.54f);
        }
        if (box)
        {
            m.Part("Enamel").Loft(new[]{new Vector4(half+.04f,.99f,2.7f,-nose+.06f),new Vector4(half+.04f,.99f,2.7f,-.28f)},.055f);
            foreach(float side in new[]{-1f,1f})
            {
                m.Box(paint,side*(half+.048f),1.33f,-1.47f,.018f,.25f,2.26f);
                m.Box("Alloy",side*(half+.055f),2.59f,-1.47f,.025f,.055f,2.27f);
                // Plain mountain-and-sun badge made of geometry, no lettering or external textures.
                var badge=m.Part(paint); float x=side*(half+.062f);
                var a=new Vector3(x,1.82f,-1.98f);var b=new Vector3(x,1.82f,-.72f);var c=new Vector3(x,2.35f,-1.44f);
                if(side<0) badge.Tri(a,b,c);else badge.Tri(b,a,c);
            }
            m.Box("Trim",0,1.82f,-nose+.018f,.028f,1.48f,.025f);
            m.Box("Alloy",-.5f,1.8f,-nose+.008f,.035f,1.36f,.035f);
            m.Box("Alloy",.5f,1.8f,-nose+.008f,.035f,1.36f,.035f);
            m.Box("Trim",0,1.15f,-nose-.045f,.44f,.07f,.05f);
        }
        m.Bake(name + " body", chassis);
        var lamps=new List<Renderer>();
        foreach(float side in new[]{-1f,1f})
        {
            var lamp=new Model();lamp.Box("Tail",side*(half-.17f),.88f,-nose-.018f,.20f,.21f,.045f);
            lamps.Add(lamp.Bake("Brake lamp " + (side<0?"L":"R"),chassis).GetComponent<Renderer>());
        }
        var steering = new List<Transform>(); var wheels = new List<Transform>();
        // Four independent rigid-part rigs. Both sides share X-axis roll, so the tread travels in the same direction.
        foreach(bool front in new[]{true,false}) foreach(float side in new[]{-1f,1f})
        {
            string corner=(front?"F":"R")+(side<0?"L":"R");
            var hub=Pivot("Suspension_"+corner,root.transform,new Vector3(side*(half-.015f),radius,front?frontZ:rearZ));
            var steer=Pivot("Steer_"+corner,hub,Vector3.zero);if(front)steering.Add(steer);
            var wheel=Pivot("Wheel_"+corner,steer,Vector3.zero);wheels.Add(wheel);
            var wm=new Model();wm.Part("Rubber").CylinderX(radius,.29f);
            wm.Part("Alloy").CylinderX(radius*.62f,.305f);
            wm.Part("Trim").CylinderX(radius*.29f,.323f);
            for(int i=0;i<5;i++)
            {
                float a=i*Mathf.PI*2/5;
                foreach(float face in new[]{-1f,1f}) wm.Box("Enamel",face*.166f,Mathf.Cos(a)*radius*.44f,Mathf.Sin(a)*radius*.44f,.014f,.045f,.045f);
            }
            wm.Bake("Wheel mesh "+corner,wheel);
        }
        var collider=root.AddComponent<BoxCollider>();collider.center=new Vector3(0,box?1.48f:truck?1.2f:1.02f,0);
        collider.size=new Vector3(width,box?2.4f:truck?1.62f:1.39f,length);
        var rbdy=root.AddComponent<Rigidbody>();rbdy.isKinematic=true;rbdy.useGravity=false;rbdy.interpolation=RigidbodyInterpolation.Interpolate;
        var rig=root.AddComponent<TrafficVehicleRig>();var so=new SerializedObject(rig);
        so.FindProperty("chassis").objectReferenceValue=chassis;
        SetArray(so,"steeringPivots",steering.Cast<Object>().ToArray());SetArray(so,"wheelPivots",wheels.Cast<Object>().ToArray());
        SetArray(so,"brakeLights",lamps.Cast<Object>().ToArray());
        so.FindProperty("tailMaterial").objectReferenceValue=palette["Tail"];so.FindProperty("brakeMaterial").objectReferenceValue=palette["Brake"];
        so.FindProperty("wheelRadius").floatValue=radius;so.FindProperty("wheelbase").floatValue=wheelbase;so.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.SaveAsPrefabAsset(root,Prefabs+"/"+name+".prefab");Object.DestroyImmediate(root);
    }

    private static void SetArray(SerializedObject so,string name,Object[] values)
    { var p=so.FindProperty(name);p.arraySize=values.Length;for(int i=0;i<values.Length;i++)p.GetArrayElementAtIndex(i).objectReferenceValue=values[i]; }

}
