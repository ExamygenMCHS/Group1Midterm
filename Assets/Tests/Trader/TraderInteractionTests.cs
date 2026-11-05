using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

//The staged prefabs are exercised only when these tests are run later in Unity.
public class TraderInteractionTests : HW6TestBase
{
    private WanderingTrader trader;
    private WorldInteractor player;

    private IEnumerator PrepareFarm()
    {
        StartSession(300, 0);
        global::FarmFund.Instance.Inventory.SetBag(true);
        AddSeeds("Sunflower", 4);
        AddSeeds("Pumpkin", 4);
        yield return Load(Farm);
        FreezeCalendar();
        player = Object.FindAnyObjectByType<WorldInteractor>();
        Assert.IsNotNull(player);
        //the farm's own trader stands by the mailbox; these tests use a fresh one placed next to the player instead
        foreach (WanderingTrader farmTrader in Object.FindObjectsByType<WanderingTrader>())
            farmTrader.gameObject.SetActive(false);
#if UNITY_EDITOR
        GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Game/Prefabs/Trader/WanderingTraderShop.prefab");
        Assert.IsNotNull(prefab, "the staged ready-to-place trader prefab must be imported first");
        GameObject instance = Object.Instantiate(prefab);
        trader = instance.GetComponent<WanderingTrader>();
        Assert.IsNotNull(trader);
        trader.transform.position = player.transform.position + Vector3.forward * .5f;
#else
        Assert.Ignore("the prefab is loaded through AssetDatabase in the Editor test runner");
#endif
        yield return null;
    }

    [UnityTearDown]
    public IEnumerator DestroyTrader()
    {
        if (trader != null)
            Object.Destroy(trader.gameObject);
        yield return null;
        trader = null;
        player = null;
    }

    [UnityTest]
    public IEnumerator ShopOpenClose_KeepsTheFarmScene_AndReusesTheSameInventory()
    {
        yield return PrepareFarm();
        global::FarmFund fund = global::FarmFund.Instance;
        string scene = SceneManager.GetActiveScene().path;
        Assert.IsTrue(trader.TryOpenShop(player));
        TraderShopPanel panel = trader.ShopInstance;
        Assert.IsTrue(panel.IsOpen);
        Assert.IsFalse(player.enabled);
        Assert.AreEqual(1f, Time.timeScale, "shopping does not pause crop growth or the world clock");
        PlantData sunflower = (PlantData)Crop("Sunflower");
        int owned = fund.Inventory.GetSeeds(sunflower);
        int money = fund.fund;
        Assert.IsTrue(panel.Store.BuySeeds(sunflower, 2));
        panel.Close();
        yield return null;

        Assert.IsFalse(TraderShopPanel.IsAnyOpen);
        Assert.IsTrue(player.enabled);
        Assert.AreSame(fund, global::FarmFund.Instance);
        Assert.AreEqual(scene, SceneManager.GetActiveScene().path);
        Assert.AreEqual(owned + 2, fund.Inventory.GetSeeds(sunflower));
        Assert.AreEqual(money - 2 * sunflower.seedPrice, fund.fund);
        Assert.IsTrue(trader.TryOpenShop(player));
        Assert.AreSame(panel, trader.ShopInstance, "reopening reuses the panel instead of duplicating the shop");
    }

    [UnityTest]
    public IEnumerator Trader_RejectsDistantAndBusyShoppers()
    {
        yield return PrepareFarm();
        trader.transform.position = player.transform.position + Vector3.forward * 20f;
        Assert.IsFalse(trader.TryOpenShop(player));
        Assert.IsNull(trader.ShopInstance, "out-of-range input must not instantiate a shop");
        trader.transform.position = player.transform.position;
        PlayerStateControl state = player.GetComponent<PlayerStateControl>();
        Assert.IsNotNull(state);
        Assert.IsTrue(state.StartAction(PlayerStateControl.PlayerState.Tilling, null));
        Assert.IsFalse(trader.TryOpenShop(player), "trade waits for the farming action to finish");
        Assert.IsFalse(TraderShopPanel.IsAnyOpen);
    }

    [UnityTest]
    public IEnumerator Modal_RestoresOnlyControlsThatItDisabled()
    {
        yield return PrepareFarm();
        Hotbar hotbar = Object.FindAnyObjectByType<Hotbar>();
        PlayerMove movement = player.GetComponent<PlayerMove>();
        PlantManager plants = Object.FindAnyObjectByType<PlantManager>();
        Assert.IsNotNull(hotbar);
        Assert.IsNotNull(movement);
        Assert.IsNotNull(plants);
        Assert.IsTrue(movement.enabled);
        Assert.IsTrue(plants.enabled);
        hotbar.enabled = false;
        Assert.IsTrue(trader.TryOpenShop(player));
        Assert.IsFalse(hotbar.enabled);
        Assert.IsFalse(movement.enabled);
        Assert.IsFalse(plants.enabled);
        trader.ShopInstance.Close();
        Assert.IsFalse(movement.enabled, "the close frame still belongs to the shop");
        yield return null;
        Assert.IsTrue(movement.enabled);
        Assert.IsTrue(plants.enabled);
        Assert.IsTrue(player.enabled);
        Assert.IsFalse(hotbar.enabled, "a script disabled before trade must stay disabled");
    }

    [UnityTest]
    public IEnumerator DestroyingTheTrader_ReleasesTheModalAndPlayerControls()
    {
        yield return PrepareFarm();
        PlayerMove movement = player.GetComponent<PlayerMove>();
        Assert.IsTrue(trader.TryOpenShop(player));
        TraderShopPanel panel = trader.ShopInstance;
        Object.Destroy(trader.gameObject);
        yield return null;
        yield return null;
        Assert.IsTrue(panel == null, "the trader owns and cleans up its shop instance");
        Assert.IsFalse(TraderShopPanel.IsAnyOpen);
        Assert.IsTrue(player.enabled);
        Assert.IsTrue(movement.enabled);
    }

    [UnityTest]
    public IEnumerator ExistingWorldInteractor_ClaimsEForTrading()
    {
        yield return PrepareFarm();
        //Use an empty part of the scene so the well or a pickup cannot win the nearest-target query.
        var position = new Vector3(1000f, 1f, 1000f);
        Rigidbody body = player.GetComponent<Rigidbody>();
        Assert.IsNotNull(body);
        body.position = position;
        player.transform.position = position;
        trader.transform.position = position + Vector3.forward * .5f;
        Call(player, "Update");
        Assert.AreSame(trader, player.GetTarget());
        Assert.IsTrue(player.Interact());
        Assert.IsTrue(player.ClaimsInteractKey(), "the same E press must not also plant a crop");
        Assert.IsTrue(trader.IsTrading);
    }
}
