using UnityEngine;
using CSG;

namespace SHM{	
//[ExecuteInEditMode]
public class HoleCutter : MonoBehaviour
{
    [Tooltip("The object you want to cut")]
    public GameObject baseMeshObject;
    [Tooltip("The object/objects you cut out from the mesh of 'baseMeshObject'. You have to gather all objects, since the result is another GameObject and subtracting should be called on this new one on the next time. This is handled in this script.")]
    public GameObject[] cutMesh;

    void Start()
    {
        
        for(int i = 0; i<cutMesh.Length;i++){
            //implementing CSG
            Model result = CSG.CSG.Subtract(baseMeshObject, cutMesh[i]);
            var composite = new GameObject();
            composite.AddComponent<MeshFilter>().sharedMesh = result.mesh;
            composite.AddComponent<MeshRenderer>().sharedMaterials = result.materials.ToArray();
            composite.name = baseMeshObject.name;
            composite.transform.parent = baseMeshObject.transform.parent;
            composite.AddComponent<MeshCollider>();
            Destroy(baseMeshObject);
            baseMeshObject = composite;
            
            cutMesh[i].SetActive(false);
        }
    }

}
}
