using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// Midterm Task 1: the store sells the bag after Scene4, shows the bag once it is bought, and after Scene5 sells the
// harvest and the 2nd plant's seeds. The bag's sprites follow what is bought and sold.
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
    public IEnumerator Store_ThirdVisit_SellsTheBag_AndScene5NeedsIt()
    {
        StartSession(150, 0, 3); // after Scene4
        AddHarvest("Pumpkin", 3);
        AddHarvest("Sunflower", 3);
        GiveToolsOnly();
        yield return Load(Scene2);

        foreach (string row in new[] { "Row - Sell Sunflowers", "Row - Sell Pumpkins", "Row - Sunflower Seeds", "Row - Pumpkin Seeds", "Row - Bag" })
            Assert.IsTrue(FindInScene(row).activeInHierarchy, row + " is offered on the third visit");
        Assert.IsFalse(FindInScene("Row - Speed Boots").activeInHierarchy, "the boots were bought on the second visit");
        Assert.IsTrue(FindInScene("Row - Bag").transform.Find("NewTag").gameObject.activeSelf, "the bag is new");
        Assert.AreEqual("$ 60", RowText("Row - Bag", "Price"));
        Assert.IsFalse(Bag().IsShown(), "the bag shows once the player owns it");

        ButtonNamed("Sell Sunflowers").onClick.Invoke(); // +45
        ButtonNamed("Sell Pumpkins").onClick.Invoke(); // +135
        Call(Row("Row - Sunflower Seeds"), "SetQuantity", 4);
        Call(Row("Row - Pumpkin Seeds"), "SetQuantity", 4);
        ButtonNamed("Buy Seed").onClick.Invoke(); // -40
        ButtonNamed("Buy Pumpkin Seed").onClick.Invoke(); // -80
        yield return null;
        Assert.AreEqual(210, Fund);
        Assert.IsFalse(ButtonNamed("Start Next Level").interactable, "Scene5 also needs the bag");
        StringAssert.Contains("the bag", TextOf("NextLevelNeeds"));

        ButtonNamed("Buy Bag").onClick.Invoke();
        yield return null;
        Assert.AreEqual(150, Fund, "the bag costs $60");
        Assert.IsTrue(FarmItems().bag);
        Assert.IsTrue(Bag().IsShown(), "the bag appears in the store right after it is bought");
        Assert.AreEqual(4, InBag("Sunflower Seeds"), "the seeds bought are in the bag");
        Assert.AreEqual(4, InBag("Pumpkin Seeds"));
        Assert.AreEqual(1, InBag("Hoe"));
        Assert.AreEqual(0, InBag("Pumpkin"), "the sold harvest is not");
        Assert.IsTrue(ButtonNamed("Start Next Level").interactable);

        ButtonNamed("Start Next Level").onClick.Invoke();
        yield return WaitForScene(Scene5);
        Assert.AreEqual(4, InBag("Sunflower Seeds"), "the bag carries the seeds into Scene5");
        Assert.AreEqual(4, InBag("Pumpkin Seeds"));
        Assert.AreEqual(150, Fund);
    }

    [UnityTest]
    public IEnumerator Store_WithTheBag_TheSpritesFollowBuyingAndSelling()
    {
        StartBagSession(100, 4); // after Scene5
        AddHarvest("Pumpkin", 4);
        yield return Load(Scene2);
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

    [UnityTest]
    public IEnumerator Store_FourthVisit_SellsTheHarvest_AndThe2ndPlantsSeeds_ForScene6()
    {
        StartBagSession(100, 4);
        AddHarvest("Sunflower", 4);
        AddHarvest("Pumpkin", 4);
        yield return Load(Scene2);
        foreach (string row in new[] { "Row - Sell Sunflowers", "Row - Sell Pumpkins", "Row - Sunflower Seeds", "Row - Pumpkin Seeds" })
            Assert.IsTrue(FindInScene(row).activeInHierarchy, row + " is offered on the fourth visit");
        //the shop keeps the bag row visible, but buying it twice must not charge u
        Assert.IsTrue(FindInScene("Row - Bag").activeInHierarchy);
        StringAssert.Contains("Owned", RowText("Row - Bag", "Owned"));
        int fundBefore = Fund;
        ButtonNamed("Buy Bag").onClick.Invoke();
        Assert.AreEqual(fundBefore, Fund);
        Assert.IsTrue(FarmItems().bag);
        StringAssert.Contains("already own the bag", TextOf("StoreFeedback"));
        Assert.IsFalse(FindInScene("Row - Speed Boots").activeInHierarchy);
        Assert.AreEqual("Scene6-FarmLevel5", Object.FindAnyObjectByType<StoreControl>().GetNextLevelScene());
        StringAssert.Contains("pumpkin seeds", TextOf("NextLevelNeeds"));

        ButtonNamed("Sell Sunflowers").onClick.Invoke();
        ButtonNamed("Buy Pumpkin Seed").onClick.Invoke(); // 3
        yield return null;
        Assert.IsTrue(ButtonNamed("Start Next Level").interactable);
        Assert.AreEqual(3, InBag("Pumpkin Seeds"));
    }

    private static void GiveToolsOnly()
    {
        foreach (var tool in new[] { ToolManager.ToolType.Hoe, ToolManager.ToolType.WateringCan, ToolManager.ToolType.Sickle })
            FarmItems().AddTool(tool);
    }
}
