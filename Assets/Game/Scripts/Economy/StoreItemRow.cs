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
        BuyBag,//one-time item: the inventory bag that shows the seeds, harvest and tools
        BuyBait,//worms or lures, any number
        SellFish//caught fish of one kind, the heaviest first
    }
    [SerializeField]
    private RowKind kind = RowKind.BuySeeds;
    [SerializeField]
    private PlantData crop;//the crop for seed and harvest rows
    [SerializeField]
    private BaitData bait;//the bait for bait rows
    [SerializeField]
    private FishData fish;//the fish for fish rows
    [SerializeField]
    private int speedBootsPrice = 35;
    [SerializeField]
    private int bagPrice = 40;
    [SerializeField]
    private EnergySettings settings;//for the speed boots' speed bonus shown in the row
    [SerializeField]
    private int startQuantity = 1;

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

    private int quantity;
    private Color actionColor = Color.white;
    private TMP_Text actionLabel;

    private void Awake()
    {
        quantity = Mathf.Max(1, startQuantity);
        Image image = actionButton != null ? actionButton.targetGraphic as Image : null;
        if (image != null)
            actionColor = image.color;
        if (actionButton != null)
            actionLabel = actionButton.GetComponentInChildren<TMP_Text>();
    }

    public RowKind GetKind()
    {
        return kind;
    }
    public PlantData GetCrop()
    {
        return crop;
    }
    public BaitData GetBait()
    {
        return bait;
    }
    public FishData GetFish()
    {
        return fish;
    }
    public int GetQuantity()
    {
        return quantity;
    }

    //price of one item (a fish row: the price of one pound, since every fish weighs something else)
    public int GetUnitPrice()
    {
        switch (kind)
        {
            case RowKind.BuySeeds: return crop.seedPrice;
            case RowKind.SellHarvest: return crop.sellPrice;
            case RowKind.BuySpeedBoots: return speedBootsPrice;
            case RowKind.BuyBag: return bagPrice;
            case RowKind.BuyBait: return bait != null ? bait.price : 0;
            case RowKind.SellFish: return fish != null ? fish.pricePerPound : 0;
        }
        return 0;
    }

    public int GetTotalPrice()
    {
        if (kind == RowKind.SellFish)//the heaviest fish of this kind in the bag, each by its own weight
            return Inventory() != null ? Inventory().GetFishValue(fish, quantity) : 0;
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
            case RowKind.BuyBait:
                store.BuyBait(bait, quantity);
                break;
            case RowKind.SellFish:
                store.SellFish(fish, quantity);
                break;
        }
    }

    private int MaxQuantity()
    {
        switch (kind)
        {
            case RowKind.SellHarvest:
                return Mathf.Max(1, Inventory() != null ? Inventory().GetHarvested(crop) : 1);
            case RowKind.SellFish:
                return Mathf.Max(1, Inventory() != null ? Inventory().GetFishCount(fish) : 1);
            case RowKind.BuySpeedBoots:
            case RowKind.BuyBag:
                return 1;
        }
        return 99;
    }

    //called by the store whenever the fund or the inventory changes
    public void Refresh()
    {
        if (kind == RowKind.SellHarvest || kind == RowKind.SellFish)
            quantity = Mathf.Clamp(quantity, 1, MaxQuantity());
        FarmInventory inventory = Inventory();
        int fund = FarmFund.Instance != null ? FarmFund.Instance.fund : 0;

        bool oneOfAKind = kind == RowKind.BuySpeedBoots || kind == RowKind.BuyBag;
        if (kind == RowKind.SellFish)
            priceLabel.text = "$ " + GetUnitPrice() + " / lb";
        else
            priceLabel.text = "$ " + GetUnitPrice() + (oneOfAKind ? "" : " each");
        if (quantityLabel != null)
            quantityLabel.text = quantity.ToString();
        bool selling = kind == RowKind.SellHarvest || kind == RowKind.SellFish;
        if (totalLabel != null)
            totalLabel.text = (selling ? "Earn $ " : "Total $ ") + GetTotalPrice();

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
                if (actionLabel != null)
                    actionLabel.text = hasBoots ? "Owned" : "Buy";
                canDo = !hasBoots && fund >= speedBootsPrice;
                break;
            case RowKind.BuyBag:
                bool hasBag = inventory != null && inventory.bag;
                ownedLabel.text = hasBag ? "Owned: open it with B" : "Holds seeds, harvest, tools";
                if (actionLabel != null)
                    actionLabel.text = hasBag ? "Owned" : "Buy";
                canDo = !hasBag && fund >= bagPrice;
                break;
            case RowKind.BuyBait:
                //the bag and the rod together
                ownedLabel.text = "You have " + (inventory != null && bait != null ? inventory.GetBaitOwned(bait) : 0);
                canDo = fund >= GetTotalPrice();
                break;
            case RowKind.SellFish:
                int fishCount = inventory != null ? inventory.GetFishCount(fish) : 0;
                string inBag = "In bag: " + fishCount;
                if (fishCount > 0)
                    inBag += "   best " + FishData.WeightText(inventory.GetBestFish(fish));
                ownedLabel.text = inBag;
                canDo = fishCount >= quantity;
                break;
        }
        //the button stays clickable, so a click without enough fund can explain why nothing was bought
        if (actionButton != null)
        {
            Image image = actionButton.targetGraphic as Image;
            if (image != null)
                image.color = canDo ? actionColor : Color.Lerp(actionColor, new Color(0.76f, 0.70f, 0.58f), 0.75f);
        }
        if (minusButton != null)
            minusButton.interactable = quantity > 1;
        if (plusButton != null)
            plusButton.interactable = quantity < MaxQuantity();
    }

    private static FarmInventory Inventory()
    {
        return FarmFund.Instance != null ? FarmFund.Instance.Inventory : null;
    }
}
