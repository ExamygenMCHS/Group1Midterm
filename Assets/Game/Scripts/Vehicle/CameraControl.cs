using UnityEngine;
using UnityEngine.InputSystem;

// Chase camera for the pickup: stays behind and above the target, and the mouse orbits it left and right.
public class CameraControl : MonoBehaviour
{
    public Transform target; // The car to be followed
    public float smoothness = 5f; // Smoothness during camera tracking
    public float rotationSpeed = 2f; // Response speed to mouse movement for rotation
    public float distance = 5f; // Distance between the camera and the car
    public float height = 2f; // Height of the camera relative to the car
    public bool lockCursor = true;

    // The legacy "Mouse X" axis was the pixel delta times 0.1.
    const float MouseDeltaToAxis = 0.1f;
    private float orbitYaw; // Mouse orbit around the car, in degrees

    void Start()
    {
        if (lockCursor)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        if (target != null) transform.position = DesiredPosition();
    }

    void LateUpdate()
    {
        if (target == null) return;

        var mouse = Mouse.current;
        if (mouse != null) orbitYaw += mouse.delta.ReadValue().x * MouseDeltaToAxis * rotationSpeed;

        transform.position = Vector3.Lerp(transform.position, DesiredPosition(), smoothness * Time.deltaTime);
        transform.LookAt(target.position + Vector3.up * height * 0.5f);
    }

    // Jumps straight behind the target, for when the target has been teleported.
    public void Snap()
    {
        orbitYaw = 0f;
        if (target == null) return;
        transform.position = DesiredPosition();
        transform.LookAt(target.position + Vector3.up * height * 0.5f);
    }

    Vector3 DesiredPosition()
    {
        // Follow the car's heading on the ground plane so the view does not pitch or roll with the body.
        Vector3 forward = Vector3.ProjectOnPlane(target.forward, Vector3.up);
        if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
        Quaternion heading = Quaternion.LookRotation(forward) * Quaternion.Euler(0f, orbitYaw, 0f);
        return target.position + heading * new Vector3(0f, height, -distance);
    }
}
