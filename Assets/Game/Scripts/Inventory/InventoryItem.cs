using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class InventoryItem : MonoBehaviour
{
    public InventoryItemData itemData;//use the scriptable object
    public TMP_Text counterText;

    //how many of this item the stack holds. It is kept here and not on the itemData asset: the asset is shared by every
    //stack and every scene, and a value changed on it in Play mode stays changed in the editor afterwards
    private int amountInBag = 0;

    //API called by the inventory manager right after it spawns the item. The name is set here instead of in Start, so a
    //second AddItem in the same frame (buying 5 seeds at once) already finds this stack and adds to it
    public void Setup(InventoryItemData data)
    {
        itemData = data;
        gameObject.GetComponent<Image>().sprite = itemData.itemSprite;
        transform.name = itemData.itemName;
        amountInBag = 1;
        RefreshUICounter();
    }

    public int GetAmount()
    {
        return amountInBag;
    }

    //API called by the inventory manager when adding an item that is already stored in the bag
    public void AddOneItem()
    {
        amountInBag++;
        RefreshUICounter();
    }
    //API called by the inventory manager when removing an item that is already stored in the bag
    public bool RemoveOneItem()
    {
        if (amountInBag > 0)
        {
            amountInBag--;
            RefreshUICounter();
            return true;
        }
        else
            return false;
    }
    private void RefreshUICounter() {
        //a tool is one of a kind (not stackable), so it shows no counter
        counterText.text = itemData != null && !itemData.stackable ? "" : amountInBag.ToString();
    }
}
