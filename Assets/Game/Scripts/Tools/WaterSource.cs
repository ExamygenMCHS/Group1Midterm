using UnityEngine;

//water to fill the watering can: the well and the pond. With the can in hand and close to the water the
//prompt says "E  Fill the watering can"; E fills it to the top. The water is an ellipse on the ground around this
//object (a circle for the well, the pond is wider than it is long).
public class WaterSource : WorldInteractable
{
    [SerializeField]
    private string sourceName = "well";
    [SerializeField]
    private Vector2 waterRadii = new Vector2(0.65f, 0.65f);//half size of the water on the ground (world x, z)
    [SerializeField]
    private float reach = 1.1f;//how far from the edge of the water the player can still fill the can
    [SerializeField]
    private float waterHeight = 0.05f;//height of the water surface above this object (for the splash)
    [SerializeField]
    private ParticleSystem splash;//optional: plays where the can dips into the water
    [SerializeField]
    private AudioSource fillSound;//optional

    public override float DistanceOutsideReach(Vector3 playerPosition)
    {
        Vector3 offset = playerPosition - transform.position;
        float a = waterRadii.x + reach, b = waterRadii.y + reach;
        float k = Mathf.Sqrt((offset.x * offset.x) / (a * a) + (offset.z * offset.z) / (b * b));//1 on the edge of the reach
        return (k - 1f) * Mathf.Min(a, b);
    }

    public override string GetPrompt(WorldInteractor player)
    {
        Hotbar hotbar = player.GetHotbar();
        if (hotbar == null || player.GetWateringCanTank() == null || !hotbar.HasItem(ToolManager.ToolType.WateringCan))
            return null;//nothing to fill yet
        if (!hotbar.IsHolding(ToolManager.ToolType.WateringCan))
            return "Hold the watering can (" + Hotbar.KeyTag((hotbar.SlotOf(ToolManager.ToolType.WateringCan) + 1).ToString()) + ") to fill it here";
        if (player.GetWateringCanTank().IsFull())
            return "The watering can is full";
        return "Fill the watering can at the " + sourceName;
    }

    public override bool CanInteract(WorldInteractor player)
    {
        Hotbar hotbar = player.GetHotbar();
        WateringCanTank tank = player.GetWateringCanTank();
        return hotbar != null && tank != null && hotbar.IsHolding(ToolManager.ToolType.WateringCan) && !tank.IsFull();
    }

    public override void Interact(WorldInteractor player)
    {
        WateringCanTank tank = player.GetWateringCanTank();
        tank.Refill();
        if (splash != null)
        {
            splash.transform.position = EdgeToward(player.transform.position);
            splash.Play();
        }
        if (fillSound != null)
            fillSound.Play();
        player.GetHotbar().Tell("Filled the watering can at the " + sourceName + " (" + tank.GetWater() + " / " + tank.GetCapacity() + ").");
    }

    //the point on the edge of the water closest to the given position (the middle for a position right above it)
    private Vector3 EdgeToward(Vector3 position)
    {
        Vector3 offset = position - transform.position;
        float k = Mathf.Sqrt((offset.x * offset.x) / (waterRadii.x * waterRadii.x) + (offset.z * offset.z) / (waterRadii.y * waterRadii.y));
        Vector3 edge = k > 1f ? new Vector3(offset.x / k, 0f, offset.z / k) : new Vector3(offset.x, 0f, offset.z);
        return transform.position + edge + Vector3.up * waterHeight;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.3f, 0.6f, 1f);
        Gizmos.matrix = Matrix4x4.TRS(transform.position, Quaternion.identity, new Vector3(waterRadii.x + reach, 0.01f, waterRadii.y + reach));
        Gizmos.DrawWireSphere(Vector3.zero, 1f);
    }
}
