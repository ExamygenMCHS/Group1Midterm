#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

// Drives the pickup in the real Mountain Delivery scene through BasicCarController's input override.
public class MountainDeliveryDrivingTests
{
    const string ScenePath = "Assets/Game/Scenes/Mountain Delivery.unity";
    const float LaneOffset = 2.5f, SpawnAhead = 7f;

    BasicCarController car;
    Rigidbody body;
    Transform spawn;
    Collider[] drivable; // road and gravel shoulders
    readonly List<Vector3> centre = new List<Vector3>(), right = new List<Vector3>();

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        yield return EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
        car = Object.FindObjectsByType<BasicCarController>(FindObjectsInactive.Exclude).Single();
        body = car.GetComponent<Rigidbody>();
        spawn = Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude).Single(t => t.name.StartsWith("Vehicle Spawn"));
        var colliders = Object.FindObjectsByType<MeshCollider>(FindObjectsInactive.Exclude);
        var road = colliders.Single(c => c.name.StartsWith("Drivable road"));
        drivable = new Collider[] { road, colliders.Single(c => c.name.StartsWith("Gravel shoulders")) };
        // The road mesh has six vertices per segment: vertex 0 is the left edge and vertex 5 the right edge.
        var v = road.sharedMesh.vertices;
        centre.Clear(); right.Clear();
        for (int i = 0; i + 5 < v.Length; i += 6)
        {
            Vector3 l = road.transform.TransformPoint(v[i]), r = road.transform.TransformPoint(v[i + 5]);
            centre.Add((l + r) * .5f); right.Add(Vector3.ProjectOnPlane(r - l, Vector3.up).normalized);
        }
        car.Override = new BasicCarController.DriveInput(0, 0, 0);
        yield return Steps(100); // let the suspension settle
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        Time.timeScale = 1f;
        if (car) car.Override = null;
        yield return null;
    }

    static IEnumerator Steps(int count)
    {
        for (int i = 0; i < count; i++) yield return new WaitForFixedUpdate();
    }

    static int Seconds(float seconds) => Mathf.CeilToInt(seconds / Time.fixedDeltaTime);

    bool OnDrivableSurface(out float height)
    {
        var ray = new Ray(body.position + Vector3.up * 3f, Vector3.down);
        foreach (var c in drivable)
            if (c.Raycast(ray, out var hit, 8f)) { height = body.position.y - hit.point.y; return true; }
        height = float.NaN;
        return false;
    }

    float ForwardSpeed => Vector3.Dot(body.linearVelocity, car.transform.forward);

    int NearestCentre(int from)
    {
        int best = from; float bestDistance = float.MaxValue;
        for (int i = Mathf.Max(0, from - 5); i < Mathf.Min(centre.Count, from + 40); i++)
        {
            float d = Vector3.ProjectOnPlane(centre[i] - body.position, Vector3.up).sqrMagnitude;
            if (d < bestDistance) { bestDistance = d; best = i; }
        }
        return best;
    }

    [UnityTest]
    public IEnumerator Truck_SpawnsAtVehicleSpawnAndSettlesOnTheRoad()
    {
        var lane = spawn.position + spawn.right * LaneOffset + spawn.forward * SpawnAhead;
        var flat = Vector3.ProjectOnPlane(car.transform.position - lane, Vector3.up);
        Assert.Less(flat.magnitude, 1f, "Truck starts in the right lane at Vehicle Spawn");
        Assert.Greater(Vector3.Dot(Vector3.ProjectOnPlane(car.transform.forward, Vector3.up).normalized, spawn.forward), .99f, "Truck faces downhill");
        Assert.IsTrue(OnDrivableSurface(out float height), "Truck is above the road");
        Assert.That(height, Is.InRange(-.2f, .5f), "Truck sits on the road instead of falling or floating");
        Assert.Greater(car.transform.up.y, .98f, "Truck is level");
        Assert.Less(body.linearVelocity.magnitude, .3f, "Truck has settled");
        foreach (var wheel in car.GetComponentsInChildren<WheelCollider>())
            Assert.IsTrue(wheel.isGrounded, wheel.name + " touches the road");
        Assert.Less(Vector3.Distance(Vector3.one, car.transform.lossyScale), 1e-3f, "Physics root is unscaled");
        yield return null;
    }

    [UnityTest]
    public IEnumerator Throttle_MovesTheTruckForward()
    {
        var start = car.transform.position; var forward = car.transform.forward;
        car.Override = new BasicCarController.DriveInput(1, 0, 0);
        yield return Steps(Seconds(3));
        float travelled = Vector3.Dot(car.transform.position - start, forward);
        Assert.Greater(travelled, 6f, "Distance forward after 3 s of throttle");
        Assert.Greater(ForwardSpeed, 3f, "Forward speed after 3 s of throttle");
    }

    [UnityTest]
    public IEnumerator Steering_TurnsTheTruck()
    {
        var forward = car.transform.forward;
        car.Override = new BasicCarController.DriveInput(.6f, 0, 1);
        yield return Steps(Seconds(3));
        float yaw = Vector3.SignedAngle(forward, car.transform.forward, Vector3.up);
        Assert.Greater(yaw, 20f, "Steering right turns the truck clockwise");
        Assert.Greater(car.transform.up.y, .9f, "Truck stays upright while turning");
    }

    [UnityTest]
    public IEnumerator Brake_StopsTheTruck()
    {
        car.Override = new BasicCarController.DriveInput(1, 0, 0);
        for (int i = 0; i < Seconds(8) && ForwardSpeed < 8f; i++) yield return new WaitForFixedUpdate();
        Assert.GreaterOrEqual(ForwardSpeed, 8f, "Truck reaches 8 m/s before braking");
        car.Override = new BasicCarController.DriveInput(0, 1, 0);
        int steps = 0;
        while (ForwardSpeed > .5f && steps < Seconds(4)) { yield return new WaitForFixedUpdate(); steps++; }
        Assert.LessOrEqual(ForwardSpeed, .5f, "Braking from 8 m/s stops the truck within 4 s");
    }

    [UnityTest]
    public IEnumerator Truck_StaysOnTheRoadThroughTheFirstHairpin()
    {
        // Waypoint 216 is past the first hairpin (waypoints 180-204).
        var goal = Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude).Single(t => t.name == "Waypoint 216").position;
        Time.timeScale = 3f;
        int index = 0, steps = 0, limit = Seconds(90);
        while (Vector3.ProjectOnPlane(goal - body.position, Vector3.up).magnitude > 6f)
        {
            Assert.Less(steps++, limit, "Truck reached Waypoint 216 in time; stopped near " + body.position);
            // Follow the right-hand lane a few metres ahead and slow down for the bend coming up.
            index = NearestCentre(index);
            float speed = ForwardSpeed;
            int ahead = Mathf.Min(centre.Count - 1, index + Mathf.Max(4, Mathf.RoundToInt(speed * .6f)));
            var target = centre[ahead] + right[ahead] * LaneOffset;
            var toTarget = Vector3.ProjectOnPlane(target - body.position, Vector3.up);
            float angle = Vector3.SignedAngle(Vector3.ProjectOnPlane(car.transform.forward, Vector3.up), toTarget, Vector3.up);
            int far = Mathf.Min(centre.Count - 1, index + 15);
            float bend = Vector3.Angle(right[index], right[far]);
            float targetSpeed = Mathf.Lerp(11f, 4f, bend / 90f);
            car.Override = new BasicCarController.DriveInput(speed < targetSpeed ? 1 : 0, speed > targetSpeed + 1f ? 1 : 0, Mathf.Clamp(angle / 15f, -1, 1));
            yield return new WaitForFixedUpdate();
            Assert.IsTrue(OnDrivableSurface(out _), "Truck left the road near " + body.position);
            Assert.Greater(car.transform.up.y, .8f, "Truck rolled over near " + body.position);
        }
    }
}
#endif
