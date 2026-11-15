using System.Collections.Generic;
using System.Collections;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace SHM{
[ExecuteInEditMode]
public class houseBridge : MonoBehaviour
{
    //This script holds all the parameters for the bridge (bridge=fillment between houses)
    [Header("Cutter (to call the cutout script)")]
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
    
    [Header("Basic Parameters")]
    [Tooltip("Pointy or flat bridge between two houses")]
    public bool pointy = false;
    public float angle = 90f;
    public float width = 10f;
    public float wallWidth = 0.3f;
    public float floorWidth = 0.3f;
    public int floors = 2;
    public float floorHeight = 2.5f;
    public float baseHeight = 1f;
    public float baseOutHeight = 0.8f;
    public float baseWidth = 0.1f;

    [Header("Roof Settings")]
    public float roofHeight = 3f;
    public float roofEndHeight = 0f;
    public float roofEndLength = 0f;
    public float roofOverlay = 1f;
    public float roofWidth = 1f;

    [Header("Texture size")]
    public float outerWallsTS = 1f;
    public float innerWallsTS = 1f;
    public float roofTS = 1f;
    public float roofBaseTS = 1f;
    public float roofBaseHeightTS = 1f;
    public float baseTS = 1f;
    public float floorsTS = 1f;
    public float innerRoofsTS = 1f;
    // [HideInInspector]
    // public bool updateInEditor = false;


    [Header("Accessorries")]
    public Additional[] additionalObjects;

    public GameObject plusParent;
    public GameObject pParent;


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
        Addition();
    }


    IEnumerator Destroy(GameObject go){
        yield return new WaitForEndOfFrame();
        DestroyImmediate(go);
        //this works as the original Destroy() method, but this is working in editmode
    }

    // void Update(){
    //     if(!Application.isPlaying && Application.isEditor && updateInEditor){
    //         UpdateParts();
    //         Addition();
    //         updateInEditor = false;
    //     }
    // }

    public void UpdateParts(){
        if(outerWalls != null){
            outerWalls.GetComponent<bridgeOuter>().Draw();
        }
        if(innerWalls != null){
            innerWalls.GetComponent<bridgeInner>().Draw();
        }
        if(roof != null){
            roof.GetComponent<bridgeRoof>().Draw();
        }
        if(roofBase != null){
            roofBase.GetComponent<bridgeRoofBase>().Draw();
        }
        if(roofBaseHeight != null){
            roofBaseHeight.GetComponent<bridgeRoofBaseHeight>().Draw();
        }
        if(baser != null){
            baser.GetComponent<bridgeBase>().Draw();
        }
        if(floor != null){
            floor.GetComponent<bridgeFloor>().Draw();
        }
        if(innerRoofs != null){
            innerRoofs.GetComponent<bridgeInnerRoofs>().Draw();
        }
    }

    public void UpdateMesh(Mesh mesh, Vector3[] vertices, int[][] triangles, Vector2[] UV){
        mesh.Clear();
        mesh.vertices = vertices;
        mesh.subMeshCount = 2;
        mesh.SetTriangles(triangles[0], 0);
        mesh.SetTriangles(triangles[1], 1);
        mesh.uv = UV;
        mesh.Optimize();

        mesh.RecalculateNormals();
 
    }

    public void Addition(){
        ClearAdditionals();
        plusParent.transform.localRotation = Quaternion.identity;
        pParent.transform.rotation = Quaternion.identity;
        if(!pointy){
            plusParent.transform.localRotation *= Quaternion.Euler(0, 90-angle/2, 0);
        }
        else{
            pParent.transform.rotation *= transform.rotation;
            pParent.transform.rotation *= Quaternion.Euler(0, 180-angle, 0);
            pParent.transform.localPosition = new Vector3(Mathf.Tan((90-angle/2)*Mathf.Deg2Rad)*width*Mathf.Sin((transform.rotation.y)*Mathf.Deg2Rad),0,Mathf.Tan((90-angle/2)*Mathf.Deg2Rad)*width*Mathf.Cos((transform.rotation.y)*Mathf.Deg2Rad));
        }
        for(int i = 0; i<additionalObjects.Length; i++){
            
            List<GameObject> cut = new List<GameObject>();
            Random.InitState(additionalObjects[i].seed);
            if(!additionalObjects[i].isRoof){
                if((additionalObjects[i].left || additionalObjects[i].right || additionalObjects[i].front || additionalObjects[i].back) && !pointy){
                    cut.AddRange(FlatSide(i));
                }
                else if((additionalObjects[i].left || additionalObjects[i].right || additionalObjects[i].front || additionalObjects[i].back) && pointy){
                    cut.AddRange(PointySide(i));
                }
            }
            else{//this is the roof
                if((additionalObjects[i].left || additionalObjects[i].right || additionalObjects[i].front || additionalObjects[i].back) && !pointy){
                    cut.AddRange(FlatRoof(i));
                }
                else if((additionalObjects[i].left || additionalObjects[i].right || additionalObjects[i].front || additionalObjects[i].back) && pointy){
                    cut.AddRange(PointyRoof(i));
                }
                
            }
            CutUpdate(cut, additionalObjects[i].cutOuterWalls, additionalObjects[i].cutInnerWalls, additionalObjects[i].cutRoof,
                additionalObjects[i].cutRoofBase, additionalObjects[i].cutRoofBaseHeight, additionalObjects[i].cutBase,
                additionalObjects[i].cutFloors, additionalObjects[i].cutInnerRoofs);
        }

    }

    void ClearAdditionals(){
        for(int i = 0; i<plusParent.transform.childCount; i++){
            if(plusParent.transform.GetChild(i).gameObject != pParent){
                StartCoroutine(Destroy(plusParent.transform.GetChild(i).gameObject));
            }
        }
        for(int i = 0; i<pParent.transform.childCount; i++){
            StartCoroutine(Destroy(pParent.transform.GetChild(i).gameObject));
        }
    }

    List<GameObject> FlatSide(int i){
        List<GameObject> back = new List<GameObject>();
        float length = Mathf.Sqrt(2*Mathf.Pow(width, 2)*(1-Mathf.Cos((180-angle)*Mathf.Deg2Rad)));//cosinus theorem...
        int left = (int)(length/additionalObjects[i].minDistance);

        //Random.InitState(additionalObjects[i].seed);
        if(additionalObjects[i].everyFloor){
            for(int x = 0; x<floors; x++){
                Vector3 pos = new Vector3(0,baseHeight+(0.5f+x)*(floorHeight+floorWidth),0);
                for(int j = 0; j<left; j++){

                    //random
                    Vector3 rot = additionalObjects[i].minRotation + (additionalObjects[i].maxRotation-additionalObjects[i].minRotation)*Random.value;
                    Vector3 currentPos = new Vector3(pos.x, pos.y, pos.z + additionalObjects[i].minDistance+(additionalObjects[i].maxDistance-additionalObjects[i].minDistance)*Random.value);// + 
                    Vector3 scale = additionalObjects[i].minScale + (additionalObjects[i].maxScale-additionalObjects[i].minScale)*Random.value;
                    Vector3 rand = additionalObjects[i].randomPosition*Random.value;

                    pos = currentPos;
                    float missValue = Random.value*100f;
                    if(missValue < additionalObjects[i].randomMissing || currentPos.z > length-additionalObjects[i].minDistance){
                        //StartCoroutine(Destroy(add));
                        continue;
                    }
                    GameObject add = Instantiate(additionalObjects[i].prefab, transform.position+currentPos+additionalObjects[i].offsetPosition+rand, Quaternion.Euler(rot), plusParent.transform);//pp
                    add.transform.localScale = scale;
                    add.transform.localPosition = currentPos+additionalObjects[i].offsetPosition+rand;
                    add.transform.localRotation *= plusParent.transform.rotation;

                    back.Add(add);
                }
            }
        }
        else{
            Vector3 pos = new Vector3(0,baseHeight+(0.5f)*(floorHeight+floorWidth),0);
            for(int j = 0; j<left; j++){

                //random
                Vector3 rot = additionalObjects[i].minRotation + (additionalObjects[i].maxRotation-additionalObjects[i].minRotation)*Random.value;
                Vector3 currentPos = new Vector3(pos.x, pos.y, pos.z + additionalObjects[i].minDistance+(additionalObjects[i].maxDistance-additionalObjects[i].minDistance)*Random.value);// + 
                Vector3 scale = additionalObjects[i].minScale + (additionalObjects[i].maxScale-additionalObjects[i].minScale)*Random.value;
                Vector3 rand = additionalObjects[i].randomPosition*Random.value;

                pos = currentPos;
                float missValue = Random.value*100f;
                if(missValue < additionalObjects[i].randomMissing || currentPos.z > length-additionalObjects[i].minDistance){
                    //StartCoroutine(Destroy(add));
                    continue;
                }
                GameObject add = Instantiate(additionalObjects[i].prefab, transform.position+currentPos+additionalObjects[i].offsetPosition+rand, Quaternion.Euler(rot), plusParent.transform);//pp
                add.transform.localScale = scale;
                add.transform.localPosition = currentPos+additionalObjects[i].offsetPosition+rand;
                add.transform.localRotation *= plusParent.transform.rotation;

                back.Add(add);
            }
        }
        return back;
    }

    List<GameObject> FlatRoof(int i){
        List<GameObject> back = new List<GameObject>();
        float length = Mathf.Sqrt(2*Mathf.Pow(width, 2)*(1-Mathf.Cos((180-angle)*Mathf.Deg2Rad)));//cosinus theorem...
        int left = (int)(length/additionalObjects[i].minDistance);

        //Random.InitState(additionalObjects[i].seed);

        Vector3 pos = new Vector3(width/4,baseHeight+floors*(floorHeight+floorWidth)+0.5f*roofHeight,0);
        for(int j = 0; j<left; j++){

            //random
            Vector3 rot = additionalObjects[i].minRotation + (additionalObjects[i].maxRotation-additionalObjects[i].minRotation)*Random.value;
            Vector3 currentPos = new Vector3(pos.x, pos.y, pos.z + additionalObjects[i].minDistance+(additionalObjects[i].maxDistance-additionalObjects[i].minDistance)*Random.value);// + 
            Vector3 scale = additionalObjects[i].minScale + (additionalObjects[i].maxScale-additionalObjects[i].minScale)*Random.value;
            Vector3 rand = additionalObjects[i].randomPosition*Random.value;
            pos = currentPos;
            float missValue = Random.value*100f;
            if(missValue < additionalObjects[i].randomMissing || currentPos.z > length-additionalObjects[i].minDistance){
                //StartCoroutine(Destroy(add));
                continue;
            }

            GameObject add = Instantiate(additionalObjects[i].prefab, transform.position+currentPos+additionalObjects[i].offsetPosition+rand, Quaternion.Euler(rot), plusParent.transform);//pp
            add.transform.localScale = scale;
            add.transform.localPosition = currentPos+additionalObjects[i].offsetPosition+rand;
            add.transform.localRotation *= plusParent.transform.rotation;

            back.Add(add);
        }
        return back;

    }

    List<GameObject> PointySide(int i){
        List<GameObject> back = new List<GameObject>();
        float length = Mathf.Tan((90-angle/2)*Mathf.Deg2Rad)*width;
        int left = (int)(length/additionalObjects[i].minDistance);

        //Random.InitState(additionalObjects[i].seed);
        if(additionalObjects[i].everyFloor){
            for(int x = 0; x<floors; x++){
                Vector3 pos = new Vector3(0,baseHeight+(0.5f+x)*(floorHeight+floorWidth),0);
                for(int j = 0; j<left; j++){

                    //random
                    Vector3 rot = additionalObjects[i].minRotation + (additionalObjects[i].maxRotation-additionalObjects[i].minRotation)*Random.value;
                    Vector3 currentPos = new Vector3(pos.x, pos.y, pos.z + additionalObjects[i].minDistance+(additionalObjects[i].maxDistance-additionalObjects[i].minDistance)*Random.value);// + 
                    Vector3 scale = additionalObjects[i].minScale + (additionalObjects[i].maxScale-additionalObjects[i].minScale)*Random.value;
                    Vector3 rand = additionalObjects[i].randomPosition*Random.value;

                    pos = currentPos;
                    float missValue = Random.value*100f;
                    if(missValue < additionalObjects[i].randomMissing || currentPos.z > length-additionalObjects[i].minDistance){
                        //StartCoroutine(Destroy(add));
                        continue;
                    }
                    GameObject add = Instantiate(additionalObjects[i].prefab, transform.position+currentPos+additionalObjects[i].offsetPosition+rand, Quaternion.Euler(rot), plusParent.transform);//PP
                    add.transform.localScale = scale;
                    add.transform.localPosition = currentPos+additionalObjects[i].offsetPosition+rand;
                    add.transform.localRotation *= plusParent.transform.rotation;

                    back.Add(add);
                }
            }

        }
        else{
            Vector3 pos = new Vector3(0,baseHeight+(0.5f)*(floorHeight+floorWidth),0);
            for(int j = 0; j<left; j++){

                //random
                Vector3 rot = additionalObjects[i].minRotation + (additionalObjects[i].maxRotation-additionalObjects[i].minRotation)*Random.value;
                Vector3 currentPos = new Vector3(pos.x, pos.y, pos.z + additionalObjects[i].minDistance+(additionalObjects[i].maxDistance-additionalObjects[i].minDistance)*Random.value);// + 
                Vector3 scale = additionalObjects[i].minScale + (additionalObjects[i].maxScale-additionalObjects[i].minScale)*Random.value;
                Vector3 rand = additionalObjects[i].randomPosition*Random.value;

                pos = currentPos;
                float missValue = Random.value*100f;
                if(missValue < additionalObjects[i].randomMissing || currentPos.z > length-additionalObjects[i].minDistance){
                    //StartCoroutine(Destroy(add));
                    continue;
                }
                GameObject add = Instantiate(additionalObjects[i].prefab, transform.position+currentPos+additionalObjects[i].offsetPosition+rand, Quaternion.Euler(rot), plusParent.transform);//pp
                add.transform.localScale = scale;
                add.transform.localPosition = currentPos+additionalObjects[i].offsetPosition+rand;
                add.transform.localRotation *= plusParent.transform.rotation;

                back.Add(add);
            }
            
        }

       
        if(additionalObjects[i].everyFloor){
            for(int x = 0; x<floors; x++){
                Vector3 pos = new Vector3(0,baseHeight+(0.5f+x)*(floorHeight+floorWidth),0);
                for(int j = 0; j<left; j++){

                    //random
                    Vector3 rot = additionalObjects[i].minRotation + (additionalObjects[i].maxRotation-additionalObjects[i].minRotation)*Random.value;
                    Vector3 currentPos = new Vector3(pos.x, pos.y, pos.z + additionalObjects[i].minDistance+(additionalObjects[i].maxDistance-additionalObjects[i].minDistance)*Random.value);// + 
                    Vector3 scale = additionalObjects[i].minScale + (additionalObjects[i].maxScale-additionalObjects[i].minScale)*Random.value;
                    Vector3 rand = additionalObjects[i].randomPosition*Random.value;

                    pos = currentPos;
                    float missValue = Random.value*100f;
                    if(missValue < additionalObjects[i].randomMissing || currentPos.z > length-additionalObjects[i].minDistance){
                        //StartCoroutine(Destroy(add));
                        continue;
                    }
                    GameObject add = Instantiate(additionalObjects[i].prefab, pParent.transform.position+currentPos+additionalObjects[i].offsetPosition+rand, Quaternion.Euler(rot), pParent.transform);
                    add.transform.localScale = scale;
                    add.transform.localPosition = currentPos+additionalObjects[i].offsetPosition+rand;
                    add.transform.localRotation *= pParent.transform.rotation;

                    back.Add(add);
                }
            }

        }
        else{
            Vector3 pos = new Vector3(0,baseHeight+(0.5f)*(floorHeight+floorWidth),0);
            for(int j = 0; j<left; j++){

                //random
                Vector3 rot = additionalObjects[i].minRotation + (additionalObjects[i].maxRotation-additionalObjects[i].minRotation)*Random.value;
                Vector3 currentPos = new Vector3(pos.x, pos.y, pos.z + additionalObjects[i].minDistance+(additionalObjects[i].maxDistance-additionalObjects[i].minDistance)*Random.value);// + 
                Vector3 scale = additionalObjects[i].minScale + (additionalObjects[i].maxScale-additionalObjects[i].minScale)*Random.value;
                Vector3 rand = additionalObjects[i].randomPosition*Random.value;

                pos = currentPos;
                float missValue = Random.value*100f;
                if(missValue < additionalObjects[i].randomMissing || currentPos.z > length-additionalObjects[i].minDistance){
                    //StartCoroutine(Destroy(add));
                    continue;
                }
                GameObject add = Instantiate(additionalObjects[i].prefab, pParent.transform.position+currentPos+additionalObjects[i].offsetPosition+rand, Quaternion.Euler(rot), pParent.transform);

                add.transform.localScale = scale;
                add.transform.localPosition = currentPos+additionalObjects[i].offsetPosition+rand;
                add.transform.localRotation *= pParent.transform.rotation;

                back.Add(add);
            }
        }

        return back;
    }

    List<GameObject> PointyRoof(int i){
        List<GameObject> back = new List<GameObject>();
        float length = Mathf.Tan((90-angle/2)*Mathf.Deg2Rad)*width;
        int left = (int)(length/additionalObjects[i].minDistance);

        //Random.InitState(additionalObjects[i].seed);

        Vector3 pos = new Vector3(width/4,baseHeight+floors*(floorHeight+floorWidth)+0.5f*roofHeight,0);
        for(int j = 0; j<left; j++){

            //random
            Vector3 rot = additionalObjects[i].minRotation + (additionalObjects[i].maxRotation-additionalObjects[i].minRotation)*Random.value;
            Vector3 currentPos = new Vector3(pos.x, pos.y, pos.z + additionalObjects[i].minDistance+(additionalObjects[i].maxDistance-additionalObjects[i].minDistance)*Random.value);// + 
            Vector3 scale = additionalObjects[i].minScale + (additionalObjects[i].maxScale-additionalObjects[i].minScale)*Random.value;
            Vector3 rand = additionalObjects[i].randomPosition*Random.value;

            pos = currentPos;
            float missValue = Random.value*100f;
            if(missValue < additionalObjects[i].randomMissing || currentPos.z > length-additionalObjects[i].minDistance){
                //StartCoroutine(Destroy(add));
                continue;
            }
            GameObject add = Instantiate(additionalObjects[i].prefab, transform.position+currentPos+additionalObjects[i].offsetPosition+rand, Quaternion.Euler(rot), plusParent.transform);//pp
            add.transform.localScale = scale;
            add.transform.localPosition = currentPos+additionalObjects[i].offsetPosition+rand;
            add.transform.localRotation *= plusParent.transform.rotation;

            back.Add(add);
        }

        
        pos = new Vector3(width/4,baseHeight+floors*(floorHeight+floorWidth)+0.5f*roofHeight,0);
        for(int j = 0; j<left; j++){

            //random
            Vector3 rot = additionalObjects[i].minRotation + (additionalObjects[i].maxRotation-additionalObjects[i].minRotation)*Random.value;
            Vector3 currentPos = new Vector3(pos.x, pos.y, pos.z + additionalObjects[i].minDistance+(additionalObjects[i].maxDistance-additionalObjects[i].minDistance)*Random.value);// + 
            Vector3 scale = additionalObjects[i].minScale + (additionalObjects[i].maxScale-additionalObjects[i].minScale)*Random.value;
            Vector3 rand = additionalObjects[i].randomPosition*Random.value;

            pos = currentPos;
            float missValue = Random.value*100f;
            if(missValue < additionalObjects[i].randomMissing || currentPos.z > length-additionalObjects[i].minDistance){
                //StartCoroutine(Destroy(add));
                continue;
            }
            GameObject add = Instantiate(additionalObjects[i].prefab, pParent.transform.position+currentPos+additionalObjects[i].offsetPosition+rand, Quaternion.Euler(rot), pParent.transform);

            add.transform.localScale = scale;
            add.transform.localPosition = currentPos+additionalObjects[i].offsetPosition+rand;
            add.transform.localRotation *= pParent.transform.rotation;

            back.Add(add);
        }
        return back;
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
}
 
#if UNITY_EDITOR
[CustomEditor(typeof(houseBridge))]
public class houseBridge_Editor : Editor
{
    public override void OnInspectorGUI()
    {
        houseBridge script = (houseBridge)target;

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
