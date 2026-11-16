using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

//Every crop the trader sells, on its own from start to finish on The Farm: the row's Buy button gives that crop's
//seeds, the seed is planted, the plant shows its three stage models as the days pass, the sickle harvests it into
//the bag, and the row's Sell button sells it.
public class CropLifecycleTests : HW6TestBase
{
    private static readonly string[] ShopCrops = { "Sunflower", "Pumpkin", "Tomato", "Cabbage", "Carrot", "Corn", "Wheat", "Potato" };

    [UnityTest]
    public IEnumerator Crop_BuyPlantGrowHarvestSell([ValueSource(nameof(ShopCrops))] string cropName)
    {
        PlantData crop = (PlantData)Crop(cropName);
        StartSession(500, 0);
        yield return Load(Farm);
        FreezeCalendar();

        // ---- buy one seed with the row's own Buy button
        yield return OpenShop();
        StoreItemRow seedRow = (StoreItemRow)Row("Row - " + cropName + " Seeds");
        Assert.AreSame(crop, seedRow.GetCrop(), "the seed row sells " + cropName);
        seedRow.SetQuantity(1);
        ActionButton(seedRow).onClick.Invoke();
        Assert.AreEqual(1, SeedsOf(cropName), "the Buy button gives " + cropName + " seeds");
        Assert.AreEqual(1, Seeds, "and no seeds of any other crop");
        Assert.AreEqual(500 - crop.seedPrice, Fund);
        Assert.AreEqual(1, BagAmount(crop.seedItem), "the bag shows the " + crop.seedItem.itemName);
        CloseShop();
        yield return null;

        // ---- plant it on a watered tile, in a season the crop accepts
        GameCalendar calendar = (GameCalendar)Calendar();
        int growDays = crop.daysToGrowing + crop.daysToMature;
        for (int i = 0; i < 100 && (!crop.CanPlantIn(calendar.season) ||
            (crop.disappears && calendar.DaysUntil(crop.disappearSeason) <= growDays)); i++)
            AdvanceDays(1);
        Assert.IsTrue(crop.CanPlantIn(calendar.season), cropName + " has a planting season");

        Component tile = Tiles()[0];
        Water(tile);
        Hold("SeedBag", crop);
        Assert.AreSame(crop, ((Hotbar)Hotbar()).GetSelectedCrop(), "the " + cropName + " seeds are in the hand");
        yield return EOn(tile);
        Plant plant = (Plant)PlantOnTile(tile);
        Assert.IsNotNull(plant, "a " + cropName + " was planted");
        Assert.AreSame(crop, plant.crop);
        Assert.AreEqual(0, SeedsOf(cropName), "planting used the seed");
        AssertStage(plant, Plant.PlantStage.Planted, plant.plantedModel);

        // ---- it grows through its stages
        PassDays(crop.daysToGrowing);
        AssertStage(plant, Plant.PlantStage.Growing, plant.growingModel);
        PassDays(crop.daysToMature);
        AssertStage(plant, Plant.PlantStage.Mature, plant.matureModel);

        // ---- the sickle harvests it into the bag
        yield return EOn(tile);
        Assert.AreEqual(1, HarvestedOf(cropName), "the " + cropName + " is in storage");
        Assert.AreEqual(1, BagAmount(crop.harvestItem), "the bag shows the harvested " + crop.harvestItem.itemName);
        Assert.That(SeedsOf(cropName), Is.InRange(crop.seedDropMin, crop.seedDropMax), "the harvest gives back seeds");
        Assert.AreEqual(SeedsOf(cropName), BagAmount(crop.seedItem));
        yield return new WaitForSeconds(0.3f);
        Assert.IsTrue(plant == null, "the harvested plant is removed from the farm");

        // ---- and the row's Sell button sells it
        int fundBeforeSale = Fund;
        yield return OpenShop();
        StoreItemRow sellRow = (StoreItemRow)Row("Row - Sell " + crop.Plural());
        Assert.AreSame(crop, sellRow.GetCrop());
        ActionButton(sellRow).onClick.Invoke();
        Assert.AreEqual(0, HarvestedOf(cropName), "the Sell button sold the " + cropName);
        Assert.AreEqual(fundBeforeSale + crop.sellPrice, Fund);
        Assert.AreEqual(0, BagAmount(crop.harvestItem));
        CloseShop();
        yield return null;
    }

    private static Button ActionButton(StoreItemRow row)
    {
        return (Button)Get(row, "actionButton");
    }

    private static void CloseShop()
    {
        Object.FindAnyObjectByType<TraderShopPanel>().Close();
    }

    private static int BagAmount(InventoryItemData item)
    {
        InventoryManager bag = (InventoryManager)Get(Object.FindAnyObjectByType<FarmBag>(), "inventoryManager");
        return bag.GetAmount(item.itemName);
    }

    private static void AssertStage(Plant plant, Plant.PlantStage stage, GameObject model)
    {
        Assert.AreEqual(stage, plant.plantStage);
        foreach (GameObject stageModel in new[] { plant.plantedModel, plant.growingModel, plant.matureModel })
            Assert.AreEqual(stageModel == model, stageModel.activeSelf, stage + ": only the " + model.name + " model shows");
        Assert.IsTrue(model.GetComponentInChildren<Renderer>() != null, model.name + " has something to draw");
    }
}
