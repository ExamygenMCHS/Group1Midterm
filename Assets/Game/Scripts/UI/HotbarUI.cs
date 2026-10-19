using UnityEngine;
using UnityEngine.UI;
using TMPro;

//the eight slots at the bottom middle of the screen, Minecraft style. Each slot shows its number, the item's
//icon, the seed count and (for the watering can) a little water bar; the slot in the hand has a gold frame and is a
//bit bigger. When the player selects another slot, the item's name shows above the hotbar for a moment.
//Display only: the items themselves live in Hotbar and the Farm Fund's inventory.
public class HotbarUI : MonoBehaviour
{
    [System.Serializable]
    public class SlotView
    {
        public RectTransform root;
        public Image icon;
        public TMP_Text count;
        public Image frame;//gold outline on the slot in the hand
        public GameObject waterBar;
        public Image waterFill;
    }

    [SerializeField]
    private Hotbar hotbar;
    [SerializeField]
    private WateringCanTank wateringCanTank;
    [SerializeField]
    private SlotView[] slots = new SlotView[Hotbar.SlotCount];
    [SerializeField]
    private TMP_Text itemNameLabel;
    [SerializeField]
    private float itemNameSeconds = 1.6f;
    [SerializeField]
    private float selectedScale = 1.12f;

    [Header("Icons")]
    [SerializeField]
    private Sprite hoeIcon;
    [SerializeField]
    private Sprite wateringCanIcon;
    [SerializeField]
    private Sprite sickleIcon;

    [SerializeField]
    private Color emptySeedsColor = new Color(1f, 1f, 1f, 0.35f);
    [SerializeField]
    private Color noSeedsCountColor = new Color(1f, 0.45f, 0.4f);

    private float itemNameLeft = 0f;
    private FarmInventory inventory;

    private void Start()
    {
        hotbar.hotbarChangedEvent.AddListener(UpdateSlots);
        hotbar.selectedSlotChangedEvent.AddListener(OnSelectedSlotChanged);
        if (wateringCanTank != null)
            wateringCanTank.waterChangedEvent.AddListener(UpdateSlots);
        if (FarmFund.Instance != null)
            inventory = FarmFund.Instance.Inventory;
        if (itemNameLabel != null)
            itemNameLabel.text = "";
        UpdateSlots();
        for (int i = 0; i < slots.Length; i++)
            slots[i].root.localScale = Vector3.one * (i == hotbar.GetSelectedSlot() ? selectedScale : 1f);
    }

    private void OnDestroy()
    {
        if (hotbar != null)
        {
            hotbar.hotbarChangedEvent.RemoveListener(UpdateSlots);
            hotbar.selectedSlotChangedEvent.RemoveListener(OnSelectedSlotChanged);
        }
        if (wateringCanTank != null)
            wateringCanTank.waterChangedEvent.RemoveListener(UpdateSlots);
    }

    private void OnSelectedSlotChanged()
    {
        UpdateSlots();
        ShowItemName();
    }

    public void UpdateSlots()
    {
        int selected = hotbar.GetSelectedSlot();
        for (int i = 0; i < slots.Length; i++)
        {
            SlotView view = slots[i];
            Hotbar.Slot slot = hotbar.GetSlot(i);
            view.frame.enabled = i == selected;

            Sprite icon = IconFor(slot);
            view.icon.enabled = icon != null;
            view.icon.sprite = icon;
            view.icon.color = Color.white;

            string count = "";
            if (slot.tool == ToolManager.ToolType.SeedBag && inventory != null)
            {
                int seeds = inventory.GetSeeds(slot.crop);
                count = seeds.ToString();
                if (seeds <= 0)
                    view.icon.color = emptySeedsColor;
                view.count.color = seeds <= 0 ? noSeedsCountColor : Color.white;
            }
            view.count.text = count;

            bool can = slot.tool == ToolManager.ToolType.WateringCan && wateringCanTank != null;
            if (view.waterBar != null)
                view.waterBar.SetActive(can);
            if (can && view.waterFill != null)
                view.waterFill.fillAmount = (float)wateringCanTank.GetWater() / wateringCanTank.GetCapacity();
        }
    }

    private Sprite IconFor(Hotbar.Slot slot)
    {
        switch (slot.tool)
        {
            case ToolManager.ToolType.Hoe: return hoeIcon;
            case ToolManager.ToolType.WateringCan: return wateringCanIcon;
            case ToolManager.ToolType.Sickle: return sickleIcon;
            case ToolManager.ToolType.SeedBag: return slot.crop != null ? slot.crop.seedIcon : null;
        }
        return null;
    }

    private void ShowItemName()
    {
        if (itemNameLabel == null)
            return;
        Hotbar.Slot slot = hotbar.GetSlot(hotbar.GetSelectedSlot());
        string text = slot.IsEmpty ? "Empty hand" : Hotbar.TitleName(slot.tool, slot.crop);
        if (slot.tool == ToolManager.ToolType.WateringCan && wateringCanTank != null)
            text += "  " + wateringCanTank.GetWater() + " / " + wateringCanTank.GetCapacity();
        else if (slot.tool == ToolManager.ToolType.SeedBag && inventory != null)
            text += "  x" + inventory.GetSeeds(slot.crop);
        itemNameLabel.text = text;
        itemNameLeft = itemNameSeconds;
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        int selected = hotbar.GetSelectedSlot();
        for (int i = 0; i < slots.Length; i++)
        {
            float target = i == selected ? selectedScale : 1f;
            float scale = Mathf.Lerp(slots[i].root.localScale.x, target, 1f - Mathf.Exp(-18f * dt));
            slots[i].root.localScale = Vector3.one * scale;
        }
        if (itemNameLabel != null && itemNameLeft > 0f)
        {
            itemNameLeft -= dt;
            itemNameLabel.alpha = Mathf.Clamp01(itemNameLeft / 0.4f);
        }
    }
}
