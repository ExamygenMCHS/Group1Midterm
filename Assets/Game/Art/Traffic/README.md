# Mountain road traffic vehicles

Four original low-poly vehicles with muted, texture-free URP materials:

| Prefab in `Assets/Game/Prefabs/Traffic/` | Triangles | Body width / length |
| --- | ---: | --- |
| Commuter Hatchback | 1,452 | 1.82 / 3.80 m |
| Commuter Sedan | 1,452 | 1.82 / 4.50 m |
| Utility Pickup | 1,568 | 2.00 / 4.80 m |
| Valley Box Truck | 1,574 | 2.00 / 5.40 m |

The prefabs are standalone assets. No road placement, traffic controller, scene edits, or Build Settings changes are included.

## Rig and integration

- Root has unit scale, ground-level origin, local +Z forward, +Y up, +X right. Dimensions above describe the body collider; mirrors and tyres extend slightly beyond it.
- `Chassis — body roll` holds the body and brake lamps. `Suspension_FL/FR/RL/RR` are independent wheel-height pivots outside the chassis so body lean does not move the wheel contact points.
- Each suspension contains `Steer_*`, then `Wheel_*`, then its visible mesh. Steer around local Y; roll around local X. Both sides use the same roll sign. Only the front steering pivots are driven by the supplied rig.
- `TrafficVehicleRig.AnimateTravel(metres, steeringDegrees, brake, deltaTime)` updates wheel roll from actual travel distance, front steering (clamped to ±32 degrees), subtle body lean and brake materials. Call once per movement step; pass zero travel while stationary. Negative travel reverses wheel spin.
- `WheelRadius` and `Wheelbase` expose the authored dimensions for a traffic controller. Cars use 0.34 m radius; trucks use 0.40 m.
- The root has one BoxCollider and a kinematic Rigidbody with interpolation. The traffic controller must move the root; the rig does not drive, find roads, register vehicles, or poll input. For physical driving, configure a dynamic Rigidbody and wheel colliders separately.
- These are rigid transform rigs, appropriate for hard-surface vehicles, rather than deforming skeletal skins. Meshes, pivots and materials are editable in Unity.

Geometry is stored in `Traffic Geometry.asset`; shared materials are in `Materials/`. `Tools > Mountain Delivery > Create Traffic Assets` generates the initial assets and refuses to overwrite existing prefabs. Edit the baked prefabs normally.

Rendered front and rear previews are in `Exports/Traffic/`.
