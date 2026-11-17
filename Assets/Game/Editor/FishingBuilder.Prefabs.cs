using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

//prefabs: the rod in the hand, the rod lying by the pond, the bobber, the three hook models and the four fish.
//Each is built in memory and saved over the old prefab (same path, same GUID).
public static partial class FishingBuilder
{
    private const string HoePickupPath = "Assets/Game/Prefabs/Pickups/Hoe Pickup.prefab";
    private const string ToolPickupClipPath = "Assets/Game/Audio/Tool Pickup.wav";
    public const float RodModelTilt = 55f;//degrees: the hoe uses 90 for "straight forward"; 55 points the rod forward and 35 degrees up
    private const float RodGripY = 0.13f;//where the hand holds the handle

    private static GameObject PrefabMeshGO(string name, Mesh mesh, Transform parent, Vector3 pos, Quaternion rot)
    {
        var go = new GameObject(name);
        if (parent != null)
            go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = rot;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = Mat("FishingLowPoly");
        mr.shadowCastingMode = ShadowCastingMode.On;
        mr.receiveShadows = true;
        return go;
    }

    //Handle (mesh) -> Section1 -> Section2 -> Section3 -> Tip, each section a child of the one below at its joint
    private static Transform BuildRodChain(Transform model, out Transform[] sections)
    {
        Transform parent = PrefabMeshGO("Handle", MeshAsset("FishingRodHandle"), model, Vector3.zero, Quaternion.identity).transform;
        sections = new Transform[3];
        float y = HandleLength;
        for (int i = 0; i < 3; i++)
        {
            parent = PrefabMeshGO("Section" + (i + 1), MeshAsset("FishingRodSection" + (i + 1)), parent, new Vector3(0f, y, 0f), Quaternion.identity).transform;
            sections[i] = parent;
            y = SectionLengths[i];
        }
        var tip = new GameObject("Tip").transform;
        tip.SetParent(parent, false);
        tip.localPosition = new Vector3(0f, SectionLengths[2] + 0.003f, 0f);
        return tip;
    }

    private static void SavePrefab(GameObject go, string name)
    {
        PrefabUtility.SaveAsPrefabAsset(go, PrefabDir + "/" + name + ".prefab");
        Object.DestroyImmediate(go);
    }

    private static void BuildRodPrefab()
    {
        var root = new GameObject("Fishing Rod");
        var held = root.AddComponent<HeldTool>();
        held.toolType = ToolManager.ToolType.FishingRod;
        var rod = root.AddComponent<FishingRod>();

        var model = new GameObject("Model").transform;
        model.SetParent(root.transform, false);
        Quaternion tilt = Quaternion.Euler(RodModelTilt, 0f, 0f);
        model.localRotation = tilt;
        model.localPosition = -(tilt * new Vector3(0f, RodGripY, 0f));//the grip sits in the hand
        Transform tip = BuildRodChain(model, out Transform[] sections);

        var so = new SerializedObject(held);
        //carried: arm a little forward and the rod tipped back toward upright, held up in front of the farmer while walking
        so.FindProperty("carryRotation").vector3Value = new Vector3(-14f, 0f, 0f);
        so.FindProperty("carryOffset").vector3Value = new Vector3(0f, 0.01f, 0.02f);
        so.FindProperty("carryArmLift").floatValue = 20f;
        so.ApplyModifiedPropertiesWithoutUndo();

        so = new SerializedObject(rod);
        SerializedProperty list = so.FindProperty("sections");
        list.arraySize = sections.Length;
        for (int i = 0; i < sections.Length; i++)
            list.GetArrayElementAtIndex(i).objectReferenceValue = sections[i];
        so.FindProperty("tip").objectReferenceValue = tip;
        so.ApplyModifiedPropertiesWithoutUndo();
        SavePrefab(root, "Fishing Rod");
    }

    private static void BuildPickupPrefab()
    {
        var root = new GameObject("Fishing Rod Pickup");
        var pickup = root.AddComponent<ToolPickup>();
        var audio = root.AddComponent<AudioSource>();

        //copy the hoe pickup's sound settings and its marker (world canvas + arrow)
        GameObject hoe = PrefabUtility.LoadPrefabContents(HoePickupPath);
        Transform marker;
        try
        {
            AudioSource hoeAudio = hoe.GetComponent<AudioSource>();
            if (hoeAudio != null)
                EditorUtility.CopySerialized(hoeAudio, audio);
            Transform hoeMarker = hoe.transform.Find("Marker");
            marker = Object.Instantiate(hoeMarker.gameObject, root.transform).transform;
            marker.name = "Marker";
            marker.localPosition = hoeMarker.localPosition;
            marker.localRotation = hoeMarker.localRotation;
            marker.localScale = hoeMarker.localScale;
            var markerRect = marker as RectTransform;
            var hoeRect = hoeMarker as RectTransform;
            if (markerRect != null && hoeRect != null)
            {
                markerRect.anchoredPosition = hoeRect.anchoredPosition;
                markerRect.sizeDelta = hoeRect.sizeDelta;
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(hoe);
        }
        audio.clip = Load<AudioClip>(ToolPickupClipPath);
        audio.playOnAwake = false;
        audio.loop = false;

        //the whole rod, leaning: the butt on the ground, rising about 22 degrees toward its tip, centred over the root
        var model = new GameObject("Model").transform;
        model.SetParent(root.transform, false);
        model.SetSiblingIndex(0);
        model.localRotation = Quaternion.Euler(0f, 24f, 0f) * Quaternion.Euler(68f, 0f, 0f);
        model.localPosition = model.localRotation * new Vector3(0f, -0.72f, 0f) + new Vector3(0f, 0.03f + Mathf.Sin(22f * Mathf.Deg2Rad) * 0.72f, 0f);
        BuildRodChain(model, out _);

        var so = new SerializedObject(pickup);
        so.FindProperty("tool").enumValueIndex = (int)ToolManager.ToolType.FishingRod;
        so.FindProperty("tool").intValue = (int)ToolManager.ToolType.FishingRod;
        so.FindProperty("whereText").stringValue = RodPickupWhere;
        so.FindProperty("usageText").stringValue = "Aim at the pond with the mouse and click to cast.";
        so.FindProperty("reach").floatValue = 1.3f;
        so.FindProperty("model").objectReferenceValue = model;
        so.FindProperty("marker").objectReferenceValue = marker;
        so.FindProperty("pickupSound").objectReferenceValue = audio;
        so.ApplyModifiedPropertiesWithoutUndo();
        SavePrefab(root, "Fishing Rod Pickup");
    }

    //a root with one mesh child called Model
    private static void BuildSimplePrefab(string prefab, string mesh, bool shadows)
    {
        var root = new GameObject(prefab);
        GameObject model = PrefabMeshGO("Model", MeshAsset(mesh), root.transform, Vector3.zero, Quaternion.identity);
        if (!shadows)
            model.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        SavePrefab(root, prefab);
    }

    private static string HookPrefabName(string bait) { return bait + " Hook"; }
    private static string FishPrefabName(string fish) { return "Fish - " + fish; }

    public static string BuildPrefabs()
    {
        EnsureFolder(PrefabDir);
        BuildRodPrefab();
        BuildPickupPrefab();
        BuildSimplePrefab("Bobber", "Bobber", true);
        BuildSimplePrefab(HookPrefabName("Lure"), "Lure", false);
        BuildSimplePrefab(HookPrefabName("Old Lure"), "OldLure", false);
        BuildSimplePrefab(HookPrefabName("Worm"), "Worm", false);
        foreach (string fish in FishNames)
            BuildSimplePrefab(FishPrefabName(fish), FishMeshName(fish), true);
        return "prefabs: rod, pickup, bobber, 3 hooks, 4 fish";
    }
}
