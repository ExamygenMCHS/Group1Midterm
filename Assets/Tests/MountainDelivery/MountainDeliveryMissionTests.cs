#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

// A delivery from start to finish: collision damage on the truck, the traffic on the road, the drop-off that opens the
// wholesale market, the sale, and the trip from the farm and back.
public class MountainDeliveryMissionTests
{
    const string ScenePath = "Assets/Game/Scenes/Mountain Delivery.unity";
    const string FarmPath = "Assets/Game/Scenes/The Farm.unity";
    const string Delivery = "Mountain Delivery", Market = "Wholesale Market", Farm = "The Farm";

    BasicCarController car;
    Rigidbody body;
    TruckDamage damage;
    DeliveryMission mission;
    TrafficRoute route;
    DropOffZone zone;

    IEnumerator LoadDelivery(bool withTraffic)
    {
        ForgetFarmFund();
        DeliveryCargo.Clear();
        yield return EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
        FindDeliveryObjects();
        if (!withTraffic) route.Clear();
        car.Override = new BasicCarController.DriveInput(0, 0, 0);
        yield return Steps(60); // let the suspension settle
    }

    void FindDeliveryObjects()
    {
        car = Object.FindObjectsByType<BasicCarController>(FindObjectsInactive.Exclude).Single();
        body = car.GetComponent<Rigidbody>();
        damage = car.GetComponent<TruckDamage>();
        mission = Object.FindObjectsByType<DeliveryMission>(FindObjectsInactive.Exclude).Single();
        route = Object.FindObjectsByType<TrafficRoute>(FindObjectsInactive.Exclude).Single();
        zone = Object.FindObjectsByType<DropOffZone>(FindObjectsInactive.Exclude).Single();
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        if (car) car.Override = null;
        DeliveryCargo.Clear();
        ForgetFarmFund();
        yield return null;
    }

    static void ForgetFarmFund()
    {
        foreach (var fund in Object.FindObjectsByType<FarmFund>(FindObjectsInactive.Include))
            Object.DestroyImmediate(fund.gameObject);
    }

    static IEnumerator Steps(int count)
    {
        for (int i = 0; i < count; i++) yield return new WaitForFixedUpdate();
    }

    static IEnumerator WaitForScene(string name, float seconds = 10f)
    {
        float end = Time.realtimeSinceStartup + seconds;
        while (SceneManager.GetActiveScene().name != name && Time.realtimeSinceStartup < end) yield return null;
        Assert.AreEqual(name, SceneManager.GetActiveScene().name);
        yield return null; // Start of the new scene's objects
    }

    static PlantData Crop(string name)
    {
        var crop = AssetDatabase.LoadAssetAtPath<PlantData>("Assets/Game/Data/Plants/" + name + ".asset");
        Assert.IsNotNull(crop, name);
        return crop;
    }

    void PutTruckInTheZone()
    {
        body.linearVelocity = Vector3.zero;
        car.transform.position = zone.transform.position + Vector3.up * .5f;
        body.position = car.transform.position;
    }

    // ---- Damage

    [UnityTest]
    public IEnumerator Damage_GentleBumpsAreFree_HardHitsCostHealthAndChangeTheState()
    {
        yield return LoadDelivery(false);
        Assert.IsNotNull(damage, "the pickup needs TruckDamage");
        Assert.AreEqual(TruckDamage.DamageState.Intact, damage.State);
        Assert.AreEqual(0f, damage.Impact(1f), "a nudge does no damage");
        Assert.AreEqual(damage.MaxHealth, damage.Health);

        float taken = damage.Impact(12f);
        Assert.Greater(taken, 20f, "a 12 m/s crash hurts");
        Assert.AreEqual(damage.MaxHealth - taken, damage.Health, .01f);
        Assert.AreEqual(TruckDamage.DamageState.Damaged, damage.State);
        Assert.AreEqual(0f, damage.Impact(12f), "the same crash is not counted twice");

        float fullPower = car.motorPower;
        yield return new WaitForSeconds(.5f);
        damage.Impact(12f);
        Assert.AreEqual(TruckDamage.DamageState.Critical, damage.State);
        Assert.Less(car.motorPower, fullPower, "a critical truck loses engine power");
        damage.Repair();
        Assert.AreEqual(TruckDamage.DamageState.Intact, damage.State);
        Assert.AreEqual(fullPower, car.motorPower, .01f);
    }

    [UnityTest]
    public IEnumerator Damage_DrivingIntoAWallHurtsTheTruck()
    {
        yield return LoadDelivery(false);
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.transform.SetPositionAndRotation(car.transform.position + car.transform.forward * 22f + Vector3.up, car.transform.rotation);
        wall.transform.localScale = new Vector3(8f, 4f, 1f);
        car.Override = new BasicCarController.DriveInput(1, 0, 0);
        for (int i = 0; i < 500 && damage.Health >= damage.MaxHealth; i++) yield return new WaitForFixedUpdate();
        Assert.Less(damage.Health, damage.MaxHealth, "the crash into the wall took health");
        Object.Destroy(wall);
    }

    [UnityTest]
    public IEnumerator Wreck_StopsTheTruck_ThenTowsItBackRepaired()
    {
        yield return LoadDelivery(false);
        Vector3 start = body.position;
        car.transform.position += car.transform.forward * 30f;
        body.position = car.transform.position;
        yield return Steps(5);
        damage.TakeDamage(damage.MaxHealth);
        Assert.AreEqual(TruckDamage.DamageState.Wrecked, damage.State);
        Assert.AreEqual(DeliveryMission.MissionState.Wrecked, mission.State);
        Assert.IsTrue(car.Override.HasValue && car.Override.Value.handbrake > 0f, "a wreck takes the controls");

        Time.timeScale = 4f;
        for (int i = 0; i < 600 && mission.State != DeliveryMission.MissionState.Driving; i++) yield return null;
        Assert.AreEqual(DeliveryMission.MissionState.Driving, mission.State);
        Assert.AreEqual(TruckDamage.DamageState.Intact, damage.State);
        Assert.IsFalse(car.Override.HasValue, "the driver has the controls back");
        Assert.Less((body.position - start).magnitude, 2f, "the truck is back at the top");
    }

    [UnityTest]
    public IEnumerator Crashes_LowerTheCargoPrice_ButNeverBelowTheFloor()
    {
        yield return LoadDelivery(false);
        DeliveryCargo.Add(Crop("Pumpkin"), 4);
        int full = DeliveryCargo.TotalValue(1.5f);
        damage.TakeDamage(50f);
        Assert.Less(DeliveryCargo.Condition, 1f);
        Assert.Less(DeliveryCargo.TotalValue(1.5f), full);
        damage.Repair();
        for (int i = 0; i < 6; i++) { damage.TakeDamage(90f); damage.Repair(); }
        Assert.GreaterOrEqual(DeliveryCargo.Condition, .25f - 1e-4f);
        Assert.Greater(DeliveryCargo.TotalValue(1.5f), 0);
    }

    // ---- Traffic

    [UnityTest]
    public IEnumerator Traffic_DrivesBothWaysInItsLanes()
    {
        yield return LoadDelivery(true);
        var cars = route.Cars;
        Assert.GreaterOrEqual(cars.Count, 5, "the road has traffic");
        Assert.IsTrue(cars.Any(c => c.Direction > 0) && cars.Any(c => c.Direction < 0), "traffic runs both ways");
        Assert.IsTrue(cars.Any(c => c.GetComponent<TrafficVehicleRig>() != null), "the traffic models are used");
        foreach (var c in cars) Assert.IsTrue(c.GetComponent<Rigidbody>().isKinematic, c.name);

        var before = cars.Select(c => c.transform.position).ToArray();
        Time.timeScale = 3f;
        yield return Steps(300);
        int moved = 0;
        for (int i = 0; i < cars.Count; i++)
        {
            var c = cars[i];
            if (c.State == TrafficCar.DriveState.Parked) continue;
            if ((c.transform.position - before[i]).magnitude > 5f) moved++;
            route.Sample(route.DistanceAt(c.transform.position), c.Direction, out Vector3 lane, out Vector3 forward, out _);
            Assert.Less(Vector3.ProjectOnPlane(c.transform.position - lane, Vector3.up).magnitude, 1.6f, c.name + " is in its lane");
            Assert.Greater(Vector3.Dot(c.transform.forward, forward), .9f, c.name + " faces the way it drives");
            // Straight down from the car, past its own collider, there is asphalt within a hand's width of its wheels.
            var below = Physics.RaycastAll(c.transform.position + Vector3.up * 2f, Vector3.down, 4f, ~0, QueryTriggerInteraction.Ignore)
                .Where(h => h.collider.name.StartsWith("Drivable road") || h.collider.name.StartsWith("Asphalt")).ToArray();
            Assert.IsNotEmpty(below, c.name + " is on the asphalt");
            Assert.Less(Mathf.Abs(below[0].point.y - c.transform.position.y), .25f, c.name + " sits on the road surface");
        }
        Assert.GreaterOrEqual(moved, 3, "the cars are driving");
    }

    [UnityTest]
    public IEnumerator Traffic_WaitsBehindTheTruckInsteadOfRammingIt()
    {
        yield return LoadDelivery(true);
        yield return Steps(20);
        var follower = route.Cars.First(c => c.Direction > 0 && c.State == TrafficCar.DriveState.Cruising
            && route.DistanceAt(c.transform.position) < route.Length - 80f);
        float ahead = route.DistanceAt(follower.transform.position) + 9f;
        route.Sample(ahead, 1, out Vector3 lane, out Vector3 forward, out _);
        car.Override = new BasicCarController.DriveInput(0, 0, 0, 1);
        body.linearVelocity = Vector3.zero;
        car.transform.SetPositionAndRotation(lane + Vector3.up * .3f, Quaternion.LookRotation(forward));
        body.position = car.transform.position; body.rotation = car.transform.rotation;
        yield return Steps(150);
        Assert.AreEqual(TrafficCar.DriveState.Waiting, follower.State);
        Assert.Less(follower.Speed, .2f, "the car behind has stopped");
        Assert.AreEqual(damage.MaxHealth, damage.Health, "nobody ran into the truck");
    }

    // ---- Drop-off and market

    [UnityTest]
    public IEnumerator ArrivingWithCargo_OpensTheMarket_AndSellingPaysTheFund()
    {
        yield return LoadDelivery(false);
        DeliveryCargo.Add(Crop("Sunflower"), 3);
        DeliveryCargo.Add(Crop("Pumpkin"), 2);
        Vector3 start = body.position;
        PutTruckInTheZone();
        yield return Steps(20);
        Assert.AreEqual(DeliveryMission.MissionState.Delivered, mission.State);
        Assert.Greater((body.position - start).magnitude, 20f, "a delivery does not send the truck back to the top");
        Time.timeScale = 4f;
        yield return WaitForScene(Market);
        Time.timeScale = 1f;

        var market = Object.FindObjectsByType<WholesaleMarket>(FindObjectsInactive.Exclude).Single();
        var fund = FarmFund.Instance;
        Assert.IsNotNull(fund, "the market scene brings a Farm Fund when it is played without the farm");
        Assert.AreEqual(WholesaleMarket.MarketState.Offer, market.State);
        int expected = Mathf.RoundToInt(Crop("Sunflower").sellPrice * 3 * market.WholesaleBonus) + Mathf.RoundToInt(Crop("Pumpkin").sellPrice * 2 * market.WholesaleBonus);
        Assert.AreEqual(expected, market.Offer, "undamaged cargo sells for the trader's price times the wholesale bonus");
        Assert.AreEqual(CursorLockMode.None, Cursor.lockState, "the market needs the mouse");

        int before = fund.fund;
        market.Sell();
        Assert.AreEqual(before + expected, fund.fund);
        Assert.AreEqual(WholesaleMarket.MarketState.Sold, market.State);
        Assert.IsFalse(DeliveryCargo.HasCargo);
        market.Sell();
        Assert.AreEqual(before + expected, fund.fund, "the cargo is only paid once");
    }

    [UnityTest]
    public IEnumerator Market_LeavingWithoutSelling_GivesTheCropsBack()
    {
        ForgetFarmFund();
        DeliveryCargo.Clear();
        DeliveryCargo.Add(Crop("Pumpkin"), 5);
        yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Game/Scenes/Wholesale Market.unity", new LoadSceneParameters(LoadSceneMode.Single));
        yield return null;
        var market = Object.FindObjectsByType<WholesaleMarket>(FindObjectsInactive.Exclude).Single();
        var fund = FarmFund.Instance;
        int money = fund.fund;
        market.GoHome();
        Assert.AreEqual(5, fund.Inventory.GetHarvested(Crop("Pumpkin")));
        Assert.AreEqual(money, fund.fund);
        Assert.IsFalse(DeliveryCargo.HasCargo);
        yield return WaitForScene(Farm);
    }

    // ---- From the farm and back

    [UnityTest]
    public IEnumerator Delivery_FromTheFarmAndBack_KeepsTheFarmAndPaysTheHarvest()
    {
        ForgetFarmFund();
        DeliveryCargo.Clear();
        yield return EditorSceneManager.LoadSceneAsyncInPlayMode(FarmPath, new LoadSceneParameters(LoadSceneMode.Single));
        yield return null; yield return null;
        var fund = FarmFund.Instance;
        var depot = Object.FindObjectsByType<DeliveryDepot>(FindObjectsInactive.Exclude).Single();
        var player = Object.FindObjectsByType<WorldInteractor>(FindObjectsInactive.Exclude).Single();
        var farmScene = SceneManager.GetActiveScene();
        var calendar = Object.FindObjectsByType<GameCalendar>(FindObjectsInactive.Exclude).Single();

        // Nothing harvested: the truck only gives a hint.
        var beside = depot.transform.position + depot.transform.right * 2.4f; beside.y = player.transform.position.y;
        player.GetComponent<Rigidbody>().position = beside; player.transform.position = beside;
        Assert.IsNotNull(depot.GetPrompt(player));
        Assert.IsFalse(depot.StartDelivery(player), "an empty truck does not leave");

        fund.Inventory.AddHarvested(Crop("Pumpkin"), 3);
        int money = fund.fund;
        Assert.IsTrue(depot.StartDelivery(player));
        Assert.AreEqual(0, fund.Inventory.GetHarvested(Crop("Pumpkin")), "the harvest is on the truck");
        Assert.AreEqual(3, DeliveryCargo.TotalCount());
        yield return WaitForScene(Delivery);
        Assert.IsTrue(FarmTrip.IsAway);
        Assert.IsTrue(farmScene.isLoaded, "the farm stays loaded while the player is away");
        Assert.IsFalse(player.gameObject.activeInHierarchy, "the farm is switched off while the player is away");

        FindDeliveryObjects();
        route.Clear();
        yield return Steps(30);
        PutTruckInTheZone();
        Time.timeScale = 4f;
        yield return WaitForScene(Market);
        Time.timeScale = 1f;
        Assert.AreSame(fund, FarmFund.Instance, "the same Farm Fund all the way");
        var market = Object.FindObjectsByType<WholesaleMarket>(FindObjectsInactive.Exclude).Single();
        int offer = market.Offer;
        Assert.Greater(offer, Crop("Pumpkin").sellPrice * 3, "the market pays more than the trader");
        market.Sell();
        market.GoHome();
        yield return WaitForScene(Farm);
        yield return null; yield return null;

        Assert.AreEqual(money + offer, fund.fund);
        Assert.IsFalse(FarmTrip.IsAway);
        Assert.AreEqual(farmScene, SceneManager.GetActiveScene(), "the same farm, not a reloaded one");
        Assert.IsTrue(player != null && player.gameObject.activeInHierarchy, "the farmer is back");
        Assert.IsTrue(calendar != null && calendar.isActiveAndEnabled, "the farm's calendar carries on");
        for (int i = 0; i < 60 && SceneManager.sceneCount > 1; i++) yield return null;
        Assert.AreEqual(1, SceneManager.sceneCount, "the trip scenes are unloaded");
    }
}
#endif
