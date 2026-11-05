# Mountain Delivery

A low-poly mountain delivery environment for Unity 6000.6 / URP, adapted from Metazeon's **Mountain road** (CC BY 4.0). Keep `ATTRIBUTION.md` with the scene, prefab and exported model.

## Open the level

Open `Assets/Game/Scenes/Mountain Delivery.unity` in Unity. The scene has an overview camera and directional light. `Assets/Game/Prefabs/Delivery/Mountain Delivery.prefab` contains the reusable environment without scene lighting. Existing farm scenes, game logic and Build Settings are unchanged.

Where things live:

| What | Path |
|---|---|
| Scene | `Assets/Game/Scenes/Mountain Delivery.unity` |
| Level prefab | `Assets/Game/Prefabs/Delivery/Mountain Delivery.prefab` |
| Baked meshes/materials, source layout, metrics, this file | `Assets/Game/Art/MountainRoad/` |
| Pickup truck model, materials and prefab (third-party pack) | `Assets/Game/Art/Pickup Truck/` |
| Pickup driving scripts (not used by this level) | `Assets/Game/Scripts/Vehicle/` |
| Level generator (Editor only) | `Assets/Game/Editor/MountainDeliveryBuilder.cs` |
| Play Mode tests | `Assets/Tests/MountainDelivery/` |

A portable `MountainDelivery.glb` contains the visible geometry, materials and named navigation markers. Use the Unity scene/prefab for physics colliders; standard glTF does not carry Unity physics components. Materials use solid colors and require no textures.

## Layout and navigation

- Approximately 829 m of continuous road; two main hairpins follow the supplied reference.
- 48 m descent from the upper approach to the valley exit; grades ease through the hairpins.
- At least 10 m paved width, plus 1.6 m shoulders. The steepest sampled road edge is approximately 8.9%.
- Two tapered delivery pull-offs, roadside cottages, parcel crates, curve chevrons and continuous guardrails beyond the shoulders.
- Named `Vehicle Spawn`, `Delivery Bay 1/2`, `Finish`, and ordered `Waypoint` transforms provide integration points. The spawn faces downhill; waypoint spacing is approximately 18 m, so use road geometry or interpolation for an AI path.
- The delivery truck is the project's pickup (`Assets/Game/Art/Pickup Truck`), scaled to 0.5 so it measures about 2.5 × 2.3 × 4.4 m and fits a 5 m lane with room to spare. It sits in the right-hand lane at the start, facing downhill. It is a **static prop**: its `CarControl` script, rigidbody and wheel colliders are removed, because `CarControl` reads the legacy Input Manager, which this project disables. This deliverable is the level environment; driving controls, delivery triggers, scoring and farm-to-delivery transitions are not implemented.
- Pines are 12-21 m tall (about 5-9 times the pickup's height), with canopies 3-6 m wide, placed at least 8 m apart and 15 m from the road centerline. Rocks are 1.4-4.8 m.
- The short open road ends are connection points for another scene or loading trigger. Add a trigger or boundary before shipping standalone gameplay.

## Geometry and collision

- Approximately 95,000 triangles including the separate hidden terrain collision mesh; about 81,000 visible triangles (about 23,000 of them are the pickup).
- 15 shared solid-color materials; scenery grouped into spatial mesh chunks for culling. Approximately 153 enabled mesh renderers; static geometry can use Unity static batching.
- Mesh colliders on terrain, road, shoulders and guardrails. Decorative trees, rocks, cottages and the pickup have no colliders; they sit outside the drivable envelope. Add simple box colliders if on-foot exploration requires them.
- No runtime generation, textures, new packages, per-object update loops or physics bodies in the environment.
- Counts are geometry budgets, not measured frame-rate guarantees. Profile with the final vehicle, camera and target device.

## Validation

Unity compilation passed. All four `MountainDeliveryTests` Play Mode tests passed: full-route lane clearance/collision for a 2.6 × 4.6 m vehicle, dynamic cargo support at both hairpins, pickup scale and static setup, and marker/material/geometry checks. An Editor scan also passed 1,626 surface probes with no gaps, no guardrail overlap in either lane, and no missing scripts. Overview and overhead renders were visually checked. Vehicle handling, AI steering, platform builds and the unrelated farm test suite were not tested.

## Authoring

`Assets/Game/Editor/MountainDeliveryBuilder.cs` bakes the scene from `ReferenceLayout.json`, a coarse terrain and road trace derived from the supplied GLB. `Tools > Mountain Delivery > Create Level` only creates a fresh level and refuses to overwrite an existing scene, preserving hand edits. `Tools > Mountain Delivery > Rebuild Level (discards hand edits)` deletes the scene, prefab and baked assets and regenerates them; tree, rock and pickup settings are constants at the top of the builder. Edit the baked scene/prefab normally; retain the source layout and generator for reproducibility.

The separate overhead image mentioned in the request was unavailable. A top-down projection of the supplied GLB was used as the overhead reference instead.
