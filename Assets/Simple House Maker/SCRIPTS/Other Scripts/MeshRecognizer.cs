using UnityEngine;
using System.Collections;
namespace SHM{
public class MeshRecognizer : MonoBehaviour
{
    //when a new mesh is generated, the collider usually won't update. This problem can be solved by reassigning the mesh
    // in the Meshcollider component.
    //This (at the moment) will be done at the start, but if you want to update the mesh AND THE COLLIDER also, you have to add this to the code:
    
    // void Update(){
    //     Recognize();
    // }

    //OR THIS:

    // [Tooltip("The frequency you want to update your collider.")]
    // public float colliderUpdateFrequency = 0.1f;
    // (Remove the original Start() function)
    // void Start(){
    //     Recognize();
    //     InvokeRepeating("Recognize", colliderUpdateFrequency, colliderUpdateFrequency);
    //     //if you ever want to stop this:
    //     //CancelInvoke("Recognize");

    // }

    //BUT NOTICE THAT THIS FUNCTION IS NOT NEEDED TO UPDATE EVERY TIME (unwanted added calculation time -> lower fps (for no useful reason)), 
    //YOU SHOULD JUST CALL IT FROM OUTSIDE:
    //somewhere in an other script: gameObject.GetComponent<MeshRecognizer>().Recognize();


    void Start()
    {
        StartCoroutine(StartRecognize());
    }

    public void Recognize(){
        GetComponent<MeshCollider>().sharedMesh = GetComponent<MeshFilter>().mesh;
    }

    IEnumerator StartRecognize(){
        yield return new WaitForSeconds(0.1f);
        GetComponent<MeshCollider>().sharedMesh = GetComponent<MeshFilter>().mesh;
    }
}
}
