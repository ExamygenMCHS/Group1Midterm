using System.Collections.Generic;
using UnityEngine;
namespace SHM{
[ExecuteInEditMode]
[RequireComponent(typeof(MeshFilter))]
public class innerRoofs : MonoBehaviour
{
    //This script will generate the mesh of roofs of each floor (planes)
    house data;

    Mesh mesh;
    Vector3[] vertices;
    List<Vector3> verts = new List<Vector3>();
    int[] triangles;
    List<int> tris = new List<int>();
    Vector2[] UV;
    List<Vector2> uvs = new List<Vector2>();
    
    void Start()
    {        
        mesh = new Mesh();
        GetComponent<MeshFilter>().mesh = mesh;
    }

    public void Draw(){
        data = transform.parent.gameObject.GetComponent<house>();

        verts.Clear();
        tris.Clear();
        uvs.Clear();

        for(int i = 0; i<data.floors; i++){
            verts.Add(new Vector3(0, data.baseHeight+data.floorHeight*(i+1)+i*data.floorWidth, 0)); //0
            verts.Add(new Vector3(data.width, data.baseHeight+data.floorHeight*(i+1)+i*data.floorWidth, 0)); //1
            verts.Add(new Vector3(0, data.baseHeight+data.floorHeight*(i+1)+i*data.floorWidth, data.length)); //2
            verts.Add(new Vector3(data.width, data.baseHeight+data.floorHeight*(i+1)+i*data.floorWidth, data.length)); //3

            tris.Add(0+i*4); tris.Add(1+i*4); tris.Add(2+i*4);
            tris.Add(1+i*4); tris.Add(3+i*4); tris.Add(2+i*4);
            uvs.Add(new Vector2(verts[0].z*data.innerRoofsTS, verts[0].x*data.innerRoofsTS));
            uvs.Add(new Vector2(verts[1].z*data.innerRoofsTS, verts[1].x*data.innerRoofsTS));
            uvs.Add(new Vector2(verts[2].z*data.innerRoofsTS, verts[2].x*data.innerRoofsTS));
            uvs.Add(new Vector2(verts[3].z*data.innerRoofsTS, verts[3].x*data.innerRoofsTS));
        }
        vertices = verts.ToArray();
        triangles = tris.ToArray();
        UV = uvs.ToArray();

        if(mesh != null){
            data.UpdateMesh(mesh, vertices, triangles, UV);
        }
    }
}
}
