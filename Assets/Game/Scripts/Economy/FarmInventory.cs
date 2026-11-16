using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

//what the player owns besides money - seeds and harvested crops of every kind of plant, and the upgrades
//bought in the store, and the tools picked up from the ground. It sits on the "Farm Fund" object, so it travels from scene to scene together with the fund.
public class FarmInventory : MonoBehaviour
{
    [Serializable]
    public class CropStock
    {
        public PlantData crop;
        public int seeds;//seeds ready to plant
        public int harvested;//harvested plants in storage, ready to sell in the store
    }

    public List<CropStock> crops = new List<CropStock>();
    public bool speedBoots = false;//upgrade from the second store visit: the player walks faster
    public bool bag = false;//bought in the third store visit: from then on the inventory bag shows everything the player owns

    //the tools the player picked up from the ground. They stay with the player from level to level.
    public bool hoe = false;
    public bool wateringCan = false;
    public bool sickle = false;
    public int canWater = 0;//waterings left in the watering can (it is found empty)

    //the storage chest in the cabin: seeds and harvested crops put away there. They are not in the bag, so the
    //trader's store and the wholesale truck (which only read 'crops') never see or sell them.
    public List<CropStock> chest = new List<CropStock>();

    //this event gets fired every time a seed, crop or upgrade count changes, so the GUI can update
    public UnityEvent inventoryChangedEvent = new UnityEvent();
    //this event gets fired every time something goes into or comes out of the chest
    public UnityEvent chestChangedEvent = new UnityEvent();

    private CropStock StockOf(PlantData crop)
    {
        return StockIn(crops, crop);
    }

    public int GetSeeds(PlantData crop)
    {
        return crop == null ? 0 : StockOf(crop).seeds;
    }
    public int GetHarvested(PlantData crop)
    {
        return crop == null ? 0 : StockOf(crop).harvested;
    }
    public int GetTotalSeeds()
    {
        int total = 0;
        foreach (CropStock stock in crops)
            total += stock.seeds;
        return total;
    }

    public void AddSeeds(PlantData crop, int amount)
    {
        if (crop == null || amount <= 0 || amount > int.MaxValue - GetSeeds(crop))
            return;
        StockOf(crop).seeds += amount;
        inventoryChangedEvent.Invoke();
    }

    //returns false (and changes nothing) when there is no seed of this crop left
    public bool TryUseSeed(PlantData crop)
    {
        if (crop == null)
            return false;
        CropStock stock = StockOf(crop);
        if (stock.seeds <= 0)
            return false;
        stock.seeds--;
        inventoryChangedEvent.Invoke();
        return true;
    }

    public void AddHarvested(PlantData crop, int amount)
    {
        if (crop == null || amount <= 0 || amount > int.MaxValue - GetHarvested(crop))
            return;
        StockOf(crop).harvested += amount;
        inventoryChangedEvent.Invoke();
    }

    //returns false (and changes nothing) when fewer than 'amount' harvested plants are in storage
    public bool TryRemoveHarvested(PlantData crop, int amount)
    {
        if (crop == null)
            return false;
        CropStock stock = StockOf(crop);
        if (amount <= 0 || stock.harvested < amount)
            return false;
        stock.harvested -= amount;
        inventoryChangedEvent.Invoke();
        return true;
    }

    // ---- Storage chest: a second store of seeds and crops, kept apart from the bag

    private static CropStock StockIn(List<CropStock> list, PlantData crop)
    {
        foreach (CropStock stock in list)
            if (stock.crop == crop)
                return stock;
        CropStock added = new CropStock { crop = crop };
        list.Add(added);
        return added;
    }

    public int GetChestSeeds(PlantData crop)
    {
        return crop == null ? 0 : StockIn(chest, crop).seeds;
    }
    public int GetChestHarvested(PlantData crop)
    {
        return crop == null ? 0 : StockIn(chest, crop).harvested;
    }
    public int GetChestTotal()
    {
        int total = 0;
        foreach (CropStock stock in chest)
            total += stock.seeds + stock.harvested;
        return total;
    }

    //moves seeds (harvest false) or harvested crops (harvest true) from the bag into the chest.
    //Returns false (and changes nothing) when the bag holds fewer than 'amount'.
    public bool StoreInChest(PlantData crop, bool harvest, int amount)
    {
        return Move(crops, chest, crop, harvest, amount);
    }

    //moves seeds or harvested crops from the chest back into the bag
    public bool TakeFromChest(PlantData crop, bool harvest, int amount)
    {
        return Move(chest, crops, crop, harvest, amount);
    }

    private bool Move(List<CropStock> from, List<CropStock> to, PlantData crop, bool harvest, int amount)
    {
        if (crop == null || amount <= 0)
            return false;
        CropStock source = StockIn(from, crop);
        CropStock target = StockIn(to, crop);
        int have = harvest ? source.harvested : source.seeds;
        int already = harvest ? target.harvested : target.seeds;
        if (have < amount || amount > int.MaxValue - already)
            return false;
        if (harvest)
        {
            source.harvested -= amount;
            target.harvested += amount;
        }
        else
        {
            source.seeds -= amount;
            target.seeds += amount;
        }
        inventoryChangedEvent.Invoke();
        chestChangedEvent.Invoke();
        return true;
    }

    public void SetSpeedBoots(bool owned)
    {
        speedBoots = owned;
        inventoryChangedEvent.Invoke();
    }

    public void SetBag(bool owned)
    {
        bag = owned;
        inventoryChangedEvent.Invoke();
    }

    // ---- Hotbar: tools picked up from the ground and the water in the can

    public bool HasTool(ToolManager.ToolType tool)
    {
        switch (tool)
        {
            case ToolManager.ToolType.Hoe: return hoe;
            case ToolManager.ToolType.WateringCan: return wateringCan;
            case ToolManager.ToolType.Sickle: return sickle;
        }
        return false;//seeds are counted per crop, empty hands are not an item
    }

    public void AddTool(ToolManager.ToolType tool)
    {
        switch (tool)
        {
            case ToolManager.ToolType.Hoe: hoe = true; break;
            case ToolManager.ToolType.WateringCan: wateringCan = true; break;
            case ToolManager.ToolType.Sickle: sickle = true; break;
            default: return;
        }
        inventoryChangedEvent.Invoke();
    }

    public void SetCanWater(int water)
    {
        canWater = Mathf.Max(0, water);
        inventoryChangedEvent.Invoke();
    }
}
