using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

//the bait in the inventory bag: it marks the items (BaitBadge: the bait loaded into the fishing rod on the rod's icon
//and on its faded copy, the durability bar on every lure) and shows a small tooltip by the mouse over a lure, the
//worms or the rod ("Lure   23 / 30"). It sits on the bag next to the FarmBag and only reads: the bait itself changes
//in the Farm Fund's inventory, and FarmBag loads a bait dropped on the rod.
//The marks are redrawn after the inventory or the bag changed (at the end of the frame, once the bag has its items).
[DefaultExecutionOrder(60)]//after the FarmBag has redrawn its items and checked the bar
public class BaitBagUI : MonoBehaviour
{
    [SerializeField]
    private FarmBag bag;
    [SerializeField]
    private Canvas bagCanvas;

    [Header("Marks on the items")]
    [SerializeField]
    private TMP_FontAsset font;
    [SerializeField]
    private Material fontMaterial;
    [SerializeField]
    private Sprite plateSprite;

    [Header("Tooltip")]
    [SerializeField]
    private RectTransform tooltip;//pivot bottom left, sized by its content
    [SerializeField]
    private CanvasGroup tooltipGroup;
    [SerializeField]
    private TMP_Text tooltipLabel;
    [Tooltip("From the mouse to the tooltip's corner: high enough to clear the top of the slot under the mouse and its number")]
    [SerializeField]
    private Vector2 tooltipOffset = new Vector2(10f, 34f);
    [SerializeField]
    private string numberColor = "#FFD45E";

    private FarmInventory inventory;
    private GraphicRaycaster raycaster;
    private PointerEventData pointer;
    private readonly List<RaycastResult> hits = new List<RaycastResult>();
    private bool dirty = true;
    private GameObject lastPlaceholder;
    private string shownTip;

    private void Awake()
    {
        if (bagCanvas != null)
            raycaster = bagCanvas.GetComponent<GraphicRaycaster>();
        if (tooltipGroup != null)
        {
            tooltipGroup.alpha = 0f;
            tooltipGroup.blocksRaycasts = false;
            tooltipGroup.interactable = false;
        }
    }

    private void Start()
    {
        if (FarmFund.Instance != null)
        {
            inventory = FarmFund.Instance.Inventory;
            inventory.inventoryChangedEvent.AddListener(MarkDirty);
        }
        if (bag != null)
            bag.bagChangedEvent.AddListener(MarkDirty);
    }

    private void OnDestroy()
    {
        if (inventory != null)
            inventory.inventoryChangedEvent.RemoveListener(MarkDirty);
        if (bag != null)
            bag.bagChangedEvent.RemoveListener(MarkDirty);
    }

    private void MarkDirty()
    {
        dirty = true;
    }

    private void LateUpdate()
    {
        if (bag == null || inventory == null)
            return;
        //the rod went into the hand or back: its faded copy is new
        if (bag.GetPlaceholder() != lastPlaceholder)
            dirty = true;
        if (dirty)
            RefreshNow();
        UpdateTooltip();
    }

    // ---------------------------------------------------------------- the marks on the items

    //redraws every mark now (the tests call it; otherwise it runs at the end of a frame in which something changed)
    public void RefreshNow()
    {
        dirty = false;
        if (bag == null || inventory == null)
            return;
        InventoryManager manager = bag.GetInventoryManager();
        lastPlaceholder = bag.GetPlaceholder();
        //every lure in the bag shows its own durability (the bag keeps them in the inventory's order)
        foreach (FarmInventory.BaitStock stock in inventory.baits)
        {
            if (stock.bait == null || stock.bait.item == null)
                continue;
            List<GameObject> items = manager.GetItems(stock.bait.item.itemName);
            for (int i = 0; i < items.Count; i++)
            {
                BaitBadge badge = BadgeOn(items[i]);
                badge.SetBait(null, 0);
                int left = i < stock.durabilities.Count ? stock.durabilities[i] : stock.bait.durability;
                badge.SetDurability(left, stock.bait.durability);
            }
        }
        //the rod and its faded copy show the bait loaded into it
        InventoryItemData rodItem = bag.GetFishingRodItem();
        if (rodItem != null)
        {
            foreach (GameObject rod in manager.GetItems(rodItem.itemName))
                ShowLoaded(BadgeOn(rod));
            if (lastPlaceholder != null && IsItem(bag.GetItemInHand(), rodItem))
                ShowLoaded(BadgeOn(lastPlaceholder));
        }
    }

    private void ShowLoaded(BaitBadge badge)
    {
        BaitData bait = inventory.HasLoadedBait() ? inventory.loadedBait : null;
        Sprite sprite = bait != null && bait.item != null ? bait.item.itemSprite : null;
        badge.SetBait(sprite, bait != null && !bait.Wears() ? inventory.loadedAmount : 0);
        badge.SetDurability(bait != null ? inventory.loadedDurability : 0, bait != null ? bait.durability : 0);
    }

    private BaitBadge BadgeOn(GameObject item)
    {
        BaitBadge badge = item.GetComponent<BaitBadge>();
        if (badge == null)
        {
            badge = item.AddComponent<BaitBadge>();
            badge.Build(font, fontMaterial, plateSprite);
        }
        return badge;
    }

    private static bool IsItem(GameObject item, InventoryItemData data)
    {
        return item != null && data != null && item.GetComponent<InventoryItem>().itemData == data;
    }

    // ---------------------------------------------------------------- the tooltip

    private void UpdateTooltip()
    {
        if (tooltip == null || tooltipLabel == null)
            return;
        if (Mouse.current == null)
        {
            ShowTip(null);
            return;
        }
        Vector2 position = Mouse.current.position.ReadValue();
        bool dragging = Mouse.current.leftButton.isPressed;//hidden while an item is dragged
        string text = dragging ? null : TextFor(ItemUnder(position));
        ShowTip(text);
        if (text != null)
            Place(position);
    }

    private void ShowTip(string text)
    {
        if (text != shownTip)
        {
            shownTip = text;
            if (text != null)
            {
                tooltipLabel.text = text;
                LayoutRebuilder.ForceRebuildLayoutImmediate(tooltip);
            }
        }
        if (tooltipGroup != null)
            tooltipGroup.alpha = text != null ? 1f : 0f;
    }

    //next to the mouse, up and to the right; to the left of it near the right edge of the screen
    private void Place(Vector2 screen)
    {
        RectTransform area = tooltip.parent as RectTransform;
        if (area == null || !RectTransformUtility.ScreenPointToLocalPointInRectangle(area, screen, null, out Vector2 local))
            return;
        bool flip = local.x + tooltipOffset.x + tooltip.rect.width > area.rect.xMax;
        tooltip.pivot = new Vector2(flip ? 1f : 0f, 0f);
        Vector2 at = local + new Vector2(flip ? -tooltipOffset.x : tooltipOffset.x, tooltipOffset.y);
        at.y = Mathf.Min(at.y, area.rect.yMax - tooltip.rect.height);
        //the anchors are at the bottom left of the bag canvas
        tooltip.anchoredPosition = at - area.rect.min;
    }

    //the bag item under a screen point: in a slot (the item in the hand for the slot that holds its faded copy), or the
    //item in the hand slot at the bottom left (it takes no raycasts itself, so the slot it is in decides)
    public GameObject ItemUnder(Vector2 screen)
    {
        if (raycaster == null || !raycaster.isActiveAndEnabled || EventSystem.current == null)
            return null;
        if (pointer == null || pointer.currentInputModule != EventSystem.current.currentInputModule)
            pointer = new PointerEventData(EventSystem.current);
        pointer.position = screen;
        hits.Clear();
        raycaster.Raycast(pointer, hits);
        InventoryManager manager = bag.GetInventoryManager();
        Transform hand = bag.GetHandSlot();
        foreach (RaycastResult hit in hits)
        {
            for (Transform t = hit.gameObject.transform; t != null && t != transform; t = t.parent)
            {
                if (hand != null && t == hand)
                    return bag.GetItemInHand();
                InventorySlot slot = t.GetComponent<InventorySlot>();
                if (slot != null)
                    return bag.GetItemInSlot(System.Array.IndexOf(manager.inventorySlots, slot.gameObject));
            }
        }
        return null;
    }

    //what the tooltip says over this item (null: no tooltip)
    public string TextFor(GameObject item)
    {
        if (item == null || inventory == null)
            return null;
        InventoryItemData data = item.GetComponent<InventoryItem>().itemData;
        if (data == null)
            return null;
        if (data == bag.GetFishingRodItem())
        {
            string loaded = inventory.HasLoadedBait()
                ? BaitText(inventory.loadedBait, inventory.loadedAmount, inventory.loadedDurability)
                : "<alpha=#C0>No bait: drag a lure or worms here";
            return data.itemName + "\n<size=85%>" + loaded + "</size>";
        }
        foreach (FarmInventory.BaitStock stock in inventory.baits)
        {
            if (stock.bait == null || stock.bait.item != data)
                continue;
            if (!stock.bait.Wears())
                return BaitText(stock.bait, inventory.GetBait(stock.bait), 0);
            int index = bag.GetInventoryManager().GetItems(data.itemName).IndexOf(item);
            int left = index >= 0 && index < stock.durabilities.Count ? stock.durabilities[index] : stock.bait.durability;
            return BaitText(stock.bait, 1, left);
        }
        return null;
    }

    //"Lure   23 / 30", "Worms   x 6"
    private string BaitText(BaitData bait, int amount, int durability)
    {
        string number = bait.Wears() ? durability + " / " + bait.durability : "x " + amount;
        string name = bait.Wears() || bait.item == null ? bait.baitName : bait.item.itemName;
        return name + "   <color=" + numberColor + ">" + number + "</color>";
    }

    // ---------------------------------------------------------------- for the tests

    public bool IsTooltipShown()
    {
        return shownTip != null && tooltipGroup != null && tooltipGroup.alpha > 0f;
    }
    public string GetTooltipText()
    {
        return shownTip;
    }
}
