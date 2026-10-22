using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class Task5ShopAudioTests : MidtermTestBase
{
    [UnityTest]
    public IEnumerator Shop_PlaysDifferentSoundsForBuyingAndSelling_OnlyOnSuccess()
    {
        StartSession(500, 0, 2);
        AddHarvest("Sunflower", 2);
        yield return Load(Scene2);
        var store = Object.FindAnyObjectByType<StoreControl>();
        var audio = (AudioSource)Get(store, "transactionAudio");
        var buy = (AudioClip)Get(store, "purchaseSound");
        var sell = (AudioClip)Get(store, "saleSound");
        var crop = (PlantData)Crop("Sunflower");
        Assert.IsNotNull(audio);
        Assert.IsNotNull(buy);
        Assert.IsNotNull(sell);
        Assert.AreNotSame(buy, sell);
        Assert.Greater(buy.samples, 0);
        Assert.Greater(sell.samples, 0);
        Assert.IsFalse(audio.playOnAwake);
        Assert.IsFalse(audio.isPlaying);

        Assert.IsTrue(store.BuySeeds(crop, 3));
        yield return null;
        Assert.IsTrue(audio.isPlaying, "a successful purchase plays its sound");
        audio.Stop();
        Assert.IsFalse(store.BuySeeds(crop, 100));
        yield return null;
        Assert.IsFalse(audio.isPlaying, "an unaffordable purchase stays silent");
        Assert.AreEqual(470, Fund);
        Assert.AreEqual(3, SeedsOf("Sunflower"));

        Assert.IsTrue(store.SellHarvest(crop, 2));
        yield return null;
        Assert.IsTrue(audio.isPlaying, "a successful sale plays its sound");
        audio.Stop();
        Assert.IsFalse(store.SellHarvest(crop, 1));
        yield return null;
        Assert.IsFalse(audio.isPlaying, "selling an empty stack stays silent");
        Assert.AreEqual(500, Fund);
        Assert.AreEqual(0, HarvestedOf("Sunflower"));
    }

    [UnityTest]
    public IEnumerator Shop_ReloadKeepsPurchases_WithoutReplayingTheirSounds()
    {
        StartSession(100, 0);
        yield return Load(Scene2);
        var store = Object.FindAnyObjectByType<StoreControl>();
        Assert.IsTrue(store.BuySeeds((PlantData)Crop("Sunflower"), 5));
        yield return Load(Scene2);
        store = Object.FindAnyObjectByType<StoreControl>();
        Assert.AreEqual(50, Fund);
        Assert.AreEqual(5, SeedsOf("Sunflower"));
        Assert.IsFalse(((AudioSource)Get(store, "transactionAudio")).isPlaying);
    }
}
