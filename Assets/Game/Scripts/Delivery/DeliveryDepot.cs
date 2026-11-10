using UnityEngine;

// The delivery truck parked on the farm. With harvested crops in the inventory, E loads them all on the truck and
// starts the drive down the mountain road; the farm waits as it is until the player comes back from the market.
public class DeliveryDepot : WorldInteractable
{
    [SerializeField, Min(0.1f)] private float reach = 2.5f;
    [SerializeField] private string deliveryScene = "Mountain Delivery";
    [SerializeField] private string emptyPrompt = "Harvest crops to load the delivery truck";

    public override float DistanceOutsideReach(Vector3 playerPosition)
    {
        Vector3 offset = playerPosition - transform.position;
        offset.y = 0f;
        return offset.magnitude - Mathf.Max(0.1f, reach);
    }

    // How many harvested crops the player could load right now.
    public int CropsToLoad()
    {
        FarmFund fund = FarmFund.Instance;
        if (fund == null)
            return 0;
        int total = 0;
        foreach (FarmInventory.CropStock stock in fund.Inventory.crops)
            if (stock.crop != null)
                total += stock.harvested;
        return total;
    }

    public override string GetPrompt(WorldInteractor player)
    {
        if (FarmFund.Instance == null || TraderShopPanel.IsAnyOpen || FarmTrip.IsAway)
            return null;
        int crops = CropsToLoad();
        return crops > 0 ? "Deliver " + crops + (crops == 1 ? " crop" : " crops") + " to the market" : emptyPrompt;
    }

    public override bool CanInteract(WorldInteractor player)
    {
        return isActiveAndEnabled && player != null && player.isActiveAndEnabled &&
            DistanceOutsideReach(player.transform.position) <= 0f && CropsToLoad() > 0 && !FarmTrip.IsAway;
    }

    public override void Interact(WorldInteractor player)
    {
        StartDelivery(player);
    }

    public bool StartDelivery(WorldInteractor player)
    {
        if (!CanInteract(player))
            return false;
        PlayerStateControl state = player.GetComponent<PlayerStateControl>();
        if (state != null && !state.CanStartAction())
            return false;
        if (DeliveryCargo.Load(FarmFund.Instance.Inventory) <= 0)
            return false;
        FarmTrip.Leave(deliveryScene);
        return true;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, .82f, .12f);
        Gizmos.DrawWireSphere(transform.position, Mathf.Max(0.1f, reach));
    }
}
