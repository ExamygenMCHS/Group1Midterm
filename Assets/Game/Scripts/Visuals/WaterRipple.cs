using UnityEngine;
using UnityEngine.Rendering;

//Visual only: scrolls the pond's ripple normal map so the water looks alive.
//The mini-map camera sees the pond from straight above, where the glossy water just glares white,
//so for that one camera the pond is drawn matte (smoothness 0).
public class WaterRipple : MonoBehaviour
{
    [SerializeField]
    private Vector2 scrollSpeed = new Vector2(0.03f, 0.02f);

    private static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");

    private Renderer lakeRenderer;
    private Camera minimapCamera;
    private MaterialPropertyBlock matte;
    private Material water;
    private Vector2 offset;
    private Vector2 detailOffset;

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
    }

    private void Update()
    {
        offset += scrollSpeed * Time.deltaTime;
        detailOffset -= scrollSpeed * 0.7f * Time.deltaTime;
        water.SetTextureOffset("_BaseMap", offset);//URP Lit: the normal map uses the base map tiling and offset
        water.SetTextureOffset("_DetailAlbedoMap", detailOffset);//and the detail normal map uses the detail tiling and offset
    }

    //just before the mini-map camera draws: the pond is matte; every other camera keeps the glossy look
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
        if (water != null)
            Destroy(water);
    }
}
