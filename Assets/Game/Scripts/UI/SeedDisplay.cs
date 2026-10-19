using UnityEngine;
using UnityEngine.UI;
using TMPro;

//keeps the "Seeds: 0" UI text showing how many seeds the player owns
//the seeds are in the Farm Fund's inventory. In a farming level the label shows the seed the player has in hand
//(PlantManager, Q switches it when the level has more than one crop); without a plant manager it shows all seeds.
public class SeedDisplay : MonoBehaviour
{
    [SerializeField]
    private TMP_Text seedLabel;
    [SerializeField]
    private PlantManager plantManager;//optional
    [SerializeField]
    private Image seedIcon;//optional: shows the seed of the crop in hand

    private FarmInventory inventory;

    private void Start()
    {
        if (FarmFund.Instance != null)
        {
            inventory = FarmFund.Instance.Inventory;
            inventory.inventoryChangedEvent.AddListener(UpdateLabel);
        }
        if (plantManager != null)
            plantManager.selectedSeedChangedEvent.AddListener(UpdateLabel);
        UpdateLabel();
    }
    private void OnDestroy()
    {
        if (inventory != null)
            inventory.inventoryChangedEvent.RemoveListener(UpdateLabel);
        if (plantManager != null)
            plantManager.selectedSeedChangedEvent.RemoveListener(UpdateLabel);
    }

    private void UpdateLabel()
    {
        if (inventory == null)
        {
            seedLabel.text = "Seeds: 0";
            return;
        }
        PlantData crop = plantManager != null ? plantManager.GetSelectedCrop() : null;
        if (crop == null)
            seedLabel.text = "Seeds: " + inventory.GetTotalSeeds().ToString();
        else if (plantManager.GetCropCount() > 1)
            seedLabel.text = crop.cropName + " seeds: " + inventory.GetSeeds(crop).ToString();
        else
            seedLabel.text = "Seeds: " + inventory.GetSeeds(crop).ToString();
        if (seedIcon != null && crop != null && crop.seedIcon != null)
            seedIcon.sprite = crop.seedIcon;
    }
}
