using System.Collections.Generic;
using UnityEngine;
using System.Collections;
#if UNITY_EDITOR
using UnityEditor;
#endif
namespace SHM{
[System.Serializable]
public class housePiece{
    public float angle = 90f;
    public bool drawBridge = true;
    public bool pointyBridge;
    public float length = 3f;
    [Header("To customize your houserow, use your own prefabs (you can leave it blank)")]
    public GameObject house;
    public GameObject bridge;
}


[ExecuteInEditMode]
public class houserow : MonoBehaviour
{
    //This script generates a more complex house (concatenate the simple houses)
    private List<GameObject> houses = new List<GameObject>();
    private List<GameObject> bridges = new List<GameObject>();
    [Header("Cluster of Houses")]
    public housePiece[] cluster;
    [Header("Default prefabs")]
    public GameObject house;
    public GameObject bridge;

    public bool isCircle = false;
    public float closeAngle = 90f;
    public bool deleteInnerAdditionals = true;

    [Header("General parameters (same as simple house)")]
    public float width = 12f;

    public float wallWidth = 0.3f;
    public float floorWidth = 0.3f;
    public int floors = 2;
    public float floorHeight = 2.5f;
    public float baseHeight = 1f;
    public float baseOutHeight = 0.8f;
    public float baseWidth = 0.1f;


    [Header("Roof Settings (same as simple house)")]
    public float roofHeight = 3f;
    public float roofEndHeight = 0f;
    public float roofEndLength = 0f;
    public float roofOverlay = 1f;
    public float roofWidth = 1f;

    [HideInInspector]
    public bool updateInEditor = true;

    void Start()
    {
        if(Application.isPlaying){
            PlaceHouses();
            StartCoroutine(StartUpdate());
        }
        else if (!Application.isPlaying && Application.isEditor){
            //updateInEditor = true;
            PlaceHouses();

        }
    }

    public void CoroutineCaller(){
        StartCoroutine(StartUpdate());
    }


    void Update(){

        if(!Application.isPlaying && Application.isEditor && updateInEditor){
            StartCoroutine(StartUpdate());
            updateInEditor = false;
        }
        if(!Application.isPlaying && Application.isEditor && transform.childCount < cluster.Length){
            StartCoroutine(StartUpdate());
        }
    }

    void Clear(){

        for(int i = 0; i<transform.childCount; i++){
            StartCoroutine(Destroy(transform.GetChild(i).gameObject));
        }
    }

    IEnumerator Destroy(GameObject go){
        yield return new WaitForEndOfFrame();
        DestroyImmediate(go);
    }

    IEnumerator StartUpdate(){
        //updateMem = updateInEditor;
        //updateInEditor = true;
        yield return new WaitForEndOfFrame();
        //yield return new WaitForSeconds(0.05f);
        Clear();
        yield return new WaitForEndOfFrame();
        //yield return new WaitForSeconds(0.05f);
        PlaceHouses();
        yield return new WaitForEndOfFrame();
        //yield return new WaitForSeconds(0.05f);
        ReAdd();
        yield return new WaitForEndOfFrame();
        //yield return new WaitForSeconds(0.05f);
        Redraw();
        //updateInEditor = updateMem;

    }

    public void Redraw(){
        for(int i = houses.Count-1; i>0; i--){
           houses[i].GetComponent<house>().UpdateParts();
        }
        for(int i = bridges.Count-1; i>0; i--){
            if(bridges[i] != null){
                bridges[i].GetComponent<houseBridge>().UpdateParts();
            }
        }
        houses[0].GetComponent<house>().UpdateParts();
    }

    public void ReAdd(){
        for(int i = houses.Count-1; i>0; i--){
           houses[i].GetComponent<house>().Addition(false);
        }
        for(int i = bridges.Count-1; i>0; i--){
            if(bridges[i] != null){
                bridges[i].GetComponent<houseBridge>().Addition();
            }
        }
    }

    public void PlaceHouses(){
        Clear();
        houses.Clear();
        //draw first house
        GameObject prefab;
        if(cluster[0].house == null){
            prefab = house;
        }
        else{
            prefab = cluster[0].house;
        }
        GameObject first = Instantiate(house, transform.position, transform.rotation*Quaternion.Euler(0,cluster[0].angle,0), transform);
        house firstH = first.GetComponent<house>();
        AssaignBuilding(firstH, 0);
        houses.Add(first);

        for(int i = 1; i < cluster.Length; i++){
            //draw bridge
            //draw house
            GameObject h;
            GameObject b;
            if(cluster[i].house == null){
                h = house;
            }
            else{
                h = cluster[i].house;
            }
            if(cluster[i].bridge == null){
                b = bridge;
            }
            else{
                b = cluster[i].bridge;
            }
            GameObject houseI = Instantiate(h, houses[i-1].transform.position, houses[i-1].transform.rotation, houses[i-1].transform);
            GameObject bridgeI = Instantiate(b, houses[i-1].transform.position, houses[i-1].transform.rotation, houses[i-1].transform);

            houseI.transform.localPosition += new Vector3(0,0,cluster[i-1].length);
            if(cluster[i].angle < 180){
                //mirror house

                GameObject tempParent = new GameObject();
                tempParent.transform.parent = houses[i-1].transform;
                tempParent.transform.localPosition = new Vector3(width, 0, cluster[i-1].length);
                tempParent.transform.localRotation = Quaternion.identity;
                houseI.transform.parent = tempParent.transform;
                houseI.transform.localPosition = new Vector3(-width,0,0);
                houseI.transform.localRotation = Quaternion.identity;
                tempParent.transform.localRotation = Quaternion.Euler(0,180-cluster[i].angle,0);
                //rotation happened, returning to the original state
                houseI.transform.parent = houses[i-1].transform;
                StartCoroutine(Destroy(tempParent));
            }
            else{
                houseI.transform.localRotation *= Quaternion.Euler(0,180-cluster[i].angle,0);
            }

            //Bridge placement
            bridgeI.transform.localPosition += new Vector3(0,0,cluster[i-1].length);
            if(cluster[i].angle > 180){
                //mirror house

                GameObject tempParent = new GameObject();
                tempParent.transform.parent = houses[i-1].transform;
                tempParent.transform.localPosition = new Vector3(width, 0, cluster[i-1].length);
                tempParent.transform.localRotation = Quaternion.identity;
                bridgeI.transform.parent = tempParent.transform;
                bridgeI.transform.localPosition = new Vector3(-width, 0,0);
                bridgeI.transform.localRotation = Quaternion.identity;
                tempParent.transform.localPosition += new Vector3(-width,0,0);
                tempParent.transform.localRotation = Quaternion.Euler(0,360-cluster[i].angle,0);
                //rotation happened, returning to the original state
                bridgeI.transform.parent = houses[i-1].transform;
                StartCoroutine(Destroy(tempParent));

            }
            houseBridge newB = bridgeI.GetComponent<houseBridge>();
            AssaignBridge(newB, i);
            if(cluster[i].angle == 180f){
                //no bridge needed
                StartCoroutine(Destroy(bridgeI));
            }

            house newH = houseI.GetComponent<house>();
            AssaignBuilding(newH, i);
            houseI.transform.parent = transform;

            houses.Add(houseI);
            if(bridgeI != null){
                bridges.Add(bridgeI);
            }

        }
        //handle isCircle event
        if(isCircle){
            GameObject c;
            if(cluster[0].bridge == null){
                c = bridge;
            }
            else{
                c = cluster[0].bridge;
            }
            GameObject circleBridge = Instantiate(c, houses[houses.Count-1].transform.position, houses[houses.Count-1].transform.rotation, houses[houses.Count-1].transform);
            circleBridge.transform.localPosition += new Vector3(0,0,cluster[cluster.Length-1].length);
            if(closeAngle > 180){
                //mirror house
                GameObject tempParent = new GameObject();
                tempParent.transform.parent = houses[houses.Count-1].transform;
                tempParent.transform.localPosition = new Vector3(width, 0, cluster[cluster.Length-1].length);
                tempParent.transform.localRotation = Quaternion.identity;
                circleBridge.transform.parent = tempParent.transform;
                circleBridge.transform.localPosition = new Vector3(-width, 0,0);
                circleBridge.transform.localRotation = Quaternion.identity;
                tempParent.transform.localPosition += new Vector3(-width,0,0);
                tempParent.transform.localRotation = Quaternion.Euler(0,360-closeAngle,0);
                //rotation happened, returning to the original state
                circleBridge.transform.parent = houses[houses.Count-1].transform;
                StartCoroutine(Destroy(tempParent));

            }
            houseBridge newB = circleBridge.GetComponent<houseBridge>();
            AssaignBridge(newB, cluster.Length);
            if(closeAngle == 180f){
                //no bridge needed
                StartCoroutine(Destroy(circleBridge));
            }
            if(circleBridge != null){
                bridges.Add(circleBridge);
            }

        }

    }


    void AssaignBuilding(house parameters, int i){
        if((i == 0 || i==cluster.Length-1) && isCircle==false){
            if(i==0){
                parameters.hasFront = true;
                if(cluster.Length > 1){
                    parameters.hasBack = false;
                }
            }
            else{
                parameters.hasBack = true;
                if(cluster.Length > 1){
                    parameters.hasFront = false;
                }
            }
        }
        else{
            parameters.hasFront = false;
            parameters.hasBack = false;
        }

        if((i == 0 || i==cluster.Length-1) && !isCircle){
            if(i==0){
                parameters.madeAngle = 180f;
                if(cluster.Length > 1){
                    parameters.prepareAngle = cluster[1].angle;
                }
            }
            else{
                parameters.prepareAngle = 180f;
                if(cluster.Length > 1){
                    parameters.madeAngle = cluster[i].angle;
                }
            }
        }
        else if(isCircle && (i == cluster.Length-1 || i==0)){
            if(i==0){
                parameters.madeAngle = closeAngle;
                parameters.prepareAngle = cluster[1].angle;
            }
            else{//this is the last one
                parameters.prepareAngle = closeAngle;
                parameters.madeAngle = cluster[i].angle;
            }
        }
        else if(i==cluster.Length && isCircle){//should never happen, 'cause AssaignBuilding never gets called with i == cluster.Length...
            parameters.madeAngle = closeAngle;
            parameters.prepareAngle = closeAngle;
        }
        else{
            parameters.madeAngle = cluster[i].angle;
            parameters.prepareAngle = cluster[i+1].angle;
            
        }
        parameters.length = cluster[i].length;

        parameters.width = width;

        parameters.floorWidth = floorWidth;

        parameters.wallWidth = wallWidth;
        parameters.floors = floors;
        parameters.floorHeight = floorHeight;
        parameters.baseHeight = baseHeight;
        parameters.baseOutHeight = baseOutHeight;
        parameters.baseWidth = baseWidth;

        parameters.roofHeight = roofHeight;
        parameters.roofEndHeight = roofEndHeight;
        parameters.roofEndLength = roofEndLength;
        parameters.roofOverlay = roofOverlay;
        parameters.roofWidth = roofWidth;
        if(deleteInnerAdditionals){
            for(int k = 0; k<parameters.additionalObjects.Length; k++){
                if(i == 0){
                    parameters.additionalObjects[k].back = false;
                }
                else if(i != 0 && i != cluster.Length-1){
                    parameters.additionalObjects[k].front = false;
                    parameters.additionalObjects[k].back = false;
                }
                else{
                    parameters.additionalObjects[k].front = false;
                }
            }
        }
    }

    void AssaignBridge(houseBridge par, int i){
        float angle;
        if(isCircle && i == cluster.Length){
            angle = closeAngle;
        }
        else{
            angle = cluster[i].angle;
        }
        
        if(angle>180){
            par.angle = 360-angle;
        }
        else{
            par.angle = angle;
        }

        par.pointy = cluster[i-1].pointyBridge;

        par.width = width;
        par.floorWidth = floorWidth;

        par.wallWidth = wallWidth;
        par.floors = floors;
        par.floorHeight = floorHeight;
        par.baseHeight = baseHeight;
        par.baseOutHeight = baseOutHeight;
        par.baseWidth = baseWidth;

        par.roofHeight = roofHeight;
        par.roofEndHeight = roofEndHeight;
        par.roofEndLength = roofEndLength;
        par.roofOverlay = roofOverlay;
        par.roofWidth = roofWidth;
        par.additionalObjects = houses[0].GetComponent<house>().additionalObjects;

    }

}

#if UNITY_EDITOR
[CustomEditor(typeof(houserow))]
public class houserow_Editor : Editor
{
    public override void OnInspectorGUI()
    {
        houserow script = (houserow)target;

        if (GUILayout.Button("UPDATE"))
        {
            script.PlaceHouses();
        }

        DrawDefaultInspector();
        EditorUtility.SetDirty(target);

    }
}
#endif
}

