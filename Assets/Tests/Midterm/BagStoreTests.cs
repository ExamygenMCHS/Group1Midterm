using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// Midterm Task 1: the trader's store sells the bag and shows it once it is bought; the bag's sprites follow what is
// bought and sold.
public class BagStoreTests : MidtermTestBase
{
    [Test]
    public void EveryPlant_HasBagItems_WithItsOwnSpritesAndPrices()
    {
        foreach (string name in new[] { "Sunflower", "Pumpkin" })
        {
            var plant = (PlantData)Crop(name);
            Assert.IsNotNull(plant.seedItem, name + " has a seed item for the bag");
            Assert.IsNotNull(plant.harvestItem, name + " has a harvest item for the bag");
            Assert.AreEqual(ItemType.seed, plant.seedItem.itemType);
            Assert.AreEqual(ItemType.harvest, plant.harvestItem.itemType);
            Assert.AreEqual(plant.seedIcon, plant.seedItem.itemSprite, "the bag shows the same seed icon as the HUD");
            Assert.AreEqual(plant.icon, plant.harvestItem.itemSprite);
            Assert.AreEqual(plant.seedPrice, plant.seedItem.buyPrice, name + ": the bag item's price matches the plant's");
            Assert.AreEqual(plant.sellPrice, plant.harvestItem.sellPrice, name + ": the bag item's price matches the plant's");
            Assert.AreNotEqual(plant.seedItem.itemName, plant.harvestItem.itemName, "the bag tells stacks apart by name");
        }
    }

    [UnityTest]
    public IEnumerator Store_SellsTheBag_AndShowsItOnceItIsBought()
    {
        StartSession(150, 0);
        AddHarvest("Pumpkin", 3);
        AddHarvest("Sunflower", 3);
        GiveToolsOnly();
        yield return Load(Farm);
        FarmItems().SetBag(false); // a new game starts without the bag; this player has to buy it
        yield return OpenShop();

        Assert.AreEqual("$ 60", RowText("Row - Bag", "Price"));
        Assert.IsFalse(Bag().IsShown(), "the bag shows once the player owns it");

        ButtonNamed("Sell Sunflowers").onClick.Invoke(); // the quantity is clamped to what is in storage
        ButtonNamed("Sell Pumpkins").onClick.Invoke();
        yield return null;
        Assert.IsTrue(Fund > 150, "selling adds to the fund");
        int fundBeforeBag = Fund;

        ButtonNamed("Buy Bag").onClick.Invoke();
        yield return null;
        Assert.AreEqual(fundBeforeBag - 60, Fund, "the bag costs $60");
        Assert.IsTrue(FarmItems().bag);
        Assert.IsTrue(Bag().IsShown(), "the bag appears right after it is bought");

        //buying it twice must not charge
        int fundBefore = Fund;
        ButtonNamed("Buy Bag").onClick.Invoke();
        Assert.AreEqual(fundBefore, Fund);
        StringAssert.Contains("already own the bag", TextOf("StoreFeedback"));
        StringAssert.Contains("Owned", RowText("Row - Bag", "Owned"));
    }

    [UnityTest]
    public IEnumerator Store_WithTheBag_TheSpritesFollowBuyingAndSelling()
    {
        StartBagSession(100);
        AddHarvest("Pumpkin", 4);
        yield return Load(Farm);
        yield return OpenShop();
        Assert.IsTrue(Bag().IsShown());
        Assert.AreEqual(4, InBag("Pumpkin"), "the harvest is in the bag");

        Call(Row("Row - Sell Pumpkins"), "SetQuantity", 3);
        ButtonNamed("Sell Pumpkins").onClick.Invoke();
        yield return null;
        Assert.AreEqual(1, InBag("Pumpkin"), "selling takes the plants out of the bag");
        Assert.AreEqual(235, Fund);

        Call(Row("Row - Sunflower Seeds"), "SetQuantity", 2);
        ButtonNamed("Buy Seed").onClick.Invoke();
        yield return null;
        Assert.AreEqual(2, InBag("Sunflower Seeds"), "buying puts the seeds into the bag");

        ButtonNamed("Sell Pumpkins").onClick.Invoke(); // the last one
        yield return null;
        Assert.IsNull(Bag().GetInventoryManager().GetItem("Pumpkin"), "the last one sold: its sprite leaves the bag");
    }

    private static void GiveToolsOnly()
    {
        foreach (var tool in new[] { ToolManager.ToolType.Hoe, ToolManager.ToolType.WateringCan, ToolManager.ToolType.Sickle })
            FarmItems().AddTool(tool);
    }
}
