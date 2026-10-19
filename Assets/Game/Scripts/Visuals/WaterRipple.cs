using UnityEngine;

//Visual only: scrolls the pond's ripple normal map so the water looks alive.
public class WaterRipple : MonoBehaviour
{
    [SerializeField]
    private Vector2 scrollSpeed = new Vector2(0.03f, 0.02f);

    private Material water;
    private Vector2 offset;
    private Vector2 detailOffset;

    private void Start()
    {
        water = GetComponent<Renderer>().material;//runtime copy, the asset is not changed
    }

    private void Update()
    {
        offset += scrollSpeed * Time.deltaTime;
        detailOffset -= scrollSpeed * 0.7f * Time.deltaTime;
        water.SetTextureOffset("_BaseMap", offset);//URP Lit: the normal map uses the base map tiling and offset
        water.SetTextureOffset("_DetailAlbedoMap", detailOffset);//and the detail normal map uses the detail tiling and offset
    }

    private void OnDestroy()
    {
        if (water != null)
            Destroy(water);
    }
}
