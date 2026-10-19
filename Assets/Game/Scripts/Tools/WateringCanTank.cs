using UnityEngine;
using UnityEngine.Events;

//the water in the watering can. Every watering uses one fill; a full can (EnergySettings.wateringCanCapacity,
//8) waters half of the 16 tiles, so the farm takes at least two full cans. The can is found empty next to the well and
//is filled at the well or the pond (WaterSource). The amount is kept in the Farm Fund's inventory, so it carries over
//to the next level like the tools do.
public class WateringCanTank : MonoBehaviour
{
    [SerializeField]
    private EnergySettings settings;

    //this event gets fired every time the water in the can changes
    public UnityEvent waterChangedEvent = new UnityEvent();
    //this event gets fired when the player tries to water with an empty can (the water meter flashes)
    public UnityEvent canEmptyEvent = new UnityEvent();

    private int localWater = 0;//used only when there is no Farm Fund (a scene without one)

    public int GetCapacity()
    {
        return settings != null ? Mathf.Max(1, settings.wateringCanCapacity) : 8;
    }

    public int GetWater()
    {
        return Inventory() != null ? Inventory().canWater : localWater;
    }

    public bool IsEmpty()
    {
        return GetWater() <= 0;
    }

    public bool IsFull()
    {
        return GetWater() >= GetCapacity();
    }

    //Space with the can: returns false (and tells the listeners) when there is no water left
    public bool TryUseWater()
    {
        if (IsEmpty())
        {
            canEmptyEvent.Invoke();
            return false;
        }
        SetWater(GetWater() - 1);
        return true;
    }

    //E at the well or the pond
    public void Refill()
    {
        SetWater(GetCapacity());
    }

    private void SetWater(int water)
    {
        water = Mathf.Clamp(water, 0, GetCapacity());
        if (Inventory() != null)
            Inventory().SetCanWater(water);
        else
            localWater = water;
        waterChangedEvent.Invoke();//make announcement to all listeners
    }

    private static FarmInventory Inventory()
    {
        return FarmFund.Instance != null ? FarmFund.Instance.Inventory : null;
    }
}
