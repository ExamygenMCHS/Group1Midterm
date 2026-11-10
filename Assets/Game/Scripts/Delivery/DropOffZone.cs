using UnityEngine;
using UnityEngine.Events;

// The yellow drop-off circle at the end of the road. When the delivery truck drives into it, TruckArrived fires once.
// DeliveryMission listens to it: with cargo on the truck the delivery is done, without it the truck goes back to the top.
[RequireComponent(typeof(Collider))]
public class DropOffZone : MonoBehaviour
{
    [SerializeField] private UnityEvent onTruckArrived = new UnityEvent();

    private bool arrived;

    public UnityEvent TruckArrived => onTruckArrived;

    // Lets the zone fire again after the mission restarts (the truck is teleported out without leaving the trigger on its own).
    public void ResetZone() { arrived = false; }

    private void OnTriggerEnter(Collider other)
    {
        // The truck has several colliders (body, wheels), so only the first one in counts.
        if (arrived || other.GetComponentInParent<BasicCarController>() == null) return;
        arrived = true;
        onTruckArrived.Invoke();
    }
}
