using UnityEngine;

//the sounds of fishing: it listens to the FishingControl's events and plays one sound for each, and runs the reel's
//clicking loop while the player reels in a fish (faster and higher the harder the line strains).
//Sound only, never changes gameplay. Every clip may be left empty; it is then skipped.
public class FishingAudio : MonoBehaviour
{
    [SerializeField]
    private FishingControl fishing;
    [SerializeField]
    private AudioSource oneShots;
    [SerializeField]
    private AudioSource reelLoop;//looping; only heard while the player reels in a fish

    [Header("Clips")]
    [SerializeField]
    private AudioClip castSound;
    [SerializeField]
    private AudioClip landSound;//the bobber hits the water
    [SerializeField]
    private AudioClip nibbleSound;
    [SerializeField]
    private AudioClip biteSound;
    [SerializeField]
    private AudioClip hookSound;
    [SerializeField]
    private AudioClip surgeSound;//the fish splashes before it pulls
    [SerializeField]
    private AudioClip snapSound;
    [SerializeField]
    private AudioClip reelInSound;//an empty line coming back
    [SerializeField]
    private AudioClip catchSound;
    [SerializeField]
    private AudioClip trophySound;//played over the catch sound for a trophy
    [SerializeField]
    private AudioClip baitSound;//another bait on the line
    [SerializeField]
    private AudioClip refusedSound;//optional: a click on a spot that cannot be cast to

    [Header("Volume")]
    [SerializeField]
    private float oneShotVolume = 0.9f;
    [SerializeField]
    private float reelVolume = 0.55f;
    [SerializeField]
    private Vector2 reelPitch = new Vector2(0.9f, 1.5f);//slack line, about to snap
    [SerializeField]
    private float reelSmoothing = 12f;

    private float reelLevel = 0f;
    private float reelPitchNow = 1f;

    private void Start()
    {
        if (fishing == null)
            return;
        fishing.castEvent.AddListener(OnCast);
        fishing.bobberLandedEvent.AddListener(OnLanded);
        fishing.nibbleEvent.AddListener(OnNibble);
        fishing.biteEvent.AddListener(OnBite);
        fishing.hookedEvent.AddListener(OnHooked);
        fishing.surgeWarningEvent.AddListener(OnSurgeWarning);
        fishing.lineSnappedEvent.AddListener(OnSnapped);
        fishing.fishCaughtEvent.AddListener(OnCaught);
        fishing.baitChangedEvent.AddListener(OnBaitChanged);
        fishing.castRefusedEvent.AddListener(OnRefused);
        fishing.fishingStateChangedEvent.AddListener(OnStateChanged);
        if (reelLoop != null)
            reelLoop.volume = 0f;
    }

    private void OnDestroy()
    {
        if (fishing == null)
            return;
        fishing.castEvent.RemoveListener(OnCast);
        fishing.bobberLandedEvent.RemoveListener(OnLanded);
        fishing.nibbleEvent.RemoveListener(OnNibble);
        fishing.biteEvent.RemoveListener(OnBite);
        fishing.hookedEvent.RemoveListener(OnHooked);
        fishing.surgeWarningEvent.RemoveListener(OnSurgeWarning);
        fishing.lineSnappedEvent.RemoveListener(OnSnapped);
        fishing.fishCaughtEvent.RemoveListener(OnCaught);
        fishing.baitChangedEvent.RemoveListener(OnBaitChanged);
        fishing.castRefusedEvent.RemoveListener(OnRefused);
        fishing.fishingStateChangedEvent.RemoveListener(OnStateChanged);
    }

    private void OnCast() { Play(castSound, false); }
    private void OnLanded() { Play(landSound, true); }
    private void OnNibble() { Play(nibbleSound, true); }
    private void OnBite() { Play(biteSound, true); }
    private void OnHooked() { Play(hookSound, false); }
    private void OnSurgeWarning() { Play(surgeSound, true); }
    private void OnSnapped() { Play(snapSound, false); }
    private void OnBaitChanged() { Play(baitSound, false); }
    private void OnRefused() { Play(refusedSound, false); }

    private void OnCaught(FishingControl.Catch caught)
    {
        Play(catchSound, true);
        if (caught.trophy)
            Play(trophySound, false);
    }

    //an empty line starts coming back
    private void OnStateChanged()
    {
        if (fishing.GetFishingState() == FishingControl.FishingState.Landing
            && fishing.GetLandingKind() == FishingControl.LandingKind.Empty)
            Play(reelInSound, false);
    }

    //water sounds vary a little, so the same splash never sounds the same twice in a row
    private void Play(AudioClip clip, bool waterSound)
    {
        if (clip == null || oneShots == null)
            return;
        oneShots.pitch = waterSound ? Random.Range(0.95f, 1.05f) : 1f;
        oneShots.PlayOneShot(clip, oneShotVolume);
    }

    private void Update()
    {
        if (reelLoop == null || fishing == null)
            return;
        bool reeling = fishing.IsReeling();
        float k = 1f - Mathf.Exp(-reelSmoothing * Time.deltaTime);
        reelLevel = Mathf.Lerp(reelLevel, reeling ? 1f : 0f, k);
        reelPitchNow = Mathf.Lerp(reelPitchNow, Mathf.Lerp(reelPitch.x, reelPitch.y, fishing.GetTension()), k);
        if (reelLevel < 0.01f)
        {
            if (reelLoop.isPlaying)
                reelLoop.Stop();
            reelLoop.volume = 0f;
            return;
        }
        if (!reelLoop.isPlaying && reelLoop.clip != null)
            reelLoop.Play();
        reelLoop.volume = reelLevel * reelVolume;
        reelLoop.pitch = reelPitchNow;
    }
}
