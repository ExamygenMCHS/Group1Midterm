using UnityEngine;
using UnityEngine.SceneManagement;

//Uses the existing E-key prompt, so the shop takes priority over planting/harvesting on a nearby tile.
public class WanderingTrader : WorldInteractable
{
    [SerializeField, Min(0.1f)] private float reach = 1.5f;
    [SerializeField] private string promptText = "Trade with the wandering trader";
    [SerializeField] private TraderShopPanel shopPrefab;
    [SerializeField] private Animator animator;

    private TraderShopPanel shopInstance;
    private static readonly int Greet = Animator.StringToHash("Greet");

    public bool IsTrading { get { return shopInstance != null && shopInstance.IsOpen; } }
    public TraderShopPanel ShopInstance { get { return shopInstance; } }

    public override float DistanceOutsideReach(Vector3 playerPosition)
    {
        Vector3 offset = playerPosition - transform.position;
        offset.y = 0f;
        return offset.magnitude - Mathf.Max(0.1f, reach);
    }

    public override string GetPrompt(WorldInteractor player)
    {
        return shopPrefab != null && FarmFund.Instance != null && !TraderShopPanel.IsAnyOpen ? promptText : null;
    }

    public override bool CanInteract(WorldInteractor player)
    {
        return isActiveAndEnabled && player != null && player.isActiveAndEnabled &&
            DistanceOutsideReach(player.transform.position) <= 0f && GetPrompt(player) != null;
    }

    public override void Interact(WorldInteractor player) { TryOpenShop(player); }

    public bool TryOpenShop(WorldInteractor player)
    {
        if (!CanInteract(player))
            return false;
        PlayerStateControl state = player.GetComponent<PlayerStateControl>();
        if (state != null && !state.CanStartAction())
            return false;
        if (shopInstance == null)
        {
            shopInstance = Instantiate(shopPrefab);
            SceneManager.MoveGameObjectToScene(shopInstance.gameObject, gameObject.scene);
        }
        if (!shopInstance.TryOpen(player, this))
            return false;
        if (animator != null)
            animator.SetTrigger(Greet);
        return true;
    }

    protected override void OnDisable()
    {
        if (shopInstance != null)
            shopInstance.Close();
        base.OnDisable();
    }

    private void OnDestroy()
    {
        if (shopInstance != null)
            Destroy(shopInstance.gameObject);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.95f, 0.78f, 0.26f);
        Gizmos.DrawWireSphere(transform.position, Mathf.Max(0.1f, reach));
    }
}
