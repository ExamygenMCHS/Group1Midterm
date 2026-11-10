using UnityEngine;
using TMPro;

//the store GUI of the wandering trader (the TraderShopCanvas prefab): buy seeds, the bag and the speed boots, sell the harvest.
//Every row shows the price of one item and lets the player choose how many. The trader calls PrepareShop() each time
//the shop opens; the money and the items change only through FarmFund and FarmInventory.
public class StoreControl : MonoBehaviour
{
    [SerializeField, TextArea]
    private string freeplayMessage = "Welcome, farmer! Buy supplies or sell your harvest.";
    [SerializeField]
    private StoreItemRow[] rows;

    [SerializeField]
    private TMP_Text storeMessage;
    [SerializeField]
    private TimedMessage feedbackMessage;

    [Header("Shop sounds")]
    [SerializeField]
    private AudioSource transactionAudio;
    [SerializeField]
    private AudioClip purchaseSound;
    [SerializeField]
    private AudioClip saleSound;

    private FarmFund farmFund;
    private FarmInventory inventory;

    private bool started;
    private bool listening;

    private void Start()
    {
        started = true;
        RefreshShop();
    }

    private void OnEnable()
    {
        if (started)
            RefreshShop();
    }

    private void OnDisable()
    {
        StopListening();
    }

    private void OnDestroy()
    {
        StopListening();
    }

    //The trader calls this before showing its store GUI.
    public void PrepareShop()
    {
        GiveStarterSeedsIfBroke();
        RefreshShop();
    }

    public const int StarterSeedCount = 2;

    //Free-play has no Try Again: a player with no seeds, no harvest to sell and too little money for the cheapest seed
    //(for example after winter withered every crop) gets a few seeds of that crop so the farm can start again.
    private void GiveStarterSeedsIfBroke()
    {
        FarmFund fund = FarmFund.Instance;
        if (fund == null || rows == null)
            return;
        FarmInventory stock = fund.Inventory;
        PlantData cheapest = null;
        foreach (StoreItemRow row in rows)
        {
            PlantData crop = row != null ? row.GetCrop() : null;
            if (crop == null)
                continue;
            if (stock.GetSeeds(crop) > 0 || stock.GetHarvested(crop) > 0)
                return;
            if (cheapest == null || crop.seedPrice < cheapest.seedPrice)
                cheapest = crop;
        }
        if (cheapest == null || fund.fund >= cheapest.seedPrice)
            return;
        stock.AddSeeds(cheapest, StarterSeedCount);
        if (feedbackMessage != null)
            feedbackMessage.Show("The trader hands you " + StarterSeedCount + " " + cheapest.cropName.ToLower() + " seeds to start over.");
    }

    //Rebind each visit: the same panel can be reopened, or the farm session can have been replaced.
    public void RefreshShop()
    {
        StopListening();
        farmFund = FarmFund.Instance;
        inventory = farmFund != null ? farmFund.Inventory : null;
        if (storeMessage != null)
            storeMessage.text = freeplayMessage;

        //An inactive prefab may be configured before its rows' Awake methods have run.
        //Activation and the first Start perform the actual refresh in that case.
        if (!isActiveAndEnabled)
            return;
        if (farmFund != null && inventory != null)
        {
            farmFund.fundChangedEvent.AddListener(Refresh);
            inventory.inventoryChangedEvent.AddListener(Refresh);
            listening = true;
        }
        Refresh();
    }

    private void StopListening()
    {
        if (!listening)
            return;
        if (farmFund != null)
            farmFund.fundChangedEvent.RemoveListener(Refresh);
        if (inventory != null)
            inventory.inventoryChangedEvent.RemoveListener(Refresh);
        listening = false;
    }

    // ---------------------------------------------------------------- buttons

    //API prepared for the "Buy Seed" button's On Click (): buys the chosen number of seeds of the first seed row
    public void BuySeed()
    {
        foreach (StoreItemRow row in rows)
        {
            if (row.isActiveAndEnabled && row.GetKind() == StoreItemRow.RowKind.BuySeeds)
            {
                BuySeeds(row.GetCrop(), row.GetQuantity());
                return;
            }
        }
    }

    public bool BuySeeds(PlantData crop, int amount)
    {
        if (farmFund == null || inventory == null || crop == null || amount <= 0 || crop.seedPrice < 0)
            return false;
        long total = (long)crop.seedPrice * amount;
        if (total > int.MaxValue || amount > int.MaxValue - inventory.GetSeeds(crop))
        {
            feedbackMessage.Show("Choose a smaller number of seeds.");
            return false;
        }
        int price = (int)total;
        if (!TryPay(price))
            return false;
        inventory.AddSeeds(crop, amount);
        PlaySound(purchaseSound);
        string seeds = amount + " " + crop.cropName.ToLower() + (amount == 1 ? " seed" : " seeds");
        feedbackMessage.Show("Bought " + seeds + " for $ " + price + ".");
        return true;
    }

    public bool SellHarvest(PlantData crop, int amount)
    {
        if (farmFund == null || inventory == null || crop == null || amount <= 0 || crop.sellPrice < 0)
            return false;
        int owned = inventory.GetHarvested(crop);
        if (owned < amount)
        {
            feedbackMessage.Show("You have " + owned + " " + crop.NameFor(owned).ToLower() + ", but tried to sell " + amount + ".");
            return false;
        }
        long total = (long)crop.sellPrice * amount;
        if (total > int.MaxValue - (long)farmFund.fund)
        {
            feedbackMessage.Show("Sell fewer crops at a time.");
            return false;
        }
        if (!inventory.TryRemoveHarvested(crop, amount))
            return false;
        int earned = (int)total;
        farmFund.AddFund(earned);
        PlaySound(saleSound);
        feedbackMessage.Show("Sold " + amount + " " + crop.NameFor(amount).ToLower() + " for $ " + earned + ".");
        return true;
    }

    public bool BuySpeedBoots(int price)
    {
        if (inventory == null)
            return false;
        if (inventory.speedBoots)
        {
            feedbackMessage.Show("You already own the Speed Boots.");
            return false;
        }
        if (!TryPay(price))
            return false;
        inventory.SetSpeedBoots(true);
        PlaySound(purchaseSound);
        feedbackMessage.Show("Speed Boots bought! Your walking speed is upgraded.");
        return true;
    }

    public bool BuyBag(int price)
    {
        if (inventory == null)
            return false;
        if (inventory.bag)
        {
            feedbackMessage.Show("You already own the bag. Open it with B or the bag button.");
            return false;
        }
        if (!TryPay(price))
            return false;
        inventory.SetBag(true);//the main bag can be opened now (the bar at the bottom already showed)
        PlaySound(purchaseSound);
        feedbackMessage.Show("Bag bought! Open it with B or the bag button.");
        return true;
    }

    private bool TryPay(int price)
    {
        if (farmFund == null || price < 0)
            return false;
        if (farmFund.fund < price)
        {
            feedbackMessage.ShowRejection("Not enough fund: this costs $ " + price + ". Sell some harvested crops first.");
            return false;
        }
        return farmFund.TrySpendFund(price);
    }

    private void PlaySound(AudioClip clip)
    {
        if (transactionAudio != null && clip != null)
            transactionAudio.PlayOneShot(clip);
    }

    private void Refresh()
    {
        if (rows == null)
            return;
        foreach (StoreItemRow row in rows)
            if (row != null && row.isActiveAndEnabled)
                row.Refresh();
    }
}
