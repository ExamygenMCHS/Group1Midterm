using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

//Spawns a Plant on the tile the player is interacting with when E is pressed with seeds in the hand. The tile must be
//watered. Every spawned Plant becomes a child of this object. E with the sickle in the hand harvests a mature plant.
//Planting and harvesting are actions of the player's state machine (the seed bag / sickle and their animation play,
//the plant appears or is cut at the hit moment). It listens to the calendar's new season event: nothing can be planted
//in winter. A level can offer more than one crop; each crop's seeds have a slot in the hotbar, and Q selects the next
//one. When a prompt is up (a tool to pick up, water to fill the can) E belongs to the prompt.
public class PlantManager : MonoBehaviour
{
    [SerializeField]
    private PlantData[] crops;//the seeds the player can plant in this level (each gets a hotbar slot, in this order)
    [SerializeField]
    private PlayerInteractPoint playerInteractPoint;//knows which tile the player is interacting with
    [SerializeField]
    private PlayerEnergy playerEnergy;
    [SerializeField]
    private PlayerStateControl playerStateControl;
    [SerializeField]
    private GameCalendar calendar;
    [SerializeField]
    private TimedMessage hudMessage;
    [SerializeField]
    private Hotbar hotbar;//the seeds or the sickle in the player's hand
    [SerializeField]
    private WorldInteractor worldInteractor;//E picks up tools and fills the can first

    [SerializeField]
    private Key plantKey = Key.E;//must not be Space or W/A/S/D
    [SerializeField]
    private Key switchSeedKey = Key.Q;

    //fired when the player harvests a plant (the level goal counts them)
    public UnityEvent<PlantData> plantHarvestedEvent = new UnityEvent<PlantData>();
    //fired when the player takes another kind of seeds into the hand (the HUD seed label follows)
    public UnityEvent selectedSeedChangedEvent = new UnityEvent();

    //remembers which tile already has a plant, so a tile cannot be planted twice
    private Dictionary<GameObject, Plant> plantOnTile = new Dictionary<GameObject, Plant>();
    private int numPlantSpawned = 0;
    private int lastHeldCrop = 0;//index in crops of the seeds in the hand, or the last ones held (the hotbar decides)

    private void Start()
    {
        if (calendar != null)
            calendar.newSeasonEvent.AddListener(OnNewSeason);
        hotbar.selectedSlotChangedEvent.AddListener(OnHeldItemChanged);
    }

    private void OnDestroy()
    {
        if (calendar != null)
            calendar.newSeasonEvent.RemoveListener(OnNewSeason);
        if (hotbar != null)
            hotbar.selectedSlotChangedEvent.RemoveListener(OnHeldItemChanged);
    }

    private void Update()
    {
        if (Keyboard.current == null)
            return;
        bool promptUsesKey = worldInteractor.ClaimsInteractKey();
        if (Keyboard.current[plantKey].wasPressedThisFrame && !playerInteractPoint.IsActionLocked() && !promptUsesKey)
            InteractWithCurTile();
        if (Keyboard.current[switchSeedKey].wasPressedThisFrame)
            SwitchSeed();
    }

    //E on a tile with a plant harvests it (if mature); E on an empty tile plants a seed
    public bool InteractWithCurTile()
    {
        GameObject curTile = playerInteractPoint.GetCurTile();
        if (curTile != null && GetPlantOn(curTile) != null)
            return TryHarvestOnCurTile();
        return TryPlantOnCurTile();
    }

    //returns true if a plant was spawned (or will appear when the planting action reaches its hit moment)
    public bool TryPlantOnCurTile()
    {
        GameObject curTile = playerInteractPoint.GetCurTile();
        if (curTile == null)//the player is not on the farm
            return false;
        if (playerInteractPoint.IsActionLocked())//busy with another action
            return false;

        //the seeds have to be in the player's hand
        if (!hotbar.RequireInHand(ToolManager.ToolType.SeedBag, "to plant"))
            return false;
        PlantData crop = hotbar.GetSelectedCrop();
        if (crop == null || crop.plantPrefab == null)//no seeds set up for this level
            return false;
        if (GetPlantOn(curTile) != null)
        {
            hudMessage.Show("This tile already has a plant.");
            return false;
        }
        //the new season event told us winter is here: nothing can be planted until spring
        if (IsWinter() && !crop.canPlantInWinter)
        {
            hudMessage.Show("Nothing grows in winter. Wait for spring.");
            return false;
        }
        FarmTileControl tile = curTile.GetComponent<FarmTileControl>();
        if (tile.tileCond != FarmTileControl.FarmTileCond.Watered)//no planting on grass or dry soil
        {
            hudMessage.Show("Seeds only grow on a watered tile.");
            return false;
        }
        FarmInventory inventory = Inventory();
        if (inventory == null || inventory.GetSeeds(crop) <= 0)
        {
            hudMessage.Show("No " + crop.cropName.ToLower() + " seeds left.");
            return false;
        }
        if (!playerEnergy.TrySpendEnergy(playerEnergy.GetPlantingCost()))//out of energy
            return false;

        if (!inventory.TryUseSeed(crop))
            return false;

        //the tile is taken now, the plant itself appears when the seed goes into the soil
        plantOnTile[curTile] = null;
        UnityAction plantIt = () => SpawnPlant(curTile, crop);
        if (!playerStateControl.StartAction(PlayerStateControl.PlayerState.Planting, plantIt))
            plantIt();
        return true;
    }

    //returns true if the plant on the tile is mature and the harvest started
    public bool TryHarvestOnCurTile()
    {
        GameObject curTile = playerInteractPoint.GetCurTile();
        if (curTile == null || playerInteractPoint.IsActionLocked())
            return false;
        Plant plant = GetPlantOn(curTile);
        if (plant == null || plant.IsGone())
            return false;
        if (!plant.IsMature())
        {
            int days = plant.DaysUntilMature();
            string name = plant.crop != null ? plant.crop.cropName : "plant";
            hudMessage.Show("This " + name.ToLower() + " is still growing (" + days + (days == 1 ? " more day)." : " more days)."));
            return false;
        }
        //a ripe plant is cut with the sickle in the hand
        if (!hotbar.RequireInHand(ToolManager.ToolType.Sickle, "to harvest"))
            return false;
        if (!playerEnergy.TrySpendEnergy(playerEnergy.GetHarvestCost()))//out of energy
            return false;

        UnityAction harvestIt = () => HarvestPlant(curTile, plant);
        if (!playerStateControl.StartAction(PlayerStateControl.PlayerState.Harvesting, harvestIt))
            harvestIt();
        return true;
    }

    private void SpawnPlant(GameObject tileObject, PlantData crop)
    {
        //spawn the plant on top of the tile, in the middle
        Vector3 plantPos = tileObject.transform.position;
        plantPos.y += tileObject.transform.localScale.y / 2;
        GameObject newPlant = Instantiate(crop.plantPrefab, plantPos, Quaternion.identity);
        numPlantSpawned++;
        newPlant.name = "Plant " + numPlantSpawned.ToString();
        newPlant.transform.parent = transform;//put all the plants under the plant manager
        Plant plant = newPlant.GetComponent<Plant>();
        plant.crop = crop;
        plantOnTile[tileObject] = plant;
    }

    private void HarvestPlant(GameObject tileObject, Plant plant)
    {
        if (plant == null || !plant.Harvest())
            return;
        plantOnTile.Remove(tileObject);
        FarmInventory inventory = Inventory();
        if (inventory != null && plant.crop != null)
            inventory.AddHarvested(plant.crop, 1);
        plantHarvestedEvent.Invoke(plant.crop);//make announcement to all listeners
    }

    //called by the calendar when a new season starts
    private void OnNewSeason()
    {
        if (IsWinter())
            hudMessage.Show("Winter is here! The frost killed every plant. No planting until spring.");
        else if (calendar.season == GameCalendar.Season.Spring)
            hudMessage.Show("Spring is here! Seeds can be planted again.");
    }

    //Q puts the next crop's seeds in the player's hand by selecting the next seed slot (OnHeldItemChanged follows)
    public void SwitchSeed()
    {
        hotbar.SelectNextSeeds();
    }

    //seeds selected in the hotbar become the seeds in hand (the seed label on the HUD follows)
    private void OnHeldItemChanged()
    {
        PlantData held = hotbar.GetSelectedCrop();
        if (held == null || crops == null)
            return;
        int index = System.Array.IndexOf(crops, held);
        if (index < 0 || index == lastHeldCrop)
            return;
        lastHeldCrop = index;
        selectedSeedChangedEvent.Invoke();
    }

    // ---- API

    public PlantData[] GetCrops()
    {
        return crops;
    }

    //the seeds in the player's hand, or the ones held last (the level's first crop before any were held)
    public PlantData GetSelectedCrop()
    {
        if (crops == null || crops.Length == 0)
            return null;
        return crops[Mathf.Clamp(lastHeldCrop, 0, crops.Length - 1)];
    }
    public int GetCropCount()
    {
        return crops == null ? 0 : crops.Length;
    }
    public bool IsWinter()
    {
        return calendar != null && calendar.IsWinter();
    }

    //the plant growing on the given tile, or null. While a seed is being planted the tile's entry is empty until the
    //plant appears; an empty entry outside planting belonged to a plant that winter removed, and is cleaned up here.
    public Plant GetPlantOn(GameObject tileObject)
    {
        if (!plantOnTile.TryGetValue(tileObject, out Plant plant))
            return null;
        if (plant == null)
        {
            if (playerStateControl.GetPlayerState() != PlayerStateControl.PlayerState.Planting)
                plantOnTile.Remove(tileObject);
            return null;
        }
        return plant.IsGone() ? null : plant;
    }

    private static FarmInventory Inventory()
    {
        return FarmFund.Instance != null ? FarmFund.Instance.Inventory : null;
    }
}
