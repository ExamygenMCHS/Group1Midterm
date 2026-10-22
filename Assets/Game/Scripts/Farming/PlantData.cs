
using UnityEngine;

// Stores the settings for each type of plant.
// Each plant can have different seasons, growth speeds, and prices.
[CreateAssetMenu(fileName = "PlantData", menuName = "Farm Game/Plant Data")]
public class PlantData : ScriptableObject
{
    public string cropName = "Sunflower";

    [Tooltip("Plural used in the GUI, e.g. Sunflowers")]
    public string pluralName = "Sunflowers";

    [Tooltip("Prefab containing the three plant growth stages")]
    public GameObject plantPrefab;

    [Header("Growth (in-game days)")]
    public int daysToGrowing = 2;
    public int daysToMature = 2;

    [Header("Store")]
    public int seedPrice = 10;
    public int sellPrice = 15;

    [Header("Seasons")]
    public bool diesInWinter = true;
    public bool canPlantInWinter = false;

    // New: Different plants can grow in different seasons.
    public bool canPlantInSpring = true;
    public bool canPlantInSummer = true;
    public bool canPlantInFall = true;

    [Header("GUI")]
    public Sprite icon;
    public Sprite seedIcon;

    [Header("Inventory bag")]
    public InventoryItemData seedItem;
    public InventoryItemData harvestItem;

    public string Plural()
    {
        return string.IsNullOrEmpty(pluralName)
            ? cropName + "s"
            : pluralName;
    }

    public string NameFor(int count)
    {
        return count == 1 ? cropName : Plural();
    }
}
