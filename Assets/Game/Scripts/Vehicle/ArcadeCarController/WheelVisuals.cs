// Adapted from Basic Arcade Car Controller by Burhan (MIT License, Copyright (c) 2026 Burhan).
// See LICENSE.md and ATTRIBUTION.md in this folder.
using UnityEngine;

public class WheelVisuals : MonoBehaviour
{
    [SerializeField] Transform[] wheelMeshes;

    [Header("Tyre deformation (visual only)")]
    [Tooltip("Vertical squash of a tyre carrying its share of the truck's weight (0.04 = 4%).")]
    [SerializeField] float squashAtRest = .04f;
    [SerializeField] float maxSquash = .1f;
    [Tooltip("Extra sidewall bulge while the tyre is pushed sideways.")]
    [SerializeField] float cornerBulge = .03f;
    [Tooltip("Spring stiffness (Hz) and damping of the tyre's squash. Lower damping gives a bounce on landing.")]
    [SerializeField] float squashFrequency = 4f;
    [SerializeField, Range(.1f, 1f)] float squashDamping = .35f;

    WheelCollider[] wheels;
    VehicleFeel feel; // optional: leans the tyres with the chassis and adds road shake
    Rigidbody body;
    // Each mesh sits under a holder that carries the squash and lean but not the spin, so the deformed
    // shape stays put while the tyre turns through it.
    Transform[] holders;
    float[] squash, squashSpeed, bulge;
    float staticLoad;

    void Start()
    {
        wheels = GetComponentsInChildren<WheelCollider>();
        feel = GetComponent<VehicleFeel>();
        body = GetComponent<Rigidbody>();
        staticLoad = body ? body.mass * Physics.gravity.magnitude / Mathf.Max(1, wheels.Length) : 1f;
        holders = new Transform[wheelMeshes.Length];
        squash = new float[wheelMeshes.Length];
        squashSpeed = new float[wheelMeshes.Length];
        bulge = new float[wheelMeshes.Length];
        for (int i = 0; i < wheelMeshes.Length; i++)
        {
            var mesh = wheelMeshes[i];
            var holder = new GameObject(mesh.name + " Deform").transform;
            holder.SetParent(mesh.parent, false);
            holder.SetPositionAndRotation(mesh.position, mesh.rotation);
            mesh.SetParent(holder, true);
            holders[i] = holder;
        }
    }

    void Update()
    {
        RotateWheelMesh();
    }

    void RotateWheelMesh()
    {
        for (int i = 0; i < wheels.Length; i++)
        {
            UpdateWheel(i, wheels[i], wheelMeshes[i]);
        }
    }

    void UpdateWheel(int index, WheelCollider collider, Transform mesh)
    {
        Vector3 pos;
        Quaternion rot;
        collider.GetWorldPose(out pos, out rot);
        float radius = collider.radius * collider.transform.lossyScale.y;
        // Wheel orientation without the spin: truck frame plus steering.
        Quaternion frame = collider.transform.rotation * Quaternion.Euler(0f, collider.steerAngle, 0f);
        Quaternion spin = Quaternion.Inverse(frame) * rot;

        float dt = Mathf.Min(Time.deltaTime, .05f);
        bool grounded = collider.GetGroundHit(out WheelHit hit);
        float load = grounded ? Mathf.Clamp(hit.force / staticLoad, 0f, 3f) : 0f;
        float targetSquash = Mathf.Min(load * squashAtRest, maxSquash);
        float side = grounded ? Mathf.Clamp01(Mathf.Abs(hit.sidewaysSlip)) : 0f;
        float w = 2f * Mathf.PI * squashFrequency;
        squashSpeed[index] += (w * w * (targetSquash - squash[index]) - 2f * squashDamping * w * squashSpeed[index]) * dt;
        squash[index] = Mathf.Clamp(squash[index] + squashSpeed[index] * dt, -.03f, maxSquash * 1.5f);
        bulge[index] = squash[index] * .6f + side * cornerBulge;

        Vector3 up = transform.up;
        if (feel) pos += up * feel.WheelBump(index);
        // A squashed tyre sits lower on its hub so the contact patch stays on the ground.
        pos -= up * (squash[index] * radius);

        if (feel)
        {
            // Lean about the contact patch, so the tread stays planted while the top of the tyre tilts with the chassis.
            Quaternion lean = Quaternion.AngleAxis(feel.RollDegrees * feel.WheelLean, transform.forward);
            Vector3 contact = pos - up * (radius * (1f - squash[index]));
            pos = contact + lean * (pos - contact);
            frame = lean * frame;
        }

        var holder = holders[index];
        holder.SetPositionAndRotation(pos, frame);
        holder.localScale = new Vector3(1f + bulge[index], 1f - squash[index], 1f + squash[index] * .6f);
        mesh.localPosition = Vector3.zero;
        mesh.localRotation = spin;
    }
}
