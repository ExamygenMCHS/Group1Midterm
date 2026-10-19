using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// The polish pass that changes what the player sees while playing: the camera framing, the camera holding still on
// the bed while the player rests, and generated meshes without broken normals (the cause of the flickering white ball).
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
        yield return Load(Scene1);
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
        yield return Load(Scene1);
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

#if UNITY_EDITOR
    [Test]
    public void GeneratedMeshes_HaveUnitLengthNormals()
    {
        string[] guids = UnityEditor.AssetDatabase.FindAssets("t:Mesh", new[] { "Assets/Game/Art/Meshes" });
        Assert.Greater(guids.Length, 10, "the generated meshes should be in Assets/HW5/Art/Meshes");
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
