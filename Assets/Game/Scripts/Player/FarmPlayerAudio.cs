using UnityEngine;

[RequireComponent(typeof(AudioSource), typeof(PlayerMove), typeof(CapsuleCollider))]
public class FarmPlayerAudio : MonoBehaviour
{
    [SerializeField] private GameCalendar calendar;
    [SerializeField] private WaterSource pond;
    [SerializeField] private AudioClip[] grassSteps;
    [SerializeField] private AudioClip[] snowSteps;
    [SerializeField] private AudioClip[] iceSteps;
    [SerializeField] private AudioClip bedIn;
    [SerializeField] private AudioClip bedOut;
    [SerializeField] private AudioClip bagOpen;
    [SerializeField] private AudioClip bagClose;
    [SerializeField] private AudioClip uiClick;
    [SerializeField, Min(0.1f)] private float stepDistance = 0.48f;

    private AudioSource source;
    private PlayerMove movement;
    private CapsuleCollider body;
    private PlayerEnergy energy;
    private FarmBag bag;
    private Vector3 previousPosition;
    private float distanceSinceStep;
    private int stepIndex;
    private float nextUiClickAt;

    private void Awake()
    {
        source = GetComponent<AudioSource>();
        movement = GetComponent<PlayerMove>();
        body = GetComponent<CapsuleCollider>();
        energy = GetComponent<PlayerEnergy>();
        bag = GetComponent<Hotbar>()?.GetBag();
    }

    private void OnEnable()
    {
        previousPosition = transform.position;
        distanceSinceStep = 0f;
        if (energy != null)
            energy.restingChangedEvent.AddListener(PlayBedSound);
        if (bag != null)
            bag.bagOpenChangedEvent.AddListener(PlayBagSound);
    }

    private void OnDisable()
    {
        if (energy != null)
            energy.restingChangedEvent.RemoveListener(PlayBedSound);
        if (bag != null)
            bag.bagOpenChangedEvent.RemoveListener(PlayBagSound);
        source.Stop();
    }

    private void FixedUpdate()
    {
        Vector3 position = movement.rb.position;
        Vector3 delta = position - previousPosition;
        previousPosition = position;
        delta.y = 0f;
        float distance = delta.magnitude;
        if (!movement.isActiveAndEnabled || movement.GetPlanarVelocity().sqrMagnitude < 0.01f ||
            (energy != null && energy.IsResting()) || distance < 0.001f || distance > stepDistance * 2f)
        {
            distanceSinceStep = 0f;
            return;
        }

        distanceSinceStep += distance;
        if (distanceSinceStep < stepDistance)
            return;
        distanceSinceStep -= stepDistance;
        //The ray starts inside the capsule so it ignores the player's body.
        Bounds bounds = body.bounds;
        if (Physics.Raycast(bounds.center, Vector3.down, bounds.extents.y + 0.15f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            PlayStep(position);
    }

    private void PlayStep(Vector3 position)
    {
        AudioClip[] steps = grassSteps;
        if (calendar != null && calendar.IsWinter())
            steps = pond != null && pond.IsOverWater(position) ? iceSteps : snowSteps;
        if (steps != null && steps.Length > 0)
            Play(steps[stepIndex++ % steps.Length]);
    }

    public void PlayUiClick()
    {
        if (Time.unscaledTime < nextUiClickAt)
            return;
        nextUiClickAt = Time.unscaledTime + 0.08f;
        Play(uiClick);
    }

    private void PlayBedSound() => Play(energy.IsResting() ? bedIn : bedOut);
    private void PlayBagSound() => Play(bag.IsBagOpen() ? bagOpen : bagClose);

    private void Play(AudioClip clip)
    {
        if (isActiveAndEnabled && clip != null)
            source.PlayOneShot(clip);
    }
}
