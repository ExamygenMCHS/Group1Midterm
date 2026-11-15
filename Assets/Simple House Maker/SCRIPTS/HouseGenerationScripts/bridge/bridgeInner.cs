using System.Collections.Generic;
using UnityEngine;

namespace SHM{
[ExecuteInEditMode]
[RequireComponent(typeof(MeshFilter))]
public class bridgeInner : MonoBehaviour
{
    //This script generates the bridge's inside walls
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
        if(data.angle<180){
            //down
            verts.Add(new Vector3(data.width-data.wallWidth, 0, 0));//0
            verts.Add(new Vector3(data.wallWidth, 0, 0));//1
            verts.Add(new Vector3(data.width-data.wallWidth, 0, data.wallWidth*Mathf.Tan((90-data.angle/2)*Mathf.Deg2Rad)));//2
            //down
            verts.Add(new Vector3(data.width-data.wallWidth*Mathf.Cos((180-data.angle)*Mathf.Deg2Rad), 0, data.wallWidth*Mathf.Sin((180-data.angle)*Mathf.Deg2Rad)));//3
            verts.Add(new Vector3(data.width-(data.width-data.wallWidth)*Mathf.Cos((180-data.angle)*Mathf.Deg2Rad), 0, (data.width-data.wallWidth)*Mathf.Sin((180-data.angle)*Mathf.Deg2Rad)));//4
            verts.Add(new Vector3((verts[1].x+verts[4].x)/2, 0, (verts[1].z+verts[4].z)/2));//5
            //top
            verts.Add(new Vector3(verts[0].x, data.baseHeight+data.floorHeight*data.floors+data.floors*data.floorWidth, 0));//6
            verts.Add(new Vector3(verts[1].x, data.baseHeight+data.floorHeight*data.floors+data.floors*data.floorWidth, 0));//7
            verts.Add(new Vector3(verts[2].x, data.baseHeight+data.floorHeight*data.floors+data.floors*data.floorWidth, verts[2].z));//8
            //top
            verts.Add(new Vector3(verts[3].x, data.baseHeight+data.floorHeight*data.floors+data.floors*data.floorWidth, verts[3].z));//9
            verts.Add(new Vector3(verts[4].x, data.baseHeight+data.floorHeight*data.floors+data.floors*data.floorWidth, verts[4].z));//10
            verts.Add(new Vector3(verts[5].x, data.baseHeight+data.floorHeight*data.floors+data.floors*data.floorWidth, verts[5].z));//11

            if(data.pointy){
                verts[5] = new Vector3(data.wallWidth, verts[5].y, (data.width-data.wallWidth)*Mathf.Tan((90-data.angle/2)*Mathf.Deg2Rad));
                verts[11] = new Vector3(data.wallWidth, verts[11].y, (data.width-data.wallWidth)*Mathf.Tan((90-data.angle/2)*Mathf.Deg2Rad));
            }
            triangles[0] = new int[]{
                0,2,8,
                0,8,6,
                1,7,5,
                7,11,5
            };
            triangles[1] = new int[]{
                2,3,9,
                2,9,8,
                4,5,11,
                4,11,10
            };
        }
        for(int i = 0; i<verts.Count; i++){
            uvs.Add(new Vector2(Mathf.Sqrt(Mathf.Pow(verts[i].x,2)+Mathf.Pow(verts[i].z,2))*data.innerWallsTS, verts[i].y*data.innerWallsTS));
        }
        uvs[10] = uvs[7];
        uvs[4] = uvs[1];
        uvs[3] = uvs[0];
        uvs[9] = uvs[6];
        
        vertices = verts.ToArray();
        UV = uvs.ToArray();

        if(mesh != null){
            data.UpdateMesh(mesh, vertices, triangles, UV);
        }
    }
}
}
