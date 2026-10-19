using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;

//links the instructor's inventory bag to the farm. The Farm Fund's inventory keeps the counts (so they carry from scene
//to scene and come back with Try Again); this script makes the bag show them: every time the inventory changes it adds
//or removes items through the InventoryManager's AddItem / RemoveItem API, the same calls the testing canvas makes,
//until the bag holds the tools, the seeds and the harvested plants the player owns.
//The bag only shows once the player has bought it in the store. B or the bag button opens the main bag.
//In a farming level the inventory bar is the hotbar: 1-8 or the mouse wheel select a slot (Hotbar), and the item in the
//selected slot moves to the hand slot at the bottom left (HoldingUI) by changing its parent. A faded copy marks the bar
//slot it came from, and the item goes back there when another slot is selected.
[DefaultExecutionOrder(50)]//after the InventoryManager has collected its slots in Start
public class FarmBag : MonoBehaviour
{
    [SerializeField]
    private InventoryManager inventoryManager;
    [SerializeField]
    private Canvas bagCanvas;//hidden until the player owns the bag
    [SerializeField]
    private GameObject background;//dark screen behind the open main bag
    [SerializeField]
    private GameObject bagButton;//the bag button that opens the main bag
    [SerializeField]
    private Key openKey = Key.B;

    [Header("Items")]
    [SerializeField]
    private InventoryItemData hoeItem;
    [SerializeField]
    private InventoryItemData wateringCanItem;
    [SerializeField]
    private InventoryItemData sickleItem;
    [Tooltip("The seeds of these plants go into the bag in this order (after the hoe and the can, before the sickle), their harvest last")]
    [SerializeField]
    private PlantData[] crops;

    [Header("Farming level (leave empty in the store)")]
    [Tooltip("The hoe, the watering can and the sickle are given to the player when the level starts")]
    [SerializeField]
    private bool giveToolsAtStart = false;
    [SerializeField]
    private Hotbar hotbar;//selects the bar slot whose item is in the hand
    [SerializeField]
    private GameObject holdingUI;//bottom left: what the farmer holds
    [SerializeField]
    private Transform handSlot;//the item in the farmer's hand
    [SerializeField]
    private Image handFrame;//gold outline on the hand slot while it holds something
    [SerializeField]
    private GameObject barNumbers;//1-8 on the bar slots
    [SerializeField]
    private RectTransform selectedFrame;//gold outline on the selected bar slot
    [SerializeField]
    private TMP_Text itemNameLabel;//the name of the item just selected, above the bar
    [SerializeField]
    private float itemNameSeconds = 1.6f;
    [SerializeField]
    private float placeholderAlpha = 0.3f;

    //this event gets fired after the bag has redrawn its items or the player has moved items between slots
    public UnityEvent bagChangedEvent = new UnityEvent();

    //an item taken out of the bag into the hand slot
    private class Held
    {
        public GameObject item;
        public GameObject placeholder;//faded copy in the bag slot the item came from
        public TMP_Text placeholderCount;//the stack's count on the faded copy (seeds and crops stay counted in the bar)
    }
    private readonly Held held = new Held();
    private FarmInventory inventory;
    private GraphicRaycaster raycaster;
    private int barSlots = 0;
    private GameObject[] barBefore;
    private float itemNameLeft = 0f;

    private void Awake()
    {
        if (bagCanvas != null)
            raycaster = bagCanvas.GetComponent<GraphicRaycaster>();
    }

    private void Start()
    {
        foreach (GameObject slot in inventoryManager.inventorySlots)
            if (!slot.transform.IsChildOf(inventoryManager.inventoryMain.transform))
                barSlots++;
        barBefore = new GameObject[barSlots];
        bool level = hotbar != null;
        if (holdingUI != null)
            holdingUI.SetActive(level);
        if (barNumbers != null)
            barNumbers.SetActive(level);
        if (selectedFrame != null)
            selectedFrame.gameObject.SetActive(level);
        if (itemNameLabel != null)
            itemNameLabel.text = "";
        if (level)
            hotbar.selectedSlotChangedEvent.AddListener(OnSelectedSlotChanged);
        if (FarmFund.Instance != null)
        {
            inventory = FarmFund.Instance.Inventory;
            if (giveToolsAtStart)//before listening: the bag is filled once, in order
                GiveTools();
            inventory.inventoryChangedEvent.AddListener(OnInventoryChanged);
        }
        OnInventoryChanged();
    }

    private void OnDestroy()
    {
        if (inventory != null)
            inventory.inventoryChangedEvent.RemoveListener(OnInventoryChanged);
        if (hotbar != null)
            hotbar.selectedSlotChangedEvent.RemoveListener(OnSelectedSlotChanged);
    }

    private void Update()
    {
        if (IsShown() && Keyboard.current != null && Keyboard.current[openKey].wasPressedThisFrame)
            SetBagOpen(!IsBagOpen());
        if (itemNameLabel != null && itemNameLeft > 0f)
        {
            itemNameLeft -= Time.unscaledDeltaTime;
            itemNameLabel.alpha = Mathf.Clamp01(itemNameLeft / 0.4f);
        }
    }

    //the player drags items between slots (the instructor's drag and drop): when a drag ends, the bar may hold other items
    private void LateUpdate()
    {
        if (Mouse.current != null && Mouse.current.leftButton.wasReleasedThisFrame)
            CheckBarChanged();
    }

    //the level gives the three tools to the player directly (they land in the inventory bar)
    private void GiveTools()
    {
        foreach (ToolManager.ToolType tool in new[] { ToolManager.ToolType.Hoe, ToolManager.ToolType.WateringCan, ToolManager.ToolType.Sickle })
            if (!inventory.HasTool(tool))
                inventory.AddTool(tool);
    }

    // ---------------------------------------------------------------- the bag follows the inventory

    //called by the Farm Fund's inventory every time a tool, seed, harvest or upgrade count changes
    private void OnInventoryChanged()
    {
        ShowBag(inventory != null && inventory.bag);
        if (inventory != null)
        {
            //the order of the hotbar in the levels before: hoe, watering can, the seeds, sickle; the harvest after them
            List<PlantData> allCrops = AllCrops();
            SyncItem(hoeItem, inventory.hoe ? 1 : 0);
            SyncItem(wateringCanItem, inventory.wateringCan ? 1 : 0);
            foreach (PlantData crop in allCrops)
                SyncItem(crop.seedItem, inventory.GetSeeds(crop));
            SyncItem(sickleItem, inventory.sickle ? 1 : 0);
            foreach (PlantData crop in allCrops)
                SyncItem(crop.harvestItem, inventory.GetHarvested(crop));
        }
        RememberBar();
        bagChangedEvent.Invoke();//make announcement to all listeners (the hotbar reads the bar again)
        UpdateHand();
    }

    //adds or removes one item at a time through the instructor's API until the bag holds 'wanted' of it
    private void SyncItem(InventoryItemData item, int wanted)
    {
        if (item == null)
            return;
        int have = inventoryManager.GetAmount(item.itemName);
        while (have < wanted && inventoryManager.AddItem(item))
            have++;
        while (have > wanted && inventoryManager.RemoveItem(item.itemName))
            have--;
    }

    //the plants listed here first, then any other plant the player has seeds or harvest of
    private List<PlantData> AllCrops()
    {
        List<PlantData> all = new List<PlantData>();
        if (crops != null)
            foreach (PlantData crop in crops)
                if (crop != null && !all.Contains(crop))
                    all.Add(crop);
        foreach (FarmInventory.CropStock stock in inventory.crops)
            if (stock.crop != null && !all.Contains(stock.crop))
                all.Add(stock.crop);
        return all;
    }

    // ---------------------------------------------------------------- what lies in the bar (the hotbar's slots)

    //the item in a bag slot: the item lying there, or the item in the hand when the slot holds its faded copy
    private GameObject ItemIn(int index)
    {
        if (inventoryManager.inventorySlots == null || index < 0 || index >= inventoryManager.inventorySlots.Length)
            return null;
        foreach (Transform child in inventoryManager.inventorySlots[index].transform)
        {
            if (held.placeholder != null && child.gameObject == held.placeholder)
                return InBag(held.item) ? held.item : null;
            if (child.GetComponent<InventoryItem>() != null && InBag(child.gameObject))
                return child.gameObject;
        }
        return null;
    }

    private bool InBag(GameObject item)
    {
        return item != null && inventoryManager.GetItem(item.name) == item;
    }

    //what a bar slot holds, as the hotbar sees it: a tool, seeds of a plant, or a harvested plant (no tool)
    public Hotbar.Slot GetBarSlot(int index)
    {
        Hotbar.Slot slot = new Hotbar.Slot();
        if (index >= barSlots)
            return slot;
        GameObject item = ItemIn(index);
        if (item == null)
            return slot;
        InventoryItemData data = item.GetComponent<InventoryItem>().itemData;
        slot.item = data;
        if (data == hoeItem)
            slot.tool = ToolManager.ToolType.Hoe;
        else if (data == wateringCanItem)
            slot.tool = ToolManager.ToolType.WateringCan;
        else if (data == sickleItem)
            slot.tool = ToolManager.ToolType.Sickle;
        else
            foreach (PlantData crop in AllCrops())
                if (crop.seedItem == data)
                {
                    slot.tool = ToolManager.ToolType.SeedBag;
                    slot.crop = crop;
                }
        return slot;
    }

    //true when the tool (or any seeds) lies in the main bag, outside the bar
    public bool HasInMainBag(ToolManager.ToolType tool)
    {
        for (int i = barSlots; i < inventoryManager.inventorySlots.Length; i++)
        {
            GameObject item = ItemIn(i);
            if (item == null)
                continue;
            InventoryItemData data = item.GetComponent<InventoryItem>().itemData;
            if (data == ToolItem(tool))
                return true;
            if (tool == ToolManager.ToolType.SeedBag)
                foreach (PlantData crop in AllCrops())
                    if (crop.seedItem == data)
                        return true;
        }
        return false;
    }

    //called when the player may have moved items between slots: the hotbar reads the bar again if it changed
    public void CheckBarChanged()
    {
        for (int i = 0; i < barSlots; i++)
            if (ItemIn(i) != barBefore[i])
            {
                RememberBar();
                bagChangedEvent.Invoke();
                UpdateHand();
                return;
            }
    }

    private void RememberBar()
    {
        for (int i = 0; i < barSlots; i++)
            barBefore[i] = ItemIn(i);
    }

    // ---------------------------------------------------------------- showing and opening the bag

    private void ShowBag(bool owned)
    {
        if (bagCanvas != null)
            bagCanvas.enabled = owned;
        if (raycaster != null)
            raycaster.enabled = owned;
        if (!owned && IsBagOpen())
            SetBagOpen(false);
    }

    //true once the player owns the bag
    public bool IsShown()
    {
        return bagCanvas == null || bagCanvas.enabled;
    }

    public bool IsBagOpen()
    {
        return inventoryManager.inventoryMain.activeSelf;
    }

    //opens or closes the main bag (what the bag button and the dark background do)
    public void SetBagOpen(bool open)
    {
        inventoryManager.inventoryMain.SetActive(open);
        if (background != null)
            background.SetActive(open);
        if (bagButton != null)
            bagButton.SetActive(!open);
    }

    // ---------------------------------------------------------------- the hand slot at the bottom left

    private void OnSelectedSlotChanged()
    {
        UpdateHand();
        ShowItemName();
    }

    //the item of the selected bar slot goes into the hand slot (called after every change of the bar or the selection)
    public void UpdateHand()
    {
        if (hotbar == null || handSlot == null)
            return;
        int selected = hotbar.GetSelectedSlot();
        Hold(selected < barSlots ? ItemIn(selected) : null);
        if (held.placeholderCount != null && held.item != null)
            held.placeholderCount.text = held.item.GetComponent<InventoryItem>().counterText.text;
        if (handFrame != null)
            handFrame.enabled = held.item != null;
        if (selectedFrame != null && selected < barSlots)
        {
            RectTransform slot = (RectTransform)inventoryManager.inventorySlots[selected].transform;
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)slot.parent);//the bar's grid has placed its slots
            selectedFrame.position = slot.TransformPoint(slot.rect.center);
        }
    }

    //moves 'item' into the hand slot by changing its parent; the item that was there goes back into the bag
    private void Hold(GameObject item)
    {
        if (held.item == item && (item == null || item.transform.parent == handSlot))
            return;
        PutBack();
        if (item == null)
            return;
        held.item = item;
        held.placeholder = MakePlaceholder(item, item.transform.parent);
        //the count stays on the bar slot too, in full colour on the faded copy (a copy of the item's own counter)
        TMP_Text counter = item.GetComponent<InventoryItem>().counterText;
        if (held.placeholder != null && counter != null)
        {
            held.placeholderCount = Instantiate(counter, held.placeholder.transform, false);
            held.placeholderCount.name = counter.name;
        }
        item.transform.SetParent(handSlot, false);
        item.transform.localScale = Vector3.one;
        SetDraggable(item, false);
    }

    private void PutBack()
    {
        GameObject item = held.item;
        Transform home = held.placeholder != null ? held.placeholder.transform.parent : null;
        if (held.placeholder != null)
        {
            held.placeholder.SetActive(false);
            held.placeholder.transform.SetParent(transform, false);//the slot is empty again right now, not at the end of the frame
            Destroy(held.placeholder);
        }
        held.item = null;
        held.placeholder = null;
        held.placeholderCount = null;
        if (!InBag(item))
            return;//used up while it was in the hand (the bag removed it)
        if (home == null || home.childCount > 0)
            home = FirstEmptySlot();
        if (home == null)
            return;
        item.transform.SetParent(home, false);
        item.transform.localScale = Vector3.one;
        SetDraggable(item, true);
    }

    //a faded copy of the item keeps its bag slot, so new items do not fill it while the item is in the hand
    private GameObject MakePlaceholder(GameObject item, Transform home)
    {
        if (home == null || home.GetComponent<InventorySlot>() == null)
            return null;
        GameObject placeholder = new GameObject(item.name + " (in hand)", typeof(RectTransform), typeof(Image));
        placeholder.transform.SetParent(home, false);
        Image source = item.GetComponent<Image>();
        Image image = placeholder.GetComponent<Image>();
        image.sprite = source.sprite;
        image.preserveAspect = source.preserveAspect;
        image.color = new Color(1f, 1f, 1f, placeholderAlpha);
        image.raycastTarget = false;
        return placeholder;
    }

    private Transform FirstEmptySlot()
    {
        foreach (GameObject slot in inventoryManager.inventorySlots)
            if (slot != null && slot.transform.childCount == 0)
                return slot.transform;
        return null;
    }

    //an item in the hand cannot be dragged (it would leave the hand slot behind)
    private static void SetDraggable(GameObject item, bool draggable)
    {
        InventoryItemDrag drag = item.GetComponent<InventoryItemDrag>();
        if (drag != null)
            drag.enabled = draggable;
        Image image = item.GetComponent<Image>();
        if (image != null)
            image.raycastTarget = draggable;
    }

    //"Watering Can", "Sunflower Seeds  x4", "Pumpkin  x2" above the bar for a moment, like the hotbar did
    private void ShowItemName()
    {
        if (itemNameLabel == null)
            return;
        Hotbar.Slot slot = hotbar.GetSlot(hotbar.GetSelectedSlot());
        string text = "Empty hand";
        if (slot.item != null)
        {
            text = slot.item.itemName;
            if (slot.item.stackable)
                text += "  x" + inventoryManager.GetAmount(slot.item.itemName);
        }
        itemNameLabel.text = text;
        itemNameLeft = itemNameSeconds;
    }

    //the bag item of a tool (null for the seed bag and empty hands)
    public InventoryItemData ToolItem(ToolManager.ToolType tool)
    {
        switch (tool)
        {
            case ToolManager.ToolType.Hoe: return hoeItem;
            case ToolManager.ToolType.WateringCan: return wateringCanItem;
            case ToolManager.ToolType.Sickle: return sickleItem;
        }
        return null;
    }

    // ---------------------------------------------------------------- API for the tests and the HUD

    //the bag item in the hand slot at the bottom left (null when the hand is empty)
    public GameObject GetItemInHand()
    {
        return held.item;
    }
    public int GetBarSlotCount()
    {
        return barSlots;
    }
    public InventoryManager GetInventoryManager()
    {
        return inventoryManager;
    }
}
