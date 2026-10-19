using UnityEngine;
using UnityEngine.UI;
using TMPro;

//one line of the store: the item, the price of one, how many (- / +), the total, and the buy or sell button.
//The buttons of the row call the methods below (On Click in the Inspector); the money and the items change in StoreControl.
public class StoreItemRow : MonoBehaviour
{
    public enum RowKind
    {
        BuySeeds,//seeds of a crop
        SellHarvest,//harvested plants of a crop
        BuySpeedBoots,//one-time upgrade: the player walks faster
        BuyBag//one-time item: the inventory bag that shows the seeds, harvest and tools
    }
    [SerializeField]
    private RowKind kind = RowKind.BuySeeds;
    [SerializeField]
    private PlantData crop;//the crop for seed and harvest rows
    [SerializeField]
    private int speedBootsPrice = 35;
    [SerializeField]
    private int bagPrice = 40;
    [SerializeField]
    private EnergySettings settings;//for the speed boots' speed bonus shown in the row
    [SerializeField]
    private int startQuantity = 1;
    [Tooltip("The first store visit that shows this row: 1 = after Scene1, 2 = after Scene3, 3 = after Scene4, 4 = after Scene5")]
    [SerializeField]
    private int firstVisit = 1;
    [Tooltip("The last store visit that shows this row (0 = every visit from the first one on)")]
    [SerializeField]
    private int lastVisit = 0;
    [SerializeField]
    private bool newItem = false;//shows a "NEW" tag on the second visit

    [SerializeField]
    private StoreControl store;
    [SerializeField]
    private TMP_Text priceLabel;//"$ 10 each"
    [SerializeField]
    private TMP_Text quantityLabel;
    [SerializeField]
    private TMP_Text totalLabel;//"Total $ 50"
    [SerializeField]
    private TMP_Text ownedLabel;//"You have 3"
    [SerializeField]
    private Button minusButton, plusButton, actionButton;
    [SerializeField]
    private GameObject newTag;

    private int quantity;
    private Color actionColor = Color.white;

    private void Awake()
    {
        quantity = Mathf.Max(1, startQuantity);
        Image image = actionButton != null ? actionButton.targetGraphic as Image : null;
        if (image != null)
            actionColor = image.color;
    }

    public RowKind GetKind()
    {
        return kind;
    }
    public PlantData GetCrop()
    {
        return crop;
    }
    public int GetFirstVisit()
    {
        return firstVisit;
    }
    //true when this row is offered on the given store visit (1 = the first one)
    public bool IsShownOnVisit(int visit)
    {
        return firstVisit <= visit && (lastVisit <= 0 || visit <= lastVisit);
    }
    public bool IsNewItem()
    {
        return newItem;
    }
    public int GetQuantity()
    {
        return quantity;
    }

    //price of one item
    public int GetUnitPrice()
    {
        switch (kind)
        {
            case RowKind.BuySeeds: return crop.seedPrice;
            case RowKind.SellHarvest: return crop.sellPrice;
            case RowKind.BuySpeedBoots: return speedBootsPrice;
            case RowKind.BuyBag: return bagPrice;
        }
        return 0;
    }

    public int GetTotalPrice()
    {
        return GetUnitPrice() * quantity;
    }

    // ---- On Click of the row's buttons

    public void IncreaseQuantity()
    {
        quantity = Mathf.Min(quantity + 1, MaxQuantity());
        Refresh();
    }
    public void DecreaseQuantity()
    {
        quantity = Mathf.Max(1, quantity - 1);
        Refresh();
    }
    public void SetQuantity(int amount)
    {
        quantity = Mathf.Clamp(amount, 1, MaxQuantity());
        Refresh();
    }

    //the row's Buy / Sell button
    public void Apply()
    {
        switch (kind)
        {
            case RowKind.BuySeeds:
                store.BuySeeds(crop, quantity);
                break;
            case RowKind.SellHarvest:
                store.SellHarvest(crop, quantity);
                break;
            case RowKind.BuySpeedBoots:
                store.BuySpeedBoots(speedBootsPrice);
                break;
            case RowKind.BuyBag:
                store.BuyBag(bagPrice);
                break;
        }
    }

    private int MaxQuantity()
    {
        switch (kind)
        {
            case RowKind.SellHarvest:
                return Mathf.Max(1, Inventory() != null ? Inventory().GetHarvested(crop) : 1);
            case RowKind.BuySpeedBoots:
            case RowKind.BuyBag:
                return 1;
        }
        return 99;
    }

    //called by the store whenever the fund or the inventory changes
    public void Refresh()
    {
        if (kind == RowKind.SellHarvest)
            quantity = Mathf.Clamp(quantity, 1, MaxQuantity());
        FarmInventory inventory = Inventory();
        int fund = FarmFund.Instance != null ? FarmFund.Instance.fund : 0;

        bool oneOfAKind = kind == RowKind.BuySpeedBoots || kind == RowKind.BuyBag;
        priceLabel.text = "$ " + GetUnitPrice() + (oneOfAKind ? "" : " each");
        if (quantityLabel != null)
            quantityLabel.text = quantity.ToString();
        if (totalLabel != null)
            totalLabel.text = (kind == RowKind.SellHarvest ? "Earn $ " : "Total $ ") + GetTotalPrice();

        bool canDo = true;
        switch (kind)
        {
            case RowKind.BuySeeds:
                ownedLabel.text = "You have " + (inventory != null ? inventory.GetSeeds(crop) : 0);
                canDo = fund >= GetTotalPrice();
                break;
            case RowKind.SellHarvest:
                int owned = inventory != null ? inventory.GetHarvested(crop) : 0;
                ownedLabel.text = "In storage: " + owned;
                canDo = owned >= quantity;
                break;
            case RowKind.BuySpeedBoots:
                bool hasBoots = inventory != null && inventory.speedBoots;
                int bonus = settings != null ? Mathf.RoundToInt((settings.speedBootsMultiplier - 1f) * 100f) : 40;
                ownedLabel.text = hasBoots ? "Owned" : "Walk " + bonus + "% faster";
                canDo = !hasBoots && fund >= speedBootsPrice;
                break;
            case RowKind.BuyBag:
                bool hasBag = inventory != null && inventory.bag;
                ownedLabel.text = hasBag ? "Owned: open it with B" : "Holds seeds, harvest, tools";
                canDo = !hasBag && fund >= bagPrice;
                break;
        }
        //the button stays clickable, so a click without enough fund can explain why nothing was bought
        if (actionButton != null)
        {
            Image image = actionButton.targetGraphic as Image;
            if (image != null)
                image.color = canDo ? actionColor : Color.Lerp(actionColor, new Color(0.7f, 0.7f, 0.7f), 0.75f);
        }
        if (minusButton != null)
            minusButton.interactable = quantity > 1;
        if (plusButton != null)
            plusButton.interactable = quantity < MaxQuantity();
    }

    public void SetNewTagShown(bool shown)
    {
        if (newTag != null)
            newTag.SetActive(shown);
    }

    private static FarmInventory Inventory()
    {
        return FarmFund.Instance != null ? FarmFund.Instance.Inventory : null;
    }
}
