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

    //this event gets fired every time a seed, crop or upgrade count changes, so the GUI can update
    public UnityEvent inventoryChangedEvent = new UnityEvent();

    private List<CropStock> checkpointCrops = new List<CropStock>();
    private bool checkpointSpeedBoots = false;
    private bool checkpointBag = false;
    private bool checkpointHoe, checkpointWateringCan, checkpointSickle;
    private int checkpointCanWater;

    private CropStock StockOf(PlantData crop)
    {
        foreach (CropStock stock in crops)
            if (stock.crop == crop)
                return stock;
        CropStock added = new CropStock { crop = crop };
        crops.Add(added);
        return added;
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

    // ---- level checkpoint (see FarmFund)

    public void SaveCheckpoint()
    {
        checkpointCrops.Clear();
        foreach (CropStock stock in crops)
            checkpointCrops.Add(new CropStock { crop = stock.crop, seeds = stock.seeds, harvested = stock.harvested });
        checkpointSpeedBoots = speedBoots;
        checkpointBag = bag;
        checkpointHoe = hoe;
        checkpointWateringCan = wateringCan;
        checkpointSickle = sickle;
        checkpointCanWater = canWater;
    }

    public void RestoreCheckpoint()
    {
        crops.Clear();
        foreach (CropStock stock in checkpointCrops)
            crops.Add(new CropStock { crop = stock.crop, seeds = stock.seeds, harvested = stock.harvested });
        speedBoots = checkpointSpeedBoots;
        bag = checkpointBag;
        hoe = checkpointHoe;
        wateringCan = checkpointWateringCan;
        sickle = checkpointSickle;
        canWater = checkpointCanWater;
        inventoryChangedEvent.Invoke();
    }
}
