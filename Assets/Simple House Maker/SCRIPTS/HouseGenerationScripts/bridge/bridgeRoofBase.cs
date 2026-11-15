using System.Collections.Generic;
using UnityEngine;

namespace SHM{
[ExecuteInEditMode]
[RequireComponent(typeof(MeshFilter))]
public class bridgeRoofBase : MonoBehaviour
{
    //This script generates the bridge's roof base (bottom element)
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
        data = transform.parent.parent.gameObject.GetComponent<houseBridge>();
        //mesh.subMeshCount = 2;
        verts.Clear();
        uvs.Clear();

        float diff = (1/(data.width/2))*data.roofOverlay*data.roofHeight;

        if(data.angle<180){
            verts.Add(new Vector3(data.width, data.baseHeight+data.floors*data.floorHeight+data.floors*data.floorWidth, 0)); //0
            verts.Add(new Vector3(-data.roofOverlay, data.baseHeight+data.floorHeight*data.floors-diff+data.floors*data.floorWidth, 0)); //1
            verts.Add(new Vector3(data.width/2, data.baseHeight+data.floorHeight*data.floors+data.roofHeight+data.floors*data.floorWidth, 0)); //2

            verts.Add(new Vector3(data.width-data.width/2*(Mathf.Cos((180-data.angle)*Mathf.Deg2Rad)), data.baseHeight+data.floorHeight*data.floors+data.roofHeight+data.floors*data.floorWidth, data.width/2*(Mathf.Sin((180-data.angle)*Mathf.Deg2Rad)))); //3
            verts.Add(new Vector3((verts[2].x+verts[3].x)/2, data.baseHeight+data.floorHeight*data.floors+data.roofHeight+data.floors*data.floorWidth, (verts[2].z+verts[3].z)/2)); //4
            verts.Add(new Vector3(data.width-(data.width+data.roofOverlay)*(Mathf.Cos((180-data.angle)*Mathf.Deg2Rad)), data.baseHeight+data.floorHeight*data.floors-diff+data.floors*data.floorWidth, (data.width+data.roofOverlay)*(Mathf.Sin((180-data.angle)*Mathf.Deg2Rad)))); //5
            verts.Add(new Vector3((verts[1].x+verts[5].x)/2, data.baseHeight+data.floorHeight*data.floors-diff+data.floors*data.floorWidth, (verts[1].z+verts[5].z)/2)); //6

            if(data.pointy){
                verts[4] = new Vector3(data.width/2, verts[4].y, data.width/2*Mathf.Tan((90-data.angle/2)*Mathf.Deg2Rad));
                verts[6] = new Vector3(-data.roofOverlay, verts[6].y, (data.width+data.roofOverlay)*Mathf.Tan((90-data.angle/2)*Mathf.Deg2Rad));
            }
            triangles[0] = new int[]{
                1,2,6,
                6,2,4,
                0,4,2
            };
            triangles[1] = new int[]{
                0,3,4,
                4,5,6,
                4,3,5
            };

        }

        if(data.pointy){
            for(int i = 0; i<verts.Count; i++){
                uvs.Add(new Vector2(Mathf.Sqrt(Mathf.Pow(verts[i].x,2)+Mathf.Pow(verts[i].y,2))*data.roofTS, verts[i].z*data.roofTS));
            }
            uvs[5] = uvs[1];
            uvs[3] = uvs[2];
            uvs[0] = new Vector2(uvs[1].x, uvs[0].y);
        }
        else{
            for(int i = 0; i<verts.Count; i++){
                uvs.Add(new Vector2(0,0));
            }
            uvs[1] = new Vector2(Mathf.Sqrt(Mathf.Pow(verts[6].x-verts[1].x, 2)+Mathf.Pow(verts[6].z-verts[1].z, 2))*data.roofTS, 0);
            uvs[4] = new Vector2(0, Mathf.Sqrt(Mathf.Pow(verts[4].x-verts[6].x, 2)+Mathf.Pow(verts[4].y-verts[6].y, 2)+Mathf.Pow(verts[4].z-verts[6].z, 2))*data.roofTS);
            uvs[2] = new Vector2(Mathf.Sqrt(Mathf.Pow(verts[2].x-verts[4].x, 2)+Mathf.Pow(verts[2].z-verts[4].z, 2))*data.roofTS, uvs[4].y);
            uvs[0] = new Vector2(0, uvs[4].y-Mathf.Sqrt(Mathf.Pow(verts[4].x-verts[0].x, 2)+Mathf.Pow(verts[4].y-verts[0].y, 2)+Mathf.Pow(verts[4].z-verts[0].z, 2))*data.roofTS);

            uvs[5] = uvs[1];
            uvs[3] = uvs[2];
        }
        

        vertices = verts.ToArray();
        UV = uvs.ToArray();

        if(mesh != null){
            data.UpdateMesh(mesh, vertices, triangles, UV);
        }
    }
}
}
