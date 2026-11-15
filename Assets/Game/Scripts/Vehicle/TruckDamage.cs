using UnityEngine;
using UnityEngine.Events;

// Collision damage for the delivery truck. A hit takes health by how hard the truck ran into something (scraping along
// a guardrail costs almost nothing, a head-on crash costs a lot). The health decides the damage state: a badly
// damaged truck loses engine power and smokes, a wrecked one stops. Other scripts react through the events.
[RequireComponent(typeof(Rigidbody))]
public class TruckDamage : MonoBehaviour
{
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
    [SerializeField, Range(0f, 1f)] private float impactVolume = .8f;

    [Header("Events")]
    [SerializeField] private UnityEvent<float> onDamaged = new UnityEvent<float>(); // health lost by this hit
    [SerializeField] private UnityEvent onStateChanged = new UnityEvent();
    [SerializeField] private UnityEvent onWrecked = new UnityEvent();

    private BasicCarController car;
    private AudioSource impactAudio;
    private AudioClip impactClip;
    private float health;
    private float fullMotorPower;
    private float lastHitTime = -10f;
    private bool holdingControls; // true while the wreck has taken the controls away from the driver

    public UnityEvent<float> Damaged { get { return onDamaged; } }
    public UnityEvent StateChanged { get { return onStateChanged; } }
    public UnityEvent Wrecked { get { return onWrecked; } }

    public DamageState State { get; private set; } = DamageState.Intact;
    public float Health { get { return health; } }
    public float MaxHealth { get { return maxHealth; } }
    public float Health01 { get { return maxHealth <= 0f ? 0f : Mathf.Clamp01(health / maxHealth); } }

    private void Awake()
    {
        car = GetComponent<BasicCarController>();
        if (car != null)
            fullMotorPower = car.motorPower;
        health = maxHealth;
        // The truck's own AudioSource belongs to the road noise (RoadFeel overwrites its samples), so impacts get their own.
        var audioObject = new GameObject("Impact Audio");
        audioObject.transform.SetParent(transform, false);
        impactAudio = audioObject.AddComponent<AudioSource>();
        impactAudio.playOnAwake = false;
        impactAudio.spatialBlend = 0f;
        impactClip = MakeImpactClip();
        ApplyState();
    }

    private void OnDestroy()
    {
        if (impactClip != null)
            Destroy(impactClip);
        // Leave the controller as it was found.
        if (car != null)
            car.motorPower = fullMotorPower;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.contactCount == 0)
            return;
        ContactPoint contact = collision.GetContact(0);
        // Landing on the road or bottoming out is not a crash.
        if (contact.normal.y > .7f)
            return;
        // Only the speed straight into the obstacle counts, so sliding along a rail is cheap.
        float impactSpeed = Mathf.Abs(Vector3.Dot(collision.relativeVelocity, contact.normal));
        Impact(impactSpeed);
    }

    // A hit at the given speed into an obstacle (m/s). Returns the health it took.
    public float Impact(float impactSpeed)
    {
        if (State == DamageState.Wrecked || impactSpeed < minImpactSpeed || Time.time - lastHitTime < hitCooldown)
            return 0f;
        lastHitTime = Time.time;
        float amount = (impactSpeed - minImpactSpeed) * damagePerSpeed;
        if (impactAudio != null && impactClip != null)
            impactAudio.PlayOneShot(impactClip, impactVolume * Mathf.Clamp01(impactSpeed / 15f + .25f));
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

    // A short thud: noise with a fast decay over a low thump. Generated, so no audio asset is needed.
    private static AudioClip MakeImpactClip()
    {
        int rate = AudioSettings.outputSampleRate > 0 ? AudioSettings.outputSampleRate : 44100;
        int count = rate * 2 / 5;
        var samples = new float[count];
        uint rng = 2463534242u;
        float low = 0f;
        for (int i = 0; i < count; i++)
        {
            float t = i / (float)rate;
            rng ^= rng << 13; rng ^= rng >> 17; rng ^= rng << 5;
            float white = (rng & 0xFFFFFF) / (float)0x7FFFFF - 1f;
            low += .18f * (white - low);
            float thump = Mathf.Sin(2f * Mathf.PI * 70f * t) * Mathf.Exp(-t * 14f);
            samples[i] = Mathf.Clamp(low * 2.2f * Mathf.Exp(-t * 22f) + thump * .8f, -1f, 1f);
        }
        var clip = AudioClip.Create("Truck impact", count, 1, rate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}
