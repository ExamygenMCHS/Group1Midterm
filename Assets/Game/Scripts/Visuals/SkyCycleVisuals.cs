using UnityEngine;

//Visual only: follows the sun that DayNightControl rotates and colours the world to match -
//warm light at sunrise and sunset, a cool blue night, matching ambient light and fog.
//(DayNightControl's own comment suggests exactly this: "Adjust other elements, like skybox, light source intensity".)
public class SkyCycleVisuals : MonoBehaviour
{
    [SerializeField]
    private Light sun;
    [SerializeField]
    private Light moon;//the MoonLight that keeps the tiles readable at night

    [Header("Sun")]
    [SerializeField]
    private float sunIntensity = 1.6f;
    [SerializeField]
    private Color sunDayColor = new Color(1f, 0.95f, 0.84f);
    [SerializeField]
    private Color sunLowColor = new Color(1f, 0.6f, 0.35f);

    [Header("Moon")]
    [SerializeField]
    private float moonDayIntensity = 0.15f;
    [SerializeField]
    private float moonNightIntensity = 0.7f;

    [Header("Ambient light (sky / horizon / ground)")]
    [SerializeField]
    private Color daySky = new Color(0.58f, 0.7f, 0.86f);
    [SerializeField]
    private Color dayEquator = new Color(0.55f, 0.6f, 0.52f);
    [SerializeField]
    private Color dayGround = new Color(0.32f, 0.3f, 0.24f);
    [SerializeField]
    private Color duskSky = new Color(0.62f, 0.48f, 0.5f);
    [SerializeField]
    private Color duskEquator = new Color(0.7f, 0.45f, 0.32f);
    [SerializeField]
    private Color nightSky = new Color(0.24f, 0.29f, 0.48f);
    [SerializeField]
    private Color nightEquator = new Color(0.17f, 0.2f, 0.33f);
    [SerializeField]
    private Color nightGround = new Color(0.1f, 0.1f, 0.14f);

    [Header("Fog")]
    [SerializeField]
    private Color dayFog = new Color(0.72f, 0.84f, 0.93f);
    [SerializeField]
    private Color duskFog = new Color(0.93f, 0.68f, 0.52f);
    [SerializeField]
    private Color nightFog = new Color(0.14f, 0.18f, 0.32f);

    //SeasonVisuals tints the ambient light and fog per season (cold in winter, warm in fall)
    [HideInInspector]
    public Color seasonTint = Color.white;

    private void LateUpdate()
    {
        if (sun == null)
            return;
        //1 at noon, 0 on the horizon, -1 at midnight
        float elevation = -sun.transform.forward.y;
        float day = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.2f, 0.3f, elevation));
        float dusk = Mathf.Clamp01(1f - Mathf.Abs(elevation - 0.03f) / 0.3f);

        sun.intensity = sunIntensity * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.04f, 0.2f, elevation));
        sun.color = Color.Lerp(sunLowColor, sunDayColor, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.02f, 0.45f, elevation)));
        if (moon != null)
            moon.intensity = Mathf.Lerp(moonNightIntensity, moonDayIntensity, day);

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = Color.Lerp(Color.Lerp(nightSky, daySky, day), duskSky, dusk * 0.6f) * seasonTint;
        RenderSettings.ambientEquatorColor = Color.Lerp(Color.Lerp(nightEquator, dayEquator, day), duskEquator, dusk * 0.6f) * seasonTint;
        RenderSettings.ambientGroundColor = Color.Lerp(nightGround, dayGround, day) * seasonTint;
        RenderSettings.fogColor = Color.Lerp(Color.Lerp(nightFog, dayFog, day), duskFog, dusk * 0.7f) * seasonTint;
    }
}
