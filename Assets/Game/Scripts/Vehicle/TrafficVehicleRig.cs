using UnityEngine;

// Rigid-part vehicle rig: steering parents turn around Y, wheel children roll around X.
public sealed class TrafficVehicleRig : MonoBehaviour
{
    [SerializeField] private Transform chassis;
    [SerializeField] private Transform[] steeringPivots;
    [SerializeField] private Transform[] wheelPivots;
    [SerializeField] private Renderer[] brakeLights;
    [SerializeField] private Material tailMaterial, brakeMaterial;
    [SerializeField] private float wheelRadius = .36f, wheelbase = 2.6f;
    private float roll;
    private bool braking;

    public float WheelRadius => wheelRadius;
    public float Wheelbase => wheelbase;

    public void AnimateTravel(float metres, float steeringDegrees, bool brake, float deltaTime)
    {
        roll = Mathf.Repeat(roll + metres / wheelRadius * Mathf.Rad2Deg, 360f);
        foreach (var wheel in wheelPivots) wheel.localRotation = Quaternion.Euler(roll, 0, 0);
        foreach (var pivot in steeringPivots) pivot.localRotation = Quaternion.Euler(0, Mathf.Clamp(steeringDegrees, -32f, 32f), 0);
        if (chassis)
            chassis.localRotation = Quaternion.Slerp(chassis.localRotation,
                Quaternion.Euler(brake ? -.6f : 0f, 0, -steeringDegrees * Mathf.Min(Mathf.Abs(metres) / Mathf.Max(deltaTime, .001f), 10f) * .008f),
                1f - Mathf.Exp(-6f * deltaTime));
        if (braking == brake) return;
        braking = brake;
        foreach (var lamp in brakeLights) lamp.sharedMaterial = brake ? brakeMaterial : tailMaterial;
    }
}
