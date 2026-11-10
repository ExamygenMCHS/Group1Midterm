using System.Collections.Generic;
using UnityEngine;

// The harvest loaded on the delivery truck. It is taken out of the FarmInventory when a delivery starts and is paid
// for at the wholesale market; rough driving lowers its condition, and with it the price.
// Plain static data, so it survives the scene changes of one delivery (farm -> road -> market).
public static class DeliveryCargo
{
    public struct Line
    {
        public PlantData crop;
        public int count;
    }

    private static readonly List<Line> lines = new List<Line>();

    public static IReadOnlyList<Line> Lines { get { return lines; } }
    /// <summary>1 = untouched, lower after crashes.</summary>
    public static float Condition { get; private set; } = 1f;
    public static bool HasCargo { get { return lines.Count > 0; } }

    // Play Mode in this project starts without a domain reload, so the load from the last play session must be cleared
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Clear();
    }

    public static int TotalCount()
    {
        int total = 0;
        foreach (Line line in lines)
            total += line.count;
        return total;
    }

    // Moves every harvested crop from the inventory onto the truck. Returns how many crops were loaded.
    public static int Load(FarmInventory inventory)
    {
        if (inventory == null)
            return 0;
        // Copy first: removing from the inventory fires its changed event, and listeners may touch the crop list.
        var owned = new List<Line>();
        foreach (FarmInventory.CropStock stock in inventory.crops)
            if (stock.crop != null && stock.harvested > 0)
                owned.Add(new Line { crop = stock.crop, count = stock.harvested });
        bool wasEmpty = lines.Count == 0;
        int loaded = 0;
        foreach (Line line in owned)
        {
            if (!inventory.TryRemoveHarvested(line.crop, line.count))
                continue;
            Add(line.crop, line.count);
            loaded += line.count;
        }
        if (wasEmpty)
            Condition = 1f; // a fresh load starts undamaged
        return loaded;
    }

    public static void Add(PlantData crop, int count)
    {
        if (crop == null || count <= 0)
            return;
        for (int i = 0; i < lines.Count; i++)
        {
            if (lines[i].crop != crop)
                continue;
            Line line = lines[i];
            line.count += count;
            lines[i] = line;
            return;
        }
        lines.Add(new Line { crop = crop, count = count });
    }

    // Puts the load back in the inventory (the delivery was called off).
    public static void Unload(FarmInventory inventory)
    {
        if (inventory != null)
            foreach (Line line in lines)
                inventory.AddHarvested(line.crop, line.count);
        Clear();
    }

    public static void SetCondition(float condition)
    {
        Condition = Mathf.Clamp01(condition);
    }

    // What one line pays at the market: the crop's normal price times the wholesale bonus, lowered by the condition.
    public static int LineValue(Line line, float wholesaleBonus)
    {
        if (line.crop == null)
            return 0;
        return Mathf.RoundToInt(line.crop.sellPrice * line.count * wholesaleBonus * Condition);
    }

    public static int TotalValue(float wholesaleBonus)
    {
        int total = 0;
        foreach (Line line in lines)
            total += LineValue(line, wholesaleBonus);
        return total;
    }

    public static void Clear()
    {
        lines.Clear();
        Condition = 1f;
    }
}
