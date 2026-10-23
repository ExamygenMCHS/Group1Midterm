using UnityEngine;

//"Plant Data": everything that makes one kind of plant different, kept in one asset per plant
//(Assets/Game/Data/Plants). The Plant state machine stays the same for every plant, only these numbers and models
//change, so a new plant is a new prefab and a new PlantData asset, without code changes.
[CreateAssetMenu(fileName = "PlantData", menuName = "Farm Game/Plant Data")]
public class PlantData : ScriptableObject
{
    public string cropName = "Sunflower";
    [Tooltip("Plural used in the GUI, e.g. \"Sunflowers\"")]
    public string pluralName = "Sunflowers";
    [Tooltip("An empty parent with the planted (small), growing (mid) and mature (large) models as children, and the Plant script")]
    public GameObject plantPrefab;

    [Header("Growth (in-game days)")]
    public int daysToGrowing = 2;//days spent in the planted stage
    public int daysToMature = 2;//days spent in the growing stage

    [Header("Store")]
    public int seedPrice = 10;
    public int sellPrice = 15;

    [Header("Seasons")]
    [Tooltip("Seasons in which seeds of this crop can be planted")]
    public GameCalendar.Season[] plantableSeasons = { GameCalendar.Season.Spring, GameCalendar.Season.Summer, GameCalendar.Season.Fall };
    [Tooltip("Tick if this plant disappears when a certain season starts")]
    public bool disappears = true;
    [Tooltip("The plant withers when this season starts")]
    public GameCalendar.Season disappearSeason = GameCalendar.Season.Winter;

    [Header("GUI")]
    public Sprite icon;
    public Sprite seedIcon;

    [Header("Inventory bag")]
    [Tooltip("The bag item for this plant's seeds (its sprite and name in the bag)")]
    public InventoryItemData seedItem;
    [Tooltip("The bag item for this plant once harvested")]
    public InventoryItemData harvestItem;

    //can seeds of this crop be planted in the given season?
    public bool CanPlantIn(GameCalendar.Season season)
    {
        return plantableSeasons != null && System.Array.IndexOf(plantableSeasons, season) >= 0;
    }
    //does this plant wither when the given season starts?
    public bool DiesIn(GameCalendar.Season season)
    {
        return disappears && season == disappearSeason;
    }
    //the next season after the given one (same year) in which seeds of this crop can be planted, for the GUI hint
    public bool TryGetNextPlantableSeason(GameCalendar.Season after, out GameCalendar.Season next)
    {
        for (int s = (int)after + 1; s < GameCalendar.SeasonsPerYear; s++)
        {
            if (CanPlantIn((GameCalendar.Season)s))
            {
                next = (GameCalendar.Season)s;
                return true;
            }
        }
        next = after;
        return false;
    }

    public string Plural()
    {
        return string.IsNullOrEmpty(pluralName) ? cropName + "s" : pluralName;
    }
    public string NameFor(int count)
    {
        return count == 1 ? cropName : Plural();
    }
}