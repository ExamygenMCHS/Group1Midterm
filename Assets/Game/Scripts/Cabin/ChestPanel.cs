using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

//the window of the storage chest in the cabin: the chest's stacks on top, the bag's seeds and crops below.
//Clicking a stack moves it to the other side (right click: just one). Only seeds and harvested crops go in the chest;
//the tools stay in the bag. What lies in the chest is kept in FarmInventory.chest, apart from the bag, so the trader's
//store and the wholesale truck never sell it.
//The window is built in code the first time a chest opens, in the colours of the trader's shop. While it is open the
//farm controls wait, the same way the trader's shop pauses them.
public class ChestPanel : MonoBehaviour
{
    [Serializable]
    public class Style
    {
        public TMP_FontAsset font;
        public Sprite rounded;//panels, slots and buttons
        public Sprite circle;//the close button
    }

    public const int Columns = 9;
    private const int ChestRows = 3;
    private const int BagRows = 2;
    private const float SlotSize = 78f;
    private const float SlotGap = 8f;

    private static readonly Color Wood = new Color(0.34f, 0.25f, 0.13f, 1f);
    private static readonly Color Parchment = new Color(0.95f, 0.88f, 0.71f, 1f);
    private static readonly Color SlotColor = new Color(0.62f, 0.42f, 0.2f, 1f);
    private static readonly Color SlotHover = new Color(0.99f, 0.74f, 0.27f, 1f);
    private static readonly Color ButtonColor = new Color(0.38f, 0.7f, 0.28f, 1f);

    private static ChestPanel activePanel;
    public static bool IsAnyOpen { get { return activePanel != null; } }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { activePanel = null; }

    private Style style;
    private Canvas canvas;
    private GraphicRaycaster raycaster;
    private Transform chestGrid;
    private Transform bagGrid;
    private TMP_Text chestCountLabel;
    private TMP_Text hoverLabel;
    private Button closeButton;
    private readonly List<ChestSlot> chestSlots = new List<ChestSlot>();
    private readonly List<ChestSlot> bagSlots = new List<ChestSlot>();

    private readonly List<Behaviour> suspended = new List<Behaviour>();
    private FarmInventory inventory;
    private WorldInteractor player;
    private StorageChest owner;
    private int openedFrame = -1;
    private bool closing;
    private CursorLockMode previousCursorLock;
    private bool previousCursorVisible;

    public bool IsOpen { get; private set; }
    public StorageChest Owner { get { return owner; } }
    public IReadOnlyList<ChestSlot> ChestSlots { get { return chestSlots; } }
    public IReadOnlyList<ChestSlot> BagSlots { get { return bagSlots; } }

    public static ChestPanel Create(Transform parent, Style style)
    {
        GameObject go = new GameObject("Chest Window");
        go.transform.SetParent(parent, false);
        ChestPanel panel = go.AddComponent<ChestPanel>();
        panel.style = style ?? new Style();
        panel.Build();
        panel.SetVisible(false);
        return panel;
    }

    // ---------------------------------------------------------------- opening and closing

    public bool TryOpen(WorldInteractor shopper, StorageChest chest)
    {
        if (IsAnyOpen || TraderShopPanel.IsAnyOpen || shopper == null || chest == null || FarmFund.Instance == null)
            return false;
        player = shopper;
        owner = chest;
        inventory = FarmFund.Instance.Inventory;
        inventory.inventoryChangedEvent.AddListener(Refresh);
        activePanel = this;
        IsOpen = true;
        closing = false;
        openedFrame = Time.frameCount;
        previousCursorLock = Cursor.lockState;
        previousCursorVisible = Cursor.visible;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        SuspendGameplay();
        if (EventSystem.current == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
        SetVisible(true);
        hoverLabel.text = "";
        Refresh();
        return true;
    }

    public void Close()
    {
        if (!IsOpen)
            return;
        IsOpen = false;
        closing = true;
        SetVisible(false);
        //gameplay stays paused for the rest of this frame, so the key that closed the chest does not also act on the farm
    }

    private void Update()
    {
        if (!IsOpen || Time.frameCount == openedFrame)
            return;
        Keyboard keyboard = Keyboard.current;
        bool closeKey = keyboard != null && (keyboard.escapeKey.wasPressedThisFrame || keyboard.eKey.wasPressedThisFrame);
        if (closeKey || player == null || !player.gameObject.activeInHierarchy || owner == null || !owner.isActiveAndEnabled)
            Close();
    }

    private void LateUpdate()
    {
        if (closing)
            ReleaseControls();
    }

    private void OnDisable()
    {
        ReleaseControls();
    }

    private void SuspendGameplay()
    {
        foreach (GameObject root in player.gameObject.scene.GetRootGameObjects())
            foreach (MonoBehaviour component in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (component is PlayerMove movement && movement.isActiveAndEnabled && movement.rb != null)
                    movement.StopPlayer();
                if (component is FarmBag bag && bag.isActiveAndEnabled && bag.IsBagOpen())
                    bag.SetBagOpen(false);
                if (component is InteractionPrompt prompt && prompt.isActiveAndEnabled)
                    prompt.Hide();
                if (component is PlayerMove || component is PlayerInteractPoint || component is PlantManager ||
                    component is Hotbar || component is WorldInteractor || component is FarmBag || component is TimeFastForward)
                    Suspend(component);
            }
    }

    private void Suspend(Behaviour control)
    {
        if (control == null || !control.enabled || suspended.Contains(control))
            return;
        suspended.Add(control);
        control.enabled = false;
    }

    private void ReleaseControls()
    {
        if (activePanel != this)
            return;
        IsOpen = false;
        closing = false;
        SetVisible(false);
        foreach (Behaviour control in suspended)
            if (control != null)
                control.enabled = true;
        suspended.Clear();
        if (inventory != null)
            inventory.inventoryChangedEvent.RemoveListener(Refresh);
        Cursor.lockState = previousCursorLock;
        Cursor.visible = previousCursorVisible;
        activePanel = null;
        player = null;
        owner = null;
    }

    private void SetVisible(bool visible)
    {
        if (canvas != null)
            canvas.enabled = visible;
        if (raycaster != null)
            raycaster.enabled = visible;
    }

    // ---------------------------------------------------------------- moving stacks

    private void OnSlotClicked(ChestSlot slot, bool wholeStack)
    {
        bool toChest = bagSlots.Contains(slot);
        Move(slot.Crop, slot.Harvest, toChest, wholeStack ? slot.Amount : 1);
    }

    //moves 'amount' seeds or crops between the bag and the chest; false when there are not that many
    public bool Move(PlantData crop, bool harvest, bool toChest, int amount)
    {
        if (inventory == null && FarmFund.Instance != null)
            inventory = FarmFund.Instance.Inventory;
        if (inventory == null)
            return false;
        bool moved = toChest ? inventory.StoreInChest(crop, harvest, amount) : inventory.TakeFromChest(crop, harvest, amount);
        if (moved)
            PlayClick();
        return moved;
    }

    //every harvested crop in the bag goes into the chest (the seeds stay in the bag for planting)
    public void StoreAllCrops()
    {
        if (inventory == null)
            return;
        foreach (FarmInventory.CropStock stock in new List<FarmInventory.CropStock>(inventory.crops))
            if (stock.crop != null && stock.harvested > 0)
                inventory.StoreInChest(stock.crop, true, stock.harvested);
        PlayClick();
    }

    //everything in the chest goes back into the bag
    public void TakeEverything()
    {
        if (inventory == null)
            return;
        foreach (FarmInventory.CropStock stock in new List<FarmInventory.CropStock>(inventory.chest))
        {
            if (stock.crop == null)
                continue;
            if (stock.seeds > 0)
                inventory.TakeFromChest(stock.crop, false, stock.seeds);
            if (stock.harvested > 0)
                inventory.TakeFromChest(stock.crop, true, stock.harvested);
        }
        PlayClick();
    }

    private void PlayClick()
    {
        if (player != null && player.TryGetComponent(out FarmPlayerAudio playerAudio))
            playerAudio.PlayUiClick();
    }

    // ---------------------------------------------------------------- showing the stacks

    private struct Stack
    {
        public PlantData crop;
        public bool harvest;
        public int amount;
    }

    //seeds of each plant first, then its harvest, in the order the plants were first owned
    private static List<Stack> StacksOf(List<FarmInventory.CropStock> list)
    {
        List<Stack> stacks = new List<Stack>();
        foreach (FarmInventory.CropStock stock in list)
        {
            if (stock.crop == null)
                continue;
            if (stock.seeds > 0)
                stacks.Add(new Stack { crop = stock.crop, harvest = false, amount = stock.seeds });
            if (stock.harvested > 0)
                stacks.Add(new Stack { crop = stock.crop, harvest = true, amount = stock.harvested });
        }
        return stacks;
    }

    public void Refresh()
    {
        if (inventory == null)
            return;
        Fill(chestGrid, chestSlots, StacksOf(inventory.chest), ChestRows);
        Fill(bagGrid, bagSlots, StacksOf(inventory.crops), BagRows);
        int total = inventory.GetChestTotal();
        chestCountLabel.text = total == 0 ? "empty" : total + (total == 1 ? " item" : " items");
    }

    private void Fill(Transform grid, List<ChestSlot> slots, List<Stack> stacks, int minRows)
    {
        int rows = Mathf.Max(minRows, (stacks.Count + Columns - 1) / Columns);
        while (slots.Count < rows * Columns)
            slots.Add(MakeSlot(grid));
        for (int i = 0; i < slots.Count; i++)
        {
            slots[i].gameObject.SetActive(i < rows * Columns);
            if (i < stacks.Count)
                slots[i].Show(stacks[i].crop, stacks[i].harvest, stacks[i].amount);
            else
                slots[i].Show(null, false, 0);
        }
    }

    private void OnSlotHovered(ChestSlot slot)
    {
        if (hoverLabel != null)
            hoverLabel.text = slot == null || slot.IsEmpty() ? "" : slot.Label();
    }

    // ---------------------------------------------------------------- building the window

    private void Build()
    {
        canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 40;
        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        raycaster = gameObject.AddComponent<GraphicRaycaster>();

        Image dim = MakeImage("Dim", transform, null, new Color(0.055f, 0.045f, 0.025f, 0.6f));
        Stretch(dim.rectTransform, 0f);

        Image window = MakeImage("Window", transform, style.rounded, Wood);
        window.rectTransform.sizeDelta = Vector2.zero;
        VerticalLayoutGroup outer = window.gameObject.AddComponent<VerticalLayoutGroup>();
        outer.padding = new RectOffset(10, 10, 10, 10);
        outer.childControlWidth = true;
        outer.childControlHeight = true;
        outer.childForceExpandWidth = true;
        outer.childForceExpandHeight = true;
        ContentSizeFitter fit = window.gameObject.AddComponent<ContentSizeFitter>();
        fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        Image inner = MakeImage("Inner", window.transform, style.rounded, Parchment);
        VerticalLayoutGroup column = inner.gameObject.AddComponent<VerticalLayoutGroup>();
        column.padding = new RectOffset(30, 30, 22, 22);
        column.spacing = 12f;
        column.childControlWidth = true;
        column.childControlHeight = true;
        column.childForceExpandWidth = true;
        column.childForceExpandHeight = false;

        //title row: name of the chest and the close button
        RectTransform titleRow = Row("Title Row", inner.transform, 56f);
        TMP_Text title = MakeText("Title", titleRow, "Storage Chest", 42f, Wood, TextAlignmentOptions.Left, FontStyles.Bold);
        Flexible(title.gameObject, 1f);
        Image close = MakeImage("Close", titleRow, style.circle != null ? style.circle : style.rounded, SlotColor);
        Fixed(close.gameObject, 52f, 52f);
        closeButton = close.gameObject.AddComponent<Button>();
        closeButton.onClick.AddListener(Close);
        TMP_Text x = MakeText("X", close.transform, "X", 30f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
        Stretch(x.rectTransform, 0f);

        //the chest
        RectTransform chestRow = Row("Chest Label Row", inner.transform, 34f);
        Flexible(MakeText("Chest Label", chestRow, "Chest", 28f, Wood, TextAlignmentOptions.Left, FontStyles.Bold).gameObject, 1f);
        chestCountLabel = MakeText("Chest Count", chestRow, "", 24f, SlotColor, TextAlignmentOptions.Right, FontStyles.Normal);
        Flexible(chestCountLabel.gameObject, 1f);
        chestGrid = Grid("Chest Grid", inner.transform);

        //buttons between the chest and the bag
        RectTransform buttons = Row("Buttons", inner.transform, 54f);
        buttons.GetComponent<HorizontalLayoutGroup>().spacing = 16f;
        MakeButton("Store All Crops", buttons, "Store all crops", StoreAllCrops);
        MakeButton("Take Everything", buttons, "Take everything", TakeEverything);

        //the bag
        RectTransform bagRow = Row("Bag Label Row", inner.transform, 34f);
        Flexible(MakeText("Bag Label", bagRow, "Your bag  (seeds and crops)", 28f, Wood, TextAlignmentOptions.Left, FontStyles.Bold).gameObject, 1f);
        bagGrid = Grid("Bag Grid", inner.transform);

        hoverLabel = MakeText("Hover", inner.transform, "", 26f, Wood, TextAlignmentOptions.Center, FontStyles.Bold);
        Fixed(hoverLabel.gameObject, -1f, 32f);
        TMP_Text hint = MakeText("Hint", inner.transform, "Click: move the stack     Right click: move one     E / Esc: close",
            22f, SlotColor, TextAlignmentOptions.Center, FontStyles.Normal);
        Fixed(hint.gameObject, -1f, 28f);

        Fill(chestGrid, chestSlots, new List<Stack>(), ChestRows);
        Fill(bagGrid, bagSlots, new List<Stack>(), BagRows);
    }

    private Transform Grid(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        GridLayoutGroup grid = go.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(SlotSize, SlotSize);
        grid.spacing = new Vector2(SlotGap, SlotGap);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = Columns;
        grid.childAlignment = TextAnchor.UpperCenter;
        return go.transform;
    }

    private ChestSlot MakeSlot(Transform grid)
    {
        Image frame = MakeImage("Slot", grid, style.rounded, SlotColor);
        ChestSlot slot = frame.gameObject.AddComponent<ChestSlot>();
        slot.frame = frame;
        slot.icon = MakeImage("Icon", frame.transform, null, Color.white);
        slot.icon.preserveAspect = true;
        slot.icon.raycastTarget = false;
        Stretch(slot.icon.rectTransform, 10f);
        slot.count = MakeText("Count", frame.transform, "", 24f, Color.white, TextAlignmentOptions.BottomRight, FontStyles.Bold);
        slot.count.raycastTarget = false;
        Stretch(slot.count.rectTransform, 6f);
        slot.SetColors(SlotColor, SlotHover);
        slot.clicked = OnSlotClicked;
        slot.hovered = OnSlotHovered;
        return slot;
    }

    private void MakeButton(string name, Transform parent, string label, UnityEngine.Events.UnityAction action)
    {
        Image image = MakeImage(name, parent, style.rounded, ButtonColor);
        Fixed(image.gameObject, 280f, 54f);
        Button button = image.gameObject.AddComponent<Button>();
        button.onClick.AddListener(action);
        TMP_Text text = MakeText("Label", image.transform, label, 26f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
        Stretch(text.rectTransform, 4f);
    }

    private static RectTransform Row(string name, Transform parent, float height)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        HorizontalLayoutGroup row = go.AddComponent<HorizontalLayoutGroup>();
        row.childControlWidth = true;
        row.childControlHeight = true;
        row.childForceExpandWidth = false;
        row.childForceExpandHeight = false;
        row.childAlignment = TextAnchor.MiddleCenter;
        Fixed(go, -1f, height);
        return (RectTransform)go.transform;
    }

    private Image MakeImage(string name, Transform parent, Sprite sprite, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        Image image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.type = sprite != null ? Image.Type.Sliced : Image.Type.Simple;
        image.color = color;
        return image;
    }

    private TMP_Text MakeText(string name, Transform parent, string text, float size, Color color, TextAlignmentOptions align, FontStyles fontStyle)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI label = go.AddComponent<TextMeshProUGUI>();
        if (style.font != null)
            label.font = style.font;
        label.text = text;
        label.fontSize = size;
        label.color = color;
        label.alignment = align;
        label.fontStyle = fontStyle;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.raycastTarget = false;
        return label;
    }

    private static void Stretch(RectTransform rect, float inset)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(inset, inset);
        rect.offsetMax = new Vector2(-inset, -inset);
    }

    private static void Fixed(GameObject go, float width, float height)
    {
        LayoutElement element = go.GetComponent<LayoutElement>();
        if (element == null)
            element = go.AddComponent<LayoutElement>();
        if (width >= 0f)
            element.preferredWidth = element.minWidth = width;
        if (height >= 0f)
            element.preferredHeight = element.minHeight = height;
    }

    private static void Flexible(GameObject go, float weight)
    {
        LayoutElement element = go.GetComponent<LayoutElement>();
        if (element == null)
            element = go.AddComponent<LayoutElement>();
        element.flexibleWidth = weight;
    }
}
