using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// The farm tiles share their materials and one sound source instead of copying them per tile.
public class TileSharedResourcesTests : HW5TestBase
{
    [UnityTest]
    public IEnumerator Tiles_ShareTheirMaterials_AndTheEdgeGlowSwapsASharedMaterial()
    {
        yield return Load(Farm);
        Component[] tiles = Tiles();
        Component first = tiles[0], second = tiles[1];
        var glow = (Material)Get(first, "edgeGlowMat");
        Assert.IsNotNull(glow, "the tile prefab has the shared glow material");
        Assert.IsTrue(glow.IsKeywordEnabled("_EMISSION"), "the glow material has emission on in the asset");

        MeshRenderer[] edges = EdgesOf(first);
        Assert.GreaterOrEqual(edges.Length, 1);
        Material plain = edges[0].sharedMaterial;
        Assert.AreNotSame(glow, plain);
        Assert.AreSame(plain, EdgesOf(second)[0].sharedMaterial, "every tile edge uses the same material, so they batch");

        Call(first, "EnterTile");
        foreach (MeshRenderer edge in edges)
            Assert.AreSame(glow, edge.sharedMaterial, "the edge glows while the player's point is on the tile");
        Assert.AreSame(plain, EdgesOf(second)[0].sharedMaterial, "the other tiles do not glow");
        Call(first, "ExitTile");
        foreach (MeshRenderer edge in edges)
            Assert.AreSame(plain, edge.sharedMaterial);
    }

    [UnityTest]
    public IEnumerator TileSounds_PlayThroughOneSharedSource()
    {
        yield return Load(Farm);
        Component[] tiles = Tiles();
        foreach (Component tile in tiles)
            Assert.AreEqual(0, tile.GetComponents<AudioSource>().Length, "the tiles carry no audio sources of their own");

        Call(tiles[0], "EnterTile");
        Call(tiles[1], "EnterTile");
        yield return null;
        var shared = 0;
        foreach (AudioSource source in Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Include))
            if (source.gameObject.name == "Tile Audio")
                shared++;
        Assert.AreEqual(1, shared, "all tiles play through one source");
    }

    private static MeshRenderer[] EdgesOf(Component tile)
    {
        var edges = new System.Collections.Generic.List<MeshRenderer>();
        foreach (Transform child in tile.transform)
            if (child.GetComponent<MeshRenderer>() != null)
                edges.Add(child.GetComponent<MeshRenderer>());
        return edges.ToArray();
    }
}
