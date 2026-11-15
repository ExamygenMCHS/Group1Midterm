using System.Collections.Generic;
using UnityEngine;

namespace SHM{
[ExecuteInEditMode]
[RequireComponent(typeof(MeshFilter))]
public class bridgeBase : MonoBehaviour
{
    //This script generates the bridge's base
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
        float plusZ = Mathf.Sin(data.angle/2*Mathf.Deg2Rad)*data.baseWidth;
        float plusX = Mathf.Cos(data.angle/2*Mathf.Deg2Rad)*data.baseWidth;

        if(data.angle < 180){
            verts.Add(new Vector3(-data.baseWidth, 0, 0));//0
            verts.Add(new Vector3(-data.baseWidth, data.baseOutHeight, 0));//1
            verts.Add(new Vector3(data.width+(data.width+data.baseWidth)/data.width*c, 0, (data.width+data.baseWidth)/data.width*b));//2
            verts.Add(new Vector3(data.width+(data.width+data.baseWidth)/data.width*c, data.baseOutHeight, (data.width+data.baseWidth)/data.width*b));//3

            verts.Add(new Vector3(-plusX, 0, plusZ));//4
            verts.Add(new Vector3(-plusX, data.baseOutHeight, plusZ));//5
            verts.Add(new Vector3(data.width+c-plusX, 0, b+plusZ));//6
            verts.Add(new Vector3(data.width+c-plusX, data.baseOutHeight, b+plusZ));//7
            verts.Add(new Vector3((verts[6].x + verts[4].x)/2, 0, (verts[6].z + verts[4].z)/2));//8
            verts.Add(new Vector3((verts[6].x + verts[4].x)/2, data.baseOutHeight, (verts[6].z + verts[4].z)/2));//9

            verts.Add(new Vector3(0, data.baseHeight, 0));//10
            //verts.Add(new Vector3(0, data.baseHeight, 0));//10
            verts.Add(new Vector3((data.width+c)/2, data.baseHeight, b/2));//11
            //verts.Add(new Vector3((data.width+c)/2, data.baseHeight, b/2));//11
            verts.Add(new Vector3(data.width+c, data.baseHeight, b));//12
            //verts.Add(new Vector3(data.width+c, data.baseHeight, b));//12
            

            if(data.pointy){
                verts[8] = new Vector3(-data.baseWidth, 0, (data.width+data.baseWidth)*Mathf.Tan((90-data.angle/2)*Mathf.Deg2Rad));
                verts[9] = new Vector3(verts[8].x, data.baseOutHeight, verts[8].z);
                verts[11] = new Vector3(0, data.baseHeight, data.width*Mathf.Tan((90-data.angle/2)*Mathf.Deg2Rad));
                //verts[13] = new Vector3(0, data.baseHeight, data.width*Mathf.Tan((90-data.angle/2)*Mathf.Deg2Rad));

                verts[4] = verts[0];
                verts[5] = verts[1];
                verts[6] = verts[2];
                verts[7] = verts[3];

            }
        }

        if(data.angle < 180){
            triangles[0]= new int[21]{
                0, 4, 1,
                5, 1, 4,
                5, 10, 1,
                5, 9, 10,
                9, 11, 10,
                9, 5, 4,
                8, 9, 4

            };
            triangles[1] = new int[21] {
                3, 12, 7,
                2, 3, 7,
                2, 7, 6,
                7, 12, 11,
                9, 7, 11,
                6, 7, 9,
                6, 9, 8

            };
        }
        else{
            triangles[0]= new int[21]{
                0, 1, 4,
                5, 4, 1,
                5, 1, 10,
                5, 10, 9,
                9, 10, 11,
                9, 4, 5,
                8, 10, 9

            };
            triangles[1] = new int[21] {
                3, 7, 12,
                2, 7, 3,
                2, 6, 7,
                7, 11, 12,
                9, 11, 7,
                6, 9, 7,
                6, 8, 9

            };
        }

        for(int i = 0; i<verts.Count; i++){
            uvs.Add(new Vector2(Mathf.Sqrt(Mathf.Pow(verts[i].x, 2) + Mathf.Pow(verts[i].z, 2))*data.baseTS, verts[i].y*data.baseTS));
        }
        uvs[0] = new Vector2(uvs[4].x+Mathf.Sqrt(Mathf.Pow(plusX, 2) + Mathf.Pow(plusZ, 2))*data.baseTS, uvs[4].y);
        uvs[1] = new Vector2(uvs[5].x+Mathf.Sqrt(Mathf.Pow(plusX, 2) + Mathf.Pow(plusZ, 2))*data.baseTS, uvs[5].y);
        uvs[2] = uvs[0];
        uvs[3] = uvs[1];
        uvs[6] = uvs[4];
        uvs[7] = uvs[5];
        uvs[10] = new Vector2(uvs[5].x, uvs[5].y+data.baseWidth*data.baseTS);
        uvs[11] = new Vector2(uvs[9].x, uvs[9].y+data.baseWidth*data.baseTS);
        uvs[12] = uvs[10];

        vertices = verts.ToArray();
        UV = uvs.ToArray();
        if(mesh != null){
            data.UpdateMesh(mesh, vertices, triangles, UV);
        }
    }
}
}
