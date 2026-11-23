using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

//the bag items, the three baits and the four fish, with the balance table from the fishing spec
public static partial class FishingBuilder
{
    private static T LoadOrCreate<T>(string path) where T : ScriptableObject
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null)
        {
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
        }
        return asset;
    }

    private static InventoryItemData Item(string name, ItemType type, InteractionType interaction, int buy, int sell, string sprite, bool stackable)
    {
        InventoryItemData item = LoadOrCreate<InventoryItemData>(DataDir + "/Items/" + name + ".asset");
        item.itemName = name;
        item.itemType = type;
        item.interactionType = interaction;
        item.buyPrice = buy;
        item.sellPrice = sell;
        item.itemSprite = Spr(sprite);
        item.stackable = stackable;
        item.maxStack = 0;
        EditorUtility.SetDirty(item);
        return item;
    }

    private static BaitData Bait(string name, InventoryItemData item, bool reusable, int durability, int price, Vector2 wait, float bite, float skew, string hookPrefab)
    {
        BaitData bait = LoadOrCreate<BaitData>(DataDir + "/Bait/" + name + ".asset");
        bait.baitName = name;
        bait.item = item;
        bait.reusable = reusable;
        bait.durability = durability;
        bait.price = price;
        bait.waitSeconds = wait;
        bait.biteChance = bite;
        bait.sizeSkew = skew;
        bait.hookModel = Load<GameObject>(PrefabDir + "/" + hookPrefab + ".prefab");
        EditorUtility.SetDirty(bait);
        return bait;
    }

    private static FishData.BaitTaste Taste(BaitData bait, float weight, float skew = 1f)
    {
        return new FishData.BaitTaste { bait = bait, weight = weight, sizeSkew = skew };
    }

    private static void Fish(string name, InventoryItemData item, float min, float max, float trophy, int perPound, FishData.BaitTaste[] tastes,
        float deep, float cover, float night, float ice, float window, int nibbles, float strength, Vector2 fight, Vector2 surge, Vector2 length)
    {
        FishData fish = LoadOrCreate<FishData>(DataDir + "/Fish/" + name + ".asset");
        fish.fishName = name;
        fish.item = item;
        fish.minWeight = min;
        fish.maxWeight = max;
        fish.trophyWeight = trophy;
        fish.pricePerPound = perPound;
        fish.baits = tastes;
        fish.deepWater = deep;
        fish.nearCover = cover;
        fish.atNight = night;
        fish.underIce = ice;
        fish.biteWindowSeconds = window;
        fish.maxNibbles = nibbles;
        fish.strength = strength;
        fish.fightSeconds = fight;
        fish.surgeSeconds = surge;
        fish.modelPrefab = Load<GameObject>(PrefabDir + "/" + FishPrefabName(name) + ".prefab");
        fish.lengthMetres = length;
        EditorUtility.SetDirty(fish);
    }

    public static string BuildData()
    {
        foreach (string dir in new[] { DataDir + "/Items", DataDir + "/Bait", DataDir + "/Fish" })
            EnsureFolder(dir);
        Item("Fishing Rod", ItemType.tool, InteractionType.usetool, 0, 0, "IconFishingRod", false);
        InventoryItemData worms = Item("Worms", ItemType.tool, InteractionType.buy, 2, 0, "IconWorms", true);
        InventoryItemData lure = Item("Lure", ItemType.tool, InteractionType.buy, 45, 0, "IconLure", false);
        InventoryItemData oldLureItem = Item("Old Lure", ItemType.tool, InteractionType.usetool, 0, 0, "IconOldLure", false);
        //typical sale price: a middling fish of each kind at its price per pound
        InventoryItemData bass = Item("Largemouth Bass", ItemType.harvest, InteractionType.sell, 0, 15, "IconBass", true);
        InventoryItemData gill = Item("Bluegill", ItemType.harvest, InteractionType.sell, 0, 4, "IconBluegill", true);
        InventoryItemData crappie = Item("Crappie", ItemType.harvest, InteractionType.sell, 0, 7, "IconCrappie", true);
        InventoryItemData cat = Item("Catfish", ItemType.harvest, InteractionType.sell, 0, 16, "IconCatfish", true);

        //a lure lasts LureCasts casts, then the lure turns into an old lure (with a fresh LureCasts) and the old lure breaks
        BaitData lureBait = Bait("Lure", lure, true, LureCasts, 45, new Vector2(5f, 11f), 0.62f, 1.0f, HookPrefabName("Lure"));
        BaitData worm = Bait("Worm", worms, false, 0, 2, new Vector2(3f, 7f), 0.9f, 1.3f, HookPrefabName("Worm"));
        BaitData old = Bait("Old Lure", oldLureItem, true, LureCasts, 0, new Vector2(7f, 14f), 0.5f, 1.6f, HookPrefabName("Old Lure"));
        lureBait.wornInto = old;
        old.wornInto = null;
        worm.wornInto = null;

        Fish("Largemouth Bass", bass, 1.0f, 8.0f, 5.0f, 6, new[] { Taste(old, 3f), Taste(lureBait, 5f, 0.7f), Taste(worm, 1.5f) },
            1f, 1.7f, 1f, 0.4f, 0.75f, 1, 1.0f, new Vector2(2.4f, 4.2f), new Vector2(0.5f, 0.9f), new Vector2(0.30f, 0.62f));
        Fish("Bluegill", gill, 0.2f, 1.2f, 1.0f, 7, new[] { Taste(old, 2f), Taste(lureBait, 1.5f), Taste(worm, 5f) },
            0.8f, 1.3f, 0.8f, 1.3f, 1.0f, 2, 0.45f, new Vector2(1.5f, 2.2f), new Vector2(0.35f, 0.6f), new Vector2(0.13f, 0.26f));
        Fish("Crappie", crappie, 0.4f, 2.2f, 1.8f, 7, new[] { Taste(old, 3f), Taste(lureBait, 4f), Taste(worm, 2f) },
            1.2f, 1.2f, 1.2f, 1.6f, 0.9f, 2, 0.6f, new Vector2(1.7f, 2.6f), new Vector2(0.4f, 0.7f), new Vector2(0.18f, 0.36f));
        Fish("Catfish", cat, 1.5f, 12.0f, 8.0f, 4, new[] { Taste(old, 0.6f), Taste(lureBait, 0.5f), Taste(worm, 4f) },
            1.7f, 0.8f, 2.0f, 0.6f, 0.85f, 1, 0.9f, new Vector2(2.8f, 4.6f), new Vector2(0.7f, 1.1f), new Vector2(0.34f, 0.75f));
        return "data: 8 items, 3 baits, 4 fish";
    }

    public const int LureCasts = 30;
}
