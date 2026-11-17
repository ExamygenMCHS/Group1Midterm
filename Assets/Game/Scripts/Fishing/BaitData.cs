using UnityEngine;

//what goes on the end of the fishing line. A lure is reusable but fish are pickier about it, and it wears out after a
//number of casts; a worm is eaten by the fish that bites, but far more fish take it. Each fish (FishData) says how much
//it likes each bait. The player loads a bait into the rod by dragging its bag item onto the fishing rod.
[CreateAssetMenu(fileName = "BaitData", menuName = "Scriptable Objects/BaitData")]
public class BaitData : ScriptableObject
{
    [Header("Game Play")]
    public string baitName = "Worm";
    [Tooltip("The item in the bag")]
    public InventoryItemData item;
    [Tooltip("On: stays on the line cast after cast. Off: the fish that bites eats it")]
    public bool reusable = false;
    [Tooltip("How many casts one of it lasts (0: it does not wear out, like the worms)")]
    public int durability = 0;
    [Tooltip("What a worn-out one turns into, with a fresh durability (empty: it breaks and is gone)")]
    public BaitData wornInto;
    public int price = 2;//in the trader's shop

    [Header("Bites")]
    [Tooltip("Seconds until a fish comes to look at the bait (shortest, longest)")]
    public Vector2 waitSeconds = new Vector2(3f, 7f);
    [Tooltip("How often a fish that swims up really bites, instead of turning away")]
    [Range(0f, 1f)]
    public float biteChance = 0.9f;
    [Tooltip("Above 1: mostly small fish. Below 1: big fish get likely")]
    public float sizeSkew = 1.3f;

    [Header("Visuals")]
    [Tooltip("Optional: the small model that hangs under the bobber")]
    public GameObject hookModel;

    //"worm" / "worms"
    public string NameFor(int amount)
    {
        return amount == 1 ? baitName : baitName + "s";
    }

    //true for a bait that is used up cast by cast (a lure), false for one that is counted (worms)
    public bool Wears()
    {
        return durability > 0;
    }
}
