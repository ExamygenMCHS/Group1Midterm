using System.Collections.Generic;
using UnityEngine;

// The road the traffic drives on: the centre line of the mountain road (baked in the Editor, ordered downhill) and the
// cars that use it. Downhill cars share the player's lane, uphill cars come the other way in the opposite lane.
public class TrafficRoute : MonoBehaviour
{
    [System.Serializable]
    public class Vehicle
    {
        [Tooltip("A vehicle with a BoxCollider and a Rigidbody. It gets a TrafficCar when it is put on the road.")]
        public GameObject prefab;
        [Min(0f)] public float weight = 1f;
    }

    [Header("Road (baked by Tools > Mountain Delivery > Add Traffic)")]
    [SerializeField] private Vector3[] points = new Vector3[0];
    [Tooltip("Unit vector to the right-hand side of the road at each point, facing downhill.")]
    [SerializeField] private Vector3[] rights = new Vector3[0];
    [Tooltip("Distance of a lane's middle from the centre line.")]
    [SerializeField] private float laneOffset = 2.5f;

    [Header("Cars")]
    [Tooltip("The vehicles on the road. A higher weight is picked more often (the recoloured pickup is the rare one).")]
    [SerializeField] private Vehicle[] vehicles = new Vehicle[0];
    [Tooltip("The same seed gives the same mix of vehicles every time.")]
    [SerializeField] private int seed = 485;
    [SerializeField] private int downhillCars = 3;
    [SerializeField] private int uphillCars = 4;
    [Tooltip("Slowest and fastest cruising speed in m/s.")]
    [SerializeField] private Vector2 downhillSpeed = new Vector2(6f, 8f);
    [SerializeField] private Vector2 uphillSpeed = new Vector2(8f, 10.5f);
    [Tooltip("Paint for vehicles that can be repainted (the pickup), so it does not look like the player's truck.")]
    [SerializeField] private Color[] paints =
    {
        new Color(.25f, .45f, .75f), new Color(.2f, .55f, .6f), new Color(.95f, .95f, .9f),
        new Color(.3f, .3f, .32f), new Color(.9f, .75f, .3f), new Color(.35f, .6f, .45f)
    };

    [Header("Player")]
    [Tooltip("The delivery truck. Cars neither start nor come back onto the road close to it.")]
    [SerializeField] private Transform player;
    [SerializeField] private float clearOfPlayer = 45f;

    private float[] distances = new float[0];
    private float[] bends = new float[0];
    private readonly List<TrafficCar> cars = new List<TrafficCar>();
    private bool cleared;
    private System.Random random;

    public float Length { get; private set; }
    public float LaneOffset { get { return laneOffset; } }
    public IReadOnlyList<TrafficCar> Cars { get { return cars; } }
    public int PointCount { get { return points.Length; } }

    private void Awake()
    {
        Measure();
    }

    private void Start()
    {
        if (cleared || points.Length < 2 || TotalWeight() <= 0f)
            return;
        random = new System.Random(seed);
        Spawn(downhillCars, 1, downhillSpeed);
        Spawn(uphillCars, -1, uphillSpeed);
    }

    // Takes every car off the road and keeps it empty (a quiet road for testing the truck on its own).
    public void Clear()
    {
        cleared = true;
        foreach (TrafficCar car in cars)
            if (car != null)
                Destroy(car.gameObject);
        cars.Clear();
    }

    // Used by the Editor setup to store the baked road.
    public void SetPath(Vector3[] centre, Vector3[] right)
    {
        points = centre;
        rights = right;
        Measure();
    }

    private void Measure()
    {
        int n = points.Length;
        distances = new float[n];
        bends = new float[n];
        Length = 0f;
        for (int i = 1; i < n; i++)
        {
            Length += Vector3.Distance(points[i - 1], points[i]);
            distances[i] = Length;
        }
        // How sharply the road turns around each point: 0 on a straight, 1 in a hairpin.
        const int reach = 5;
        for (int i = 0; i < n; i++)
        {
            int before = Mathf.Max(0, i - reach), after = Mathf.Min(n - 1, i + reach);
            if (before == i || after == i) continue;
            float angle = Vector3.Angle(points[i] - points[before], points[after] - points[i]);
            bends[i] = Mathf.InverseLerp(5f, 45f, angle);
        }
    }

    // Cars are spread evenly along the road, skipping the stretch around the player.
    private void Spawn(int count, int direction, Vector2 speedRange)
    {
        for (int i = 0; i < count; i++)
        {
            float distance = Length * (i + .5f) / count;
            Sample(distance, direction, out Vector3 position, out Vector3 forward, out _);
            GameObject prefab = PickVehicle();
            GameObject vehicle = Instantiate(prefab, position, Quaternion.LookRotation(forward), transform);
            vehicle.name = (direction > 0 ? "Traffic downhill " : "Traffic uphill ") + (i + 1) + " (" + prefab.name + ")";
            TrafficCar car = vehicle.GetComponent<TrafficCar>();
            if (car == null) car = vehicle.AddComponent<TrafficCar>();
            float speed = Mathf.Lerp(speedRange.x, speedRange.y, count <= 1 ? .5f : i / (float)(count - 1));
            Color paint = paints.Length == 0 ? Color.white : paints[random.Next(paints.Length)];
            car.Begin(this, direction, distance, speed, paint);
            if (!IsClearOfPlayer(position))
                car.Park();
            cars.Add(car);
        }
    }

    private float TotalWeight()
    {
        float total = 0f;
        foreach (Vehicle v in vehicles)
            if (v != null && v.prefab != null) total += Mathf.Max(0f, v.weight);
        return total;
    }

    private GameObject PickVehicle()
    {
        float roll = (float)random.NextDouble() * TotalWeight();
        GameObject last = null;
        foreach (Vehicle v in vehicles)
        {
            if (v == null || v.prefab == null || v.weight <= 0f) continue;
            last = v.prefab;
            roll -= v.weight;
            if (roll <= 0f) break;
        }
        return last;
    }

    // Where a car is at the given distance from the top of the road: the middle of its lane, facing its way.
    // direction 1 drives downhill (the player's lane), -1 uphill (the opposite lane).
    public void Sample(float distance, int direction, out Vector3 position, out Vector3 forward, out float bend)
    {
        int n = points.Length;
        if (n < 2)
        {
            position = transform.position; forward = transform.forward; bend = 0f;
            return;
        }
        distance = Mathf.Clamp(distance, 0f, Length);
        int low = 0, high = n - 1;
        while (high - low > 1)
        {
            int mid = (low + high) / 2;
            if (distances[mid] <= distance) low = mid; else high = mid;
        }
        float span = distances[high] - distances[low];
        float t = span <= 0f ? 0f : (distance - distances[low]) / span;
        Vector3 centre = Vector3.Lerp(points[low], points[high], t);
        Vector3 right = Vector3.Slerp(rights[low], rights[high], t);
        position = centre + right * (laneOffset * direction);
        // The heading is taken over a few points, so a car turns smoothly instead of snapping at each point.
        Vector3 ahead = points[Mathf.Min(n - 1, high + 1)] - points[Mathf.Max(0, low - 1)];
        forward = (ahead.sqrMagnitude < 1e-6f ? points[high] - points[low] : ahead).normalized * direction;
        bend = Mathf.Lerp(bends[low], bends[high], t);
    }

    // Distance along the road (from the top) of the point nearest to a world position.
    public float DistanceAt(Vector3 world)
    {
        int best = 0;
        float bestDistance = float.MaxValue;
        for (int i = 0; i < points.Length; i++)
        {
            float d = (points[i] - world).sqrMagnitude;
            if (d < bestDistance) { bestDistance = d; best = i; }
        }
        return distances.Length == 0 ? 0f : distances[best];
    }

    public bool IsClearOfPlayer(Vector3 position)
    {
        return player == null || (player.position - position).sqrMagnitude > clearOfPlayer * clearOfPlayer;
    }
}
