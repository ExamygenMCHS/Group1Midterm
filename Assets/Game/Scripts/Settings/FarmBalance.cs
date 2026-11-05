using UnityEngine;

//copies the balance numbers from the EnergySettings asset to the example scripts,
//so player speed, day length and how quickly the soil dries can all be tuned in one place.
//Note: the values typed on PlayerMove / DayNightControl / the Farm Tile prefab are overwritten by this script in Play Mode.
[DefaultExecutionOrder(50)]//Start() must run after FarmTIleManager.Start() has spawned the tiles
public class FarmBalance : MonoBehaviour
{
    [SerializeField]
    private EnergySettings settings;
    [SerializeField]
    private PlayerMove playerMove;
    [SerializeField]
    private DayNightControl dayNightControl;
    [SerializeField]
    private Transform farmTileManager;

    private FarmInventory inventory;
    private bool started;

    private void Start()
    {
        started = true;
        ListenForUpgrades();

        dayNightControl.cycleLengthInSeconds = settings.dayLengthInSeconds;

        foreach (FarmTileControl tile in farmTileManager.GetComponentsInChildren<FarmTileControl>())
            tile.daysUntilRevert = settings.daysUntilTileReverts;
    }

    private void OnEnable()
    {
        if (started)
            ListenForUpgrades();
    }

    private void OnDisable()
    {
        StopListening();
    }

    private void OnDestroy()
    {
        StopListening();
    }

    private void ListenForUpgrades()
    {
        StopListening();
        inventory = FarmFund.Instance != null ? FarmFund.Instance.Inventory : null;
        if (inventory != null)
            inventory.inventoryChangedEvent.AddListener(ApplyPlayerSpeed);
        ApplyPlayerSpeed();
    }

    private void StopListening()
    {
        if (inventory != null)
            inventory.inventoryChangedEvent.RemoveListener(ApplyPlayerSpeed);
        inventory = null;
    }

    private void ApplyPlayerSpeed()
    {
        //A trader purchase takes effect in the current farm, without a scene reload.
        float speed = settings.playerSpeed * (inventory != null && inventory.speedBoots ? settings.speedBootsMultiplier : 1f);
        playerMove.forwardSpeed = speed;
        playerMove.sidewaySpeed = speed;
    }

    public static bool HasSpeedBoots()
    {
        return FarmFund.Instance != null && FarmFund.Instance.Inventory.speedBoots;
    }
}
