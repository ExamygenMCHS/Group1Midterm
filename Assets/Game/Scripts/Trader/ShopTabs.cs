using UnityEngine;
using UnityEngine.UI;
using TMPro;

//The trader's shop shows one tab at a time: buy seeds, sell crops, equipment (the bag and the speed boots), or fishing
//(bait to buy, fish to sell).
//Each store row goes to a tab by its kind, so a new seed or crop row needs no wiring here.
public class ShopTabs : MonoBehaviour
{
    public enum Tab
    {
        BuySeeds,
        SellCrops,
        Equipment,
        Fishing
    }

    [SerializeField] private StoreControl store;
    [SerializeField] private Button seedsTab;
    [SerializeField] private Button cropsTab;
    [SerializeField] private Button equipmentTab;
    [SerializeField] private Button fishingTab;//bait to buy, fish to sell
    [SerializeField] private ScrollRect scroll;
    [SerializeField] private Tab firstTab = Tab.BuySeeds;

    [Header("Look")]
    [SerializeField] private Color selectedColor = new Color(0.36f, 0.69f, 0.27f);
    [SerializeField] private Color idleColor = new Color(0.80f, 0.70f, 0.52f);
    [SerializeField] private Color selectedTextColor = Color.white;
    [SerializeField] private Color idleTextColor = new Color(0.36f, 0.25f, 0.15f);

    private bool started;

    public Tab Current { get; private set; }

    private void Awake()
    {
        Listen(seedsTab, ShowSeeds);
        Listen(cropsTab, ShowCrops);
        Listen(equipmentTab, ShowEquipment);
        Listen(fishingTab, ShowFishing);
    }

    //Rows hide in Start, after every row's Awake has run, so a hidden row still knows its quantity and labels.
    private void Start()
    {
        started = true;
        Show(Current = firstTab);
    }

    private void OnDestroy()
    {
        Unlisten(seedsTab, ShowSeeds);
        Unlisten(cropsTab, ShowCrops);
        Unlisten(equipmentTab, ShowEquipment);
        Unlisten(fishingTab, ShowFishing);
    }

    //Every visit opens on the first tab, so the shop always greets the player the same way.
    public void ShowFirstTab()
    {
        Current = firstTab;
        if (started)
            Show(firstTab);
    }

    public void ShowSeeds() { Show(Tab.BuySeeds); }
    public void ShowCrops() { Show(Tab.SellCrops); }
    public void ShowEquipment() { Show(Tab.Equipment); }
    public void ShowFishing() { Show(Tab.Fishing); }

    public void Show(Tab tab)
    {
        Current = tab;
        if (store != null && store.Rows != null)
            foreach (StoreItemRow row in store.Rows)
                if (row != null)
                    row.gameObject.SetActive(TabOf(row.GetKind()) == tab);
        Paint(seedsTab, tab == Tab.BuySeeds);
        Paint(cropsTab, tab == Tab.SellCrops);
        Paint(equipmentTab, tab == Tab.Equipment);
        Paint(fishingTab, tab == Tab.Fishing);
        if (scroll != null && scroll.content != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);
            scroll.verticalNormalizedPosition = 1f;
        }
    }

    public static Tab TabOf(StoreItemRow.RowKind kind)
    {
        switch (kind)
        {
            case StoreItemRow.RowKind.SellHarvest: return Tab.SellCrops;
            case StoreItemRow.RowKind.BuySpeedBoots:
            case StoreItemRow.RowKind.BuyBag: return Tab.Equipment;
            case StoreItemRow.RowKind.BuyBait:
            case StoreItemRow.RowKind.SellFish: return Tab.Fishing;
        }
        return Tab.BuySeeds;
    }

    private void Paint(Button tab, bool selected)
    {
        if (tab == null)
            return;
        if (tab.targetGraphic != null)
            tab.targetGraphic.color = selected ? selectedColor : idleColor;
        TMP_Text label = tab.GetComponentInChildren<TMP_Text>();
        if (label != null)
            label.color = selected ? selectedTextColor : idleTextColor;
    }

    private static void Listen(Button tab, UnityEngine.Events.UnityAction action)
    {
        if (tab != null)
            tab.onClick.AddListener(action);
    }

    private static void Unlisten(Button tab, UnityEngine.Events.UnityAction action)
    {
        if (tab != null)
            tab.onClick.RemoveListener(action);
    }
}
