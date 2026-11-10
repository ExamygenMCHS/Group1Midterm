using UnityEngine;

// Teleports the delivery truck back to where it started (the top of the hill) and stops it dead.
public class TruckRespawn : MonoBehaviour
{
    [SerializeField] private Rigidbody truck;
    [SerializeField] private CameraControl chaseCamera;
    [SerializeField] private DropOffZone zone;

    private Vector3 startPosition;
    private Quaternion startRotation;

    private void Start()
    {
        if (truck == null) return;
        startPosition = truck.position;
        startRotation = truck.rotation;
    }

    public void Respawn()
    {
        if (truck == null) return;
        truck.linearVelocity = Vector3.zero;
        truck.angularVelocity = Vector3.zero;
        truck.transform.SetPositionAndRotation(startPosition, startRotation);
        truck.position = startPosition;
        truck.rotation = startRotation;
        Physics.SyncTransforms();
        if (chaseCamera != null) chaseCamera.Snap();
        if (zone != null) zone.ResetZone();
    }
}
