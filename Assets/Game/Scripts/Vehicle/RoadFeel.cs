using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Reads what the tyres are rolling on and turns it into road feel: a vibration level other scripts can use
/// (<see cref="VehicleFeel"/> shakes the chassis with it) and grip data for <see cref="TruckDriveAudio"/>.
/// Nothing here pushes on the Rigidbody or edits the WheelColliders, so the handling is untouched.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class RoadFeel : MonoBehaviour
{
    [Header("Surface")]
    [Tooltip("A collider whose name contains one of these (case-insensitive) counts as rough ground, anything else is asphalt.")]
    [SerializeField] string[] roughSurfaces = { "gravel", "shoulder", "terrain", "grass", "meadow", "dirt" };
    [Tooltip("Vibration at full speed on asphalt, as a fraction of vibration on rough ground.")]
    [SerializeField, Range(0f, 1f)] float asphaltVibration = .35f;
    [SerializeField] float fullSpeedKmh = 90f;

    /// <summary>0 at rest, about 0.35 at full speed on asphalt, 1 at full speed on rough ground.</summary>
    public float Vibration { get; private set; }
    /// <summary>Fraction of the wheels touching the ground.</summary>
    public float Grounded { get; private set; }
    /// <summary>0..1 share of the wheels on rough ground.</summary>
    public float Roughness { get; private set; }
    /// <summary>Highest tyre slip, where 1 is the grip limit.</summary>
    public float Slip { get; private set; }
    public float SidewaysSlip { get; private set; }
    public float Speed01 { get; private set; }

    Rigidbody body;
    WheelCollider[] wheels;
    readonly Dictionary<Collider, bool> roughCache = new Dictionary<Collider, bool>();

    void Awake()
    {
        body = GetComponent<Rigidbody>();
        wheels = GetComponentsInChildren<WheelCollider>();
    }

    /// <summary>True when the collider counts as rough ground (gravel, shoulder, meadow...) rather than asphalt.</summary>
    public bool IsRough(Collider c)
    {
        if (!c) return false;
        if (roughCache.TryGetValue(c, out bool rough)) return rough;
        string lower = c.name.ToLowerInvariant();
        foreach (var key in roughSurfaces)
            if (lower.Contains(key)) { rough = true; break; }
        roughCache[c] = rough;
        return rough;
    }

    void FixedUpdate()
    {
        int grounded = 0, rough = 0;
        float forwardSlip = 0f, sidewaysSlip = 0f;
        foreach (var wheel in wheels)
        {
            if (!wheel.GetGroundHit(out WheelHit hit)) continue;
            grounded++;
            if (IsRough(hit.collider)) rough++;
            float side = Mathf.Abs(hit.sidewaysSlip) / Mathf.Max(.01f, wheel.sidewaysFriction.extremumSlip);
            float fwd = Mathf.Abs(hit.forwardSlip) / Mathf.Max(.01f, wheel.forwardFriction.extremumSlip);
            sidewaysSlip = Mathf.Max(sidewaysSlip, side);
            forwardSlip = Mathf.Max(forwardSlip, fwd);
        }

        Grounded = wheels.Length == 0 ? 0f : grounded / (float)wheels.Length;
        Roughness = grounded == 0 ? 0f : rough / (float)grounded;
        SidewaysSlip = sidewaysSlip;
        Slip = Mathf.Max(forwardSlip, sidewaysSlip);

        float speed = body.linearVelocity.magnitude;
        Speed01 = Mathf.Clamp01(speed * 3.6f / fullSpeedKmh);
        float surface = Mathf.Lerp(asphaltVibration, 1f, Roughness);
        float target = Speed01 * surface * Grounded;
        Vibration = Mathf.MoveTowards(Vibration, target, 4f * Time.fixedDeltaTime);
    }
}
