using UnityEngine;
using UnityEngine.Rendering;

//Visual only: scrolls the pond's ripple normal map so the water looks alive.
//The mini-map camera sees the pond from straight above, where the glossy water just glares white,
//so for that one camera the pond is drawn matte (smoothness 0).
//In winter the pond freezes: it switches to the ice material and the ripples stop.
public class WaterRipple : MonoBehaviour
{
    [SerializeField]
    private Vector2 scrollSpeed = new Vector2(0.03f, 0.02f);
    [SerializeField]
    private Material iceMaterial;//opaque, so the white snow layer cannot wash it out

    private static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");

    private Renderer lakeRenderer;
    private Camera minimapCamera;
    private MaterialPropertyBlock matte;
    private Material water;
    private GameCalendar calendar;
    private Vector2 offset;
    private Vector2 detailOffset;
    private bool frozen;

    private void Start()
    {
        lakeRenderer = GetComponent<Renderer>();
        water = lakeRenderer.material;//runtime copy, the asset is not changed
        MinimapFollow minimap = FindFirstObjectByType<MinimapFollow>();//once; not every scene has a mini-map
        if (minimap != null)
        {
            minimapCamera = minimap.GetComponent<Camera>();
            matte = new MaterialPropertyBlock();
            matte.SetFloat(SmoothnessId, 0f);
            RenderPipelineManager.beginCameraRendering += OnBeginCamera;
            RenderPipelineManager.endCameraRendering += OnEndCamera;
        }
        calendar = FindFirstObjectByType<GameCalendar>();//once; not every scene has a calendar
        if (calendar != null)
        {
            calendar.newSeasonEvent.AddListener(CheckSeason);
            CheckSeason();
        }
    }

    private void Update()
    {
        if (frozen)
            return;
        offset += scrollSpeed * Time.deltaTime;
        detailOffset -= scrollSpeed * 0.7f * Time.deltaTime;
        water.SetTextureOffset("_BaseMap", offset);//URP Lit: the normal map uses the base map tiling and offset
        water.SetTextureOffset("_DetailAlbedoMap", detailOffset);//and the detail normal map uses the detail tiling and offset
    }

    //called when a new season starts: water in the warm seasons, ice in winter
    private void CheckSeason()
    {
        bool freeze = iceMaterial != null && calendar.season == GameCalendar.Season.Winter;
        if (freeze == frozen)
            return;
        frozen = freeze;
        lakeRenderer.sharedMaterial = frozen ? iceMaterial : water;
    }

    //just before the mini-map camera draws: the pond (water or ice) is matte; every other camera keeps the glossy look
    private void OnBeginCamera(ScriptableRenderContext context, Camera cam)
    {
        if (cam == minimapCamera)
            lakeRenderer.SetPropertyBlock(matte);
    }

    private void OnEndCamera(ScriptableRenderContext context, Camera cam)
    {
        if (cam == minimapCamera)
            lakeRenderer.SetPropertyBlock(null);
    }

    private void OnDestroy()
    {
        RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
        RenderPipelineManager.endCameraRendering -= OnEndCamera;
        if (calendar != null)
            calendar.newSeasonEvent.RemoveListener(CheckSeason);
        if (water != null)
            Destroy(water);
    }
}
