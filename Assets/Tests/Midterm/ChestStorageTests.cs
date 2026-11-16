using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;

// The log cabin on The Farm: the storage chest keeps seeds and crops apart from the bag, so the wandering trader and
// the wholesale truck never sell them; the roof and front wall make way while the farmer is indoors.
public class ChestStorageTests : MidtermTestBase
{
    private static StorageChest Chest()
    {
        StorageChest chest = Object.FindAnyObjectByType<StorageChest>();
        Assert.IsNotNull(chest, "The Farm has a storage chest");
        return chest;
    }

    private static PlantData Plant(string name)
    {
        return (PlantData)Crop(name);
    }

    private static IEnumerator Teleport(Vector3 position)
    {
        Rigidbody rb = GameObject.Find("Player").GetComponent<Rigidbody>();
        position.y = rb.position.y;
        rb.position = position;
        rb.transform.position = position;
        yield return new WaitForFixedUpdate();
        yield return null;
    }

    private static IEnumerator OpenChest()
    {
        yield return Teleport(Chest().transform.position + new Vector3(0f, 0f, -1.0f));
        WorldInteractor player = Object.FindAnyObjectByType<WorldInteractor>();
        Assert.AreEqual(Chest(), player.GetTarget(), "standing in front of the chest, E belongs to it");
        Assert.IsTrue(player.Interact(), "E opens the chest");
        yield return null;
        Assert.IsTrue(Chest().IsOpen);
    }

    private static void Click(ChestSlot slot, PointerEventData.InputButton button)
    {
        var click = new PointerEventData(EventSystem.current) { button = button };
        slot.OnPointerClick(click);
    }

    [UnityTest]
    public IEnumerator CropsInTheChest_AreNotSoldByTheTrader_NorLoadedOnTheTruck()
    {
        StartBagSession(0);
        AddHarvest("Pumpkin", 5);
        yield return LoadFarm();
        PlantData pumpkin = Plant("Pumpkin");

        Assert.IsTrue(FarmItems().StoreInChest(pumpkin, true, 3));
        Assert.AreEqual(2, FarmItems().GetHarvested(pumpkin), "two pumpkins stay in the bag");
        Assert.AreEqual(3, FarmItems().GetChestHarvested(pumpkin), "three go in the chest");
        Assert.AreEqual(2, InBag(pumpkin.harvestItem.itemName), "the bag shows only what it holds");
        Assert.IsFalse(FarmItems().StoreInChest(pumpkin, true, 3), "the bag has only two left");

        yield return OpenShop();
        ButtonNamed("Sell Pumpkins").onClick.Invoke();
        yield return null;
        Assert.AreEqual(2 * pumpkin.sellPrice, Fund, "the trader buys the two pumpkins in the bag");
        Assert.AreEqual(0, HarvestedOf("Pumpkin"));
        Assert.AreEqual(3, FarmItems().GetChestHarvested(pumpkin), "the pumpkins in the chest are safe");

        FarmItems().AddHarvested(pumpkin, 1);
        Assert.AreEqual(1, DeliveryCargo.Load(FarmItems()), "the truck takes only the crop in the bag");
        Assert.AreEqual(3, FarmItems().GetChestHarvested(pumpkin), "and leaves the chest alone");
        DeliveryCargo.Clear();
    }

    [UnityTest]
    public IEnumerator Chest_OpensWithE_AndMovesStacksBothWays()
    {
        StartBagSession(0);
        AddHarvest("Sunflower", 4);
        AddSeeds("Pumpkin", 2);
        yield return LoadFarm();
        PlantData sunflower = Plant("Sunflower");
        PlantData pumpkin = Plant("Pumpkin");
        int pumpkinSeeds = FarmItems().GetSeeds(pumpkin);

        yield return OpenChest();
        ChestPanel window = Chest().Window;
        Assert.IsFalse(Object.FindAnyObjectByType<PlayerMove>().enabled, "the farmer waits while the chest is open");

        ChestSlot sunflowers = window.BagSlots.First(s => s.Crop == sunflower && s.Harvest);
        Click(sunflowers, PointerEventData.InputButton.Right);
        Assert.AreEqual(1, FarmItems().GetChestHarvested(sunflower), "right click moves one");
        Assert.AreEqual(3, FarmItems().GetHarvested(sunflower));

        ChestSlot seeds = window.BagSlots.First(s => s.Crop == pumpkin && !s.Harvest);
        Click(seeds, PointerEventData.InputButton.Left);
        Assert.AreEqual(pumpkinSeeds, FarmItems().GetChestSeeds(pumpkin), "click moves the whole stack of seeds");
        Assert.AreEqual(0, FarmItems().GetSeeds(pumpkin));
        Assert.IsTrue(window.ChestSlots.Any(s => s.Crop == pumpkin && !s.Harvest && s.Amount == pumpkinSeeds), "the chest shows them");

        window.StoreAllCrops();
        Assert.AreEqual(0, FarmItems().GetHarvested(sunflower), "store all crops empties the bag's harvest");
        Assert.AreEqual(4, FarmItems().GetChestHarvested(sunflower));

        ChestSlot back = window.ChestSlots.First(s => s.Crop == sunflower && s.Harvest);
        Click(back, PointerEventData.InputButton.Left);
        Assert.AreEqual(4, FarmItems().GetHarvested(sunflower), "a click on the chest's stack takes it back");
        Assert.AreEqual(4, InBag(sunflower.harvestItem.itemName));

        window.TakeEverything();
        Assert.AreEqual(0, FarmItems().GetChestTotal(), "take everything empties the chest");
        Assert.AreEqual(pumpkinSeeds, FarmItems().GetSeeds(pumpkin));

        window.Close();
        yield return null;
        Assert.IsFalse(Chest().IsOpen);
        Assert.IsTrue(Object.FindAnyObjectByType<PlayerMove>().enabled, "the farmer can walk again");
    }

    [UnityTest]
    public IEnumerator Cabin_HidesItsRoofAndFrontWall_WhileTheFarmerIsInside()
    {
        StartBagSession(0);
        yield return LoadFarm();
        FarmCabin cabin = Object.FindAnyObjectByType<FarmCabin>();
        Assert.IsNotNull(cabin, "The Farm has the cabin");
        Renderer roof = cabin.transform.Find("Upper").GetComponentInChildren<Renderer>();
        Renderer frontWall = cabin.transform.Find("Front Wall").GetComponentInChildren<Renderer>();
        Transform markers = cabin.transform.Find("Markers");

        yield return Teleport(markers.Find("Door Outside").position);
        Assert.IsFalse(cabin.PlayerInside);
        Assert.IsTrue(roof.enabled && frontWall.enabled, "from outside the cabin is whole");

        yield return Teleport(markers.Find("Door Inside").position);
        Assert.IsTrue(cabin.PlayerInside);
        Assert.IsFalse(roof.enabled || frontWall.enabled, "indoors the camera sees in");
        Assert.IsTrue(cabin.Contains(GameObject.Find("SleepArea").transform.position), "the bed is inside the cabin");
        Assert.IsTrue(cabin.Contains(Chest().transform.position), "and so is the chest");

        yield return Teleport(markers.Find("Door Outside").position);
        Assert.IsTrue(roof.enabled && frontWall.enabled, "the roof comes back on the way out");
    }
}
