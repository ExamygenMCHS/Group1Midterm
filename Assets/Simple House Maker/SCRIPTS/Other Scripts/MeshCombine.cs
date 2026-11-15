using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace SHM{
public class MeshCombine : MonoBehaviour
{
    public List<GameObject> sourceMeshFilters;
    public bool destroyOldOnes = true;
    public bool localize = false;

    //[ContextMenu("Combine Meshes")]
    void Awake(){
        CombineMeshes();
    }
    public void CombineMeshes()
    {
        var combine = new CombineInstance[sourceMeshFilters.Count];

        for (var i = 0; i < sourceMeshFilters.Count; i++)
        {
            if(!localize){
                combine[i].mesh = sourceMeshFilters[i].GetComponent<MeshFilter>().sharedMesh;
                Matrix4x4 matrix = sourceMeshFilters[i].GetComponent<MeshFilter>().transform.localToWorldMatrix;
                matrix[0,3] = sourceMeshFilters[i].transform.localPosition.x;
                matrix[1,3] = sourceMeshFilters[i].transform.localPosition.y;
                matrix[2,3] = sourceMeshFilters[i].transform.localPosition.z;
                combine[i].transform = matrix;
            }
            else{
                combine[i].mesh = sourceMeshFilters[i].GetComponent<MeshFilter>().sharedMesh;
                combine[i].transform = sourceMeshFilters[i].GetComponent<MeshFilter>().transform.localToWorldMatrix;
            }
            if(destroyOldOnes){
                Destroy(sourceMeshFilters[i]);
            }
            else{
                sourceMeshFilters[i].SetActive(false);
            }
        }

        transform.GetComponent<MeshFilter>().mesh = new Mesh();
        transform.GetComponent<MeshFilter>().mesh.CombineMeshes(combine);
    }
}
}
