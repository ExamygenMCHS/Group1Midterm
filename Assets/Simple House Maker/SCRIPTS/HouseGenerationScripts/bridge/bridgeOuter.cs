using System.Collections.Generic;
using UnityEngine;

namespace SHM{
[ExecuteInEditMode]
[RequireComponent(typeof(MeshFilter))]
public class bridgeOuter : MonoBehaviour
{
    //This script generates the bridge's outside walls
    houseBridge data;

    Mesh mesh;
    Vector3[] vertices;
    List<Vector3> verts = new List<Vector3>();
    int[][] triangles = new int[2][];
    List<List<int>> tris = new List<List<int>>();
    Vector2[] UV;
    List<Vector2> uvs = new List<Vector2>();
    
    void Start()
    {
        mesh = new Mesh();
        GetComponent<MeshFilter>().mesh = mesh;
    }


    public void Draw(){
        data = transform.parent.gameObject.GetComponent<houseBridge>();
        verts.Clear();
        uvs.Clear();
        //mesh.subMeshCount = 2;
        float c = data.width * Mathf.Cos(data.angle*Mathf.Deg2Rad);
        float b = (data.width + c)*Mathf.Tan(data.angle/2*Mathf.Deg2Rad);

        if(data.angle < 180){
            verts.Add(new Vector3(0, data.baseHeight, 0));
            verts.Add(new Vector3((data.width+c)/2, data.baseHeight, b/2));
            verts.Add(new Vector3(data.width+c, data.baseHeight, b));
            verts.Add(new Vector3(0, data.baseHeight+data.floors*data.floorHeight+data.floors*data.floorWidth, 0));
            verts.Add(new Vector3((data.width+c)/2, data.baseHeight+data.floors*data.floorHeight+data.floors*data.floorWidth, b/2));
            verts.Add(new Vector3(data.width+c, data.baseHeight+data.floors*data.floorHeight+data.floors*data.floorWidth, b));

            if(data.pointy){
                verts[1] = new Vector3(0, verts[1].y, data.width*Mathf.Tan((90-data.angle/2)*Mathf.Deg2Rad));
                verts[4] = new Vector3(verts[1].x, verts[4].y, verts[1].z);
            }
        }

        
        if(data.angle < 180){
            triangles[0]= new int[6]{
                0, 4, 3,
                0, 1, 4,
            };
            triangles[1] = new int[6] {
                1, 5, 4,
                1, 2, 5
            };
        }
        else{//>=180
            triangles[0]= new int[6]{
                0, 3, 4,
                0, 4, 1,
            };
            triangles[1] = new int[6] {
                1, 4, 5,
                1, 5, 2
            };
        }
        for(int i = 0; i<verts.Count; i++){
            uvs.Add(new Vector2(Mathf.Sqrt(Mathf.Pow(verts[i].x, 2) + Mathf.Pow(verts[i].z, 2))*data.outerWallsTS, verts[i].y*data.outerWallsTS));
        }
        uvs[2] = new Vector2(2*uvs[1].x-uvs[0].x, verts[2].y*data.outerWallsTS);
        uvs[5] = new Vector2(2*uvs[1].x-uvs[0].x, verts[5].y*data.outerWallsTS);
        
        vertices = verts.ToArray();
        UV = uvs.ToArray();
        if(mesh != null){
            data.UpdateMesh(mesh, vertices, triangles, UV);
        }
    }
}
}
