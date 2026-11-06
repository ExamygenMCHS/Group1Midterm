#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

public class MountainDeliveryTests
{
    private GameObject level;
    private MeshCollider road;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Prefabs/Delivery/Mountain Delivery.prefab");
        Assert.IsNotNull(prefab);
        level=Object.Instantiate(prefab, new Vector3(2000,0,0),Quaternion.identity);
        road=level.GetComponentsInChildren<MeshCollider>().Single(c=>c.name.StartsWith("Drivable road"));
        Physics.SyncTransforms();
        yield return new WaitForFixedUpdate();
    }

    [UnityTearDown]
    public IEnumerator TearDown() { Object.Destroy(level); yield return null; }

    [Test]
    public void EntireRoute_HasContinuousCollisionAndVehicleClearance()
    {
        var v=road.sharedMesh.vertices;
        var guard=level.GetComponentsInChildren<MeshCollider>().Single(c=>c.name.StartsWith("Guardrails"));
        for(int i=36;i<v.Length-36;i+=6)
        {
            var a=road.transform.TransformPoint(v[i]);var b=road.transform.TransformPoint(v[i+1]);
            var c=road.transform.TransformPoint(v[i+2]);var d=road.transform.TransformPoint(v[i+5]);
            Assert.GreaterOrEqual(Vector3.Distance(a,d),9.99f,"Paved width");
            foreach(float lane in new[]{.25f,.75f})
            {
                var p=Vector3.Lerp((a+b)*.5f,(c+d)*.5f,lane);
                Assert.IsTrue(road.Raycast(new Ray(p+Vector3.up*3,Vector3.down),out var hit,6),"Road surface gap at segment "+i/6);
                Assert.Less(Mathf.Abs(hit.point.y-p.y),.08f);
                var overlaps=Physics.OverlapBox(p+Vector3.up*1.2f,new Vector3(1.3f,1.15f,2.3f),Quaternion.LookRotation(b-a));
                Assert.IsFalse(overlaps.Contains(guard),"Vehicle touches guardrail at segment "+i/6);
            }
        }
    }

    [UnityTest]
    public IEnumerator DynamicCargo_RemainsSupportedAtBothHairpins()
    {
        var markers=level.GetComponentsInChildren<Transform>().Where(t=>t.name.StartsWith("Waypoint")).ToArray();
        var upper=markers.OrderByDescending(t=>t.position.z).First();
        var lower=markers.OrderBy(t=>t.position.z).First();
        foreach(var marker in new[]{upper,lower})
        {
            var cargo=GameObject.CreatePrimitive(PrimitiveType.Cube);
            cargo.transform.position=marker.position+Vector3.up;
            var body=cargo.AddComponent<Rigidbody>();body.constraints=RigidbodyConstraints.FreezePositionX|RigidbodyConstraints.FreezePositionZ|RigidbodyConstraints.FreezeRotation;
            try
            {
                for(int i=0;i<60;i++) yield return new WaitForFixedUpdate();
                Assert.Greater(cargo.transform.position.y,marker.position.y-.2f,"Cargo fell through the road");
                Assert.Less(Mathf.Abs(body.linearVelocity.y),.15f,"Cargo did not settle on the road");
            }
            finally {Object.Destroy(cargo);}
        }
    }

    [Test]
    public void LevelPrefab_HasNoTruckAndClosesBothRoadEnds()
    {
        Assert.IsFalse(level.GetComponentsInChildren<Transform>().Any(t=>t.name.StartsWith("Pickup")),"The truck belongs to the scene, not the environment prefab");
        var walls=level.transform.Find("Road end boundaries — invisible");
        Assert.IsNotNull(walls);
        Assert.IsTrue(walls.GetComponentsInChildren<BoxCollider>().Any(c=>c.name=="Start cap"));
        Assert.IsTrue(walls.GetComponentsInChildren<BoxCollider>().Any(c=>c.name=="Finish cap"));
        Assert.AreEqual(0,walls.GetComponentsInChildren<Renderer>().Length,"Boundary walls are invisible");
    }

    [Test]
    public void Pickup_IsScaledForTheRoadAndDrivable()
    {
        var pickup=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Art/Pickup Truck/Prefabs/Pickup.prefab").transform;
        // Measure in the pickup's own frame so its yaw and the road pitch do not inflate the size.
        var bounds=new Bounds();bool first=true;
        foreach(var r in pickup.GetComponentsInChildren<MeshRenderer>())
        {
            var lb=r.GetComponent<MeshFilter>().sharedMesh.bounds;
            for(int i=0;i<8;i++)
            {
                var corner=lb.center+Vector3.Scale(lb.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
                var local=pickup.InverseTransformPoint(r.transform.TransformPoint(corner));
                if(first){bounds=new Bounds(local,Vector3.zero);first=false;} else bounds.Encapsulate(local);
            }
        }
        Assert.IsFalse(first,"Pickup has no renderers");
        var size=bounds.size*pickup.lossyScale.x;
        Assert.That(size.y,Is.InRange(1.8f,2.8f),"Pickup height");
        Assert.That(size.z,Is.InRange(4f,5.5f),"Pickup length");
        Assert.That(size.x,Is.InRange(2f,3f),"Pickup width must fit a 5 m lane");
        // WheelColliders and rigidbodies misbehave under scaled parents, so only the Model child is scaled.
        Assert.AreEqual(Vector3.one,pickup.localScale,"Pickup root is unscaled");
        var body=pickup.GetComponent<Rigidbody>();
        Assert.IsNotNull(body); Assert.That(body.mass,Is.InRange(1000f,3000f),"Mass in kg");
        Assert.IsNotNull(pickup.GetComponent<BasicCarController>());
        Assert.IsFalse(pickup.GetComponentsInChildren<MonoBehaviour>(true).Any(m=>m==null),"No missing scripts");
        var wheels=pickup.GetComponentsInChildren<WheelCollider>(true);
        CollectionAssert.AreEqual(new[]{"FL_Col","FR_Col","BL_Col","BR_Col"},wheels.Select(w=>w.name).ToArray(),"Wheel order the controller expects");
        foreach(var w in wheels)
        {
            Assert.AreEqual(Vector3.one,w.transform.lossyScale,w.name+" is unscaled");
            Assert.That(w.radius,Is.InRange(.4f,.6f),w.name+" radius in metres");
        }
        var com=pickup.Find("CenterOfMass").localPosition;
        Assert.Less(com.y,wheels[0].transform.localPosition.y+.2f,"Centre of mass is low");
        Assert.That(com.z,Is.InRange(wheels[2].transform.localPosition.z,wheels[0].transform.localPosition.z),"Centre of mass is between the axles");
    }

    [Test]
    public void Level_HasDeliveryMarkersAndBoundedGeometry()
    {
        var transforms=level.GetComponentsInChildren<Transform>();
        Assert.AreEqual(2,transforms.Count(t=>t.name.StartsWith("Delivery Bay")));
        Assert.IsTrue(transforms.Any(t=>t.name.StartsWith("Vehicle Spawn")));
        Assert.IsTrue(transforms.Any(t=>t.name.StartsWith("Finish")));
        // GetIndexCount works on meshes that are not CPU-readable, like the pickup model.
        int triangles=level.GetComponentsInChildren<MeshFilter>().Sum(f=>Enumerable.Range(0,f.sharedMesh.subMeshCount).Sum(i=>(int)f.sharedMesh.GetIndexCount(i))/3);
        Assert.Less(triangles,110000);
        foreach(var renderer in level.GetComponentsInChildren<MeshRenderer>())
            Assert.IsTrue(renderer.sharedMaterial!=null && renderer.sharedMaterial.shader.isSupported,renderer.name);
    }

    [Test]
    public void Facades_AreOneRingAroundTheMapWithRealTerrainBeyondBothCaps()
    {
        var facades=level.transform.Find("Facades");
        Assert.IsNotNull(facades,"Bake the facade images (Tools > Mountain Delivery)");
        Assert.AreEqual(0,facades.GetComponentsInChildren<Collider>(true).Length,"Facades must not block anything");
        var walls=level.transform.Find("Road end boundaries — invisible");
        Vector2 Flat(Vector3 v) { var p=level.transform.InverseTransformPoint(v); return new Vector2(p.x,p.z); }

        // The backdrop is a single ring: every panel corner on one circle, all the way round, beyond everything else.
        var backdrop=facades.Find("Backdrop");
        Assert.IsNotNull(backdrop,"Backdrop");
        var panels=backdrop.GetComponentsInChildren<MeshFilter>();
        Assert.AreEqual(24,panels.Length,"a 24-sided ring");
        float minRadius=float.MaxValue,maxRadius=0,maxApron=0;
        foreach(var f in panels)
        {
            foreach(var v in f.sharedMesh.vertices)
            { float r=Flat(f.transform.TransformPoint(v)).magnitude; minRadius=Mathf.Min(minRadius,r); maxRadius=Mathf.Max(maxRadius,r); }
            var renderer=f.GetComponent<MeshRenderer>();
            Assert.AreEqual(UnityEngine.Rendering.ShadowCastingMode.Off,renderer.shadowCastingMode,f.name);
            Assert.IsNotNull(renderer.sharedMaterial.mainTexture,f.name+" shows its painting");
        }
        Assert.Less(maxRadius-minRadius,.5f,"all panel corners lie on one circle");

        foreach(var end in new[]{"Start","Finish"})
        {
            // The map is a square centred on the level root, and each cap sits on its border; measure in the level's frame.
            var cap=walls.Find(end+" cap").GetComponent<BoxCollider>();
            var c=level.transform.InverseTransformPoint(cap.transform.position);
            float capOuterFace=Mathf.Max(Mathf.Abs(c.x),Mathf.Abs(c.z))+cap.size.x/2;
            var beyond=facades.Find("Beyond "+end);
            Assert.IsNotNull(beyond,end+" has terrain beyond its cap");
            var filters=beyond.GetComponentsInChildren<MeshFilter>();
            Assert.Greater(filters.Length,3,end+" terrain has ground, road, rails and trees");
            float farthest=0;
            foreach(var f in filters)
            {
                Assert.AreEqual(UnityEngine.Rendering.ShadowCastingMode.Off,f.GetComponent<MeshRenderer>().shadowCastingMode,f.name);
                foreach(var v in f.sharedMesh.vertices)
                {
                    var p=Flat(f.transform.TransformPoint(v));
                    farthest=Mathf.Max(farthest,p.magnitude);
                }
            }
            // The terrain runs on to the ring (a little past it, hidden behind the panels), so the road meets the painted road.
            Assert.Greater(farthest,minRadius-5,end+" terrain should lead all the way out to the backdrop");
            maxApron=Mathf.Max(maxApron,farthest);
        }
        Assert.IsNotNull(facades.Find("Beyond ground"),"ground ring fills the gap on the other sides");
        Assert.Greater(facades.Find("Beyond ground").GetComponentsInChildren<MeshFilter>().Length,0);
    }
}
#endif
