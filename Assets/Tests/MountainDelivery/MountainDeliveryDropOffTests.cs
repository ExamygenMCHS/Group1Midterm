#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

// The yellow drop-off circle at the end of the road, in the real Mountain Delivery scene.
public class MountainDeliveryDropOffTests
{
    const string ScenePath = "Assets/Game/Scenes/Mountain Delivery.unity";

    DropOffZone zone;
    Rigidbody truck;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        yield return EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
        zone = Object.FindObjectsByType<DropOffZone>(FindObjectsInactive.Exclude).Single();
        truck = Object.FindObjectsByType<BasicCarController>(FindObjectsInactive.Exclude).Single().GetComponent<Rigidbody>();
        yield return new WaitForFixedUpdate();
    }

    [Test]
    public void Zone_IsAYellowTriggerAtTheRoadEnd()
    {
        var collider = zone.GetComponent<Collider>();
        Assert.IsTrue(collider.isTrigger);
        var finish = Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude).Single(t => t.name.StartsWith("Finish —"));
        Assert.Less(Vector3.ProjectOnPlane(zone.transform.position - finish.position, Vector3.up).magnitude, 3f, "zone should sit at the very end of the road");
        Assert.GreaterOrEqual(zone.GetComponentsInChildren<MeshRenderer>().Length, 3, "beam, disc and rim");
    }

    [UnityTest]
    public IEnumerator DrivingIntoTheZone_TeleportsTheTruckBackToTheStart()
    {
        Vector3 start = truck.position;
        truck.transform.position = zone.transform.position + Vector3.up * .5f;
        truck.linearVelocity = Vector3.down * 3f;
        for (int i = 0; i < 20; i++) yield return new WaitForFixedUpdate();
        Assert.Less((truck.position - start).magnitude, 1.5f, "truck should be back at the top of the hill");
        Assert.Less(truck.linearVelocity.magnitude, 2f);
    }

    [UnityTest]
    public IEnumerator Zone_CanFireAgainAfterTheRestart()
    {
        for (int round = 0; round < 2; round++)
        {
            truck.transform.position = zone.transform.position + Vector3.up * .5f;
            for (int i = 0; i < 20; i++) yield return new WaitForFixedUpdate();
            Assert.Greater((truck.position - zone.transform.position).magnitude, 20f, "round " + round);
        }
    }
}
#endif
