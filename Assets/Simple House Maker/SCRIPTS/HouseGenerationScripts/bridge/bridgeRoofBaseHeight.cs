using System.Collections.Generic;
using UnityEngine;

namespace SHM{
[ExecuteInEditMode]
[RequireComponent(typeof(MeshFilter))]
public class bridgeRoofBaseHeight : MonoBehaviour
{
    //This script generates the bridge's roof base side (the side elements, not top and bottom)
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
            verts.Add(new Vector3(-data.roofOverlay, data.baseHeight+data.floorHeight*data.floors-diff+data.floors*data.floorWidth, 0)); //1(0)
            
            verts.Add(new Vector3(data.width-(data.width+data.roofOverlay)*(Mathf.Cos((180-data.angle)*Mathf.Deg2Rad)), data.baseHeight+data.floorHeight*data.floors-diff+data.floors*data.floorWidth, (data.width+data.roofOverlay)*(Mathf.Sin((180-data.angle)*Mathf.Deg2Rad)))); //5(1)
            verts.Add(new Vector3((verts[0].x+verts[1].x)/2, data.baseHeight+data.floorHeight*data.floors-diff+data.floors*data.floorWidth, (verts[0].z+verts[1].z)/2)); //6(2)

            verts.Add(new Vector3(-data.roofOverlay, data.baseHeight+data.floorHeight*data.floors-diff+data.floors*data.floorWidth+data.roofWidth, 0)); //1(3)
            
            verts.Add(new Vector3(data.width-(data.width+data.roofOverlay)*(Mathf.Cos((180-data.angle)*Mathf.Deg2Rad)), data.baseHeight+data.floorHeight*data.floors-diff+data.floors*data.floorWidth+data.roofWidth, (data.width+data.roofOverlay)*(Mathf.Sin((180-data.angle)*Mathf.Deg2Rad)))); //5(4)
            verts.Add(new Vector3((verts[0].x+verts[1].x)/2, data.baseHeight+data.floorHeight*data.floors-diff+data.floors*data.floorWidth+data.roofWidth, (verts[0].z+verts[1].z)/2)); //6(5)



            if(data.pointy){
                
                verts[2] = new Vector3(-data.roofOverlay, verts[2].y, (data.width+data.roofOverlay)*Mathf.Tan((90-data.angle/2)*Mathf.Deg2Rad));
                
                verts[5] = new Vector3(-data.roofOverlay, verts[5].y, (data.width+data.roofOverlay)*Mathf.Tan((90-data.angle/2)*Mathf.Deg2Rad));
            }
            triangles[0] = new int[]{
                2,3,0,
                2,5,3
            };
            triangles[1] = new int[]{
                1,5,2,
                1,4,5
            };

        }

        uvs.Add(new Vector2(0,0));
        uvs.Add(uvs[0]);
        uvs.Add(new Vector2(Mathf.Sqrt(Mathf.Pow(verts[2].x-verts[0].x, 2) + Mathf.Pow(verts[2].z-verts[0].z, 2))*data.roofBaseHeightTS,0));
        uvs.Add(new Vector2(0,data.roofWidth*data.roofBaseHeightTS));
        uvs.Add(uvs[3]);
        uvs.Add(new Vector2(uvs[2].x,uvs[3].y));
        

        vertices = verts.ToArray();
        UV = uvs.ToArray();

        if(mesh != null){
            data.UpdateMesh(mesh, vertices, triangles, UV);
        }
    }
}
}