using UnityEngine;

[CreateAssetMenu(fileName = "InventoryItemData", menuName = "Scriptable Objects/InventoryItemData")]
public class InventoryItemData : ScriptableObject
{
    [Header ("Game Play")]
    public string itemName;//will be used as ID

    public int buyPrice;
    public int sellPrice;

    public ItemType itemType;
    public InteractionType interactionType;

    [Header("UI")]
    //sprites used by the inventory
    public Sprite itemSprite;
    public bool stackable = true;
    [Tooltip("How many fit in one slot (0 = no limit). Coins stop at 1000: the next coin starts a new stack in another slot")]
    public int maxStack = 0;

    //how many more of this item one stack holding 'amount' can take
    public int SpaceLeft(int amount)
    {
        if (!stackable)
            return amount > 0 ? 0 : 1;
        return maxStack > 0 ? Mathf.Max(0, maxStack - amount) : int.MaxValue - amount;
    }

}
//A plant can only be planted in the right season and all plants disappear in winter
public enum ItemType
{
    tool,seed,harvest,coin
}

//you can change the usetool type to different type of interactions for different tools
public enum InteractionType
{
    usetool,buy,sell
}
