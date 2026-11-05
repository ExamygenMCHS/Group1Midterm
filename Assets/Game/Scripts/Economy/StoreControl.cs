using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

//the store in Scene2-Store. The "Start Next Level" button only works after a seed was bought.
//the store is loaded again after every farming level, and what it offers depends on how many levels the
//player has won (FarmFund.levelsWon). After Scene1: seeds for the plant level. After Scene3: the player also sells the
//harvest and finds new items (pumpkin seeds, speed boots) for Scene4. After Scene4: the inventory bag, which Scene5
//needs; from then on the bag shows in the store too. After Scene5: sell the harvest, seeds for Scene6.
//Every row shows the price of one item and lets the player choose how many. "Start Next Level" unlocks once the player
//owns what the next level needs (its seeds, and the bag when the visit asks for it).
//Freeplay mode reuses these transactions for the trader without visit locks or scene transitions.
public class StoreControl : MonoBehaviour
{
    public enum ShopMode
    {
        LevelProgression,
        Freeplay
    }

    [Header("Shop mode")]
    [SerializeField]
    private ShopMode shopMode = ShopMode.LevelProgression;
    [SerializeField, TextArea]
    private string freeplayMessage = "Welcome, farmer! Buy supplies or sell your harvest.";

    public bool IsFreeplay { get { return shopMode == ShopMode.Freeplay; } }

    [Serializable]
    public class SeedNeed
    {
        public PlantData crop;
        public int count;
    }

    [Serializable]
    public class StoreVisit
    {
        public string nextLevelScene = "Scene3-Plant";
        [TextArea]
        public string message;
        public SeedNeed[] seedsNeeded;//seeds the next level needs; Start Next Level stays locked until the player owns them
        public bool needsBag;//the next level needs the inventory bag
    }

    [SerializeField]
    private StoreVisit[] visits;//[0] = first visit (after Scene1), [1] after Scene3, [2] after Scene4, [3] after Scene5
    [SerializeField]
    private StoreItemRow[] rows;

    [SerializeField]
    private TMP_Text storeMessage;
    [SerializeField]
    private TMP_Text requirementLabel;//"The next level needs 5 sunflower seeds"
    [SerializeField]
    private Button startNextLevelButton;
    [SerializeField]
    private TimedMessage feedbackMessage;
    [SerializeField]
    private LoadScene loadScene;
    [SerializeField]
    private GameObject firstVisitOnly;//"more items after the next harvest" hint, hidden on later visits

    [Header("Shop sounds")]
    [SerializeField]
    private AudioSource transactionAudio;
    [SerializeField]
    private AudioClip purchaseSound;
    [SerializeField]
    private AudioClip saleSound;

    private int visitIndex = 0;
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

    //The trader calls this before showing its reused store GUI. Existing store scenes keep LevelProgression.
    public void ConfigureFreeplay()
    {
        shopMode = ShopMode.Freeplay;
        RefreshShop();
    }

    //Rebind each visit: the same panel can be reopened, or the farm session can have been replaced.
    public void RefreshShop()
    {
        StopListening();
        farmFund = FarmFund.Instance;
        inventory = farmFund != null ? farmFund.Inventory : null;
        if (!IsFreeplay)
        {
            if (visits == null || visits.Length == 0)
            {
                Debug.LogError("A level-progression store needs at least one StoreVisit.", this);
                return;
            }
            int levelsWon = farmFund != null ? farmFund.levelsWon : 1;
            visitIndex = Mathf.Clamp(levelsWon - 1, 0, visits.Length - 1);
        }

        if (rows != null)
            foreach (StoreItemRow row in rows)
            {
                if (row == null)
                    continue;
                bool shown = IsFreeplay || row.IsShownOnVisit(visitIndex + 1);
                row.gameObject.SetActive(shown);
                row.SetNewTagShown(!IsFreeplay && shown && row.IsNewItem() && row.GetFirstVisit() == visitIndex + 1);
            }
        if (storeMessage != null)
            storeMessage.text = IsFreeplay ? freeplayMessage : visits[visitIndex].message;
        if (firstVisitOnly != null)
            firstVisitOnly.SetActive(!IsFreeplay && visitIndex == 0);
        if (startNextLevelButton != null)
            startNextLevelButton.gameObject.SetActive(!IsFreeplay);
        if (requirementLabel != null)
            requirementLabel.gameObject.SetActive(!IsFreeplay);

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

    public string GetNextLevelScene()
    {
        return IsFreeplay ? string.Empty : visits[visitIndex].nextLevelScene;
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
        if (!TryPay(price, crop, amount))
            return false;
        inventory.AddSeeds(crop, amount);
        PlaySound(purchaseSound);
        string seeds = amount + " " + crop.cropName.ToLower() + (amount == 1 ? " seed" : " seeds");
        feedbackMessage.Show("Bought " + seeds + " for $ " + price + "." + (IsNextLevelUnlocked() ? " You can start the next level now." : ""));
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
        {
            if (farmFund.fund < price)
                feedbackMessage.Show("Not enough fund: the Speed Boots cost $ " + price + ".");
            return false;
        }
        inventory.SetSpeedBoots(true);
        PlaySound(purchaseSound);
        feedbackMessage.Show(IsFreeplay ? "Speed Boots bought! Your walking speed is upgraded." : "Speed Boots bought! You will walk faster in the next level.");
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
        if (!TryPay(price, buyingBag: true))
        {
            if (farmFund.fund < price)
                feedbackMessage.Show("Not enough fund: the bag costs $ " + price + ".");
            return false;
        }
        inventory.SetBag(true);//the bag appears at the bottom of the screen with everything the player owns
        PlaySound(purchaseSound);
        feedbackMessage.Show("Bag bought! Your tools, seeds and harvest are in it now." + (IsNextLevelUnlocked() ? " You can start the next level now." : ""));
        return true;
    }

    //keep enough money for required seeds and the bag, even if u buy extra items first
    private bool TryPay(int price, PlantData boughtCrop = null, int seedCount = 0, bool buyingBag = false)
    {
        if (farmFund == null || price < 0)
            return false;
        if (farmFund.fund < price)
        {
            feedbackMessage.Show("Not enough fund: this costs $ " + price + ". Sell some harvested crops first.");
            return false;
        }

        //Freeplay has no next-level shopping list or reserved fund.
        if (IsFreeplay)
            return farmFund.TrySpendFund(price);

        long stillNeeded = 0;
        foreach (SeedNeed need in visits[visitIndex].seedsNeeded)
        {
            long have = inventory.GetSeeds(need.crop) + (need.crop == boughtCrop ? (long)seedCount : 0);
            stillNeeded += Math.Max(0, need.count - have) * need.crop.seedPrice;
        }
        if (visits[visitIndex].needsBag && !inventory.bag && !buyingBag)
            foreach (StoreItemRow row in rows)
                if (row.isActiveAndEnabled && row.GetKind() == StoreItemRow.RowKind.BuyBag)
                {
                    stillNeeded += row.GetUnitPrice();
                    break;
                }

        //harvest that can be sold here also counts, so u can sell it after shopping
        long canSell = 0;
        foreach (FarmInventory.CropStock stock in inventory.crops)
            foreach (StoreItemRow row in rows)
                if (row.isActiveAndEnabled && row.GetKind() == StoreItemRow.RowKind.SellHarvest && row.GetCrop() == stock.crop)
                {
                    canSell += (long)stock.harvested * stock.crop.sellPrice;
                    break;
                }
        if ((long)farmFund.fund - price + canSell < stillNeeded)
        {
            feedbackMessage.Show("Keep $ " + stillNeeded + " for required seeds and the bag. Buy those first, or sell harvest.");
            return false;
        }
        return farmFund.TrySpendFund(price);
    }

    private void PlaySound(AudioClip clip)
    {
        if (transactionAudio != null && clip != null)
            transactionAudio.PlayOneShot(clip);
    }

    //API prepared for the "Start Next Level" button's On Click ()
    public void StartNextLevel()
    {
        if (IsFreeplay)
            return;
        if (!IsNextLevelUnlocked())
        {
            feedbackMessage.Show("You can't start a new level without meeting the requirements!");
            return;
        }
        if (!Application.CanStreamedLevelBeLoaded(GetNextLevelScene()))//a level a teammate has not added to the build yet
        {
            feedbackMessage.Show(GetNextLevelScene() + " is not in the game yet.");
            return;
        }
        loadScene.LadScenebyName(GetNextLevelScene());
    }

    // ---------------------------------------------------------------- lock and texts

    public bool IsNextLevelUnlocked()
    {
        if (IsFreeplay || inventory == null)
            return false;
        if (visits[visitIndex].needsBag && !inventory.bag)
            return false;
        foreach (SeedNeed need in visits[visitIndex].seedsNeeded)
            if (inventory.GetSeeds(need.crop) < need.count)
                return false;
        return true;
    }

    private void Refresh()
    {
        if (rows != null)
            foreach (StoreItemRow row in rows)
                if (row != null && row.isActiveAndEnabled)
                    row.Refresh();

        bool unlocked = IsNextLevelUnlocked();
        if (startNextLevelButton != null)
            startNextLevelButton.interactable = unlocked;
        if (IsFreeplay)
            return;

        if (requirementLabel != null)
        {
            string text = "Next level needs: ";
            bool first = true;
            if (visits[visitIndex].needsBag)
            {
                bool hasBag = inventory != null && inventory.bag;
                text += hasBag ? "<color=#3E8E2A>the bag - ready</color>" : "the bag";
                first = false;
            }
            SeedNeed[] needs = visits[visitIndex].seedsNeeded;
            for (int i = 0; i < needs.Length; i++)
            {
                int have = inventory != null ? inventory.GetSeeds(needs[i].crop) : 0;
                bool ok = have >= needs[i].count;
                text += (first ? "" : ",  ") + (ok ? "<color=#3E8E2A>" : "") + needs[i].count + " " + needs[i].crop.cropName.ToLower()
                    + " seeds (you have " + have + ")" + (ok ? " - ready</color>" : "");
                first = false;
            }
            string reminder = unlocked ? "\nYou're ready! You have what you need for the next level." : "\nYou can't start a new level without meeting the requirements!";
            requirementLabel.text = text + reminder;
        }
    }
}
