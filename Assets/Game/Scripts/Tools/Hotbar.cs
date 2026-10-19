using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

//Minecraft-style item slots. The player has to pick a tool up from the ground before it lands in the hotbar,
//and has to hold it (select its slot with 1-8 or the mouse wheel) to use it: Space tills with the hoe and waters
//with the watering can, E plants the seeds in hand and harvests with the sickle.
//Slot 1 is the hoe, slot 2 the watering can, then one slot for each kind of seed the level uses, and the sickle goes
//into the first free slot after the seeds. What the player owns lives in the Farm Fund's inventory, so picked-up tools
//stay with the player from level to level; this script only arranges it into slots and remembers which one is held.
//In the levels with the inventory bag (Bag set) the slots are the 8 slots of the bag's inventory bar, in the order the
//bag shows them (the player can drag items around in the bag); the item held moves to the bag's hand slot.
[DefaultExecutionOrder(-30)]//selects the slot before the other scripts read it this frame
public class Hotbar : MonoBehaviour
{
    public const int SlotCount = 8;//room for more kinds of seeds before the game needs an inventory screen
    public const int HoeSlot = 0, WateringCanSlot = 1, FirstSeedSlot = 2;//shown to the player as 1, 2, 3

    //one slot: a tool (ToolType.None = no tool), for the seed bag the crop it holds, and in the bag levels the bag item
    //(also a harvested plant, which is no tool)
    public struct Slot
    {
        public ToolManager.ToolType tool;
        public PlantData crop;
        public InventoryItemData item;
        public bool IsEmpty { get { return tool == ToolManager.ToolType.None && item == null; } }
    }

    [SerializeField]
    private PlantManager plantManager;//optional: the seeds of this level (none in Scene1)
    [SerializeField]
    private TimedMessage hudMessage;
    [SerializeField]
    private bool scrollWheelSelects = true;
    [Tooltip("Optional (levels with the inventory bag): the slots are the bag's inventory bar")]
    [SerializeField]
    private FarmBag bag;

    //this event gets fired when what lies in the slots changes (a tool picked up, a seed used)
    public UnityEvent hotbarChangedEvent = new UnityEvent();
    //this event gets fired when the item in the player's hand changes (another slot selected, or an item landed in it)
    public UnityEvent selectedSlotChangedEvent = new UnityEvent();

    private readonly Slot[] slots = new Slot[SlotCount];
    private int selected = 0;
    private FarmInventory inventory;
    private Slot heldBefore;

    private static readonly Key[] SlotKeys = { Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5, Key.Digit6, Key.Digit7, Key.Digit8 };
    private static readonly Key[] NumpadKeys = { Key.Numpad1, Key.Numpad2, Key.Numpad3, Key.Numpad4, Key.Numpad5, Key.Numpad6, Key.Numpad7, Key.Numpad8 };

    private void Start()
    {
        if (FarmFund.Instance != null)
            inventory = FarmFund.Instance.Inventory;
        //with the bag, the slots follow the bag (after it has redrawn its items), otherwise the inventory
        if (bag != null)
            bag.bagChangedEvent.AddListener(Refresh);
        else if (inventory != null)
            inventory.inventoryChangedEvent.AddListener(Refresh);
        Refresh();
    }

    private void OnDestroy()
    {
        if (bag != null)
            bag.bagChangedEvent.RemoveListener(Refresh);
        else if (inventory != null)
            inventory.inventoryChangedEvent.RemoveListener(Refresh);
    }

    private void Update()
    {
        if (Keyboard.current != null)
        {
            for (int i = 0; i < SlotCount; i++)
                if (Keyboard.current[SlotKeys[i]].wasPressedThisFrame || Keyboard.current[NumpadKeys[i]].wasPressedThisFrame)
                    SelectSlot(i);
        }
        if (scrollWheelSelects && Mouse.current != null)
        {
            float scroll = Mouse.current.scroll.ReadValue().y;
            if (scroll > 0.01f)
                SelectSlot((selected + SlotCount - 1) % SlotCount);//wheel up: the slot to the left, like Minecraft
            else if (scroll < -0.01f)
                SelectSlot((selected + 1) % SlotCount);
        }
    }

    //puts the owned items into their slots (called whenever the inventory or the bag changes)
    public void Refresh()
    {
        for (int i = 0; i < SlotCount; i++)
            slots[i] = bag != null ? bag.GetBarSlot(i) : new Slot();
        if (bag != null)
        {
            hotbarChangedEvent.Invoke();
            AnnounceIfHeldItemChanged();
            return;
        }
        if (Owns(ToolManager.ToolType.Hoe))
            slots[HoeSlot].tool = ToolManager.ToolType.Hoe;
        if (Owns(ToolManager.ToolType.WateringCan))
            slots[WateringCanSlot].tool = ToolManager.ToolType.WateringCan;
        int next = FirstSeedSlot;
        PlantData[] crops = plantManager != null ? plantManager.GetCrops() : null;
        for (int c = 0; crops != null && c < crops.Length && next < SlotCount; c++, next++)
        {
            slots[next].tool = ToolManager.ToolType.SeedBag;
            slots[next].crop = crops[c];
        }
        if (Owns(ToolManager.ToolType.Sickle) && next < SlotCount)
            slots[next].tool = ToolManager.ToolType.Sickle;
        hotbarChangedEvent.Invoke();
        AnnounceIfHeldItemChanged();
    }

    private bool Owns(ToolManager.ToolType tool)
    {
        return inventory != null && inventory.HasTool(tool);
    }

    private void AnnounceIfHeldItemChanged()
    {
        Slot held = slots[selected];
        if (held.tool == heldBefore.tool && held.crop == heldBefore.crop && held.item == heldBefore.item)
            return;
        heldBefore = held;
        selectedSlotChangedEvent.Invoke();//make announcement to all listeners
    }

    // ---------------------------------------------------------------- selecting

    public void SelectSlot(int index)
    {
        if (index < 0 || index >= SlotCount || index == selected)
            return;
        selected = index;
        heldBefore = slots[selected];
        selectedSlotChangedEvent.Invoke();//the player chose another slot (the selection frame moves even if both are empty)
    }

    //selects the slot that holds this item; returns false when the player does not have it
    public bool SelectItem(ToolManager.ToolType tool, PlantData crop = null)
    {
        int index = SlotOf(tool, crop);
        if (index < 0)
            return false;
        SelectSlot(index);
        return true;
    }

    //Q (PlantManager.SwitchSeed): the next kind of seed into the hand
    public bool SelectNextSeeds()
    {
        for (int step = 1; step <= SlotCount; step++)
        {
            int i = (selected + step) % SlotCount;
            if (slots[i].tool == ToolManager.ToolType.SeedBag)
            {
                SelectSlot(i);
                return true;
            }
        }
        return false;
    }

    public int SlotOf(ToolManager.ToolType tool, PlantData crop = null)
    {
        for (int i = 0; i < SlotCount; i++)
            if (slots[i].tool == tool && (crop == null || slots[i].crop == crop))
                return i;
        return -1;
    }

    // ---------------------------------------------------------------- what is in the hand

    public int GetSelectedSlot()
    {
        return selected;
    }
    public Slot GetSlot(int index)
    {
        return slots[index];
    }
    public ToolManager.ToolType GetSelectedTool()
    {
        return slots[selected].tool;
    }
    //the crop of the seeds in the hand (null when the hand holds no seeds)
    public PlantData GetSelectedCrop()
    {
        return slots[selected].tool == ToolManager.ToolType.SeedBag ? slots[selected].crop : null;
    }
    public bool IsHolding(ToolManager.ToolType tool)
    {
        return slots[selected].tool == tool;
    }
    public bool HasItem(ToolManager.ToolType tool)
    {
        return SlotOf(tool) >= 0;
    }

    //Space and E call this before they use a tool. When the tool is not in the hand it tells the player what to do:
    //which key selects it, or where to find it when it has not been picked up yet. 'purpose' completes "... to till the grass".
    public bool RequireInHand(ToolManager.ToolType tool, string purpose)
    {
        if (IsHolding(tool))
            return true;
        if (hudMessage == null)
            return false;
        int slot = SlotOf(tool);
        if (slot >= 0)
            hudMessage.Show("Hold the " + ItemName(tool) + " " + purpose + ": press " + KeyTag((slot + 1).ToString()) + ".");
        else if (bag != null && bag.HasInMainBag(tool))//moved out of the bar by the player
            hudMessage.Show("The " + ItemName(tool) + " is in your bag: press " + KeyTag("B") + " and drag it into the bar.");
        else if (bag != null && tool == ToolManager.ToolType.SeedBag)
            hudMessage.Show("You have no seeds left. Buy them in the store.");
        else
        {
            ToolPickup pickup = ToolPickup.Find(tool);
            hudMessage.Show("You need the " + ItemName(tool) + " " + purpose + ". " + (pickup != null ? pickup.GetWhereText() : ""));
        }
        return false;
    }

    public void Tell(string message)
    {
        if (hudMessage != null)
            hudMessage.Show(message);
    }

    // ---------------------------------------------------------------- names for the UI and messages

    public static string ItemName(ToolManager.ToolType tool, PlantData crop = null)
    {
        switch (tool)
        {
            case ToolManager.ToolType.Hoe: return "hoe";
            case ToolManager.ToolType.WateringCan: return "watering can";
            case ToolManager.ToolType.Sickle: return "sickle";
            case ToolManager.ToolType.SeedBag: return crop != null ? crop.cropName.ToLower() + " seeds" : "seeds";
        }
        return "empty hand";
    }

    //"watering can" -> "Watering Can"
    public static string TitleName(ToolManager.ToolType tool, PlantData crop = null)
    {
        string name = ItemName(tool, crop);
        var builder = new System.Text.StringBuilder(name.Length);
        for (int i = 0; i < name.Length; i++)
            builder.Append(i == 0 || name[i - 1] == ' ' ? char.ToUpper(name[i]) : name[i]);
        return builder.ToString();
    }

    //a key in the same gold as the controls hint
    public static string KeyTag(string key)
    {
        return "<color=#FFD45E><b>" + key + "</b></color>";
    }
}
