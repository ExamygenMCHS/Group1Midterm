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

    private void Start()
    {
        //the Speed Boots bought in the store make the player walk faster
        float speed = settings.playerSpeed * (HasSpeedBoots() ? settings.speedBootsMultiplier : 1f);
        playerMove.forwardSpeed = speed;
        playerMove.sidewaySpeed = speed;

        dayNightControl.cycleLengthInSeconds = settings.dayLengthInSeconds;

        foreach (FarmTileControl tile in farmTileManager.GetComponentsInChildren<FarmTileControl>())
            tile.daysUntilRevert = settings.daysUntilTileReverts;
    }

    public static bool HasSpeedBoots()
    {
        return FarmFund.Instance != null && FarmFund.Instance.Inventory.speedBoots;
    }
}
