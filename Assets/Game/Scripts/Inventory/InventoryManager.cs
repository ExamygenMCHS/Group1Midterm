using System.Collections.Generic;
using UnityEngine;

public class InventoryManager : MonoBehaviour
{
    public bool isDebug=false;
    public GameObject[] inventorySlots;
    public GameObject inventoryMain;//will hide it after grabbing all the inventory slots in start
    public GameObject inventoryItemPrefab;
    private List<GameObject> itemInBagList= new List<GameObject>();

    public void Start()
    {
        inventoryMain.SetActive(true);//temporary turn on the Main bag so that all the slots gets stored in the list
        inventorySlots = GameObject.FindGameObjectsWithTag("InventorySlot");
        inventoryMain.SetActive(false);
        //FindGameObjectsWithTag returns the slots in no particular order: the new items should fill the inventory bar
        //(always on screen) from the left first, then the main bag
        inventorySlots = BarFirst(inventorySlots);
    }

    //this bag's slots: the ones outside the main bag (the inventory bar) first, each group in hierarchy order
    private GameObject[] BarFirst(GameObject[] slots)
    {
        List<GameObject> mine = new List<GameObject>();
        foreach (GameObject slot in slots)
            if (slot.transform.IsChildOf(transform))
                mine.Add(slot);
        mine.Sort((a, b) =>
        {
            int inMainA = a.transform.IsChildOf(inventoryMain.transform) ? 1 : 0;
            int inMainB = b.transform.IsChildOf(inventoryMain.transform) ? 1 : 0;
            if (inMainA != inMainB)
                return inMainA - inMainB;
            if (a.transform.parent != b.transform.parent)
                return a.transform.parent.GetSiblingIndex() - b.transform.parent.GetSiblingIndex();
            return a.transform.GetSiblingIndex() - b.transform.GetSiblingIndex();
        });
        return mine.ToArray();
    }
    public bool AddItem(InventoryItemData itemData) {
        if (isDebug)
            Debug.Log("InventoryManager adding item checking if existed: " + itemData.itemName);
        return AddItems(itemData, 1) == 1;
    }

    //API for other parts of the program to add several of an item at once (a sale paid in coins). Existing stacks of
    //the item fill up first; a full stack (coins: 1000 per slot) makes the rest start a new stack in the next empty
    //slot. Returns how many were added (fewer than 'amount' when the bag runs out of room).
    public int AddItems(InventoryItemData itemData, int amount)
    {
        int added = 0;
        foreach (GameObject stack in itemInBagList)//if this item already exists in the bag, just refresh the counter
            if (added < amount && stack != null && stack.name == itemData.itemName)
                added += stack.GetComponent<InventoryItem>().AddAmount(amount - added);
        while (added < amount)
        {
            InventoryItem stack = AddNewItem(itemData);//find an empty slot to start a new stack in
            if (stack == null)
                break;//the slots are all full
            added += 1 + stack.AddAmount(amount - added - 1);
        }
        return added;
    }

    private InventoryItem AddNewItem(InventoryItemData itemData) {
        //find an empty slot in the list of inventory slots.
        foreach (var slot in inventorySlots)
        {
            if (slot.GetComponent<InventorySlot>().IsEmpty())//a dragged item keeps its source slot reserved
            {
                if (isDebug)
                    Debug.Log("Add item to empty slot " + slot.name);
                return SpawnANewItem(itemData, slot);//stop the loop once 1 empty slot is found
            }
        }
        return null;//the slots are all full, give user a warning message
    }
    private InventoryItem SpawnANewItem(InventoryItemData itemData, GameObject emptySlot) {
        GameObject newItem = Instantiate(inventoryItemPrefab, emptySlot.transform);
        InventoryItem item = newItem.GetComponent<InventoryItem>();
        item.Setup(itemData);//names the stack now, so the next AddItem finds it
        //add to the list of items 
        itemInBagList.Add(newItem);
        return item;
    }

    //API for other parts of the program to remove an item from the inventory, like selling or planting
    public bool RemoveItem(string itemName) {
        if (isDebug)
            Debug.Log("InventoryManager removeing item: " + itemName);
        return RemoveItems(itemName, 1) == 1;
    }

    //API for other parts of the program to remove several of an item at once (paying with coins). The newest stack
    //goes first, so the bag keeps its full stacks; an emptied stack leaves its slot. Returns how many were removed.
    public int RemoveItems(string itemName, int amount)
    {
        int removed = 0;
        for (int index = itemInBagList.Count - 1; index >= 0 && removed < amount; index--)
        {
            GameObject item = itemInBagList[index];
            if (item == null || item.name != itemName)
                continue;
            InventoryItem stack = item.GetComponent<InventoryItem>();
            removed += stack.RemoveAmount(amount - removed);
            if (stack.GetAmount() == 0) //item count==0, also remove the sprite from the slot
            {
                //out of the slot right now, not at the end of the frame: coins earned in the same frame may use the slot again
                item.SetActive(false);
                item.transform.SetParent(transform, false);
                Destroy(item);
                itemInBagList.RemoveAt(index);
            }
        }
        return removed;
    }

    //API for other parts of the program to remove one particular UI item (a lure with its own durability), not the
    //newest stack of its name. Returns false when the bag does not hold it.
    public bool RemoveItem(GameObject item)
    {
        int index = itemInBagList.IndexOf(item);
        if (item == null || index < 0)
            return false;
        item.SetActive(false);
        item.transform.SetParent(transform, false);
        Destroy(item);
        itemInBagList.RemoveAt(index);
        return true;
    }

    //the UI items of an item in the bag, oldest first (the order they were added in)
    public List<GameObject> GetItems(string itemName)
    {
        List<GameObject> found = new List<GameObject>();
        foreach (GameObject item in itemInBagList)
            if (item != null && item.name == itemName)
                found.Add(item);
        return found;
    }

    //API for other parts of the program to read the bag: how many of an item it holds in all its stacks (0 when it has none)
    public int GetAmount(string itemName)
    {
        long total = 0;
        foreach (GameObject item in itemInBagList)
            if (item != null && item.name == itemName)
                total += item.GetComponent<InventoryItem>().GetAmount();
        return (int)System.Math.Min(total, int.MaxValue);
    }

    //true when this UI item is one of the bag's stacks (an item may have several, like coins)
    public bool Contains(GameObject item)
    {
        return item != null && itemInBagList.Contains(item);
    }

    //the UI item (sprite + counter) of an item in the bag (its first stack), or null when the bag does not contain it
    public GameObject GetItem(string itemName)
    {
        foreach (GameObject item in itemInBagList)
            if (item != null && item.name == itemName)
                return item;
        return null;
    }
}
