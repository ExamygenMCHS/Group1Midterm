using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BasicCarController), typeof(RoadFeel), typeof(TruckDamage))]
public class TruckDriveAudio : MonoBehaviour
{
    [SerializeField] private AudioClip engineIdle;
    [SerializeField] private AudioClip engineLoad;
    [SerializeField] private AudioClip brakeFriction;
    [SerializeField] private AudioClip brakeSkid;
    [SerializeField] private AudioClip collisionScrape;
    [SerializeField] private AudioClip collisionBump;
    [SerializeField] private AudioClip collisionCrash;
    [SerializeField] private AudioClip checkpoint;

    private BasicCarController car;
    private RoadFeel road;
    private TruckDamage damage;
    private AudioSource idleSource;
    private AudioSource loadSource;
    private AudioSource brakeSource;
    private AudioSource skidSource;
    private AudioSource scrapeSource;
    private AudioSource effectsSource;
    private float previousSpeed;
    private float deceleration;
    private float brake;
    private float load;
    private float checkpointDuckUntil;
    private float shiftUntil;
    private int gear;

    private void Awake()
    {
        car = GetComponent<BasicCarController>();
        road = GetComponent<RoadFeel>();
        damage = GetComponent<TruckDamage>();

        idleSource = CreateSource("Engine Idle", engineIdle);
        loadSource = CreateSource("Engine Load", engineLoad);
        brakeSource = CreateSource("Brake Rub", brakeFriction);
        skidSource = CreateSource("Tyre Skid", brakeSkid);
        scrapeSource = CreateSource("Collision Scrape", collisionScrape);
        effectsSource = CreateSource("Truck Effects");
    }

    private AudioSource CreateSource(string sourceName, AudioClip loop = null)
    {
        var node = new GameObject(sourceName);
        node.transform.SetParent(transform, false);
        var source = node.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.clip = loop;
        source.loop = loop != null;
        source.volume = source.loop ? 0f : 1f;
        return source;
    }

    private void OnEnable()
    {
        previousSpeed = Mathf.Abs(car.ForwardSpeedKmh) / 3.6f;
        deceleration = brake = load = 0f;
        checkpointDuckUntil = shiftUntil = 0f;
        gear = 0;
        damage.Impacted += PlayImpact;
        foreach (var source in new[] { idleSource, loadSource, brakeSource, skidSource, scrapeSource })
        {
            source.volume = 0f;
            if (source.clip != null)
                source.Play();
        }
    }

    private void OnDisable()
    {
        damage.Impacted -= PlayImpact;
        idleSource.Stop();
        loadSource.Stop();
        brakeSource.Stop();
        skidSource.Stop();
        scrapeSource.Stop();
        effectsSource.Stop();
    }

    private void FixedUpdate()
    {
        float speed = Mathf.Abs(car.ForwardSpeedKmh) / 3.6f;
        float slowing = Mathf.Clamp((previousSpeed - speed) / Time.fixedDeltaTime, 0f, 12f);
        deceleration = Smooth(deceleration, slowing, .08f, Time.fixedDeltaTime);
        previousSpeed = speed;
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        bool driving = car.isActiveAndEnabled && damage.State != TruckDamage.DamageState.Wrecked;
        bool paused = Time.timeScale <= 0f;
        float speedKmh = Mathf.Abs(car.ForwardSpeedKmh);
        float duck = paused ? 0f : Time.unscaledTime < checkpointDuckUntil ? .4f : 1f;

        UpdateTyres(speedKmh, car.isActiveAndEnabled && !paused, duck, dt);
        float brakeDuck = Mathf.Lerp(1f, .52f, brake * road.Grounded * Mathf.InverseLerp(2f, 22f, speedKmh));
        UpdateEngine(speedKmh, driving && !paused, duck * brakeDuck, dt);
        float scrape = damage.ScrapeIntensity;
        SetLoop(scrapeSource, .48f * scrape * duck, Mathf.Lerp(.88f, 1.12f, scrape), dt);
    }

    private void UpdateEngine(float speedKmh, bool running, float duck, float dt)
    {
        float drive = running ? Mathf.Clamp01(car.DriveDemand) : 0f;
        float speed = Mathf.InverseLerp(0f, car.maxSpeed, speedKmh);
        float rev;
        if (car.ForwardSpeedKmh < -.72f)
        {
            gear = 0;
            shiftUntil = 0f;
            rev = Mathf.InverseLerp(0f, car.maxReverseSpeed, speedKmh);
        }
        else
        {
            // A small speed gap between downshifts and upshifts keeps the pitch stable near each shift.
            if (gear < 3 && speed > (gear + 1) * .25f + .025f)
            {
                gear++;
                shiftUntil = Time.unscaledTime + .13f;
            }
            else if (gear > 0 && speed < gear * .25f - .025f)
            {
                gear--;
            }
            rev = Mathf.InverseLerp(gear * .25f - .035f, (gear + 1) * .25f + .035f, speed);
        }

        float shift = Time.unscaledTime < shiftUntil ? .55f : 1f;
        float engine = running ? duck : 0f;
        load = Smooth(load, drive * shift, .13f, dt);
        float rpm = Mathf.Clamp01(rev * .76f + load * .24f);
        SetLoop(idleSource, (.22f - .12f * load) * engine, Mathf.Lerp(.92f, 1.22f, rpm), dt);
        SetLoop(loadSource, (.23f * load + .075f * speed) * engine, Mathf.Lerp(.76f, 1.35f, rpm), dt);
    }

    private void UpdateTyres(float speedKmh, bool active, float duck, float dt)
    {
        float demand = active ? Mathf.Clamp01(car.BrakeDemand) : 0f;
        brake = Smooth(brake, demand, demand > brake ? .025f : .065f, dt);
        float grip = road.Grounded;
        float speed = Mathf.InverseLerp(1.5f, 52f, speedKmh);
        float slowing = Mathf.InverseLerp(1f, 8f, deceleration);
        float rub = brake * Mathf.Sqrt(speed) * Mathf.Lerp(.72f, 1f, slowing);
        SetLoop(brakeSource, .58f * rub * grip * duck, Mathf.Lerp(.83f, 1.13f, speed), dt);

        // A loose driven wheel can spin without a skid sound. Braking and sideways slip are audible.
        float brakeSlip = brake * Mathf.InverseLerp(.65f, 1.5f, road.Slip);
        float cornerSlip = Mathf.InverseLerp(1.15f, 2f, road.SidewaysSlip);
        float skid = Mathf.Max(brakeSlip, cornerSlip) * Mathf.InverseLerp(12f, 40f, speedKmh) * grip * (1f - road.Roughness);
        SetLoop(skidSource, active ? .48f * skid * duck : 0f, Mathf.Lerp(.86f, 1.12f, speed), dt);
    }

    private static void SetLoop(AudioSource source, float volume, float pitch, float dt)
    {
        source.volume = Smooth(source.volume, volume, .052f, dt);
        source.pitch = Smooth(source.pitch, pitch, .08f, dt);
    }

    private static float Smooth(float current, float target, float seconds, float dt)
    {
        return Mathf.Lerp(current, target, 1f - Mathf.Exp(-dt / seconds));
    }

    private void PlayImpact(float speed)
    {
        if (speed < .5f)
            return;

        float severity = Mathf.InverseLerp(.5f, 19f, speed);
        AudioClip clip = speed >= 8.5f ? collisionCrash : collisionBump;
        if (clip != null)
            effectsSource.PlayOneShot(clip, Mathf.Lerp(.34f, .82f, severity));
    }

    public void PlayCheckpoint()
    {
        if (!isActiveAndEnabled || checkpoint == null)
            return;

        checkpointDuckUntil = Time.unscaledTime + checkpoint.length;
        effectsSource.PlayOneShot(checkpoint, .78f);
    }
}
