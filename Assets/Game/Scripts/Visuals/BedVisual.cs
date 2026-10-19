using UnityEngine;

//Visual only: the bed in the sleep area. Floating "Z"s while the player rests,
//a softly flickering bedside lantern, and the spot the farmer lies down on.
public class BedVisual : MonoBehaviour
{
    [SerializeField]
    private Transform sleepAnchor;//where the farmer model lies: forward = up to the sky, up = toward the headboard
    [SerializeField]
    private ParticleSystem zzz;
    [SerializeField]
    private Light lanternLight;
    [SerializeField]
    private float lanternFlicker = 0.12f;

    private PlayerEnergy playerEnergy;
    private float lanternBase;

    public Transform SleepAnchor { get { return sleepAnchor; } }

    private void Start()
    {
        playerEnergy = FindAnyObjectByType<PlayerEnergy>();
        if (lanternLight != null)
            lanternBase = lanternLight.intensity;
    }

    private void Update()
    {
        bool resting = playerEnergy != null && playerEnergy.IsResting();
        if (zzz != null)
        {
            if (resting && !zzz.isEmitting)
                zzz.Play();
            else if (!resting && zzz.isEmitting)
                zzz.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
        if (lanternLight != null)
        {
            float t = Time.time;
            lanternLight.intensity = lanternBase * (1f + lanternFlicker * (Mathf.PerlinNoise(t * 3f, 0.3f) - 0.5f) * 2f);
        }
    }
}
