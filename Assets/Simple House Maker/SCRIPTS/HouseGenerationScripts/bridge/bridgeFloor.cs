using System.Collections.Generic;
using UnityEngine;

namespace SHM{
[ExecuteInEditMode]
[RequireComponent(typeof(MeshFilter))]
public class bridgeFloor : MonoBehaviour
{
    //This script generates the bridge's floors of each floor (planes)
    houseBridge data;

    Mesh mesh;
    Vector3[] vertices;
    List<Vector3> verts = new List<Vector3>();
    int[][] triangles = new int[2][];
    List<int> tris1 = new List<int>();
    List<int> tris2 = new List<int>();
    Vector2[] UV;
    List<Vector2> uvs = new List<Vector2>();
    
    void Start()
    {
        mesh = new Mesh();
        GetComponent<MeshFilter>().mesh = mesh;
    }

    public void Draw(){
        data = transform.parent.gameObject.GetComponent<houseBridge>();
        //mesh.subMeshCount = 2;
        float c = data.width * Mathf.Cos(data.angle*Mathf.Deg2Rad);
        float b = (data.width + c)*Mathf.Tan(data.angle/2*Mathf.Deg2Rad);
        tris1.Clear();
        tris2.Clear();
        verts.Clear();
        uvs.Clear();

        if(data.angle < 180){
            for(int i = 0; i <= data.floors; i++){
                verts.Add(new Vector3(0, data.baseHeight+data.floorHeight*i+i*data.floorWidth, 0));//0
                verts.Add(new Vector3(data.width, data.baseHeight+data.floorHeight*i+i*data.floorWidth, 0));//1
                verts.Add(new Vector3(data.width-data.width*Mathf.Cos((180-data.angle)*Mathf.Deg2Rad), 
                data.baseHeight+data.floorHeight*i+i*data.floorWidth, data.width*Mathf.Sin((180-data.angle)*Mathf.Deg2Rad)));//2

                
                if(data.pointy){
                    verts.Add(new Vector3(0, data.baseHeight+data.floorHeight*i+i*data.floorWidth, (data.width-data.wallWidth)*Mathf.Tan((90-data.angle/2)*Mathf.Deg2Rad)));//3
                }
                else{
                    verts.Add(new Vector3((data.width-data.width*Mathf.Cos((180-data.angle)*Mathf.Deg2Rad))/2,
                     data.baseHeight+data.floorHeight*i+i*data.floorWidth, (data.width*Mathf.Sin((180-data.angle)*Mathf.Deg2Rad))/2));//3
                }
                tris1.Add(i*4); tris1.Add(i*4+3); tris1.Add(i*4+1);
                tris2.Add(i*4+1);  tris2.Add(i*4+3); tris2.Add(i*4+2);


                uvs.Add(new Vector2(verts[0].z*data.floorsTS, verts[0].x*data.floorsTS));
                uvs.Add(new Vector2(verts[1].z*data.floorsTS, verts[1].x*data.floorsTS));
                uvs.Add(new Vector2(verts[2].z*data.floorsTS, verts[2].x*data.floorsTS));
                uvs.Add(new Vector2(verts[3].z*data.floorsTS, verts[3].x*data.floorsTS));
            }
        }
        
        vertices = verts.ToArray();
        triangles[0] = tris1.ToArray();
        triangles[1] = tris2.ToArray();
        UV = uvs.ToArray();
        
        if(mesh != null){
            data.UpdateMesh(mesh, vertices, triangles, UV);
        }
    }
}
}
