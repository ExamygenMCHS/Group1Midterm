using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Reads what the tyres are rolling on and turns it into road feel: a vibration level other scripts can use
/// (<see cref="VehicleFeel"/> shakes the chassis with it) and a procedurally generated tyre/road noise bed.
/// Nothing here pushes on the Rigidbody or edits the WheelColliders, so the handling is untouched.
/// </summary>
[RequireComponent(typeof(Rigidbody), typeof(AudioSource))]
public class RoadFeel : MonoBehaviour
{
    [Header("Surface")]
    [Tooltip("A collider whose name contains one of these (case-insensitive) counts as rough ground, anything else is asphalt.")]
    [SerializeField] string[] roughSurfaces = { "gravel", "shoulder", "terrain", "grass", "meadow", "dirt" };
    [Tooltip("Vibration at full speed on asphalt, as a fraction of vibration on rough ground.")]
    [SerializeField, Range(0f, 1f)] float asphaltVibration = .35f;
    [SerializeField] float fullSpeedKmh = 90f;

    [Header("Sound")]
    [SerializeField, Range(0f, 1f)] float volume = .5f;
    [Tooltip("Tyre slip (1 = at the grip limit) above which the tyres start to squeal.")]
    [SerializeField] float squealStart = 1f;

    /// <summary>0 at rest, about 0.35 at full speed on asphalt, 1 at full speed on rough ground.</summary>
    public float Vibration { get; private set; }
    /// <summary>Fraction of the wheels touching the ground.</summary>
    public float Grounded { get; private set; }
    /// <summary>0..1 share of the wheels on rough ground.</summary>
    public float Roughness { get; private set; }
    /// <summary>Highest tyre slip, where 1 is the grip limit.</summary>
    public float Slip { get; private set; }
    public float Speed01 { get; private set; }

    Rigidbody body;
    WheelCollider[] wheels;
    AudioSource source;
    AudioClip carrier;
    readonly Dictionary<Collider, bool> roughCache = new Dictionary<Collider, bool>();

    // Written by the main thread, read by the audio thread (single floats, so no locking is needed).
    float rumbleTarget, rumbleCoef = .05f, gritTarget, gritChance, squealTarget, squealHz = 900f;
    int sampleRate = 48000;

    void Awake()
    {
        body = GetComponent<Rigidbody>();
        wheels = GetComponentsInChildren<WheelCollider>();
        sampleRate = AudioSettings.outputSampleRate;
        source = GetComponent<AudioSource>();
        // The source only needs something to play so Unity runs OnAudioFilterRead; the filter overwrites the samples.
        carrier = AudioClip.Create("Road noise carrier", sampleRate, 1, sampleRate, false);
        source.clip = carrier;
        source.loop = true;
        source.spatialBlend = 0f;
        source.playOnAwake = false;
        source.volume = 1f;
    }

    void OnEnable()
    {
        if (source) source.Play();
    }

    void OnDisable()
    {
        if (source) source.Stop();
        rumbleTarget = gritTarget = squealTarget = 0f;
    }

    void OnDestroy()
    {
        if (carrier) Destroy(carrier);
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
        float slip = 0f;
        foreach (var wheel in wheels)
        {
            if (!wheel.GetGroundHit(out WheelHit hit)) continue;
            grounded++;
            if (IsRough(hit.collider)) rough++;
            float side = Mathf.Abs(hit.sidewaysSlip) / Mathf.Max(.01f, wheel.sidewaysFriction.extremumSlip);
            float fwd = Mathf.Abs(hit.forwardSlip) / Mathf.Max(.01f, wheel.forwardFriction.extremumSlip);
            slip = Mathf.Max(slip, side, fwd);
        }

        Grounded = wheels.Length == 0 ? 0f : grounded / (float)wheels.Length;
        Roughness = grounded == 0 ? 0f : rough / (float)grounded;
        Slip = slip;

        float speed = body.linearVelocity.magnitude;
        Speed01 = Mathf.Clamp01(speed * 3.6f / fullSpeedKmh);
        float surface = Mathf.Lerp(asphaltVibration, 1f, Roughness);
        float target = Speed01 * surface * Grounded;
        Vibration = Mathf.MoveTowards(Vibration, target, 4f * Time.fixedDeltaTime);
    }

    void Update()
    {
        bool paused = Time.timeScale <= 0f;
        float s = paused ? 0f : Speed01;
        float grip = Grounded;

        // Tyre hum: low-passed noise that gets louder and brighter with speed.
        rumbleTarget = volume * (.04f + .5f * Mathf.Pow(s, 1.1f)) * grip * (paused ? 0f : 1f);
        rumbleCoef = Mathf.Lerp(.015f, .16f, s) * Mathf.Lerp(1f, .6f, Roughness);

        // Aggregate crackle: random ticks, denser with speed and much louder on gravel.
        float ticksPerSecond = Mathf.Lerp(0f, 2500f, s) * Mathf.Lerp(1f, 1.8f, Roughness);
        gritChance = ticksPerSecond / sampleRate;
        gritTarget = volume * Mathf.Lerp(.05f, .45f, Roughness) * Mathf.Sqrt(s) * grip;

        // Squeal when a tyre is past its grip limit and the truck is actually moving.
        float over = Mathf.Clamp01((Slip - squealStart) / .6f);
        squealTarget = volume * .3f * over * Mathf.Clamp01(Speed01 * 8f) * grip * (paused ? 0f : 1f);
        squealHz = 820f + 380f * Speed01;
    }

    // Audio thread. Only touches private state; UnityEngine.Random and most of the Unity API are off limits here.
    uint rng = 2463534242u;
    float low, grit, phase, rumble, gritGain, squeal;

    float NextSigned()
    {
        rng ^= rng << 13; rng ^= rng >> 17; rng ^= rng << 5;
        return (rng & 0xFFFFFF) / (float)0x7FFFFF - 1f;
    }

    void OnAudioFilterRead(float[] data, int channels)
    {
        float rumbleT = rumbleTarget, coef = rumbleCoef, gritT = gritTarget, chance = gritChance, squealT = squealTarget;
        float step = squealHz * 2f * Mathf.PI / sampleRate;
        for (int i = 0; i + channels <= data.Length; i += channels)
        {
            // Ease the gains so the 60 Hz parameter updates do not click.
            rumble += (rumbleT - rumble) * .002f;
            gritGain += (gritT - gritGain) * .002f;
            squeal += (squealT - squeal) * .003f;

            float white = NextSigned();
            low += coef * (white - low);
            float tick = (NextSigned() * .5f + .5f) < chance ? NextSigned() : 0f;
            grit = grit * .55f + tick;
            phase += step;
            if (phase > 6.2831853f) phase -= 6.2831853f;
            float wobble = .6f + .4f * Mathf.Sin(phase * .013f + white * .2f);

            float sample = low * rumble * 4f + grit * gritGain + Mathf.Sin(phase) * squeal * wobble * (.7f + .3f * white);
            sample = Mathf.Clamp(sample, -1f, 1f);
            for (int c = 0; c < channels; c++) data[i + c] = sample;
        }
    }
}
