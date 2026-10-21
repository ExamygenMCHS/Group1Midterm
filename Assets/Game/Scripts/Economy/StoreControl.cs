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
public class StoreControl : MonoBehaviour
{
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

    private void Start()
    {
        startNextLevelButton.interactable = false;//locked until the player buys the seeds
        farmFund = FarmFund.Instance;
        inventory = farmFund != null ? farmFund.Inventory : null;
        int levelsWon = farmFund != null ? farmFund.levelsWon : 1;
        visitIndex = Mathf.Clamp(levelsWon - 1, 0, visits.Length - 1);

        //later visits enable rows that were hidden before (and drop the ones the player no longer needs)
        foreach (StoreItemRow row in rows)
        {
            bool shown = row.IsShownOnVisit(visitIndex + 1);
            row.gameObject.SetActive(shown);
            row.SetNewTagShown(shown && row.IsNewItem() && row.GetFirstVisit() == visitIndex + 1);
        }
        storeMessage.text = visits[visitIndex].message;
        if (firstVisitOnly != null)
            firstVisitOnly.SetActive(visitIndex == 0);

        if (farmFund != null)
        {
            farmFund.fundChangedEvent.AddListener(Refresh);
            inventory.inventoryChangedEvent.AddListener(Refresh);
        }
        Refresh();
    }

    private void OnDestroy()
    {
        if (farmFund != null)
        {
            farmFund.fundChangedEvent.RemoveListener(Refresh);
            inventory.inventoryChangedEvent.RemoveListener(Refresh);
        }
    }

    public string GetNextLevelScene()
    {
        return visits[visitIndex].nextLevelScene;
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
        if (farmFund == null || amount <= 0)
            return false;
        int price = crop.seedPrice * amount;
        string seeds = amount + " " + crop.cropName.ToLower() + (amount == 1 ? " seed" : " seeds");
        if (!farmFund.TrySpendFund(price))
        {
            feedbackMessage.Show("Not enough fund: " + seeds + " cost $ " + price + ".");
            return false;
        }
        inventory.AddSeeds(crop, amount);
        PlaySound(purchaseSound);
        feedbackMessage.Show("Bought " + seeds + " for $ " + price + "." + (IsNextLevelUnlocked() ? " You can start the next level now." : ""));
        return true;
    }

    public bool SellHarvest(PlantData crop, int amount)
    {
        if (inventory == null || !inventory.TryRemoveHarvested(crop, amount))
        {
            feedbackMessage.Show("You have no " + crop.Plural().ToLower() + " to sell.");
            return false;
        }
        int earned = crop.sellPrice * amount;
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
        if (!farmFund.TrySpendFund(price))
        {
            feedbackMessage.Show("Not enough fund: the Speed Boots cost $ " + price + ".");
            return false;
        }
        inventory.SetSpeedBoots(true);
        PlaySound(purchaseSound);
        feedbackMessage.Show("Speed Boots bought! You will walk faster in the next level.");
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
        if (!farmFund.TrySpendFund(price))
        {
            feedbackMessage.Show("Not enough fund: the bag costs $ " + price + ".");
            return false;
        }
        inventory.SetBag(true);//the bag appears at the bottom of the screen with everything the player owns
        PlaySound(purchaseSound);
        feedbackMessage.Show("Bag bought! Your tools, seeds and harvest are in it now." + (IsNextLevelUnlocked() ? " You can start the next level now." : ""));
        return true;
    }

    private void PlaySound(AudioClip clip)
    {
        if (transactionAudio != null && clip != null)
            transactionAudio.PlayOneShot(clip);
    }

    //API prepared for the "Start Next Level" button's On Click ()
    public void StartNextLevel()
    {
        if (!startNextLevelButton.interactable)
            return;
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
        if (inventory == null)
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
        foreach (StoreItemRow row in rows)
            if (row.gameObject.activeSelf)
                row.Refresh();

        bool unlocked = IsNextLevelUnlocked();
        startNextLevelButton.interactable = unlocked;

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
            requirementLabel.text = text;
        }
    }
}
