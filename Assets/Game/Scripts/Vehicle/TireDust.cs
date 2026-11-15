using UnityEngine;

/// <summary>
/// Kicks up a little dust and a few pebbles from each tyre: a light haze on gravel and grass that builds with
/// speed, a few grit specks on asphalt and a puff of grey smoke when a tyre slides. Purely cosmetic; the
/// particle systems are built at runtime and follow each tyre's contact patch.
/// </summary>
[RequireComponent(typeof(Rigidbody), typeof(RoadFeel))]
public class TireDust : MonoBehaviour
{
    [SerializeField] Material dustMaterial;
    [SerializeField] Material pebbleMaterial;

    [Header("Dust")]
    [SerializeField] Color gravelDust = new Color(.78f, .68f, .5f, .3f);
    [SerializeField] Color asphaltSmoke = new Color(.8f, .8f, .8f, .22f);
    [Tooltip("Particles per second per tyre at full speed on rough ground.")]
    [SerializeField] float roughRate = 28f;
    [Tooltip("Particles per second per tyre when a tyre is sliding on asphalt.")]
    [SerializeField] float slideRate = 60f;

    [Header("Pebbles")]
    [Tooltip("Pebbles per second per tyre at full speed on rough ground.")]
    [SerializeField] float pebbleRate = 10f;
    [SerializeField] Color pebbleColor = new Color(.45f, .4f, .34f, 1f);

    Rigidbody body;
    RoadFeel road;
    WheelCollider[] wheels;
    ParticleSystem[] dust, pebbles;
    Vector3[] contact;
    bool[] touching;

    void Awake()
    {
        body = GetComponent<Rigidbody>();
        road = GetComponent<RoadFeel>();
        wheels = GetComponentsInChildren<WheelCollider>();
        dust = new ParticleSystem[wheels.Length];
        pebbles = new ParticleSystem[wheels.Length];
        contact = new Vector3[wheels.Length];
        touching = new bool[wheels.Length];
        for (int i = 0; i < wheels.Length; i++)
        {
            dust[i] = Build(wheels[i].name + " Dust", false);
            pebbles[i] = Build(wheels[i].name + " Pebbles", true);
        }
    }

    ParticleSystem Build(string name, bool pebble)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = pebble ? new ParticleSystem.MinMaxCurve(.45f, .8f) : new ParticleSystem.MinMaxCurve(.7f, 1.3f);
        main.startSize = pebble ? new ParticleSystem.MinMaxCurve(.035f, .08f) : new ParticleSystem.MinMaxCurve(.25f, .55f);
        main.startSpeed = pebble ? new ParticleSystem.MinMaxCurve(1.5f, 4f) : new ParticleSystem.MinMaxCurve(.3f, 1.2f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.gravityModifier = pebble ? 1.4f : -.04f;
        main.maxParticles = pebble ? 40 : 60;
        main.startColor = pebble ? pebbleColor : gravelDust;

        var emission = ps.emission;
        emission.rateOverTime = 0f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = pebble ? 35f : 25f;
        shape.radius = .12f;

        var colour = ps.colorOverLifetime;
        colour.enabled = true;
        var fade = new Gradient();
        fade.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(pebble ? 1f : .9f, .12f), new GradientAlphaKey(pebble ? 1f : 0f, 1f) });
        colour.color = fade;

        if (!pebble)
        {
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, .5f, 1f, 1.8f));
            var drag = ps.limitVelocityOverLifetime;
            drag.enabled = true;
            drag.dampen = .12f;
            drag.limit = .6f;
        }

        var rendererSettings = ps.GetComponent<ParticleSystemRenderer>();
        rendererSettings.renderMode = ParticleSystemRenderMode.Billboard;
        rendererSettings.sharedMaterial = pebble ? pebbleMaterial : dustMaterial;
        rendererSettings.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        rendererSettings.receiveShadows = false;
        ps.Play();
        return ps;
    }

    void FixedUpdate()
    {
        for (int i = 0; i < wheels.Length; i++)
        {
            touching[i] = wheels[i].GetGroundHit(out WheelHit hit);
            if (touching[i]) contact[i] = hit.point;
        }
    }

    void Update()
    {
        float speed = body.linearVelocity.magnitude;
        float s = Mathf.Clamp01(speed / 20f);
        Vector3 back = -body.linearVelocity.normalized;
        if (speed < .5f) back = -transform.forward;

        for (int i = 0; i < wheels.Length; i++)
        {
            bool grounded = touching[i];
            float dustRate = 0f, stoneRate = 0f;
            Color colour = gravelDust;
            if (grounded && wheels[i].GetGroundHit(out WheelHit hit))
            {
                bool rough = road.IsRough(hit.collider);
                float side = Mathf.Abs(hit.sidewaysSlip) / Mathf.Max(.01f, wheels[i].sidewaysFriction.extremumSlip);
                float fwd = Mathf.Abs(hit.forwardSlip) / Mathf.Max(.01f, wheels[i].forwardFriction.extremumSlip);
                float slide = Mathf.Clamp01((Mathf.Max(side, fwd) - .9f) / .6f) * Mathf.Clamp01(speed / 4f);
                float driven = Mathf.Clamp01(Mathf.Max(0f, fwd - .3f)) * Mathf.Clamp01(speed / 8f);
                if (rough)
                {
                    dustRate = roughRate * Mathf.Sqrt(s) * (1f + 1.5f * slide + driven);
                    stoneRate = pebbleRate * s * (1f + slide + driven);
                    colour = gravelDust;
                }
                else
                {
                    dustRate = slideRate * slide + 2f * s;
                    stoneRate = 0f;
                    colour = asphaltSmoke;
                }
            }

            var d = dust[i];
            var dMain = d.main; dMain.startColor = colour;
            var dEmission = d.emission; dEmission.rateOverTime = dustRate;
            var p = pebbles[i];
            var pEmission = p.emission; pEmission.rateOverTime = stoneRate;
            if (grounded)
            {
                // Dust lifts straight off the ground; pebbles are flicked up and back.
                d.transform.SetPositionAndRotation(contact[i] + Vector3.up * .05f, Quaternion.LookRotation(Vector3.up, back));
                Vector3 flick = (Vector3.up + back * .8f).normalized;
                p.transform.SetPositionAndRotation(contact[i] + Vector3.up * .05f, Quaternion.LookRotation(flick, Vector3.up));
            }
        }
    }

    void OnDisable()
    {
        if (dust == null) return;
        for (int i = 0; i < dust.Length; i++)
        {
            if (dust[i]) dust[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (pebbles[i]) pebbles[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    void OnEnable()
    {
        if (dust == null) return;
        for (int i = 0; i < dust.Length; i++) { dust[i].Play(); pebbles[i].Play(); }
    }
}
