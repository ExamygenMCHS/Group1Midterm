using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// All geometry is baked in the Editor. No generator runs in a player build.
// The terrain beyond the border and the painted backdrop ring live in MountainDeliveryBuilder.Facades.cs.
public static partial class MountainDeliveryBuilder
{
    const string Folder = "Assets/Game/Art/MountainRoad";
    const string ScenePath = "Assets/Game/Scenes/Mountain Delivery.unity";
    const string PrefabPath = "Assets/Game/Prefabs/Delivery/Mountain Delivery.prefab";
    const string PickupPath = "Assets/Game/Art/Pickup Truck/Prefabs/Pickup.prefab";
    // The pickup prefab bakes the pack's 0.5 scale into its Model child, so its root, rigidbody and
    // wheel colliders are unscaled. It measures about 2.5 x 2.3 x 4.4 m and fits a 5 m lane.
    // Vehicle Spawn marks the first road vertex, so the truck starts a little down the lane to keep its rear wheels on the road.
    const float LaneOffset = 2.5f, SpawnAhead = 7f, EndWallSetback = 3.5f, BoundaryLateral = 9f;
    // The road runs straight on to the edge of the map and an invisible wall closes it. Real, undrivable terrain carries on beyond the wall
    // and a painted ring backdrop stands around the whole map (see MountainDeliveryBuilder.Facades.cs).
    const float BorderWallHeight = 120f;
    // Pines are tall relative to the road and the pickup (real pines are ~8x a pickup's height), so there are fewer of them.
    const int TreeAttempts = 800;
    const float TreeMinHeight = 12f, TreeMaxHeight = 21f, TreeSpacing = 8f, TreeRoadClearance = 15f, HouseClearance = 15f;
    [Serializable] class Layout { public int grid; public float step, lower; public float[] heights; public Vector3[] trace; }
    class Batch
    {
        public readonly List<Vector3> vertices = new List<Vector3>();
        public readonly List<int> triangles = new List<int>();
        // When set, triangles are cut off at the terrain border (a diagonal cut where the road runs past it).
        // The cut pieces go to their own batch, so this mesh keeps whole segments (the tests read six vertices per segment).
        public Batch cuts;
        public void Tri(Vector3 a, Vector3 b, Vector3 c)
        {
            if(cuts!=null&&!ClipInside(a,b,c))
            {
                var poly=new List<Vector3>{a,b,c};
                for(int k=0;k<4;k++) poly=ClipPlane(poly,k);
                for(int k=1;k<poly.Count-1;k++) cuts.Add(poly[0],poly[k],poly[k+1]);
                return;
            }
            Add(a,b,c);
        }
        public void Add(Vector3 a, Vector3 b, Vector3 c)
        { int n = vertices.Count; vertices.Add(a); vertices.Add(b); vertices.Add(c); triangles.Add(n); triangles.Add(n+1); triangles.Add(n+2); }
        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d) { Tri(a,b,c); Tri(a,c,d); }
        public void Box(Vector3 p, Vector3 size, Quaternion q)
        {
            Vector3[] v = new Vector3[8];
            for (int i=0;i<8;i++) v[i] = p + q * Vector3.Scale(size, new Vector3((i&1)==0?-.5f:.5f,(i&2)==0?-.5f:.5f,(i&4)==0?-.5f:.5f));
            Quad(v[0],v[2],v[3],v[1]); Quad(v[4],v[5],v[7],v[6]);
            Quad(v[0],v[4],v[6],v[2]); Quad(v[1],v[3],v[7],v[5]);
            Quad(v[2],v[6],v[7],v[3]); Quad(v[0],v[1],v[5],v[4]);
        }
        public void Cone(Vector3 p, float r, float height, int sides, float rotation=0)
        {
            for(int i=0;i<sides;i++)
            {
                float a=rotation+i*Mathf.PI*2/sides, b=rotation+(i+1)*Mathf.PI*2/sides;
                var va=p+new Vector3(Mathf.Cos(a)*r,0,Mathf.Sin(a)*r);
                var vb=p+new Vector3(Mathf.Cos(b)*r,0,Mathf.Sin(b)*r);
                Tri(va,p+Vector3.up*height,vb); Tri(p,va,vb);
            }
        }
        public Mesh Mesh(string name)
        {
            var m=new Mesh {name=name, indexFormat=vertices.Count>65535?IndexFormat.UInt32:IndexFormat.UInt16};
            m.SetVertices(vertices); m.SetTriangles(triangles,0); m.RecalculateNormals(); m.RecalculateBounds(); return m;
        }
    }
    static bool Inside(Vector3 p) { return p.x>=borderLow&&p.x<=borderHigh&&p.z>=borderLow&&p.z<=borderHigh; }
    // The road surface is cut RoadOverhang past the border, just behind the border cap, so no gap shows between the road and the terrain beyond it.
    const float RoadOverhang = 1.4f;
    static bool ClipInside(Vector3 a,Vector3 b,Vector3 c) { for(int k=0;k<4;k++) if(Signed(a,k)<0||Signed(b,k)<0||Signed(c,k)<0) return false; return true; }
    // Sutherland-Hodgman against one side of the cut square (k: 0 x>=low, 1 x<=high, 2 z>=low, 3 z<=high).
    static float Signed(Vector3 p,int k) { float lo=borderLow-RoadOverhang,hi=borderHigh+RoadOverhang; return k==0?p.x-lo:k==1?hi-p.x:k==2?p.z-lo:hi-p.z; }
    static List<Vector3> ClipPlane(List<Vector3> poly,int k)
    {
        var result=new List<Vector3>();
        for(int i=0;i<poly.Count;i++)
        {
            var a=poly[i]; var b=poly[(i+1)%poly.Count]; float da=Signed(a,k),db=Signed(b,k);
            if(da>=0) result.Add(a);
            if((da>=0)!=(db>=0)) result.Add(Vector3.Lerp(a,b,da/(da-db)));
        }
        return result;
    }
    static float borderLow, borderHigh;
    static int firstInside, lastInside, tracedStart, finishIndex;
    static Layout layout;
    static readonly List<Vector3> route=new List<Vector3>();
    static readonly List<float> distances=new List<float>();
    static readonly Dictionary<string,Batch> scenery=new Dictionary<string,Batch>();
    static readonly Dictionary<string,Material> materials=new Dictionary<string,Material>();
    static Transform root;
    static float length;
    // The original traced route sits between startExtension and startExtension+coreLength; the rest is road added out to the map border.
    // Bays (dirt turnouts), the tree-free clearings beside them and curve signs are placed by fraction of the traced route, so they stay put when the ends are extended.
    static float startExtension, coreLength;
    static float T(int i) { return (distances[i]-startExtension)/coreLength; }
    static System.Random random;
    static float Rand(float a,float b) { return a+(b-a)*(float)random.NextDouble(); }
    static Vector3 Flat(Vector3 p) { p.y=0; return p; }
    static Vector3 Right(int i)
    { return Vector3.Cross(Vector3.up, Flat(route[Mathf.Min(i+1,route.Count-1)]-route[Mathf.Max(i-1,0)])).normalized; }
    static Quaternion Facing(int i) { return Quaternion.LookRotation(route[Mathf.Min(i+1,route.Count-1)]-route[Mathf.Max(i-1,0)],Vector3.up); }
    static float Bay(float t)
    {
        float v=0;
        foreach(float center in new[]{.19f,.77f})
        { float d=Mathf.Abs(t-center); if(d<.035f) v=Mathf.Max(v,7*Mathf.Pow(Mathf.Cos(d/.035f*Mathf.PI*.5f),2)); }
        return v;
    }
    static Batch Group(string mat,Vector3 p)
    {
        string key=mat+"_"+Mathf.FloorToInt((p.x+170)/85)+"_"+Mathf.FloorToInt((p.z+170)/85);
        if(!scenery.TryGetValue(key,out Batch b)) scenery[key]=b=new Batch(); return b;
    }
    static float Nearest(Vector3 p,out Vector3 center,out int index)
    {
        float best=float.MaxValue; center=Vector3.zero; index=0;
        for(int i=0;i<route.Count-1;i++)
        {
            var a=route[i]; var ab=route[i+1]-a; var flat=Flat(ab);
            float t=Mathf.Clamp01(Vector3.Dot(Flat(p-a),flat)/flat.sqrMagnitude);
            var c=a+ab*t; float ds=Flat(p-c).sqrMagnitude;
            if(ds<best) {best=ds; center=c; index=i;}
        }
        return Mathf.Sqrt(best);
    }
    static float Height(float x,float z)
    {
        float gx=Mathf.Clamp((x-layout.lower)/layout.step,0,layout.grid-1.001f), gz=Mathf.Clamp((z-layout.lower)/layout.step,0,layout.grid-1.001f);
        int ix=(int)gx, iz=(int)gz; float tx=gx-ix,tz=gz-iz;
        float a=Mathf.Lerp(layout.heights[iz*layout.grid+ix],layout.heights[iz*layout.grid+ix+1],tx);
        float b=Mathf.Lerp(layout.heights[(iz+1)*layout.grid+ix],layout.heights[(iz+1)*layout.grid+ix+1],tx);
        float raw=Mathf.Lerp(a,b,tz);
        float d=Nearest(new Vector3(x,0,z),out var c,out int index);
        float bench=13+Bay(T(index));
        // The wide bench prevents coarse terrain facets protruding through the pavement.
        float blend=Mathf.SmoothStep(0,1,Mathf.InverseLerp(bench,bench+15,d));
        return Mathf.Lerp(c.y-.65f,raw,blend);
    }
    static Material Mat(string name,string hex)
    {
        ColorUtility.TryParseHtmlString(hex,out Color color);
        var m=new Material(Shader.Find("Universal Render Pipeline/Lit")) {name=name, color=color};
        m.SetFloat("_Smoothness",.08f); m.enableInstancing=true;
        AssetDatabase.CreateAsset(m,Folder+"/Baked/"+name+".mat"); materials[name]=m; return m;
    }
    static GameObject Bake(string name,Batch b,string material,bool collider=false)
    {
        if(b.vertices.Count==0) return null;
        var mesh=b.Mesh(name); if(name.StartsWith("Drivable road")) DrivingFeelSetup.ProjectUVs(mesh); AssetDatabase.AddObjectToAsset(mesh,Folder+"/Baked/Geometry.asset");
        var go=new GameObject(name); go.transform.SetParent(root,false); go.isStatic=true;
        go.AddComponent<MeshFilter>().sharedMesh=mesh; var renderer=go.AddComponent<MeshRenderer>(); renderer.sharedMaterial=materials[material];
        if(collider) go.AddComponent<MeshCollider>().sharedMesh=mesh;
        return go;
    }
    static Transform Marker(string name,Vector3 pos,Quaternion rotation)
    { var go=new GameObject(name); go.transform.SetParent(root); go.transform.SetPositionAndRotation(pos,rotation); return go.transform; }
    static void Beam(Batch b,Vector3 a,Vector3 c,float width,float height)
    { b.Box((a+c)/2,new Vector3(width,height,Vector3.Distance(a,c)+.04f),Quaternion.LookRotation(c-a)); }

    [MenuItem("Tools/Mountain Delivery/Rebuild Level (discards hand edits)")]
    public static void Rebuild()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode before rebuilding.");
        // Build() adds its scene additively, which needs a saved scene to be open. Any other scene works.
        EditorSceneManager.OpenScene("Assets/Game/Scenes/Scene0-Intro.unity",OpenSceneMode.Single);
        AssetDatabase.DeleteAsset(ScenePath); AssetDatabase.DeleteAsset(PrefabPath); AssetDatabase.DeleteAsset(Folder+"/Baked");
        AssetDatabase.Refresh();
        Build();
        EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
    }

    [MenuItem("Tools/Mountain Delivery/Create Level")]
    public static void Build()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode before building.");
        if(AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath)!=null) throw new InvalidOperationException("Level already exists. Preserve edits; generate into a fresh folder for a new version.");
        layout=JsonUtility.FromJson<Layout>(File.ReadAllText(Folder+"/ReferenceLayout.json"));
        random=new System.Random(485); route.Clear(); distances.Clear(); scenery.Clear(); materials.Clear();
        Directory.CreateDirectory(Folder+"/Baked"); AssetDatabase.Refresh();
        var previous=SceneManager.GetActiveScene();
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive); SceneManager.SetActiveScene(scene);
        root=new GameObject("Mountain Delivery | Metazeon reference • CC BY 4.0").transform;
        var container=new Mesh {name="Baked mesh collection"}; AssetDatabase.CreateAsset(container,Folder+"/Baked/Geometry.asset");
        Mat("Meadow","#788866"); Mat("MeadowLight","#81906D"); Mat("MeadowDark","#6F8063");
        Mat("Cliff","#8A8B7D"); Mat("Road","#424F58"); Mat("Shoulder","#B9AB87");
        Mat("Paint","#EFE8CB"); Mat("Yellow","#EDB94F"); Mat("Pine","#355E50"); Mat("PineLight","#4B7560");
        Mat("Wood","#765440"); Mat("Steel","#A9BDC0"); Mat("Roof","#B75D44"); Mat("Wall","#D7C7A1"); Mat("Glass","#304F5D"); Mat("Dirt","#8C6F4E");
        DrivingFeelSetup.ApplyAsphalt(materials["Road"]);
        MakeRoute(); Terrain(); Road(); RoadEndBoundaries(root); Forest(); Buildings();
        foreach(var pair in scenery) Bake(pair.Key,pair.Value,pair.Key.Split('_')[0]);
        Marker("Vehicle Spawn — face downhill",route[firstInside+8]+Vector3.up*.7f,Facing(firstInside+8));
        var navigation=Marker("Route centerline — ordered downhill",Vector3.zero,Quaternion.identity);
        // Waypoints are numbered from the traced start, so extending the road does not renumber them.
        for(int i=tracedStart;i<route.Count;i+=12) if(i<=lastInside) Marker("Waypoint "+(i-tracedStart).ToString("D3"),route[i]+Vector3.up*.3f,Facing(i)).SetParent(navigation);
        Marker("Finish — valley exit",route[finishIndex],Facing(finishIndex));
        RoadSigns(root);
        Facades();
        Lighting();
        PrefabUtility.SaveAsPrefabAsset(root.gameObject,PrefabPath);
        // The truck and its camera belong to the scene, not to the reusable environment prefab.
        PlaceVehicle(root);
        DropOffSetup.Create(scene);
        DeliverySetup.Apply(scene);
        EditorSceneManager.SaveScene(scene,ScenePath); AssetDatabase.SaveAssets();
        SceneManager.SetActiveScene(previous);
        File.WriteAllText(Folder+"/LevelMetrics.json",JsonUtility.ToJson(new Metrics { routeLength=length, elevationDrop=48, samples=route.Count, roadWidth=10, triangleCount=TriangleCount(root), renderers=root.GetComponentsInChildren<MeshRenderer>().Length },true));
        Debug.Log("Mountain delivery level created: "+length.ToString("F1")+" m route; "+TriangleCount(root)+" triangles.");
    }
    [Serializable] class Metrics {public float routeLength,elevationDrop,roadWidth; public int samples,triangleCount,renderers;}
    static int TriangleCount(Transform t) {int n=0; foreach(var mf in t.GetComponentsInChildren<MeshFilter>()) n+=mf.sharedMesh.triangles.Length/3; return n;}
    static void MakeRoute()
    {
        var trace=layout.trace; var dense=new List<Vector3>();
        for(int i=0;i<trace.Length-1;i++)
        {
            Vector3 a=trace[Mathf.Max(0,i-1)],b=trace[i],c=trace[i+1],d=trace[Mathf.Min(trace.Length-1,i+2)];
            int steps=Mathf.CeilToInt(Vector3.Distance(b,c)*2);
            for(int j=0;j<steps;j++) {float t=(float)j/steps; dense.Add(.5f*((2*b)+(-a+c)*t+(2*a-5*b+4*c-d)*t*t+(-a+3*b-3*c+d)*t*t*t));}
        }
        dense.Add(trace[trace.Length-1]); route.Add(dense[0]);
        for(int i=1;i<dense.Count;i++) if(Vector3.Distance(route[route.Count-1],dense[i])>=1.35f) route.Add(dense[i]);
        route.Add(dense[dense.Count-1]);
        int tracedCount=route.Count, prepended=ExtendToBorder(); tracedStart=prepended;
        length=0; distances.Add(0);
        for(int i=1;i<route.Count;i++) {length+=Vector3.Distance(route[i-1],route[i]); distances.Add(length);}
        startExtension=distances[prepended]; coreLength=distances[prepended+tracedCount-1]-startExtension;
        // Ease the gradient through hairpins; distribute the remaining descent along the straights.
        var descent=new float[route.Count];
        for(int i=1;i<route.Count;i++)
        {
            int before=Mathf.Max(0,i-4),after=Mathf.Min(route.Count-1,i+4);
            float bend=Vector3.Angle(route[i]-route[before],route[after]-route[i]);
            float weight=Mathf.Lerp(1,.32f,Mathf.InverseLerp(8,50,bend));
            weight*=Mathf.SmoothStep(0,1,Mathf.Min(distances[i],length-distances[i])/10);
            descent[i]=descent[i-1]+(distances[i]-distances[i-1])*weight;
        }
        for(int i=0;i<route.Count;i++) {var p=route[i]; p.y=52-48*descent[i]/descent[route.Count-1]; route[i]=p;}
    }
    // Continues the road straight along each end's heading until its centreline is past the terrain border. The road mesh is
    // clipped at the border (a diagonal cut), and the border wall is the stop, so the road looks like it carries on.
    // Returns how many points were added before the traced start.
    static int ExtendToBorder()
    {
        borderLow=layout.lower; borderHigh=layout.lower+layout.step*(layout.grid-1);
        var head=new List<Vector3>(); var tail=new List<Vector3>();
        var startDir=Flat(route[0]-route[4]).normalized; var endDir=Flat(route[route.Count-1]-route[route.Count-5]).normalized;
        // Go a few samples past the border so the guardrails and shoulders are cut there too, not before it.
        for(var p=route[0]+startDir*1.35f; head.Count==0||Inside(head[head.Count-1]); p+=startDir*1.35f) head.Add(p);
        for(int k=0;k<8;k++) head.Add(head[head.Count-1]+startDir*1.35f);
        for(var p=route[route.Count-1]+endDir*1.35f; tail.Count==0||Inside(tail[tail.Count-1]); p+=endDir*1.35f) tail.Add(p);
        for(int k=0;k<8;k++) tail.Add(tail[tail.Count-1]+endDir*1.35f);
        head.Reverse(); route.InsertRange(0,head); route.AddRange(tail);
        firstInside=0; while(!Inside(route[firstInside])) firstInside++;
        lastInside=route.Count-1; while(!Inside(route[lastInside])) lastInside--;
        // The finish (and the drop-off circle on it) stays where it was before the road ran out to the border: the last point
        // with a road-wide wall 3.5 m ahead still inside the terrain, so the circle is not cut by the border.
        finishIndex=lastInside;
        while(finishIndex>0)
        {
            var p=route[finishIndex]; var side=Vector3.Cross(Vector3.up,endDir)*9; var cap=p+endDir*3.5f; bool clear=true;
            foreach(var q in new[]{p+side,p-side,cap+side,cap-side}) if(q.x<borderLow+.5f||q.x>borderHigh-.5f||q.z<borderLow+.5f||q.z>borderHigh-.5f) clear=false;
            if(clear) break; finishIndex--;
        }
        return head.Count;
    }
    static void Terrain()
    {
        int n=layout.grid; var v=new Vector3[n*n];
        for(int z=0;z<n;z++) for(int x=0;x<n;x++)
        {float px=layout.lower+x*layout.step,pz=layout.lower+z*layout.step;
         if(x>0&&x<n-1&&z>0&&z<n-1) {px+=Rand(-1.25f,1.25f);pz+=Rand(-1.25f,1.25f);}
         v[z*n+x]=new Vector3(px,Height(px,pz),pz);}
        var collision=new Batch();
        for(int z=0;z<n-1;z++) for(int x=0;x<n-1;x++)
        {
            Vector3 a=v[z*n+x],b=v[(z+1)*n+x],c=v[(z+1)*n+x+1],d=v[z*n+x+1];
            float slope=Mathf.Max(Mathf.Abs(a.y-c.y),Mathf.Abs(b.y-d.y));
            float shade=Mathf.PerlinNoise(a.x*.035f+20,a.z*.035f+20)+Rand(-.1f,.1f);
            string mat=slope>5?"Cliff":shade<.42f?"MeadowDark":shade>.58f?"MeadowLight":"Meadow";
            var batch=Group(mat,(a+c)/2); batch.Tri(a,b,c); batch.Tri(a,c,d); collision.Tri(a,b,c); collision.Tri(a,c,d);
        }
        var terrain=Bake("Terrain collision",collision,"Meadow",true); terrain.GetComponent<MeshRenderer>().enabled=false;
        var skirt=new Batch();
        for(int i=0;i<n-1;i++)
        {
            foreach(var edge in new[]{new[]{v[i],v[i+1]},new[]{v[(n-1)*n+i+1],v[(n-1)*n+i]},new[]{v[(i+1)*n],v[i*n]},new[]{v[i*n+n-1],v[(i+1)*n+n-1]}})
            {var a=edge[0];var b=edge[1];skirt.Quad(a,b,new Vector3(b.x,-15,b.z),new Vector3(a.x,-15,a.z));}
        }
        Bake("Mountain cutaway",skirt,"Cliff");
    }
    static void Road()
    {
        var road=new Batch(); var shoulder=new Batch(); var paint=new Batch(); var rail=new Batch(); var posts=new Batch(); var dirt=new Batch();
        road.cuts=new Batch(); shoulder.cuts=new Batch(); paint.cuts=new Batch();
        for(int i=0;i<route.Count-1;i++)
        {
            var a=route[i];var b=route[i+1];var ra=Right(i);var rb=Right(i+1);
            float wa=5+Bay(T(i)),wb=5+Bay(T(i+1));
            // The asphalt keeps its normal 10 m width. Where the bay widens the right side, that strip is bare dirt (a turnout).
            road.Quad(a-ra*5,b-rb*5,b+rb*5,a+ra*5);
            if(wa>5.001f||wb>5.001f) dirt.Quad(a+ra*5-Vector3.up*.03f,b+rb*5-Vector3.up*.03f,b+rb*wb-Vector3.up*.03f,a+ra*wa-Vector3.up*.03f);
            foreach(int side in new[]{-1,1})
            {
                float ea=side<0?5:wa,eb=side<0?5:wb;
                var ai=a+ra*side*ea;var bi=b+rb*side*eb;var ao=a+ra*side*(ea+1.6f);var bo=b+rb*side*(eb+1.6f);
                if(side>0) shoulder.Quad(ai,bi,bo,ao); else shoulder.Quad(ao,bo,bi,ai);
                var up=Vector3.up*.035f;
                var pa=a+ra*side*(5-.35f)+up;var pb=b+rb*side*(5-.35f)+up;
                paint.Quad(pa-ra*.08f,pb-rb*.08f,pb+rb*.08f,pa+ra*.08f);
                // A continuous physical barrier, outside the shoulder, including both hairpins, all the way to the map border.
                // Each rail runs until both ends of a segment are past the cut, so it reaches the facade at the diagonal border.
                var ga=a+ra*side*(ea+1.35f)+Vector3.up*.9f;
                var gb=b+rb*side*(eb+1.35f)+Vector3.up*.9f;
                if(ClipInside(ga,ga,ga)||ClipInside(gb,gb,gb))
                {
                    Beam(rail,ga,gb,.18f,.44f);
                    if(i%3==0) posts.Box(ga-Vector3.up*.36f,new Vector3(.18f,1.2f,.18f),Quaternion.identity);
                }
            }
            if((int)(distances[i]/4)%2==0)
                paint.Quad(a-ra*.10f+Vector3.up*.04f,b-rb*.10f+Vector3.up*.04f,b+rb*.10f+Vector3.up*.04f,a+ra*.10f+Vector3.up*.04f);
        }
        Bake("Drivable road — continuous 10 m carriageway",road,"Road",true);
        Bake("Gravel shoulders",shoulder,"Shoulder",true); Bake("Lane markings",paint,"Paint"); Bake("Dirt turnouts",dirt,"Dirt",true);
        Bake("Guardrails — collision",rail,"Steel",true); Bake("Guardrail posts",posts,"Wood");
        // The diagonal cuts at the border. Names avoid "Drivable road" so the main road stays a single mesh for the tests.
        var cutRoad=Bake("Asphalt at the road ends",road.cuts,"Road",true); if(cutRoad) DrivingFeelSetup.ProjectUVs(cutRoad.GetComponent<MeshFilter>().sharedMesh);
        Bake("Gravel shoulder ends",shoulder.cuts,"Shoulder",true); Bake("Lane marking ends",paint.cuts,"Paint");
    }
    static List<Vector3> HouseSites()
    {
        var sites=new List<Vector3>();
        foreach(float fraction in new[]{.19f,.77f})
        {
            int i=0;while(i<route.Count-1&&T(i)<fraction)i++;
            sites.Add(route[i]+Right(i)*20);
        }
        return sites;
    }
    static void Forest()
    {
        var houses=HouseSites(); var trees=new List<Vector3>();
        for(int i=0;i<TreeAttempts;i++)
        {
            float x=Rand(-160,160),z=Rand(-160,160);var p=new Vector3(x,Height(x,z),z);
            float d=Nearest(p,out var c,out int index);
            if(d<TreeRoadClearance+Bay(T(index))||p.y>90||Mathf.Abs(Height(x+2,z)-p.y)>3||Mathf.Abs(Height(x,z+2)-p.y)>3) continue;
            if(houses.Exists(h=>Flat(h-p).magnitude<HouseClearance)) continue;
            if(trees.Exists(t=>Flat(t-p).magnitude<TreeSpacing)) continue;
            trees.Add(p);
            float h=Rand(TreeMinHeight,TreeMaxHeight); float w=Mathf.Max(.5f,h*.05f); string mat=random.Next(3)==0?"PineLight":"Pine";
            Group("Wood",p).Box(p+Vector3.up*h*.2f,new Vector3(w,h*.4f,w),Quaternion.Euler(0,Rand(0,90),0));
            var leaves=Group(mat,p);
            leaves.Cone(p+Vector3.up*h*.18f,h*.30f,h*.42f,7,Rand(0,6));
            leaves.Cone(p+Vector3.up*h*.42f,h*.23f,h*.34f,7,Rand(0,6));
            leaves.Cone(p+Vector3.up*h*.62f,h*.15f,h*.38f,7,Rand(0,6));
        }
        for(int i=0;i<160;i++)
        {
            float x=Rand(-163,163),z=Rand(-163,163);var p=new Vector3(x,Height(x,z)-.3f,z);
            if(Nearest(p,out var c,out int index)<12+Bay(T(index))) continue;
            float r=Rand(1.4f,4.8f); Group("Cliff",p).Cone(p,r,r*Rand(.65f,1.4f),5,Rand(0,6));
        }
    }
    static void Buildings()
    {
        int number=0;
        foreach(float fraction in new[]{.19f,.77f})
        {
            int i=0;while(i<route.Count-1&&T(i)<fraction)i++;
            var q=Facing(i);var p=route[i]+Right(i)*9; number++;
            Marker("Delivery Bay "+number+" — clear 7 × 18 m",p+Vector3.up*.15f,q);
            // The bays are bare dirt turnouts now (see Road()); the old shack and its parking lines are gone.
        }
    }
    // Invisible walls: a cap across each road end (the road runs to the map border, so these sit at the border) plus
    // four walls around the whole terrain edge. The guardrails run the full length, so no side walls are needed.
    static void RoadEndBoundaries(Transform parent)
    {
        var group=new GameObject("Road end boundaries — invisible").transform; group.SetParent(parent,false); group.gameObject.isStatic=true;
        EndCap(group,"Start",firstInside,-1); EndCap(group,"Finish",lastInside,1);
        float low=layout.lower, high=layout.lower+layout.step*(layout.grid-1), span=high-low;
        BorderWall(group,"Border west",new Vector3(low-.5f,0,(low+high)/2),new Vector3(1,BorderWallHeight,span+2));
        BorderWall(group,"Border east",new Vector3(high+.5f,0,(low+high)/2),new Vector3(1,BorderWallHeight,span+2));
        BorderWall(group,"Border south",new Vector3((low+high)/2,0,low-.5f),new Vector3(span+2,BorderWallHeight,1));
        BorderWall(group,"Border north",new Vector3((low+high)/2,0,high+.5f),new Vector3(span+2,BorderWallHeight,1));
    }
    // The cap is the border wall where the road meets it: axis-aligned, centred on the road's crossing point.
    static void EndCap(Transform group,string name,int inside,int outward)
    {
        var p=route[inside]; var dir=Flat(route[inside+outward]-route[inside]).normalized;
        float t=1000; // distance along the heading to the border
        if(dir.x>.001f) t=Mathf.Min(t,(borderHigh-p.x)/dir.x); else if(dir.x<-.001f) t=Mathf.Min(t,(borderLow-p.x)/dir.x);
        if(dir.z>.001f) t=Mathf.Min(t,(borderHigh-p.z)/dir.z); else if(dir.z<-.001f) t=Mathf.Min(t,(borderLow-p.z)/dir.z);
        var hit=p+dir*t; bool onX=Mathf.Abs(hit.x-borderLow)<.01f||Mathf.Abs(hit.x-borderHigh)<.01f;
        var along=onX?Vector3.forward:Vector3.right; var push=onX?new Vector3(Mathf.Sign(dir.x),0,0):new Vector3(0,0,Mathf.Sign(dir.z));
        hit+=push*.5f; // thin wall just outside the border line, like the border walls
        Wall(group,name+" cap",hit-along*BoundaryLateral,hit+along*BoundaryLateral);
    }
    static void BorderWall(Transform group,string name,Vector3 center,Vector3 size)
    {
        var go=new GameObject(name); go.transform.SetParent(group,false); go.isStatic=true;
        center.y=BorderWallHeight/2-20; go.transform.position=center; go.AddComponent<BoxCollider>().size=size;
    }
    static void Wall(Transform group,string name,Vector3 a,Vector3 b)
    {
        var go=new GameObject(name); go.transform.SetParent(group,false); go.isStatic=true;
        go.transform.SetPositionAndRotation((a+b)/2+Vector3.up,Quaternion.LookRotation(Flat(b-a)));
        go.AddComponent<BoxCollider>().size=new Vector3(.5f,5,Flat(b-a).magnitude+.5f);
    }
    // The delivery truck is the project's pickup (Assets/Game/Art/Pickup Truck), driven by BasicCarController.
    // It starts at Vehicle Spawn in the right-hand lane, facing downhill, with a chase camera.
    static void PlaceVehicle(Transform level)
    {
        var pickupPrefab=AssetDatabase.LoadAssetAtPath<GameObject>(PickupPath);
        if(pickupPrefab==null) throw new InvalidOperationException("Missing "+PickupPath);
        Transform spawn=null;
        foreach(Transform t in level) if(t.name.StartsWith("Vehicle Spawn")) spawn=t;
        if(spawn==null) throw new InvalidOperationException("Missing Vehicle Spawn");
        var pickup=(GameObject)PrefabUtility.InstantiatePrefab(pickupPrefab,level.gameObject.scene);
        pickup.name="Pickup — delivery truck";
        var start=spawn.position+spawn.right*LaneOffset+Flat(spawn.forward).normalized*SpawnAhead;
        Physics.SyncTransforms();
        if(Physics.Raycast(start+Vector3.up*3,Vector3.down,out var hit,10,~0,QueryTriggerInteraction.Ignore)) start=hit.point+Vector3.up*.05f;
        pickup.transform.SetPositionAndRotation(start,Quaternion.LookRotation(Flat(spawn.forward),Vector3.up));
        foreach(var root in level.gameObject.scene.GetRootGameObjects())
            if(root.name=="Overview Camera") {root.tag="Untagged"; root.SetActive(false);}
        var camera=new GameObject("Truck Camera").AddComponent<Camera>(); camera.tag="MainCamera";
        SceneManager.MoveGameObjectToScene(camera.gameObject,level.gameObject.scene);
        camera.farClipPlane=TruckCameraFar; camera.nearClipPlane=.1f; camera.fieldOfView=60;
        camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.72f,.81f,.82f); camera.gameObject.AddComponent<AudioListener>();
        var follow=camera.gameObject.AddComponent<CameraControl>(); follow.target=pickup.transform; follow.distance=8; follow.height=3.5f;
        camera.transform.position=pickup.transform.position+pickup.transform.rotation*new Vector3(0,follow.height,-follow.distance);
        camera.transform.LookAt(pickup.transform.position);
    }

    // Upgrades a level made before the truck could drive, without rebuilding it, so hand edits are kept:
    // removes the old static pickup, adds the road-end walls to the scene and prefab, and places the truck.
    [MenuItem("Tools/Mountain Delivery/Add Driving Setup (keeps hand edits)")]
    public static void AddDrivingSetup()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
        layout=JsonUtility.FromJson<Layout>(File.ReadAllText(Folder+"/ReferenceLayout.json"));
        route.Clear(); distances.Clear(); MakeRoute();
        var scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
        Transform level=null;
        foreach(var r in scene.GetRootGameObjects()) if(r.name.StartsWith("Mountain Delivery")) level=r.transform;
        if(level==null) throw new InvalidOperationException("Missing level root in "+ScenePath);
        if(level.Find("Road end boundaries — invisible")!=null) {Debug.Log("Mountain Delivery already has the driving setup."); return;}
        for(int i=level.childCount-1;i>=0;i--) if(level.GetChild(i).name.StartsWith("Pickup")) Object.DestroyImmediate(level.GetChild(i).gameObject);
        RoadEndBoundaries(level);
        PlaceVehicle(level);
        EditorSceneManager.SaveScene(scene);
        var prefab=PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            for(int i=prefab.transform.childCount-1;i>=0;i--) if(prefab.transform.GetChild(i).name.StartsWith("Pickup")) Object.DestroyImmediate(prefab.transform.GetChild(i).gameObject);
            if(prefab.transform.Find("Road end boundaries — invisible")==null) RoadEndBoundaries(prefab.transform);
            PrefabUtility.SaveAsPrefabAsset(prefab,PrefabPath);
        }
        finally {PrefabUtility.UnloadPrefabContents(prefab);}
        Debug.Log("Mountain Delivery driving setup added.");
    }
    static void Lighting()
    {
        RenderSettings.ambientMode=AmbientMode.Trilight;
        RenderSettings.ambientSkyColor=new Color(.7f,.8f,.85f);RenderSettings.ambientEquatorColor=new Color(.53f,.58f,.52f);RenderSettings.ambientGroundColor=new Color(.28f,.32f,.3f);
        RenderSettings.fog=false;
        var light=new GameObject("Afternoon sun").AddComponent<Light>();light.type=LightType.Directional;light.color=new Color(1,.91f,.76f);light.intensity=1.5f;light.shadows=LightShadows.Soft;light.transform.rotation=Quaternion.Euler(48,-35,0);
        var camera=new GameObject("Overview Camera").AddComponent<Camera>();camera.tag="MainCamera";
        camera.transform.position=new Vector3(230,270,-300);camera.transform.LookAt(new Vector3(0,23,0));camera.orthographic=true;camera.orthographicSize=220;camera.farClipPlane=1400;camera.nearClipPlane=.3f;
        camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.72f,.81f,.82f);camera.gameObject.AddComponent<AudioListener>();
    }
}
