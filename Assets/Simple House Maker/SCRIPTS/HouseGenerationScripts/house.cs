using UnityEngine;
using System.Collections.Generic;
using System.Collections;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace SHM{
[System.Serializable]
public class Additional
{
    public GameObject prefab;
    [Header("Objects per meter")]
    public bool isRoof = false;
    public bool followRoofAngle = true;
    public bool everyFloor = true;
    public bool evenDistribution = true;

    public float minDistance = 2f;

    public float maxDistance = 2f;
    [Range(0f, 100f)]
    public float randomMissing = 0f;
    [Header("Placing Sides")]
    public bool front = true;
    public bool left = true;
    public bool back = true;
    public bool right = true;
    [Header("Transform prefab values")]
    public Vector3 offsetPosition = new Vector3(0, 0, 0);
    public Vector3 randomPosition = new Vector3(0, 0, 0);
    public Vector3 minRotation = new Vector3(0, 0, 0);
    public Vector3 maxRotation = new Vector3(0, 0, 0);
    public Vector3 minScale = new Vector3(1, 1, 1);
    public Vector3 maxScale = new Vector3(1, 1, 1);
    public int seed = 0;
    //public bool combineInstances = false;
    [Header("Cut original parts (EXPERIMENTAL: has limitations!)")]
    public bool cutOuterWalls = false;
    public bool cutInnerWalls = false;
    public bool cutRoof = false;
    public bool cutRoofBase = false;
    public bool cutRoofBaseHeight = false;
    public bool cutBase = false;
    public bool cutFloors = false;
    public bool cutInnerRoofs = false;

}

[ExecuteInEditMode]
public class house : MonoBehaviour
{    

    Mesh mesh;
    Vector3[] vertices;
    int[] triangles;

    //("These are parameters for the COMPLEX HOUSE")
    [Header("Complex House Part")]
    [Tooltip("Disabled: not drawing front side (good when another house comes after)")]
    public bool hasFront = true;
    [Tooltip("Enabled: not drawing front side, BUT closing the block's front (good for row house)\nExamined only when hasFront is false.")]
    public bool closedFront = false;
    [Tooltip("Disabled: not drawing back side (good when another house comes after)")]
    public bool hasBack = true;
    [Tooltip("Enabled: not drawing back side, BUT closing the block's back (good for row house)\nExamined only when hasBack is false.")]
    public bool closedBack = false;

    [Tooltip("angle on front")]
    public float madeAngle = 180f;
    [Tooltip("angle on back")]
    public float prepareAngle = 180f;

    [Header("Reference (to call functions on children)")]
    [Tooltip("Child of this GameObject, which has the outerwalls component")]
    public GameObject outerWalls;
    [Tooltip("Child of this GameObject, which has the innerwalls component")]
    public GameObject innerWalls;
    [Tooltip("Child of this GameObject, which has the roof component")]
    public GameObject roof;
    [Tooltip("Child of this GameObject, which has the roofbase component")]
    public GameObject roofBase;
    [Tooltip("Child of this GameObject, which has the roofBaseHeight component")]
    public GameObject roofBaseHeight;
    [Tooltip("Child of this GameObject, which has the base component")]
    public GameObject baser;
    [Tooltip("Child of this GameObject, which has the floor component")]
    public GameObject floor;
    [Tooltip("Child of this GameObject, which has the innerRoofs component")]
    public GameObject innerRoofs;
    
    [Header("Bounding Box")]
    [Tooltip("Width of the house")]
    public float width = 12f;
    [Tooltip("Length of the house")]
    public float length = 16f;
    [Tooltip("Width of the walls")]
    public float wallWidth = 0.3f;
    [Tooltip("Width of the floor/ceiling walls")]
    public float floorWidth = 0.3f;
    [Tooltip("How many floors the house has")]
    public int floors = 2;
    [Tooltip("How high one floor should be (meters)")]
    public float floorHeight = 2.5f;
    [Tooltip("The height of the house's base")]
    public float baseHeight = 1f;
    [Tooltip("The height of the house's base, measured in the 'outer ring'. If this value is less than the 'baseheight', the base's top will have a cone-like effect")]
    public float baseOutHeight = 0.8f;
    [Tooltip("The width of the base (how much wider should it be, than the given width and length)")]
    public float baseWidth = 0.1f;

    [Header("Roof Settings")]
    [Tooltip("Height of the highest point of the roof (set to 0 to get a flat roof)")]
    public float roofHeight = 3f;
    [Tooltip("If you want to 'cut off the end' of the roof, this value will be the height of the cut")]
    public float roofEndHeight = 0f;
    [Tooltip("If you want to 'cut off the end' of the roof, this value will be the length of the cut")]
    public float roofEndLength = 0f;
    [Tooltip("The distance the roof goes further the walls")]
    public float roofOverlay = 1f;
    [Tooltip("The width of the roof")]
    public float roofWidth = 0.3f;



    [Header("Texture size")]
    public float outerWallsTS = 1f;
    public float innerWallsTS = 1f;
    public float roofTS = 1f;
    public float roofBaseTS = 1f;
    public float roofBaseHeightTS = 1f;
    public float baseTS = 1f;
    public float floorsTS = 1f;
    public float innerRoofsTS = 1f;

    [Header("Accessories")]
    //[Tooltip("Updates addition when turned on. IT WILL CAUSE WARNINGS!")]
    //public bool updateAdditionEditor = false;
    [Tooltip("The GameObject, that will be the parent of the additional objects in the house")]
    public GameObject plusParent;
    [Tooltip("Additional objects to place in the house(windows, air conditioners...)")]
    public Additional[] additionalObjects;
    // [HideInInspector]
    // public bool updateInEditor  =false;


    void Start()
    {
        UpdateParts();
        Addition();
        StartCoroutine(WaitUpdate());
    }

    IEnumerator WaitUpdate(){
        yield return new WaitForSeconds(0.05f);
        UpdateParts();
        ClearAdditionals();
        yield return new WaitForSeconds(0.5f);
        Addition(false);
    }

    public void UpdateMesh(Mesh mesh, Vector3[] vertices, int[] triangles, Vector2[] UV){
        mesh.Clear();
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = UV;
        mesh.Optimize();

        mesh.RecalculateNormals();
    }

    // void Update()
    // {
    //     if(!Application.isPlaying && Application.isEditor && updateInEditor){
    //         UpdateParts();
    //         Addition();
    //         updateInEditor = false;
    //     }

    // }

    

    IEnumerator Destroy(GameObject go){
        yield return new WaitForEndOfFrame();
        DestroyImmediate(go);
    }

    public void UpdateParts(){
        if(outerWalls != null){
            outerWalls.GetComponent<outerWalls>().Draw();
        }
        if(innerWalls != null){
            innerWalls.GetComponent<innerWalls>().Draw();
        }
        if(roof != null){
            roof.GetComponent<roof>().Draw();
        }
        if(roofBase != null){
            roofBase.GetComponent<roofBase>().Draw();
        }
        if(roofBaseHeight != null){
            roofBaseHeight.GetComponent<roofBaseHeight>().Draw();
        }
        if(baser != null){
            baser.GetComponent<baseCube>().Draw();
        }
        if(floor != null){
            floor.GetComponent<floors>().Draw();
        }
        if(innerRoofs != null){
            innerRoofs.GetComponent<innerRoofs>().Draw();
        }
    }



    public void Addition(bool isGizmo = false){
        if(!isGizmo){
            ClearAdditionals();
        }
        if(!isGizmo){
            //plusParent.transform.rotation *= transform.rotation;
            plusParent.transform.localRotation = Quaternion.identity;
        }
        for(int i = 0; i<additionalObjects.Length; i++){
            if(additionalObjects[i].prefab == null){
                continue;
            }
            List<GameObject> cut = new List<GameObject>();
            Random.InitState(additionalObjects[i].seed);
            
            if(!additionalObjects[i].isRoof){
                if(additionalObjects[i].left){
                    cut.AddRange(LeftSide(i, isGizmo));
                }
                if(additionalObjects[i].right){
                    cut.AddRange(RightSide(i, isGizmo));
                }
                if(additionalObjects[i].front){
                    cut.AddRange(FrontSide(i, isGizmo));
                }
                if(additionalObjects[i].back){
                    cut.AddRange(BackSide(i, isGizmo));
                }
            }
            else{//this is the roof
                if(additionalObjects[i].left){
                    cut.AddRange(RoofLeftSide(i, isGizmo));
                }
                if(additionalObjects[i].right){
                    cut.AddRange(RoofRightSide(i, isGizmo));
                }
            }
            if(!isGizmo){
                CutUpdate(cut, additionalObjects[i].cutOuterWalls, additionalObjects[i].cutInnerWalls, additionalObjects[i].cutRoof,
                    additionalObjects[i].cutRoofBase, additionalObjects[i].cutRoofBaseHeight, additionalObjects[i].cutBase,
                    additionalObjects[i].cutFloors, additionalObjects[i].cutInnerRoofs);
            }
        }
    }
    List<GameObject> LeftSide(int i, bool isGizmo){
        List<GameObject> back = new List<GameObject>();
        int left = (int)(length/additionalObjects[i].minDistance);
        float evenStep = length/left;

        //Random.InitState(additionalObjects[i].seed);
        if(additionalObjects[i].everyFloor){
            for(int x = 0; x<floors; x++){
                Vector3 pos = new Vector3(0,baseHeight+(0.5f+x)*(floorHeight+floorWidth),0);
                for(int j = 0; j<left; j++){
                    
                    //random
                    Vector3 rot = additionalObjects[i].minRotation + (additionalObjects[i].maxRotation-additionalObjects[i].minRotation)*Random.value;
                    Vector3 currentPos;
                    if(!additionalObjects[i].evenDistribution){
                        currentPos = new Vector3(pos.x, pos.y, pos.z + additionalObjects[i].minDistance+(additionalObjects[i].maxDistance-additionalObjects[i].minDistance)*Random.value);
                    }
                    else{
                        currentPos = new Vector3(pos.x, pos.y, pos.z + evenStep);
                    }
                    
                    Vector3 scale = additionalObjects[i].minScale + (additionalObjects[i].maxScale-additionalObjects[i].minScale)*Random.value;
                    Vector3 rand = additionalObjects[i].randomPosition*Random.value;

                    pos = currentPos;
                    float missValue = Random.value*100f;
                    if(missValue < additionalObjects[i].randomMissing || currentPos.z > length-additionalObjects[i].minDistance){
                        continue;
                    }
                    if(isGizmo){
                        Gizmos.color = new Color(1, 0, 0, 0.5f);
                        Gizmos.DrawCube(transform.position+currentPos+additionalObjects[i].offsetPosition+rand, new Vector3(1, 1, 1));
                        continue;
                    }
                    GameObject add = Instantiate(additionalObjects[i].prefab, transform.position+currentPos+additionalObjects[i].offsetPosition+rand, Quaternion.Euler(rot), plusParent.transform);
                    add.transform.localScale = scale;
                    add.transform.localPosition = currentPos+additionalObjects[i].offsetPosition+rand;
                    add.transform.localRotation *= transform.rotation;
                    back.Add(add);

                }
            }
        }
        else{
            Vector3 pos = new Vector3(0,baseHeight+(0.5f)*(floorHeight+floorWidth),0);
            for(int j = 0; j<left; j++){
                //random
                Vector3 rot = additionalObjects[i].minRotation + (additionalObjects[i].maxRotation-additionalObjects[i].minRotation)*Random.value; 
                Vector3 currentPos;
                if(!additionalObjects[i].evenDistribution){
                    currentPos = new Vector3(pos.x, pos.y, pos.z + additionalObjects[i].minDistance+(additionalObjects[i].maxDistance-additionalObjects[i].minDistance)*Random.value);
                }
                else{
                    currentPos = new Vector3(pos.x, pos.y, pos.z + evenStep);
                }
                Vector3 scale = additionalObjects[i].minScale + (additionalObjects[i].maxScale-additionalObjects[i].minScale)*Random.value;
                Vector3 rand = additionalObjects[i].randomPosition*Random.value;

                pos = currentPos;
                float missValue = Random.value*100f;
                if(missValue < additionalObjects[i].randomMissing || currentPos.z > length-additionalObjects[i].minDistance){
                    continue;
                }
                if(isGizmo){
                    Gizmos.color = new Color(1, 0, 0, 0.5f);
                    Gizmos.DrawCube(transform.position+currentPos+additionalObjects[i].offsetPosition+rand, new Vector3(1, 1, 1));
                    continue;
                }
                GameObject add = Instantiate(additionalObjects[i].prefab, transform.position+currentPos+additionalObjects[i].offsetPosition+rand, Quaternion.Euler(rot), plusParent.transform);
                add.transform.localScale = scale;
                add.transform.localPosition = currentPos+additionalObjects[i].offsetPosition+rand;
                add.transform.localRotation *= transform.rotation;
                back.Add(add);

            }
        }
        return back;
    }

    List<GameObject> RightSide(int i, bool isGizmo){
        List<GameObject> back = new List<GameObject>();
        int left = (int)(length/additionalObjects[i].minDistance);
        float evenStep = length/left;

        //Random.InitState(additionalObjects[i].seed);
        if(additionalObjects[i].everyFloor){
            for(int x = 0; x<floors; x++){
                Vector3 pos = new Vector3(width,baseHeight+(0.5f+x)*(floorHeight+floorWidth),0);
                for(int j = 0; j<left; j++){
                    //random
                    Vector3 rot = additionalObjects[i].minRotation + (additionalObjects[i].maxRotation-additionalObjects[i].minRotation)*Random.value;
                    Vector3 currentPos;
                    if(!additionalObjects[i].evenDistribution){
                        currentPos = new Vector3(pos.x, pos.y, pos.z + additionalObjects[i].minDistance+(additionalObjects[i].maxDistance-additionalObjects[i].minDistance)*Random.value); 
                    }
                    else{
                        currentPos = new Vector3(pos.x, pos.y, pos.z + evenStep);
                    }
                    Vector3 scale = additionalObjects[i].minScale + (additionalObjects[i].maxScale-additionalObjects[i].minScale)*Random.value;
                    Vector3 rand = new Vector3(-additionalObjects[i].randomPosition.x, additionalObjects[i].randomPosition.y, -additionalObjects[i].randomPosition.z)*Random.value;
                    Vector3 offset = new Vector3(-additionalObjects[i].offsetPosition.x, additionalObjects[i].offsetPosition.y, -additionalObjects[i].offsetPosition.z);
                    rot = new Vector3(-rot.x, rot.y, -rot.z);

                    pos = currentPos;
                    float missValue = Random.value*100f;
                    if(missValue < additionalObjects[i].randomMissing || currentPos.z > length-additionalObjects[i].minDistance){
                        continue;
                    }
                    if(isGizmo){
                        Gizmos.color = new Color(1, 0, 0, 0.5f);
                        Gizmos.DrawCube(transform.position+currentPos+offset+rand, new Vector3(1, 1, 1));
                        continue;
                    }
                    GameObject add = Instantiate(additionalObjects[i].prefab, transform.position+currentPos+offset+rand, Quaternion.Euler(rot), plusParent.transform);
                    add.transform.localScale = scale;
                    add.transform.localPosition = currentPos+offset+rand;
                    add.transform.localRotation *= transform.rotation;
                    add.transform.rotation *= Quaternion.Euler(0,180,0);
                    back.Add(add);

                }
            }
        }
        else{
            Vector3 pos = new Vector3(width,baseHeight+(0.5f)*(floorHeight+floorWidth),0);
            for(int j = 0; j<left; j++){
                //random
                Vector3 rot = additionalObjects[i].minRotation + (additionalObjects[i].maxRotation-additionalObjects[i].minRotation)*Random.value;
                Vector3 currentPos;
                if(!additionalObjects[i].evenDistribution){
                    currentPos = new Vector3(pos.x, pos.y, pos.z + additionalObjects[i].minDistance+(additionalObjects[i].maxDistance-additionalObjects[i].minDistance)*Random.value);
                }
                else{
                    currentPos = new Vector3(pos.x, pos.y, pos.z + evenStep);
                }
                Vector3 scale = additionalObjects[i].minScale + (additionalObjects[i].maxScale-additionalObjects[i].minScale)*Random.value;
                Vector3 rand = new Vector3(-additionalObjects[i].randomPosition.x, additionalObjects[i].randomPosition.y, -additionalObjects[i].randomPosition.z)*Random.value;
                Vector3 offset = new Vector3(-additionalObjects[i].offsetPosition.x, additionalObjects[i].offsetPosition.y, -additionalObjects[i].offsetPosition.z);
                rot = new Vector3(-rot.x, rot.y, -rot.z);

                pos = currentPos;
                float missValue = Random.value*100f;
                if(missValue < additionalObjects[i].randomMissing || currentPos.z > length-additionalObjects[i].minDistance){
                    continue;
                }
                if(isGizmo){
                    Gizmos.color = new Color(1, 0, 0, 0.5f);
                    Gizmos.DrawCube(transform.position+currentPos+offset+rand, new Vector3(1, 1, 1));
                    continue;
                }
                GameObject add = Instantiate(additionalObjects[i].prefab, transform.position+currentPos+offset+rand, Quaternion.Euler(rot), plusParent.transform);
                add.transform.localScale = scale;
                add.transform.localPosition = currentPos+offset+rand;
                add.transform.localRotation *= transform.rotation;
                add.transform.rotation *= Quaternion.Euler(0,180,0);
                back.Add(add);

            }
        }
        return back;
    }

    List<GameObject> FrontSide(int i, bool isGizmo){
        List<GameObject> back = new List<GameObject>();
        int left = (int)(width/additionalObjects[i].minDistance);
        float evenStep = width/left;

        //Random.InitState(additionalObjects[i].seed);
        if(additionalObjects[i].everyFloor){
            for(int x = 0; x<floors; x++){
                Vector3 pos = new Vector3(0,baseHeight+(0.5f+x)*(floorHeight+floorWidth),0);
                for(int j = 0; j<left; j++){
                    //random
                    Vector3 rot = additionalObjects[i].minRotation + (additionalObjects[i].maxRotation-additionalObjects[i].minRotation)*Random.value;
                    Vector3 currentPos;
                    if(!additionalObjects[i].evenDistribution){
                        currentPos = new Vector3(pos.x + additionalObjects[i].minDistance+(additionalObjects[i].maxDistance-additionalObjects[i].minDistance)*Random.value, pos.y, pos.z);
                    }
                    else{
                        currentPos = new Vector3(pos.x + evenStep, pos.y, pos.z);
                    }
                    Vector3 scale = additionalObjects[i].minScale + (additionalObjects[i].maxScale-additionalObjects[i].minScale)*Random.value;
                    Vector3 rand = new Vector3(-additionalObjects[i].randomPosition.z, additionalObjects[i].randomPosition.y, additionalObjects[i].randomPosition.x)*Random.value;
                    Vector3 offset = new Vector3(-additionalObjects[i].offsetPosition.z, additionalObjects[i].offsetPosition.y, additionalObjects[i].offsetPosition.x);
                    rot = new Vector3(-rot.z, rot.y, -rot.x);
                    scale = new Vector3(scale.z, scale.y, scale.x);

                    pos = currentPos;
                    float missValue = Random.value*100f;
                    if(missValue < additionalObjects[i].randomMissing || currentPos.x > width-additionalObjects[i].minDistance){
                        continue;
                    }
                    if(isGizmo){
                        Gizmos.color = new Color(1, 0, 0, 0.5f);
                        Gizmos.DrawCube(transform.position+currentPos+offset+rand, new Vector3(1, 1, 1));
                        continue;
                    }
                    GameObject add = Instantiate(additionalObjects[i].prefab, transform.position+currentPos+offset+rand, Quaternion.Euler(rot), plusParent.transform);
                    add.transform.localScale = new Vector3(scale.z, scale.y, scale.x);
                    add.transform.localPosition = currentPos+offset+rand;
                    add.transform.localRotation *= transform.rotation;
                    add.transform.rotation *= Quaternion.Euler(0,-90,0);
                    back.Add(add);

                }
            }
        }
        else{
            Vector3 pos = new Vector3(0,baseHeight+(0.5f)*(floorHeight+floorWidth),0);
            for(int j = 0; j<left; j++){
                //random
                Vector3 rot = additionalObjects[i].minRotation + (additionalObjects[i].maxRotation-additionalObjects[i].minRotation)*Random.value;
                Vector3 currentPos;
                if(!additionalObjects[i].evenDistribution){
                    currentPos = new Vector3(pos.x + additionalObjects[i].minDistance+(additionalObjects[i].maxDistance-additionalObjects[i].minDistance)*Random.value, pos.y, pos.z);
                }
                else{
                    currentPos = new Vector3(pos.x + evenStep, pos.y, pos.z);
                }
                Vector3 scale = additionalObjects[i].minScale + (additionalObjects[i].maxScale-additionalObjects[i].minScale)*Random.value;
                Vector3 rand = new Vector3(-additionalObjects[i].randomPosition.z, additionalObjects[i].randomPosition.y, additionalObjects[i].randomPosition.x)*Random.value;
                Vector3 offset = new Vector3(-additionalObjects[i].offsetPosition.z, additionalObjects[i].offsetPosition.y, additionalObjects[i].offsetPosition.x);
                rot = new Vector3(-rot.z, rot.y, -rot.x);
                scale = new Vector3(scale.z, scale.y, scale.x);

                pos = currentPos;
                float missValue = Random.value*100f;
                if(missValue < additionalObjects[i].randomMissing || currentPos.x > width-additionalObjects[i].minDistance){
                    continue;
                }
                if(isGizmo){
                    Gizmos.color = new Color(1, 0, 0, 0.5f);
                    Gizmos.DrawCube(transform.position+currentPos+offset+rand, new Vector3(1, 1, 1));
                    continue;
                }
                GameObject add = Instantiate(additionalObjects[i].prefab, transform.position+currentPos+offset+rand, Quaternion.Euler(rot), plusParent.transform);
                add.transform.localScale = new Vector3(scale.z, scale.y, scale.x);
                add.transform.localPosition = currentPos+offset+rand;
                add.transform.localRotation *= transform.rotation;
                add.transform.rotation *= Quaternion.Euler(0,-90,0);
                back.Add(add);

            }
        }
        return back;
    }

    List<GameObject> BackSide(int i, bool isGizmo){
        List<GameObject> back = new List<GameObject>();
        int left = (int)(width/additionalObjects[i].minDistance);
        float evenStep = width/left;

        //Random.InitState(additionalObjects[i].seed);
        if(additionalObjects[i].everyFloor){
            for(int x = 0; x<floors; x++){
                Vector3 pos = new Vector3(0,baseHeight+(0.5f+x)*(floorHeight+floorWidth),length);
                for(int j = 0; j<left; j++){

                    //random
                    Vector3 rot = additionalObjects[i].minRotation + (additionalObjects[i].maxRotation-additionalObjects[i].minRotation)*Random.value;
                    Vector3 currentPos;
                    if(!additionalObjects[i].evenDistribution){
                        currentPos = new Vector3(pos.x + additionalObjects[i].minDistance+(additionalObjects[i].maxDistance-additionalObjects[i].minDistance)*Random.value, pos.y, pos.z);
                    }
                    else{
                        currentPos = new Vector3(pos.x + evenStep, pos.y, pos.z);
                    }
                    Vector3 scale = additionalObjects[i].minScale + (additionalObjects[i].maxScale-additionalObjects[i].minScale)*Random.value;
                    Vector3 rand = new Vector3(additionalObjects[i].randomPosition.z, additionalObjects[i].randomPosition.y, -additionalObjects[i].randomPosition.x)*Random.value;
                    Vector3 offset = new Vector3(additionalObjects[i].offsetPosition.z, additionalObjects[i].offsetPosition.y, -additionalObjects[i].offsetPosition.x);
                    rot = new Vector3(-rot.z, rot.y, -rot.x);
                    scale = new Vector3(scale.z, scale.y, scale.x);

                    pos = currentPos;
                    float missValue = Random.value*100f;
                    if(missValue < additionalObjects[i].randomMissing || currentPos.x > width-additionalObjects[i].minDistance){
                        continue;
                    }
                    if(isGizmo){
                        Gizmos.color = new Color(1, 0, 0, 0.5f);
                        Gizmos.DrawCube(transform.position+currentPos+offset+rand, new Vector3(1, 1, 1));
                        continue;
                    }
                    GameObject add = Instantiate(additionalObjects[i].prefab, transform.position+currentPos+offset+rand, Quaternion.Euler(rot), plusParent.transform);
                    add.transform.localScale = new Vector3(scale.z, scale.y, scale.x);
                    add.transform.localPosition = currentPos+offset+rand;
                    add.transform.localRotation *= transform.rotation;
                    add.transform.rotation *= Quaternion.Euler(0,90,0);
                    back.Add(add);

                }
            }
        }
        else{
            Vector3 pos = new Vector3(0,baseHeight+(0.5f)*(floorHeight+floorWidth),length);
            for(int j = 0; j<left; j++){
                //random
                Vector3 rot = additionalObjects[i].minRotation + (additionalObjects[i].maxRotation-additionalObjects[i].minRotation)*Random.value;
                Vector3 currentPos;
                if(!additionalObjects[i].evenDistribution){
                    currentPos = new Vector3(pos.x + additionalObjects[i].minDistance+(additionalObjects[i].maxDistance-additionalObjects[i].minDistance)*Random.value, pos.y, pos.z);
                }
                else{
                    currentPos = new Vector3(pos.x + evenStep, pos.y, pos.z);
                }
                Vector3 scale = additionalObjects[i].minScale + (additionalObjects[i].maxScale-additionalObjects[i].minScale)*Random.value;
                Vector3 rand = new Vector3(additionalObjects[i].randomPosition.z, additionalObjects[i].randomPosition.y, -additionalObjects[i].randomPosition.x)*Random.value;
                Vector3 offset = new Vector3(additionalObjects[i].offsetPosition.z, additionalObjects[i].offsetPosition.y, -additionalObjects[i].offsetPosition.x);
                rot = new Vector3(-rot.z, rot.y, -rot.x);
                scale = new Vector3(scale.z, scale.y, scale.x);

                pos = currentPos;
                float missValue = Random.value*100f;
                if(missValue < additionalObjects[i].randomMissing || currentPos.x > width-additionalObjects[i].minDistance){
                    continue;
                }
                if(isGizmo){
                    Gizmos.color = new Color(1, 0, 0, 0.5f);
                    Gizmos.DrawCube(transform.position+currentPos+offset+rand, new Vector3(1, 1, 1));
                    continue;
                }
                GameObject add = Instantiate(additionalObjects[i].prefab, transform.position+currentPos+offset+rand, Quaternion.Euler(rot), plusParent.transform);
                add.transform.localScale = new Vector3(scale.z, scale.y, scale.x);
                add.transform.localPosition = currentPos+offset+rand;
                add.transform.localRotation *= transform.rotation;
                add.transform.rotation *= Quaternion.Euler(0,90,0);
                back.Add(add);

            }
        }
        return back;
    }

    List<GameObject> RoofLeftSide(int i, bool isGizmo){
        List<GameObject> back = new List<GameObject>();
        int left = (int)(length/additionalObjects[i].minDistance);
        float evenStep = length/left;

        //Random.InitState(additionalObjects[i].seed);
        Vector3 pos = new Vector3(width/4,baseHeight+floors*(floorHeight+floorWidth)+0.5f*roofHeight,0);
        for(int j = 0; j<left; j++){
            //random
            Vector3 rot = additionalObjects[i].minRotation + (additionalObjects[i].maxRotation-additionalObjects[i].minRotation)*Random.value;
            Vector3 currentPos;
            if(!additionalObjects[i].evenDistribution){
                currentPos = new Vector3(pos.x, pos.y, pos.z + additionalObjects[i].minDistance+(additionalObjects[i].maxDistance-additionalObjects[i].minDistance)*Random.value);
            }
            else{
                currentPos = new Vector3(pos.x, pos.y, pos.z + evenStep);
            }
            Vector3 scale = additionalObjects[i].minScale + (additionalObjects[i].maxScale-additionalObjects[i].minScale)*Random.value;
            Vector3 rand = additionalObjects[i].randomPosition*Random.value;

            pos = currentPos;
            float missValue = Random.value*100f;
            if(missValue < additionalObjects[i].randomMissing || currentPos.z > length-additionalObjects[i].minDistance){
                continue;
            }
            if(isGizmo){
                Gizmos.color = new Color(1, 0, 0, 0.5f);
                Gizmos.DrawCube(transform.position+currentPos+additionalObjects[i].offsetPosition+rand, new Vector3(1, 1, 1));
                continue;
            }
            GameObject add = Instantiate(additionalObjects[i].prefab, transform.position+currentPos+additionalObjects[i].offsetPosition+rand, Quaternion.Euler(rot), plusParent.transform);
            add.transform.localScale = scale;
            if(additionalObjects[i].followRoofAngle){
                add.transform.rotation *= Quaternion.Euler(0, 0, Mathf.Tan(roofHeight/(width/2)*Mathf.Deg2Rad));
            }
            add.transform.localPosition = currentPos+additionalObjects[i].offsetPosition+rand;
            add.transform.localRotation *= transform.rotation;
            back.Add(add);

        }
        return back;

    }

    List<GameObject> RoofRightSide(int i, bool isGizmo){
        List<GameObject> back = new List<GameObject>();
        int left = (int)(length/additionalObjects[i].minDistance);
        float evenStep = length/left;
        //Random.InitState(additionalObjects[i].seed);
        Vector3 pos = new Vector3(width-width/4,baseHeight+floors*(floorHeight+floorWidth)+0.5f*roofHeight,0);
        for(int j = 0; j<left; j++){
            //random
            Vector3 rot = additionalObjects[i].minRotation + (additionalObjects[i].maxRotation-additionalObjects[i].minRotation)*Random.value;
            Vector3 currentPos;
            if(!additionalObjects[i].evenDistribution){
                currentPos = new Vector3(pos.x, pos.y, pos.z + additionalObjects[i].minDistance+(additionalObjects[i].maxDistance-additionalObjects[i].minDistance)*Random.value);
            }
            else{
                currentPos = new Vector3(pos.x, pos.y, pos.z + evenStep);
            }
            Vector3 scale = additionalObjects[i].minScale + (additionalObjects[i].maxScale-additionalObjects[i].minScale)*Random.value;
            Vector3 rand = new Vector3(-additionalObjects[i].randomPosition.x, additionalObjects[i].randomPosition.y, -additionalObjects[i].randomPosition.z)*Random.value;
            Vector3 offset = new Vector3(-additionalObjects[i].offsetPosition.x, additionalObjects[i].offsetPosition.y, -additionalObjects[i].offsetPosition.z);
            rot = new Vector3(-rot.x, rot.y, -rot.z);

            pos = currentPos;
            float missValue = Random.value*100f;
            if(missValue < additionalObjects[i].randomMissing || currentPos.z > length-additionalObjects[i].minDistance){
                continue;
            }
            if(isGizmo){
                Gizmos.color = new Color(1, 0, 0, 0.5f);
                Gizmos.DrawCube(transform.position+currentPos+offset+rand, new Vector3(1, 1, 1));
                continue;
            }
            GameObject add = Instantiate(additionalObjects[i].prefab, transform.position+currentPos+offset+rand, Quaternion.Euler(rot), plusParent.transform);
            add.transform.localScale = scale;
            add.transform.localPosition = currentPos+offset+rand;
            add.transform.localRotation *= transform.rotation;
            add.transform.rotation *= Quaternion.Euler(0,180,0); 
            if(additionalObjects[i].followRoofAngle){
                add.transform.rotation *= Quaternion.Euler(0, 0, Mathf.Tan(roofHeight/(width/2)*Mathf.Deg2Rad));
            }
            back.Add(add);

        }
        return back;

    }

    void ClearAdditionals(){
        for(int i = 0; i<plusParent.transform.childCount; i++){
            StartCoroutine(Destroy(plusParent.transform.GetChild(i).gameObject));
            //DestroyImmediate(plusParent.transform.GetChild(i).gameObject);
        }
    }

    GameObject CombinedCut(List<GameObject> cuts){//combines all the cut objects EXPERIMENTAL (also NOT IN USE)
        GameObject combined = new GameObject();
        combined.transform.parent = plusParent.transform;
        combined.AddComponent<MeshFilter>();
        combined.AddComponent<MeshRenderer>();
        MeshCombine comber = combined.AddComponent<MeshCombine>();
        List<GameObject> cutparts = new List<GameObject>();
        for(int i = 0; i<cuts.Count; i++){
            cutparts.Add(cuts[i].GetComponent<cutout>().cutMesh);
        }
        comber.sourceMeshFilters = cutparts;
        comber.destroyOldOnes = false;
        comber.CombineMeshes();
        cutout c = combined.AddComponent<cutout>();
        c.cutMesh = combined;
        return combined;
    }

    //EXPERIMENTAL, this gets called, when cutting options enabled (should avoid using this, might cause stack overflow...)
    void CutUpdate(List<GameObject> cutters, bool outer, bool inw, bool _roof, bool rbase, bool rbaseheight, bool bas, bool floors, bool inroof){
        if(outer){
            for(int i = 0; i<cutters.Count; i++){
                GameObject newer = cutters[i].GetComponent<cutout>().Cut(outerWalls);
                StartCoroutine(Destroy(outerWalls));
                outerWalls = newer;
            }
        }
        if(inw){
            for(int i = 0; i<cutters.Count; i++){
                GameObject newer = cutters[i].GetComponent<cutout>().Cut(innerWalls);
                StartCoroutine(Destroy(innerWalls));
                innerWalls = newer;
            }
        }
        if(_roof){
            for(int i = 0; i<cutters.Count; i++){
                GameObject newer = cutters[i].GetComponent<cutout>().Cut(roof);
                StartCoroutine(Destroy(roof));
                roof = newer;
            }
        }
        if(rbase){
            for(int i = 0; i<cutters.Count; i++){
                GameObject newer = cutters[i].GetComponent<cutout>().Cut(roofBase);
                StartCoroutine(Destroy(roofBase));
                roofBase = newer;
            }
        }
        if(rbaseheight){
            for(int i = 0; i<cutters.Count; i++){
                GameObject newer = cutters[i].GetComponent<cutout>().Cut(roofBaseHeight);
                StartCoroutine(Destroy(roofBaseHeight));
                roofBaseHeight = newer;
            }
        }
        if(bas){
            for(int i = 0; i<cutters.Count; i++){
                GameObject newer = cutters[i].GetComponent<cutout>().Cut(baser);
                StartCoroutine(Destroy(baser));
                baser = newer;
            }
        }
        if(floors){
            for(int i = 0; i<cutters.Count; i++){
                GameObject newer = cutters[i].GetComponent<cutout>().Cut(floor);
                StartCoroutine(Destroy(floor));
                floor = newer;
            }
        }
        if(inroof){
            for(int i = 0; i<cutters.Count; i++){
                GameObject newer = cutters[i].GetComponent<cutout>().Cut(innerRoofs);
                StartCoroutine(Destroy(innerRoofs));
                innerRoofs = newer;
            }
        }
    }

    void CutOne(GameObject cutters, bool outer, bool inw, bool _roof, bool rbase, bool rbaseheight, bool bas, bool floors, bool inroof){//experimental, not in use
        if(outer){
            GameObject newer = cutters.GetComponent<cutout>().Cut(outerWalls);
            StartCoroutine(Destroy(outerWalls));
            outerWalls = newer;
        }
        if(inw){
            GameObject newer = cutters.GetComponent<cutout>().Cut(innerWalls);
            StartCoroutine(Destroy(innerWalls));
            innerWalls = newer;
        }
        if(_roof){
            GameObject newer = cutters.GetComponent<cutout>().Cut(roof);
            StartCoroutine(Destroy(roof));
            roof = newer;
        }
        if(rbase){
            GameObject newer = cutters.GetComponent<cutout>().Cut(roofBase);
            StartCoroutine(Destroy(roofBase));
            roofBase = newer;
        }
        if(rbaseheight){
            GameObject newer = cutters.GetComponent<cutout>().Cut(roofBaseHeight);
            StartCoroutine(Destroy(roofBaseHeight));
            roofBaseHeight = newer;
        }
        if(bas){
            GameObject newer = cutters.GetComponent<cutout>().Cut(baser);
            StartCoroutine(Destroy(baser));
            baser = newer;
        }
        if(floors){
            GameObject newer = cutters.GetComponent<cutout>().Cut(floor);
            StartCoroutine(Destroy(floor));
            floor = newer;
        }
        if(inroof){
            GameObject newer = cutters.GetComponent<cutout>().Cut(innerRoofs);
            StartCoroutine(Destroy(innerRoofs));
            innerRoofs = newer;
        }
    }

    void OnDrawGizmosSelected(){
        //Addition(true);
        
    }

}

#if UNITY_EDITOR
[CustomEditor(typeof(house))]
public class house_Editor : Editor
{
    public override void OnInspectorGUI()
    {
        house script = (house)target;

        if (GUILayout.Button("UPDATE"))
        {
            script.UpdateParts();
            script.Addition();
            EditorUtility.SetDirty(target);
        }

        DrawDefaultInspector(); 
    }
}
#endif
}