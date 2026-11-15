using UnityEngine;

// One traffic car on the mountain road. It follows its lane on the TrafficRoute at a steady speed, slows for bends,
// waits behind anything in its way, stops for a moment when the truck hits it, and at the end of the road goes
// back to the other end to drive it again. It is a kinematic body: the truck bounces off it, it never leaves its lane.
[RequireComponent(typeof(Rigidbody))]
public class TrafficCar : MonoBehaviour
{
    public enum DriveState { Cruising, Waiting, Stopped, Parked }

    [Header("Driving")]
    [SerializeField] private float acceleration = 3f;
    [SerializeField] private float braking = 9f;
    [Tooltip("Share of the cruising speed kept in the tightest bend.")]
    [SerializeField, Range(.2f, 1f)] private float cornerSpeed = .55f;
    [Tooltip("Seconds the car stands still after the truck hit it.")]
    [SerializeField] private float stopSeconds = 2.5f;

    [Header("Sensor")]
    [Tooltip("How far ahead the car looks for the truck or another car.")]
    [SerializeField] private float lookAhead = 10f;
    [SerializeField] private float sensorRadius = .9f;
    [Tooltip("Where the sensor starts, in the car's local space. Taken from the front of the BoxCollider when there is one.")]
    [SerializeField] private Vector3 sensorOrigin = new Vector3(0f, .9f, 2.3f);

    [Header("Visuals")]
    [Tooltip("Wheels to roll when the vehicle has no TrafficVehicleRig (the pickup model).")]
    [SerializeField] private Transform[] wheels;
    [SerializeField] private float wheelRadius = .5f;
    [Tooltip("Renderers that take the car's paint colour, on the material slots that use the paint material.")]
    [SerializeField] private Renderer[] paintRenderers;
    [SerializeField] private Material paintMaterial;

    private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

    private Rigidbody body;
    private Collider[] colliders;
    private Renderer[] renderers;
    private TrafficVehicleRig rig; // the traffic models animate their own wheels, steering and brake lamps
    private Vector3 lastForward;
    private TrafficRoute route;
    private int direction = 1;
    private float distance;
    private float cruiseSpeed;
    private float speed;
    private float stopTimer;
    private float wheelAngle;

    public DriveState State { get; private set; } = DriveState.Cruising;
    public float Speed { get { return speed; } }
    public int Direction { get { return direction; } }
    public float CruiseSpeed { get { return cruiseSpeed; } }

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        body.isKinematic = true;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        colliders = GetComponentsInChildren<Collider>();
        renderers = GetComponentsInChildren<Renderer>();
        rig = GetComponent<TrafficVehicleRig>();
        BoxCollider box = GetComponent<BoxCollider>();
        if (box != null)
            sensorOrigin = new Vector3(box.center.x, Mathf.Min(box.center.y, 1.1f), box.center.z + box.size.z * .5f + .1f);
        lastForward = transform.forward;
    }

    // Called by the route right after the car is created.
    public void Begin(TrafficRoute owner, int drivingDirection, float startDistance, float cruise, Color paint)
    {
        route = owner;
        direction = drivingDirection >= 0 ? 1 : -1;
        distance = startDistance;
        cruiseSpeed = cruise;
        speed = cruise;
        if (paintRenderers != null)
        {
            var block = new MaterialPropertyBlock();
            foreach (Renderer r in paintRenderers)
            {
                if (r == null) continue;
                Material[] slots = r.sharedMaterials;
                for (int slot = 0; slot < slots.Length; slot++)
                {
                    if (paintMaterial != null && slots[slot] != paintMaterial) continue;
                    r.GetPropertyBlock(block, slot);
                    block.SetColor(BaseColor, paint);
                    r.SetPropertyBlock(block, slot);
                }
            }
        }
        Place();
    }

    // Takes the car off the road until its end of the road is clear of the player.
    public void Park()
    {
        distance = direction > 0 ? 0f : route.Length;
        speed = 0f;
        ChangeState(DriveState.Parked);
    }

    // The only place the driving state changes.
    private void ChangeState(DriveState next)
    {
        if (next == State)
            return;
        State = next;
        bool onRoad = State != DriveState.Parked;
        foreach (Renderer r in renderers) if (r != null) r.enabled = onRoad;
        foreach (Collider c in colliders) c.enabled = onRoad;
        if (State == DriveState.Stopped) stopTimer = stopSeconds;
    }

    private void FixedUpdate()
    {
        if (route == null)
            return;
        float dt = Time.fixedDeltaTime;

        if (State == DriveState.Parked)
        {
            route.Sample(distance, direction, out Vector3 entry, out Vector3 heading, out _);
            if (!route.IsClearOfPlayer(entry))
                return;
            body.position = entry;
            body.rotation = Quaternion.LookRotation(heading);
            transform.SetPositionAndRotation(entry, body.rotation);
            speed = cruiseSpeed;
            lastForward = heading;
            ChangeState(DriveState.Cruising);
            return;
        }

        if (State == DriveState.Stopped)
        {
            stopTimer -= dt;
            if (stopTimer <= 0f) ChangeState(DriveState.Cruising);
        }
        else
        {
            ChangeState(SomethingAhead() ? DriveState.Waiting : DriveState.Cruising);
        }

        route.Sample(distance, direction, out _, out _, out float bend);
        float target = State == DriveState.Cruising ? cruiseSpeed * Mathf.Lerp(1f, cornerSpeed, bend) : 0f;
        speed = Mathf.MoveTowards(speed, target, (target < speed ? braking : acceleration) * dt);
        distance += direction * speed * dt;

        if (distance <= 0f || distance >= route.Length)
        {
            Park();
            return;
        }
        Place();
        Animate(speed * dt, target < speed - .05f, dt);
    }

    // Rolls the wheels by the distance travelled. The traffic models also steer into the bend and light their brake lamps.
    private void Animate(float metres, bool brake, float dt)
    {
        if (rig != null)
        {
            route.Sample(distance, direction, out _, out Vector3 forward, out _);
            float yaw = Vector3.SignedAngle(Vector3.ProjectOnPlane(lastForward, Vector3.up), Vector3.ProjectOnPlane(forward, Vector3.up), Vector3.up);
            lastForward = forward;
            float steer = metres > .001f ? Mathf.Atan(rig.Wheelbase * yaw * Mathf.Deg2Rad / metres) * Mathf.Rad2Deg : 0f;
            rig.AnimateTravel(metres, steer, brake, dt);
            return;
        }
        wheelAngle = Mathf.Repeat(wheelAngle + metres / Mathf.Max(.05f, wheelRadius) * Mathf.Rad2Deg, 360f);
        if (wheels != null)
            foreach (Transform wheel in wheels)
                if (wheel != null) wheel.localRotation = Quaternion.Euler(wheelAngle, 0f, 0f);
    }

    private void Place()
    {
        route.Sample(distance, direction, out Vector3 position, out Vector3 forward, out _);
        Quaternion rotation = Quaternion.LookRotation(forward, Vector3.up);
        if (body != null && Application.isPlaying && State != DriveState.Parked && Time.inFixedTimeStep)
        {
            body.MovePosition(position);
            body.MoveRotation(rotation);
        }
        else
        {
            transform.SetPositionAndRotation(position, rotation);
        }
    }

    // True when the truck or another car is in the lane just ahead. Rails, signs and the road itself are ignored.
    private bool SomethingAhead()
    {
        Vector3 origin = transform.TransformPoint(sensorOrigin);
        float reach = Mathf.Max(3f, lookAhead * Mathf.Clamp01(speed / Mathf.Max(.1f, cruiseSpeed) + .35f));
        if (!Physics.SphereCast(origin, sensorRadius, transform.forward, out RaycastHit hit, reach, ~0, QueryTriggerInteraction.Ignore))
            return false;
        return hit.rigidbody != null && hit.rigidbody != body;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (State == DriveState.Parked)
            return;
        if (collision.rigidbody != null && collision.rigidbody.GetComponent<BasicCarController>() != null)
        {
            speed = 0f;
            ChangeState(DriveState.Stopped);
            stopTimer = stopSeconds;
        }
    }
}
