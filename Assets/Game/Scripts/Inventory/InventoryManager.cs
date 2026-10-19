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
        for (int index=0;index<itemInBagList.Count;index++)//if this item already exist in the bag, just refresh counter
        {
            if (itemInBagList[index].name == itemData.itemName)
            {
                AddExistingItem(index,itemData);
                return true;
            }
        }
        return AddNewItem(itemData);//if this item is a new item, find a empty slot to add it in
    }
    private void AddExistingItem(int index,InventoryItemData itemData)
    {
        itemInBagList[index].GetComponent<InventoryItem>().AddOneItem();
    }
    private bool AddNewItem(InventoryItemData itemData) {
        //find an empty slot in the list of inventory slots.
        foreach (var slot in inventorySlots)
        {
            if (slot.transform.childCount == 0)//no child object means an empty slot
            {
                if (isDebug)
                    Debug.Log("Add item to empty slot " + slot.name);
                SpawnANewItem(itemData, slot);
                return true;//stop the loop once 1 empty slot is found
            }
        }
        return false;//the slots are all full, give user a warning message
    }
    private void SpawnANewItem(InventoryItemData itemData, GameObject emptySlot) {
        GameObject newItem = Instantiate(inventoryItemPrefab, emptySlot.transform);
        newItem.GetComponent<InventoryItem>().Setup(itemData);//names the stack now, so the next AddItem finds it
        //add to the list of items 
        itemInBagList.Add(newItem);
    }

    //API for other parts of the program to remove an item from the inventory, like selling or planting
    public bool RemoveItem(string itemName) {
        if (isDebug)
            Debug.Log("InventoryManager removeing item: " + itemName);
        for (int index = 0; index < itemInBagList.Count; index++)//if this item already exist in the bag, just refresh counter
        {
            if (itemInBagList[index].name == itemName)
            {
                GameObject item= itemInBagList[index];
                bool successfulRemove=item.GetComponent<InventoryItem>().RemoveOneItem();

                int amountInBag = item.GetComponent<InventoryItem>().GetAmount();
                if (amountInBag == 0) //item count==0, also remove the sprite from the slot
                {
                    Destroy(item);
                    itemInBagList.RemoveAt(index);
                }
                return successfulRemove;
           
            }
        }
        return false;//the bag does not contain the item to be removed
    }

    //API for other parts of the program to read the bag: how many of an item it holds (0 when it has none)
    public int GetAmount(string itemName)
    {
        GameObject item = GetItem(itemName);
        return item != null ? item.GetComponent<InventoryItem>().GetAmount() : 0;
    }

    //the UI item (sprite + counter) of an item in the bag, or null when the bag does not contain it
    public GameObject GetItem(string itemName)
    {
        foreach (GameObject item in itemInBagList)
            if (item != null && item.name == itemName)
                return item;
        return null;
    }
}
