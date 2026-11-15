using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

// Collision damage for the delivery truck. A hit takes health by how hard the truck ran into something (scraping along
// a guardrail costs almost nothing, a head-on crash costs a lot). The health decides the damage state: a badly
// damaged truck loses engine power and smokes, a wrecked one stops. Other scripts react through the events.
[RequireComponent(typeof(Rigidbody))]
public class TruckDamage : MonoBehaviour
{
    private const float AudibleImpactSpeed = .5f;
    private const float ImpactSoundCooldown = .18f;
    private const float ScrapeHoldSeconds = .08f;

    public enum DamageState { Intact, Damaged, Critical, Wrecked }

    [Header("Health")]
    [SerializeField] private float maxHealth = 100f;
    [Tooltip("Health share below which the truck counts as damaged, and below which it is critical.")]
    [SerializeField, Range(0f, 1f)] private float damagedBelow = .7f;
    [SerializeField, Range(0f, 1f)] private float criticalBelow = .35f;

    [Header("Impacts")]
    [Tooltip("Speed into the obstacle (m/s) below which a hit does no damage.")]
    [SerializeField] private float minImpactSpeed = 2.5f;
    [Tooltip("Health lost per m/s of impact speed above the minimum.")]
    [SerializeField] private float damagePerSpeed = 4f;
    [Tooltip("Seconds after a hit during which another hit is ignored (one crash touches several colliders).")]
    [SerializeField] private float hitCooldown = .3f;

    [Header("Effects")]
    [Tooltip("Engine power left when the truck is critical (1 = full power).")]
    [SerializeField, Range(.1f, 1f)] private float criticalPower = .55f;
    [Tooltip("Optional smoke from the engine, switched on when the truck is damaged.")]
    [SerializeField] private ParticleSystem smoke;

    [Header("Events")]
    [SerializeField] private UnityEvent<float> onDamaged = new UnityEvent<float>(); // health lost by this hit
    [SerializeField] private UnityEvent onStateChanged = new UnityEvent();
    [SerializeField] private UnityEvent onWrecked = new UnityEvent();

    private BasicCarController car;
    private Rigidbody body;
    private readonly List<(UnityEngine.Object obstacle, float time)> recentImpactSounds = new();
    private float health;
    private float fullMotorPower;
    private float lastHitTime = -10f;
    private float lastScrapeTime = float.NegativeInfinity;
    private float scrapeIntensity;
    private bool holdingControls; // true while the wreck has taken the controls away from the driver

    public UnityEvent<float> Damaged { get { return onDamaged; } }
    public event Action<float> Impacted;
    public UnityEvent StateChanged { get { return onStateChanged; } }
    public UnityEvent Wrecked { get { return onWrecked; } }

    public DamageState State { get; private set; } = DamageState.Intact;
    public float Health { get { return health; } }
    public float MaxHealth { get { return maxHealth; } }
    public float Health01 { get { return maxHealth <= 0f ? 0f : Mathf.Clamp01(health / maxHealth); } }
    public float ScrapeIntensity => Time.time - lastScrapeTime < ScrapeHoldSeconds ? scrapeIntensity : 0f;

    private void Awake()
    {
        car = GetComponent<BasicCarController>();
        body = GetComponent<Rigidbody>();
        if (car != null)
            fullMotorPower = car.motorPower;
        health = maxHealth;
        ApplyState();
    }

    private void OnDestroy()
    {
        // Leave the controller as it was found.
        if (car != null)
            car.motorPower = fullMotorPower;
    }

    private void OnDisable()
    {
        recentImpactSounds.Clear();
        lastScrapeTime = float.NegativeInfinity;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.contactCount == 0)
            return;
        PlayContactImpact(collision);
        ContactPoint contact = collision.GetContact(0);
        // Landing on the road or bottoming out is not a crash.
        if (contact.normal.y > .7f)
            return;
        // Only the speed straight into the obstacle counts, so sliding along a rail is cheap.
        float impactSpeed = Mathf.Abs(Vector3.Dot(collision.relativeVelocity, contact.normal));
        ApplyImpactDamage(impactSpeed);
    }

    private void PlayContactImpact(Collision collision)
    {
        Collider obstacle = collision.collider;
        if (!isActiveAndEnabled || IsRoadSurface(obstacle))
            return;

        float severity = 0f;
        Rigidbody otherBody = collision.rigidbody;
        bool bodyOrRail = HasSolidContactOnAllSides(obstacle, otherBody);
        Vector3 velocity = collision.relativeVelocity;
        // A slow sideswipe still touches a hard surface, even at a shallow angle.
        if (bodyOrRail && velocity.sqrMagnitude >= AudibleImpactSpeed * AudibleImpactSpeed)
            severity = AudibleImpactSpeed;

        for (int i = 0; i < collision.contactCount; i++)
        {
            ContactPoint contact = collision.GetContact(i);
            if (contact.normal.y > .7f && !bodyOrRail)
                continue;

            float intoSurface = Mathf.Abs(Vector3.Dot(velocity, contact.normal));
            float alongSurface = Vector3.ProjectOnPlane(velocity, contact.normal).magnitude;
            float glance = Mathf.Min(4f, alongSurface * .3f);
            severity = Mathf.Max(severity, Mathf.Max(intoSurface, glance));
        }

        if (severity < AudibleImpactSpeed)
            return;

        UnityEngine.Object obstacleBody = otherBody != null ? otherBody : obstacle;
        for (int i = recentImpactSounds.Count - 1; i >= 0; i--)
        {
            var previous = recentImpactSounds[i];
            if (Time.time - previous.time >= ImpactSoundCooldown)
                recentImpactSounds.RemoveAt(i);
            else if (previous.obstacle == obstacleBody)
                return;
        }

        recentImpactSounds.Add((obstacleBody, Time.time));
        Impacted?.Invoke(severity);
    }

    private void OnCollisionStay(Collision collision)
    {
        Collider obstacle = collision.collider;
        if (!isActiveAndEnabled || IsRoadSurface(obstacle))
            return;

        Rigidbody otherBody = collision.rigidbody;
        bool bodyOrRail = HasSolidContactOnAllSides(obstacle, otherBody);
        for (int i = 0; i < collision.contactCount; i++)
        {
            ContactPoint contact = collision.GetContact(i);
            if (contact.normal.y > .7f && !bodyOrRail)
                continue;

            Vector3 relativeVelocity = body.GetPointVelocity(contact.point);
            if (otherBody != null)
                relativeVelocity -= otherBody.GetPointVelocity(contact.point);
            float alongSurface = Vector3.ProjectOnPlane(relativeVelocity, contact.normal).magnitude;
            float intensity = Mathf.InverseLerp(1f, 12f, alongSurface);
            if (intensity <= 0f)
                continue;

            if (lastScrapeTime != Time.fixedTime)
                scrapeIntensity = 0f;
            lastScrapeTime = Time.fixedTime;
            scrapeIntensity = Mathf.Max(scrapeIntensity, intensity);
        }
    }

    private static bool HasSolidContactOnAllSides(Collider obstacle, Rigidbody otherBody)
    {
        // Vehicle bodies and rails also make contact at their top edges.
        return obstacle.name.StartsWith("Guardrails", StringComparison.Ordinal) ||
               (otherBody != null && otherBody.TryGetComponent<TrafficCar>(out _));
    }

    private static bool IsRoadSurface(Collider obstacle)
    {
        // These are the support meshes baked by MountainDeliveryBuilder.
        string surface = obstacle.name;
        return surface.StartsWith("Drivable road", StringComparison.Ordinal) ||
               surface.StartsWith("Asphalt at the road ends", StringComparison.Ordinal) ||
               surface.StartsWith("Gravel shoulder", StringComparison.Ordinal) ||
               surface.StartsWith("Dirt turnouts", StringComparison.Ordinal) ||
               surface.StartsWith("Terrain collision", StringComparison.Ordinal);
    }

    // A hit at the given speed into an obstacle (m/s). Returns the health it took.
    public float Impact(float impactSpeed)
    {
        return ApplyImpactDamage(impactSpeed, true);
    }

    private float ApplyImpactDamage(float impactSpeed, bool playSound = false)
    {
        if (State == DamageState.Wrecked || Time.time - lastHitTime < hitCooldown)
            return 0f;
        if (impactSpeed < minImpactSpeed)
        {
            if (playSound)
                Impacted?.Invoke(impactSpeed);
            return 0f;
        }

        lastHitTime = Time.time;
        if (playSound)
            Impacted?.Invoke(impactSpeed);
        float amount = (impactSpeed - minImpactSpeed) * damagePerSpeed;
        return TakeDamage(amount);
    }

    public float TakeDamage(float amount)
    {
        if (amount <= 0f || State == DamageState.Wrecked)
            return 0f;
        amount = Mathf.Min(amount, health);
        health -= amount;
        onDamaged.Invoke(amount);
        ChangeState(StateFor(health));
        return amount;
    }

    // Back to full health (after the wreck is towed to the start, or a repair).
    public void Repair()
    {
        health = maxHealth;
        lastHitTime = -10f;
        recentImpactSounds.Clear();
        lastScrapeTime = float.NegativeInfinity;
        ChangeState(DamageState.Intact);
    }

    private DamageState StateFor(float value)
    {
        float share = maxHealth <= 0f ? 0f : value / maxHealth;
        if (share <= 0f) return DamageState.Wrecked;
        if (share < criticalBelow) return DamageState.Critical;
        if (share < damagedBelow) return DamageState.Damaged;
        return DamageState.Intact;
    }

    // The only place the damage state changes.
    private void ChangeState(DamageState next)
    {
        if (next == State)
            return;
        State = next;
        ApplyState();
        onStateChanged.Invoke();
        if (State == DamageState.Wrecked)
            onWrecked.Invoke();
    }

    private void ApplyState()
    {
        if (car != null)
        {
            car.motorPower = fullMotorPower * (State == DamageState.Critical ? criticalPower : 1f);
            // A wreck rolls to a stop with the handbrake on; anything else hands the controls back.
            if (State == DamageState.Wrecked)
            {
                car.Override = new BasicCarController.DriveInput(0f, 0f, 0f, 1f);
                holdingControls = true;
            }
            else if (holdingControls)
            {
                car.Override = null;
                holdingControls = false;
            }
        }
        if (smoke != null)
        {
            var emission = smoke.emission;
            emission.rateOverTime = State == DamageState.Damaged ? 6f : State == DamageState.Critical ? 16f : State == DamageState.Wrecked ? 30f : 0f;
            if (State == DamageState.Intact)
                smoke.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            else if (!smoke.isPlaying)
                smoke.Play(true);
        }
    }
}
