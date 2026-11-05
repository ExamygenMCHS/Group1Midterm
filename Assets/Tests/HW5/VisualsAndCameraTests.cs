using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// The polish pass that changes what the player sees while playing: the camera framing, the camera holding still on
// the bed while the player rests, the pond freezing in winter, and generated meshes without broken normals (the cause
// of the flickering white ball).
public class VisualsAndCameraTests : HW5TestBase
{
    private static bool IsResting()
    {
        return (bool)Call(Energy(), "IsResting");
    }

    private static IEnumerator Teleport(Vector3 position)
    {
        Rigidbody rb = GameObject.Find("Player").GetComponent<Rigidbody>();
        position.y = rb.position.y;
        rb.position = position;
        rb.transform.position = position;
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
        yield return null;
    }

    [UnityTest]
    public IEnumerator Camera_KeepsTheFarmerNearTheMiddleOfTheScreen()
    {
        yield return Load(Farm);
        yield return null;
        Camera cam = GameObject.Find("Main Camera").GetComponent<Camera>();
        Transform player = GameObject.Find("Player").transform;
        // the capsule's centre is 1 above the feet; the farmer model is about 1.33 tall
        Vector3 farmerMiddle = player.position + Vector3.down * (1f - 0.65f);
        Vector3 vp = cam.WorldToViewportPoint(farmerMiddle);
        Assert.AreEqual(0.5f, vp.x, 0.05f, "the farmer should be centred horizontally");
        Assert.AreEqual(0.5f, vp.y, 0.08f, "the farmer should be near the vertical middle of the screen (the example framed him a quarter of the way up)");
    }

    [UnityTest]
    public IEnumerator Camera_HoldsStillOnTheBedWhileResting_AndFollowsAgainAfterwards()
    {
        yield return Load(Farm);
        FreezeCalendar();
        Transform cam = GameObject.Find("Main Camera").transform;
        Transform player = GameObject.Find("Player").transform;
        Vector3 bed = GameObject.Find("SleepArea").transform.position;

        yield return Teleport(bed);
        Assert.IsTrue(IsResting(), "on the bed = resting");
        yield return new WaitForSeconds(0.8f); // the camera eases over to the bed
        Vector3 held = cam.position;

        yield return Teleport(bed + new Vector3(0.4f, 0f, -0.5f)); // moved, but still on the bed
        Assert.IsTrue(IsResting(), "still inside the sleep area");
        yield return new WaitForSeconds(0.3f);
        Assert.Less(Vector3.Distance(held, cam.position), 0.001f, "walking keys while in bed must not move the camera");

        yield return Teleport(bed + new Vector3(0f, 0f, -2.5f)); // stepped off the bed
        Assert.IsFalse(IsResting(), "off the bed = not resting");
        yield return new WaitForSeconds(0.8f);
        Assert.AreEqual(player.position.x, cam.position.x, 0.01f, "after leaving the bed the camera follows the player again");
    }

    // the pond freezes over in winter (the ice material, no ripples) and thaws when spring comes back
    [UnityTest]
    public IEnumerator Pond_FreezesInWinter_AndThawsInSpring()
    {
        yield return Load(Farm);
        FreezeCalendar();
        WaterRipple pond = Object.FindAnyObjectByType<WaterRipple>();
        Assert.IsNotNull(pond, "The Farm has a pond");
        Renderer lake = pond.GetComponent<Renderer>();
        Material ice = (Material)Get(pond, "iceMaterial");
        Assert.IsNotNull(ice, "the pond has an ice material for winter");
        Assert.AreNotSame(ice, lake.sharedMaterial, "spring: open water");

        AdvanceDays(21); // day 22, the first day of winter
        Assert.AreEqual("Winter", Season());
        Assert.AreSame(ice, lake.sharedMaterial, "winter: the pond is frozen");
        Assert.IsTrue((bool)Get(pond, "frozen"), "and its ripples stop");

        AdvanceDays(7); // day 29, spring again
        Assert.AreEqual("Spring", Season());
        Assert.AreNotSame(ice, lake.sharedMaterial, "spring: the ice has melted");
        Assert.IsFalse((bool)Get(pond, "frozen"));
    }

#if UNITY_EDITOR
    [Test]
    public void GeneratedMeshes_HaveUnitLengthNormals()
    {
        string[] guids = UnityEditor.AssetDatabase.FindAssets("t:Mesh", new[] { "Assets/Game/Art/Meshes" });
        Assert.Greater(guids.Length, 10, "the generated meshes should be in Assets/Game/Art/Meshes");
        foreach (string guid in guids)
        {
            Mesh mesh = UnityEditor.AssetDatabase.LoadAssetAtPath<Mesh>(UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
            int bad = 0;
            foreach (Vector3 n in mesh.normals)
                if (!(Mathf.Abs(n.magnitude - 1f) < 0.01f))//also catches NaN
                    bad++;
            Assert.AreEqual(0, bad, mesh.name + ": a zero or NaN normal makes the shader output NaN, which bloom turns into a flickering white ball");
        }
    }
#endif
}
