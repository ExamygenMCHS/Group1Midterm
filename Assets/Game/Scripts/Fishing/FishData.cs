using System;
using UnityEngine;

//one kind of fish in the pond: how big it gets, what it sells for, which bait it likes, where and when it feeds, and
//how hard it bites and fights. The FishingControl reads these when a fish comes to the bait.
[CreateAssetMenu(fileName = "FishData", menuName = "Scriptable Objects/FishData")]
public class FishData : ScriptableObject
{
    [Serializable]
    public class BaitTaste
    {
        public BaitData bait;
        [Tooltip("How likely this fish is the one that comes to this bait (compared with the other fish)")]
        public float weight = 1f;
        [Tooltip("Multiplies the bait's size skew for this fish. Below 1: this bait brings the big ones")]
        public float sizeSkew = 1f;
    }

    [Header("Game Play")]
    public string fishName = "Bluegill";
    public InventoryItemData item;//the fish in the bag
    [Tooltip("Pounds: the smallest and the biggest one in the pond")]
    public float minWeight = 0.2f;
    public float maxWeight = 1.2f;
    [Tooltip("From this weight on the catch is a trophy")]
    public float trophyWeight = 1f;
    public int pricePerPound = 6;

    [Header("Bait")]
    public BaitTaste[] baits;

    [Header("Where and when it feeds (1 = no difference)")]
    public float deepWater = 1f;//the dark middle of the pond
    public float nearCover = 1f;//next to a lily pad
    public float atNight = 1f;
    public float underIce = 1f;//in winter, through the fishing hole

    [Header("Bite and fight")]
    [Tooltip("Seconds the bobber stays under before the fish lets go")]
    public float biteWindowSeconds = 0.9f;
    [Tooltip("How many times it may tap the bait before it bites")]
    public int maxNibbles = 2;
    [Tooltip("How fast the line strains while it pulls (1 = a largemouth bass)")]
    public float strength = 0.6f;
    [Tooltip("Seconds a clean fight with the smallest one takes, and with the biggest one")]
    public Vector2 fightSeconds = new Vector2(1.6f, 2.6f);
    [Tooltip("Seconds one pull lasts (shortest, longest)")]
    public Vector2 surgeSeconds = new Vector2(0.45f, 0.8f);

    [Header("Visuals")]
    public GameObject modelPrefab;//1 unit long, nose toward +z; shown when the fish comes out of the water
    [Tooltip("Metres from nose to tail: the smallest one and the biggest one")]
    public Vector2 lengthMetres = new Vector2(0.18f, 0.3f);

    public float TasteFor(BaitData bait)
    {
        BaitTaste taste = FindTaste(bait);
        return taste != null ? Mathf.Max(0f, taste.weight) : 0f;
    }

    public float SizeSkewFor(BaitData bait)
    {
        BaitTaste taste = FindTaste(bait);
        float skew = (bait != null ? bait.sizeSkew : 1f) * (taste != null ? taste.sizeSkew : 1f);
        return Mathf.Max(0.05f, skew);
    }

    private BaitTaste FindTaste(BaitData bait)
    {
        if (baits != null)
            foreach (BaitTaste taste in baits)
                if (taste != null && taste.bait == bait)
                    return taste;
        return null;
    }

    //a weight for a roll from 0 to 1; the skew bends the roll toward the small or the big end
    public float WeightFor(float roll, float skew)
    {
        float t = Mathf.Pow(Mathf.Clamp01(roll), Mathf.Max(0.05f, skew));
        return Mathf.Round(Mathf.Lerp(minWeight, maxWeight, t) * 10f) / 10f;//tenths of a pound, like a tackle-shop scale
    }

    //0 for the smallest one, 1 for the biggest
    public float Size01(float weight)
    {
        return maxWeight > minWeight ? Mathf.Clamp01((weight - minWeight) / (maxWeight - minWeight)) : 0f;
    }

    public float LengthFor(float weight)
    {
        return Mathf.Lerp(lengthMetres.x, lengthMetres.y, Mathf.Sqrt(Size01(weight)));
    }

    public bool IsTrophy(float weight)
    {
        return weight >= trophyWeight;
    }

    //what the trader pays for one fish of this weight
    public int PriceFor(float weight)
    {
        return Mathf.Max(1, Mathf.RoundToInt(weight * pricePerPound));
    }

    //"3.4 lb"
    public static string WeightText(float weight)
    {
        return weight.ToString("0.0") + " lb";
    }
}
