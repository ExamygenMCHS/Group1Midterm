
using UnityEngine;
using UnityEngine.UI;

public class DebugCropButton : MonoBehaviour
{
    [Header("Crop to give")]
    public PlantData crop;

    [Header("Amount of seeds")]
    public int seedAmount = 10;

    public void GiveFreeSeeds()
    {
        FarmInventory inventory = FindObjectOfType<FarmInventory>();

        if (inventory == null)
        {
            Debug.LogError("FarmInventory could not be found!");
            return;
        }

        if (crop == null)
        {
            Debug.LogError("No crop has been assigned!");
            return;
        }

        inventory.AddSeeds(crop, seedAmount);

        Debug.Log("Gave " + seedAmount + " seeds of " + crop.cropName);
    }
}
