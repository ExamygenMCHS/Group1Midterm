using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SHM{

//[ExecuteInEditMode]
public class MaskScript : MonoBehaviour
{
    [SerializeField] int renderQueue = 3000;

    void Start()
    {
        Material[] mats = GetComponent<Renderer>().materials;
        for(int i = 0; i<mats.Length; i++){
            //gameObject.GetComponent<MeshRenderer>().material.renderQueue = renderQueue;
            mats[i].renderQueue = renderQueue;
        }
    }

}
}
