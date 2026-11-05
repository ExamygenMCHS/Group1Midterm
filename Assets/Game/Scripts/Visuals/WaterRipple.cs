using UnityEngine;
using UnityEngine.Rendering;

//Scrolls the pond's ripple normal map and switches its visuals and access by season.
//The mini-map shows the pond as a flat, matte light-blue shape: a copy of the pond mesh with an unlit material on the
//Minimap layer, which only the mini-map camera draws (the glossy water glares white from straight above).
//In winter the pond freezes: it switches to the ice material and the ripples stop.
public class WaterRipple : MonoBehaviour
{
    [SerializeField]
    private Vector2 scrollSpeed = new Vector2(0.03f, 0.02f);
    [SerializeField]
    private Material iceMaterial;//opaque, so the white snow layer cannot wash it out
    [SerializeField]
    private GameObject fishingHole;//a visual marker on the ice, without collision
    [SerializeField]
    private GameObject[] lilyPads;
    [SerializeField]
    private Collider pondBarrier;
    [SerializeField]
    private Material minimapMaterial;//unlit light blue: the pond on the mini-map
    [SerializeField]
    private Color minimapIceColor = new Color(0.85f, 0.93f, 1f);//the frozen pond on the mini-map

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    private Renderer lakeRenderer;
    private Material minimapPond;//runtime copy on the mini-map shape
    private Color minimapWaterColor;
    private Material water;
    private GameCalendar calendar;
    private Vector2 offset;
    private Vector2 detailOffset;
    private bool frozen;

    private void Start()
    {
        lakeRenderer = GetComponent<Renderer>();
        water = lakeRenderer.material;//runtime copy, the asset is not changed
        if (minimapMaterial != null && FindFirstObjectByType<MinimapFollow>() != null)//once; not every scene has a mini-map
            MakeMinimapPond();
        calendar = FindFirstObjectByType<GameCalendar>();//once; not every scene has a calendar
        if (fishingHole != null)
            fishingHole.SetActive(false);
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
        bool winter = calendar.season == GameCalendar.Season.Winter;
        if (pondBarrier != null)
            pondBarrier.enabled = !winter;
        if (lilyPads != null)
            foreach (GameObject pad in lilyPads)
                if (pad != null)
                    pad.SetActive(calendar.season == GameCalendar.Season.Spring || calendar.season == GameCalendar.Season.Summer);
        if (fishingHole != null)
            fishingHole.SetActive(freeze);
        if (freeze == frozen)
            return;
        frozen = freeze;
        lakeRenderer.sharedMaterial = frozen ? iceMaterial : water;
        if (minimapPond != null)
            minimapPond.SetColor(BaseColorId, frozen ? minimapIceColor : minimapWaterColor);
    }

    //a flat copy of the pond just above the water, on the Minimap layer (the main camera does not draw that layer)
    private void MakeMinimapPond()
    {
        GameObject shape = new GameObject("MinimapPond");
        shape.layer = LayerMask.NameToLayer("Minimap");
        shape.transform.SetParent(transform, false);
        shape.transform.position += Vector3.up * 0.05f;
        shape.AddComponent<MeshFilter>().sharedMesh = GetComponent<MeshFilter>().sharedMesh;
        MeshRenderer shapeRenderer = shape.AddComponent<MeshRenderer>();
        shapeRenderer.shadowCastingMode = ShadowCastingMode.Off;
        shapeRenderer.receiveShadows = false;
        minimapPond = new Material(minimapMaterial);
        shapeRenderer.sharedMaterial = minimapPond;
        minimapWaterColor = minimapPond.GetColor(BaseColorId);
    }

    private void OnDestroy()
    {
        if (calendar != null)
            calendar.newSeasonEvent.RemoveListener(CheckSeason);
        if (water != null)
            Destroy(water);
        if (minimapPond != null)
            Destroy(minimapPond);
    }
}
