using UnityEngine;
using CSG;
namespace SHM{
public class cutout : MonoBehaviour
{
    [Tooltip("Assaign the object, that will be subtracted from the house (normally a cube)")]
    public GameObject cutMesh;

    public GameObject Cut(GameObject baseMeshObject){
        Model result = CSG.CSG.Subtract(baseMeshObject, cutMesh);
        var composite = new GameObject();
        composite.AddComponent<MeshFilter>().sharedMesh = result.mesh;
        composite.AddComponent<MeshRenderer>().sharedMaterials = result.materials.ToArray();
        composite.name = baseMeshObject.name;
        composite.transform.parent = baseMeshObject.transform.parent;

        return composite;
    }

}
}
