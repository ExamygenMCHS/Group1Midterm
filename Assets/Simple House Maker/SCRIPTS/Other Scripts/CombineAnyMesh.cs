using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace SHM{
public class CombineAnyMesh : MonoBehaviour
{
    public GameObject[] instances;
    public bool areChildren = false;

    void Start(){
        if(instances.Length >0){
            gameObject.GetComponent<MeshFilter>().mesh = Caller();
        }
    }


    public Mesh Caller(){
        MeshFilter[] argum = new MeshFilter[instances.Length];
        for (int i = 0; i< instances.Length; i++){
            argum[i] = instances[i].GetComponent<MeshFilter>();
        }
        return CombineMeshes(argum);
    }


    Mesh CombineMeshes(MeshFilter[] meshes) {
        // Key: shared mesh instance ID, Value: arguments to combine meshes
        var helper = new Dictionary<int, List<CombineInstance>>();

        // Build combine instances for each type of mesh
        int i = 0;
        foreach (var m in meshes) {
            List<CombineInstance> tmp;
            if (!helper.TryGetValue(m.sharedMesh.GetInstanceID(), out tmp)) {
                tmp = new List<CombineInstance>();
                helper.Add(m.sharedMesh.GetInstanceID(), tmp);
            }

            var ci = new CombineInstance();
            ci.mesh = m.sharedMesh;
            if(!areChildren){
                ci.transform = m.transform.localToWorldMatrix;
            }
            else{
                Matrix4x4 matrix = instances[i].transform.localToWorldMatrix;
                matrix[0,3] = instances[i].transform.localPosition.x;
                matrix[1,3] = instances[i].transform.localPosition.y;
                matrix[2,3] = instances[i].transform.localPosition.z;
                ci.transform = matrix;
            }
            tmp.Add(ci);
            i++;
        }

        // Combine meshes and build combine instance for combined meshes
        var list = new List<CombineInstance>();
        foreach (var e in helper) {
            var m = new Mesh();
            m.CombineMeshes(e.Value.ToArray());
            var ci = new CombineInstance();
            ci.mesh = m;
            list.Add(ci);
        }

        // And now combine everything
        var result = new Mesh();
        result.CombineMeshes(list.ToArray(), false, false);

        // It is a good idea to clean unused meshes now
        foreach (var m in list) {
            Destroy(m.mesh);
        }

        return result;

    }
}
}
