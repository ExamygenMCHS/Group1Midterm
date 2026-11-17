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

    //fishing: the rod is picked up by the pond, bait is bought from the trader (the rod comes with an old lure)
    [Serializable]
    public class BaitStock
    {
        public BaitData bait;
        public int count;//worms left; 1 for a lure the player owns
    }
    [Serializable]
    public class FishStock
    {
        public FishData fish;
        public List<float> weights = new List<float>();//pounds of every fish in the bag, one entry each
        public float best;//the heaviest one ever caught
        public int caught;//how many were caught in all
    }
    public bool fishingRod = false;
    public List<BaitStock> baits = new List<BaitStock>();
    public BaitData selectedBait;//the bait on the line (the player switches with the right mouse button)
    public List<FishStock> fish = new List<FishStock>();

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
            case ToolManager.ToolType.FishingRod: return fishingRod;
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
            case ToolManager.ToolType.FishingRod: fishingRod = true; break;
            default: return;
        }
        inventoryChangedEvent.Invoke();
    }

    // ---- Fishing: bait and the fish in the bag

    private BaitStock BaitStockOf(BaitData bait)
    {
        foreach (BaitStock stock in baits)
            if (stock.bait == bait)
                return stock;
        BaitStock added = new BaitStock { bait = bait };
        baits.Add(added);
        return added;
    }

    private FishStock FishStockOf(FishData kind)
    {
        foreach (FishStock stock in fish)
            if (stock.fish == kind)
                return stock;
        FishStock added = new FishStock { fish = kind };
        fish.Add(added);
        return added;
    }

    //worms left, or 1 for a lure the player owns (the old lure counts as owned with the rod)
    public int GetBait(BaitData bait)
    {
        if (bait == null)
            return 0;
        if (bait.comesWithRod)
            return fishingRod ? 1 : 0;
        return BaitStockOf(bait).count;
    }

    //true when this bait can go on the line: the player has it, and has not replaced it with a better one
    public bool CanUseBait(BaitData bait)
    {
        if (GetBait(bait) <= 0)
            return false;
        return bait.replacedBy == null || GetBait(bait.replacedBy) <= 0;
    }

    public void AddBait(BaitData bait, int amount)
    {
        if (bait == null || bait.comesWithRod || amount <= 0)
            return;
        BaitStock stock = BaitStockOf(bait);
        if (amount > int.MaxValue - stock.count)
            return;
        stock.count = bait.reusable ? 1 : stock.count + amount;
        inventoryChangedEvent.Invoke();
    }

    //the fish that bit ate the bait: returns false (and changes nothing) when there is none left. A lure is never used up.
    public bool TryUseBait(BaitData bait)
    {
        if (GetBait(bait) <= 0)
            return false;
        if (bait.reusable)
            return true;
        BaitStockOf(bait).count--;
        inventoryChangedEvent.Invoke();
        return true;
    }

    public void SelectBait(BaitData bait)
    {
        if (bait == selectedBait)
            return;
        selectedBait = bait;
        inventoryChangedEvent.Invoke();
    }

    public int GetFishCount(FishData kind)
    {
        return kind == null ? 0 : FishStockOf(kind).weights.Count;
    }

    public float GetBestFish(FishData kind)
    {
        return kind == null ? 0f : FishStockOf(kind).best;
    }

    //a caught fish goes into the bag; returns true when it is the heaviest of its kind so far
    public bool AddFish(FishData kind, float weight)
    {
        if (kind == null || weight <= 0f)
            return false;
        FishStock stock = FishStockOf(kind);
        stock.weights.Add(weight);
        stock.weights.Sort((a, b) => b.CompareTo(a));//heaviest first: the trader buys them in that order
        stock.caught++;
        bool best = weight > stock.best;
        if (best)
            stock.best = weight;
        inventoryChangedEvent.Invoke();
        return best;
    }

    //what the trader pays for the 'amount' heaviest fish of this kind in the bag
    public int GetFishValue(FishData kind, int amount)
    {
        if (kind == null)
            return 0;
        List<float> weights = FishStockOf(kind).weights;
        long total = 0;
        for (int i = 0; i < amount && i < weights.Count; i++)
            total += kind.PriceFor(weights[i]);
        return (int)Math.Min(total, int.MaxValue);
    }

    //takes the 'amount' heaviest fish of this kind out of the bag; returns false (and changes nothing) when it holds fewer
    public bool TryRemoveFish(FishData kind, int amount)
    {
        if (kind == null || amount <= 0)
            return false;
        List<float> weights = FishStockOf(kind).weights;
        if (weights.Count < amount)
            return false;
        weights.RemoveRange(0, amount);
        inventoryChangedEvent.Invoke();
        return true;
    }

    public void SetCanWater(int water)
    {
        canWater = Mathf.Max(0, water);
        inventoryChangedEvent.Invoke();
    }
}
