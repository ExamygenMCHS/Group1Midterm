#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

// The truck's visual feel: the chassis follows the physics (squat, dive, lean), the road has a texture and shakes,
// and none of it changes how the truck drives.
public class MountainDeliveryFeelTests
{
    const string ScenePath = "Assets/Game/Scenes/Mountain Delivery.unity";

    BasicCarController car;
    VehicleFeel feel;
    RoadFeel road;
    Rigidbody body;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        yield return EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
        car = Object.FindObjectsByType<BasicCarController>(FindObjectsInactive.Exclude).Single();
        feel = car.GetComponent<VehicleFeel>();
        road = car.GetComponent<RoadFeel>();
        body = car.GetComponent<Rigidbody>();
        car.Override = new BasicCarController.DriveInput(0, 0, 0);
        yield return Steps(100);
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (car) car.Override = null;
        yield return null;
    }

    static IEnumerator Steps(int count)
    {
        for (int i = 0; i < count; i++) yield return new WaitForFixedUpdate();
    }

    [Test]
    public void TruckHasTheFeelComponents()
    {
        Assert.IsNotNull(feel, "VehicleFeel is missing from the pickup");
        Assert.IsNotNull(road, "RoadFeel is missing from the pickup");
        Assert.IsNotNull(car.GetComponent<AudioSource>(), "RoadFeel needs its AudioSource");
    }

    [UnityTest]
    public IEnumerator ChassisNosesUpUnderPowerAndDivesUnderBraking()
    {
        car.Override = new BasicCarController.DriveInput(1, 0, 0);
        float lift = 0f;
        for (int i = 0; i < 90; i++) { yield return null; lift = Mathf.Min(lift, feel.PitchDegrees); }
        Assert.Less(lift, -.4f, "the nose should rise while accelerating");

        car.Override = new BasicCarController.DriveInput(0, 1, 0);
        float dive = 0f;
        for (int i = 0; i < 90; i++) { yield return null; dive = Mathf.Max(dive, feel.PitchDegrees); }
        Assert.Greater(dive, .4f, "the nose should dip while braking");
    }

    [UnityTest]
    public IEnumerator ChassisLeansOutOfACorner()
    {
        car.Override = new BasicCarController.DriveInput(1, 0, 0);
        yield return Steps(Mathf.CeilToInt(3f / Time.fixedDeltaTime));
        car.Override = new BasicCarController.DriveInput(.4f, 0, 1);
        float roll = 0f;
        for (int i = 0; i < 90; i++) { yield return null; if (Mathf.Abs(feel.RollDegrees) > Mathf.Abs(roll)) roll = feel.RollDegrees; }
        // Turning right throws the weight to the left, so the body rolls left (positive).
        Assert.Greater(roll, .5f, "a right turn should roll the body to the outside");
    }

    [UnityTest]
    public IEnumerator FeelNeverMovesTheRigidbodyOrTheWheelColliders()
    {
        var wheels = car.GetComponentsInChildren<WheelCollider>();
        var local = wheels.Select(w => w.transform.localPosition).ToArray();
        car.Override = new BasicCarController.DriveInput(1, 0, .5f);
        yield return Steps(Mathf.CeilToInt(2f / Time.fixedDeltaTime));
        for (int i = 0; i < wheels.Length; i++)
            Assert.AreEqual(local[i], wheels[i].transform.localPosition, "the wheel colliders belong to the physics and must not move");
    }

    [UnityTest]
    public IEnumerator RoadVibrationBuildsWithSpeed()
    {
        Assert.Less(road.Vibration, .01f, "the truck is standing still");
        car.Override = new BasicCarController.DriveInput(1, 0, 0);
        yield return Steps(Mathf.CeilToInt(4f / Time.fixedDeltaTime));
        Assert.Greater(road.Speed01, .1f);
        Assert.Greater(road.Vibration, .02f, "driving should shake the chassis");
    }

    [Test]
    public void RoadHasAnAsphaltTextureAndUVs()
    {
        var roadMesh = Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude).Single(r => r.name.StartsWith("Drivable road"));
        var material = roadMesh.sharedMaterial;
        Assert.IsNotNull(material.GetTexture("_BaseMap"), "the road material needs its asphalt albedo");
        Assert.IsNotNull(material.GetTexture("_BumpMap"), "the road material needs its asphalt normal map");
        var uv = roadMesh.GetComponent<MeshFilter>().sharedMesh.uv;
        Assert.AreEqual(roadMesh.GetComponent<MeshFilter>().sharedMesh.vertexCount, uv.Length);
        Assert.IsTrue(uv.Any(u => u.sqrMagnitude > 1f), "the road UVs should span the road, not sit at zero");
    }
}
#endif
