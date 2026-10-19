using UnityEngine;

//every number used to balance the farming levels, kept in one asset (Assets/Game/Data/Settings) that every farm scene
//shares. FarmBalance copies the level numbers onto the example scripts when a scene starts.
[CreateAssetMenu(fileName = "EnergySettings", menuName = "Farm Game/Energy Settings")]
public class EnergySettings : ScriptableObject
{
    [Header("Energy")]
    public float maxEnergy = 100f;
    [Tooltip("Energy used every time the player presses Space on a farm tile (till, water, or re-water)")]
    public float tileInteractionCost = 5f;
    [Tooltip("Energy used to plant one seed (Scene3-Plant)")]
    public float plantingCost = 5f;
    [Tooltip("Energy gained per second while the player stands in the sleep area")]
    public float sleepRegenPerSecond = 5f;
    [Tooltip("Energy gained per second while the player stands still outside the sleep area")]
    public float idleRegenPerSecond = 1f;
    [Tooltip("The player must stand still (and not use energy) for this long before the idle regeneration starts")]
    public float idleDelayInSeconds = 1f;

    [Header("Level balance (copied to the example scripts by FarmBalance when the scene starts)")]
    [Tooltip("PlayerMove forwardSpeed and sidewaySpeed")]
    public float playerSpeed = 2.75f;
    [Tooltip("DayNightControl cycleLengthInSeconds: real seconds per in-game day")]
    public float dayLengthInSeconds = 15f;
    [Tooltip("FarmTileControl daysUntilRevert: in-game days without interaction before watered goes back to dry soil, and dry soil back to grass")]
    public int daysUntilTileReverts = 4;

    [Header("Harvest and upgrades")]
    [Tooltip("Energy used to harvest one mature plant (E on the plant)")]
    public float harvestCost = 5f;
    [Tooltip("Walking speed multiplier while the player owns the Speed Boots from the store")]
    public float speedBootsMultiplier = 1.4f;

    [Header("Hotbar")]
    [Tooltip("Waterings in one full watering can. 8 means the 16 tiles take two full cans (one refill at the well or the pond)")]
    public int wateringCanCapacity = 8;
}
