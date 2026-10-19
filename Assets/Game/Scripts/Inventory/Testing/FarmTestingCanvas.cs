using UnityEngine;
using TMPro;

//the testing canvas for the farm's bag (InventoryTest scene): like the instructor's testing canvas, but its buttons
//change the Farm Fund and its inventory the same way the store and the farm do, so the bag + shop logic can be tested
//without the 3D world. Buy and sell use the plant's prices, plant takes a seed, harvest adds a harvested plant.
//Testing only: the game itself never uses it.
public class FarmTestingCanvas : MonoBehaviour
{
    [SerializeField]
    private PlantData[] crops;//index used by the buttons: 0 = sunflower, 1 = pumpkin
    [SerializeField]
    private int bagPrice = 40;
    [SerializeField]
    private TMP_Text fundLabel;
    [SerializeField]
    private TMP_Text resultLabel;//what the last button did

    private FarmFund farmFund;

    private void Start()
    {
        farmFund = FarmFund.Instance;
        if (farmFund != null)
            farmFund.fundChangedEvent.AddListener(ShowFund);
        ShowFund();
    }

    private void OnDestroy()
    {
        if (farmFund != null)
            farmFund.fundChangedEvent.RemoveListener(ShowFund);
    }

    // ---- On Click of the buttons

    public void BuyBag()
    {
        if (farmFund.Inventory.bag)
            Show("The bag is already bought.");
        else if (farmFund.TrySpendFund(bagPrice))
        {
            farmFund.Inventory.SetBag(true);
            Show("Bought the bag for $ " + bagPrice + ".");
        }
        else
            Show("Not enough fund for the bag.");
    }

    public void GiveTools()
    {
        farmFund.Inventory.AddTool(ToolManager.ToolType.Hoe);
        farmFund.Inventory.AddTool(ToolManager.ToolType.WateringCan);
        farmFund.Inventory.AddTool(ToolManager.ToolType.Sickle);
        Show("Added the hoe, the watering can and the sickle.");
    }

    public void BuySeed(int index)
    {
        PlantData crop = crops[index];
        if (farmFund.TrySpendFund(crop.seedPrice))
        {
            farmFund.Inventory.AddSeeds(crop, 1);
            Show("Bought 1 " + crop.cropName.ToLower() + " seed for $ " + crop.seedPrice + ".");
        }
        else
            Show("Not enough fund for a " + crop.cropName.ToLower() + " seed.");
    }

    public void PlantSeed(int index)
    {
        PlantData crop = crops[index];
        Show(farmFund.Inventory.TryUseSeed(crop) ? "Planted 1 " + crop.cropName.ToLower() + " seed." : "No " + crop.cropName.ToLower() + " seeds to plant.");
    }

    public void HarvestPlant(int index)
    {
        PlantData crop = crops[index];
        farmFund.Inventory.AddHarvested(crop, 1);
        Show("Harvested 1 " + crop.cropName.ToLower() + ".");
    }

    public void SellHarvest(int index)
    {
        PlantData crop = crops[index];
        if (farmFund.Inventory.TryRemoveHarvested(crop, 1))
        {
            farmFund.AddFund(crop.sellPrice);
            Show("Sold 1 " + crop.cropName.ToLower() + " for $ " + crop.sellPrice + ".");
        }
        else
            Show("No " + crop.Plural().ToLower() + " to sell.");
    }

    private void ShowFund()
    {
        if (fundLabel != null)
            fundLabel.text = "Fund: $ " + (farmFund != null ? farmFund.fund : 0);
    }

    private void Show(string text)
    {
        if (resultLabel != null)
            resultLabel.text = text;
    }
}
