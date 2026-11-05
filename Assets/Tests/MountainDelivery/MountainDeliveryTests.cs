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
    public void Pickup_IsScaledForTheRoadAndHasNoDrivingComponents()
    {
        var pickup=level.GetComponentsInChildren<Transform>().Single(t=>t.name.StartsWith("Pickup — delivery truck"));
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
        Assert.AreEqual(0,pickup.GetComponentsInChildren<MonoBehaviour>(true).Length);
        Assert.AreEqual(0,pickup.GetComponentsInChildren<Rigidbody>(true).Length);
        Assert.AreEqual(0,pickup.GetComponentsInChildren<WheelCollider>(true).Length);
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
}
#endif
