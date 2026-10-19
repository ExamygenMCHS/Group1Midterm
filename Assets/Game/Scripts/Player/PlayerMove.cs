using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem; // 1. The Input System "using" statement

public class PlayerMove : MonoBehaviour
{
    public Rigidbody rb;

    //Important: public variables values set in the Unity editor will overwrite this init value set in code
    public float forwardSpeed = 1f;
    public float sidewaySpeed = 1f;

    //how fast the player speeds up and slows down (units per second, per second).
    //High values feel snappy, low values feel floaty.
    public float acceleration = 22f;
    public float deceleration = 32f;

    // 2. These variables are to hold the Action references
    InputAction moveAction;
    Vector2 moveValue;
    Vector3 planarVelocity;//the velocity on the ground the player is moving with right now
    PhysicsMaterial slippery;
    PhysicsMaterial originalMaterial;
    Vector3 facing = Vector3.forward;//the direction the player last walked in (starts facing the farm)
    PlayerEnergy playerEnergy;//knows whether the player is lying in bed
    bool wasResting = false;
    bool wakeKeyReady = false;//the movement keys were let go after getting into bed
    PlayerStateControl playerStateControl;//the player stands still while an action (tilling, watering...) plays

    // Start is called before the first frame update
    void OnEnable()
    {
        // 3. Find the references to the "Move" and "Jump" actions
        moveAction = InputSystem.actions.FindAction("Move");
        playerEnergy = rb.GetComponent<PlayerEnergy>();
        playerStateControl = rb.GetComponent<PlayerStateControl>();

        //physics set-up for smooth movement
        //the body is drawn between physics steps, so it glides instead of stepping 50 times a second
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        //bumps against the tile edges can no longer spin the body
        rb.constraints |= RigidbodyConstraints.FreezeRotation;
        //no friction while this script drives the body, so the player slides over the small tile steps
        //instead of catching on them (this script sets the velocity every physics step, so nothing drifts)
        if (slippery == null)
        {
            slippery = new PhysicsMaterial("PlayerNoFriction");
            slippery.staticFriction = 0f;
            slippery.dynamicFriction = 0f;
            slippery.frictionCombine = PhysicsMaterialCombine.Minimum;
            slippery.bounceCombine = PhysicsMaterialCombine.Minimum;
            Collider body = BodyCollider();
            if (body != null)
                originalMaterial = body.sharedMaterial;
        }
        SetBodyMaterial(slippery);
    }

    //when something else moves the body (this script switched off), it gets its normal friction back
    void OnDisable()
    {
        planarVelocity = Vector3.zero;
        SetBodyMaterial(originalMaterial);
    }

    private Collider BodyCollider()
    {
        if (rb == null)
            return null;
        foreach (Collider c in rb.GetComponents<Collider>())
            if (!c.isTrigger)
                return c;
        return null;
    }

    private void SetBodyMaterial(PhysicsMaterial material)
    {
        Collider body = BodyCollider();
        if (body != null)
            body.sharedMaterial = material;
    }

    // Update is called once per frame
    void Update()
    {
        // 4. Read the "Move" action value, which is a 2D vector
        moveValue = moveAction.ReadValue<Vector2>();

        //in bed the player lies still. The key that walked them into the bed does not count;
        //once the keys were let go, pressing any movement key gets them out of bed (and they walk off)
        bool keysDown = moveValue.sqrMagnitude > 0.01f;
        bool resting = IsInBed();
        if (resting)
        {
            if (!wasResting)
                wakeKeyReady = false;
            if (!keysDown)
                wakeKeyReady = true;
            else if (wakeKeyReady)
            {
                playerEnergy.SetResting(false);
                resting = false;
            }
        }
        wasResting = resting;

        //the player faces the way they last walked; the tile they work on is in front of them
        //not while an action plays, the player keeps facing the tile they work on
        if (keysDown && !resting && !IsBusy())
            facing = new Vector3(moveValue.x, 0f, moveValue.y).normalized;
    }

    private bool IsBusy()
    {
        return playerStateControl != null && playerStateControl.IsBusy();
    }

    private bool IsInBed()
    {
        return playerEnergy != null && playerEnergy.IsResting();
    }

    //the body is moved in the physics step, at the same rate the physics runs
    void FixedUpdate()
    {
        MovePlayer();
    }

    //Move player forward
    private void MovePlayer()
    {
        //lying in bed, the body does not move until the player gets up
        if (IsInBed())
        {
            planarVelocity = Vector3.zero;
            rb.linearVelocity = new Vector3(0f, Mathf.Min(rb.linearVelocity.y, 0f), 0f);
            return;
        }

        //W/S move along the world z axis (up/down the screen) and A/D along x (left/right the screen).
        //The camera never turns, so these are the screen directions, whatever way the body happens to face.
        //(moveValue.y = 1 when pressing W, moveValue.y = -1 when pressing S; diagonals are already normalized)
        Vector3 targetVelocity = new Vector3(moveValue.x * sidewaySpeed, 0f, moveValue.y * forwardSpeed);
        if (IsBusy())//stand still until the action is over
            targetVelocity = Vector3.zero;

        //speed up toward the target, brake harder when the keys are released
        float rate = targetVelocity.sqrMagnitude > 0.0001f ? acceleration : deceleration;
        planarVelocity = Vector3.MoveTowards(planarVelocity, targetVelocity, rate * Time.fixedDeltaTime);

        //keep the vertical velocity from gravity (stepping on and off the tiles)
        rb.linearVelocity = new Vector3(planarVelocity.x, Mathf.Min(rb.linearVelocity.y, 1f), planarVelocity.z);
    }

    public void StopPlayer()
    {
        planarVelocity = Vector3.zero;
        rb.linearVelocity = new Vector3(0, 0, 0);
    }

    //lets other objects (the farmer animation) know how fast the player is moving on the ground
    public Vector3 GetPlanarVelocity()
    {
        return planarVelocity;
    }

    //the direction the player faces on the ground (unit length); the interact point and the farmer model follow it
    public Vector3 GetFacing()
    {
        return facing;
    }

}
