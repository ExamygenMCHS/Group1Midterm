using UnityEngine;

//Visual only: shows the season on the farm. It listens to the calendar's new season event:
//in winter snow falls and settles on the meadow and the light turns cold, in fall the meadow turns golden.
public class SeasonVisuals : MonoBehaviour
{
    [SerializeField]
    private GameCalendar calendar;
    [SerializeField]
    private Transform followTarget;//the snow falls around the player
    [SerializeField]
    private ParticleSystem snowfall;
    [SerializeField]
    private Renderer snowCover;//a white layer over the meadow, faded in during winter
    [SerializeField]
    private Renderer ground;
    [SerializeField]
    private SkyCycleVisuals sky;

    [Header("Meadow tint per season")]
    [SerializeField]
    private Color springGround = Color.white;
    [SerializeField]
    private Color summerGround = new Color(1f, 0.97f, 0.86f);
    [SerializeField]
    private Color fallGround = new Color(1f, 0.8f, 0.52f);
    [SerializeField]
    private Color winterGround = new Color(0.82f, 0.86f, 0.92f);

    [Header("Light tint per season (ambient light and fog)")]
    [SerializeField]
    private Color fallLight = new Color(1f, 0.9f, 0.8f);
    [SerializeField]
    private Color winterLight = new Color(0.85f, 0.92f, 1.08f);

    [SerializeField]
    private float blendSeconds = 2.5f;
    [SerializeField]
    private float snowCoverAlpha = 0.82f;

    private MaterialPropertyBlock block;
    private Color groundFrom, groundTo, groundNow;
    private Color lightFrom, lightTo, lightNow;
    private float coverFrom, coverTo, coverNow;
    private float blend = 1f;

    private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

    private void Start()
    {
        block = new MaterialPropertyBlock();
        calendar.newSeasonEvent.AddListener(OnNewSeason);
        SetTargets(calendar.season);
        groundFrom = groundTo;//the level starts in its season without a fade
        lightFrom = lightTo;
        coverFrom = coverTo;
        blend = 1f;
        Apply(1f);
    }

    private void OnDestroy()
    {
        if (calendar != null)
            calendar.newSeasonEvent.RemoveListener(OnNewSeason);
        if (sky != null)
            sky.seasonTint = Color.white;
    }

    //called by the calendar when a new season starts
    public void OnNewSeason()
    {
        groundFrom = groundNow;
        lightFrom = lightNow;
        coverFrom = coverNow;
        SetTargets(calendar.season);
        blend = 0f;
    }

    private void SetTargets(GameCalendar.Season season)
    {
        switch (season)
        {
            case GameCalendar.Season.Spring:
                groundTo = springGround;
                lightTo = Color.white;
                coverTo = 0f;
                break;
            case GameCalendar.Season.Summer:
                groundTo = summerGround;
                lightTo = Color.white;
                coverTo = 0f;
                break;
            case GameCalendar.Season.Fall:
                groundTo = fallGround;
                lightTo = fallLight;
                coverTo = 0f;
                break;
            case GameCalendar.Season.Winter:
                groundTo = winterGround;
                lightTo = winterLight;
                coverTo = snowCoverAlpha;
                break;
        }
        if (snowfall != null)
        {
            if (season == GameCalendar.Season.Winter)
                snowfall.Play();
            else
                snowfall.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    private void LateUpdate()
    {
        if (followTarget != null && snowfall != null)
        {
            Vector3 p = followTarget.position;
            snowfall.transform.position = new Vector3(p.x, snowfall.transform.position.y, p.z + 2f);
        }
        if (blend < 1f)
        {
            blend = Mathf.Min(1f, blend + Time.deltaTime / Mathf.Max(0.01f, blendSeconds));
            Apply(Mathf.SmoothStep(0f, 1f, blend));
        }
    }

    private void Apply(float k)
    {
        groundNow = Color.Lerp(groundFrom, groundTo, k);
        lightNow = Color.Lerp(lightFrom, lightTo, k);
        coverNow = Mathf.Lerp(coverFrom, coverTo, k);
        if (ground != null)
        {
            ground.GetPropertyBlock(block);
            block.SetColor(BaseColor, groundNow);
            ground.SetPropertyBlock(block);
        }
        if (snowCover != null)
        {
            snowCover.enabled = coverNow > 0.01f;
            snowCover.GetPropertyBlock(block);
            block.SetColor(BaseColor, new Color(1f, 1f, 1f, coverNow));
            snowCover.SetPropertyBlock(block);
        }
        if (sky != null)
            sky.seasonTint = lightNow;
    }
}
