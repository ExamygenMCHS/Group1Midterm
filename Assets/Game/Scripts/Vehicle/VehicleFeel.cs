using UnityEngine;

/// <summary>
/// Visual-only suspension feel for the pickup. The chassis mesh leans out of corners, squats under power, dives
/// under braking, bounces on soft mounts and shudders with the road, while the wheels lean with it.
/// It only moves the body mesh; the Rigidbody, WheelColliders and BasicCarController are never touched,
/// so the handling stays exactly as tuned.
/// </summary>
[DefaultExecutionOrder(100)]
[RequireComponent(typeof(Rigidbody))]
public class VehicleFeel : MonoBehaviour
{
    const float Gravity = 9.81f;

    [Header("Rig")]
    [Tooltip("Chassis visuals that lean and bounce (not the wheels). Found under 'Model' when left empty.")]
    [SerializeField] Transform body;
    [Tooltip("Point the chassis rolls and pitches about, in the truck's local space (about axle height).")]
    [SerializeField] Vector3 pivot = new Vector3(0f, .5f, 0f);

    [Header("Lean")]
    [Tooltip("Degrees the chassis rolls to the outside per g of cornering force.")]
    [SerializeField] float rollPerG = 5f;
    [Tooltip("Degrees the nose lifts under power and dives under braking, per g.")]
    [SerializeField] float pitchPerG = 7f;
    [SerializeField] float maxRoll = 6f;
    [SerializeField] float maxPitch = 5f;
    [Tooltip("How much of the chassis roll the tyres follow (the wheels lean with the body).")]
    [SerializeField, Range(0f, 1f)] float wheelLean = .25f;

    [Header("Springs")]
    [Tooltip("Natural frequency of the lean and bounce springs. Lower is floatier.")]
    [SerializeField] float springFrequency = 2.2f;
    [Tooltip("1 settles without overshoot. Lower values jiggle for longer.")]
    [SerializeField, Range(.1f, 1f)] float damping = .3f;
    [Tooltip("How strongly bumps and landings throw the body up and down.")]
    [SerializeField] float bounceGain = .5f;
    [SerializeField] float maxBounce = .06f;
    [Tooltip("Seconds over which the measured acceleration is smoothed.")]
    [SerializeField] float accelSmoothing = .08f;

    [Header("Road shake")]
    [Tooltip("Vertical shake of the chassis in metres at full road vibration.")]
    [SerializeField] float shakeHeight = .006f;
    [Tooltip("Roll and pitch shake in degrees at full road vibration.")]
    [SerializeField] float shakeAngle = .35f;
    [Tooltip("Vertical shake of each tyre in metres at full road vibration.")]
    [SerializeField] float wheelShake = .004f;
    [Tooltip("Engine shudder in metres, strongest when the truck is standing still.")]
    [SerializeField] float idleShudder = .0015f;

    Rigidbody rb;
    RoadFeel road;
    Vector3 restLocalPosition;
    Quaternion restLocalRotation;
    Vector3 lastVelocity;
    Vector3 localAccel;       // smoothed, truck space (x right, y up, z forward)
    float bumpAccel;          // smoothed vertical acceleration

    float roll, rollSpeed, pitch, pitchSpeed, heave, heaveSpeed;
    float shakeRoll, shakePitch, shakeHeave;

    /// <summary>Current chassis roll in degrees (positive leans to the left, as in a right-hand corner).</summary>
    public float RollDegrees => roll + shakeRoll;
    /// <summary>Current chassis pitch in degrees (positive dives).</summary>
    public float PitchDegrees => pitch + shakePitch;
    /// <summary>How much of the roll the tyres follow, 0..1.</summary>
    public float WheelLean => wheelLean;

    /// <summary>Small vertical jitter for one tyre, in metres.</summary>
    public float WheelBump(int index)
    {
        if (!road) return 0f;
        return Noise(Time.time * 41f, 13.7f * (index + 1)) * wheelShake * road.Vibration;
    }

    static float Noise(float x, float seed) => Mathf.PerlinNoise(x, seed) * 2f - 1f;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        road = GetComponent<RoadFeel>();
        if (!body)
        {
            var model = transform.Find("Model");
            if (model)
                for (int i = 0; i < model.childCount; i++)
                    if (model.GetChild(i).name != "WheelsMesh") { body = model.GetChild(i); break; }
        }
        if (body)
        {
            restLocalPosition = body.localPosition;
            restLocalRotation = body.localRotation;
        }
    }

    void OnEnable()
    {
        lastVelocity = rb ? rb.linearVelocity : Vector3.zero;
        localAccel = Vector3.zero;
        bumpAccel = 0f;
    }

    void OnDisable()
    {
        if (body) body.SetLocalPositionAndRotation(restLocalPosition, restLocalRotation);
        roll = rollSpeed = pitch = pitchSpeed = heave = heaveSpeed = shakeRoll = shakePitch = shakeHeave = 0f;
    }

    void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;
        Vector3 velocity = rb.linearVelocity;
        Vector3 accel = transform.InverseTransformDirection((velocity - lastVelocity) / dt);
        lastVelocity = velocity;

        // Clamp so a collision spike cannot throw the body around.
        accel = Vector3.ClampMagnitude(accel, 1.5f * Gravity);
        float blend = 1f - Mathf.Exp(-dt / Mathf.Max(.001f, accelSmoothing));
        localAccel = Vector3.Lerp(localAccel, accel, blend);
        bumpAccel = Mathf.Lerp(bumpAccel, Mathf.Clamp(accel.y, -12f, 12f), 1f - Mathf.Exp(-dt / .03f));
    }

    void Update()
    {
        float dt = Mathf.Min(Time.deltaTime, .05f);
        if (dt <= 0f) return;

        float grounded = road ? road.Grounded : 1f;
        float w = 2f * Mathf.PI * springFrequency;
        float stiffness = w * w, damp = 2f * damping * w;

        // Targets: roll leans out of the corner, pitch noses up when accelerating and dives when braking.
        float targetRoll = Mathf.Clamp(localAccel.x / Gravity * rollPerG, -maxRoll, maxRoll) * grounded;
        float targetPitch = Mathf.Clamp(-localAccel.z / Gravity * pitchPerG, -maxPitch, maxPitch) * grounded;

        rollSpeed += (stiffness * (targetRoll - roll) - damp * rollSpeed) * dt;
        roll += rollSpeed * dt;
        pitchSpeed += (stiffness * (targetPitch - pitch) - damp * pitchSpeed) * dt;
        pitch += pitchSpeed * dt;

        // The body sits on its mounts, so when the chassis jolts up the body lags behind (and the reverse on landing).
        heaveSpeed += (stiffness * (0f - heave) - damp * heaveSpeed - bumpAccel * bounceGain) * dt;
        heave = Mathf.Clamp(heave + heaveSpeed * dt, -maxBounce, maxBounce);

        // Road shake and engine shudder.
        float t = Time.time;
        float vibration = road ? road.Vibration : 0f;
        float still = 1f - Mathf.Clamp01(rb.linearVelocity.magnitude / 4f);
        float shudder = Mathf.Sin(t * 2f * Mathf.PI * 29f) * idleShudder * still;
        shakeHeave = (Noise(t * 23f, 1.3f) * .6f + Noise(t * 57f, 7.1f) * .4f) * shakeHeight * vibration + shudder;
        shakeRoll = Noise(t * 19f, 3.7f) * shakeAngle * vibration + shudder * 25f;
        shakePitch = Noise(t * 27f, 9.9f) * shakeAngle * .6f * vibration;
    }

    void LateUpdate()
    {
        if (!body) return;
        Transform parent = body.parent;
        Transform root = transform;
        Quaternion rootRotation = root.rotation;
        // Roll and pitch are applied in the truck's frame, about the pivot, on top of the rest pose.
        Quaternion lean = Quaternion.Euler(PitchDegrees, 0f, RollDegrees);
        Quaternion delta = rootRotation * lean * Quaternion.Inverse(rootRotation);
        Vector3 pivotWorld = root.TransformPoint(pivot);
        Vector3 restPosition = parent ? parent.TransformPoint(restLocalPosition) : restLocalPosition;
        Quaternion restRotation = parent ? parent.rotation * restLocalRotation : restLocalRotation;
        Vector3 lift = root.TransformVector(new Vector3(0f, heave + shakeHeave, 0f));
        body.SetPositionAndRotation(pivotWorld + delta * (restPosition - pivotWorld) + lift, delta * restRotation);
    }
}
