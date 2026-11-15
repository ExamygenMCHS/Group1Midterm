using System.Collections.Generic;
using UnityEngine;

namespace SHM{
[ExecuteInEditMode]
[RequireComponent(typeof(MeshFilter))]
public class outerWalls : MonoBehaviour
{    
    //This script will generate the mesh of the outside walls
    house data;

    Mesh mesh;
    Vector3[] vertices;
    List<Vector3> verts = new List<Vector3>();
    int[] triangles;
    List<int> tris = new List<int>();
    Vector2[] UV;
    List<Vector2> uvs = new List<Vector2>();
    
    void Start(){
        mesh = new Mesh();
        GetComponent<MeshFilter>().mesh = mesh;
    }


    public void Draw(){

        data = transform.parent.gameObject.GetComponent<house>();

        verts.Clear();
        tris.Clear();
        vertices = new Vector3[]{
            //base
            new Vector3 (0, data.baseHeight, 0), //0
            new Vector3 (0, data.baseHeight, data.length), //1
            new Vector3 (data.width, data.baseHeight, 0), //2
            new Vector3 (data.width, data.baseHeight, data.length), //3

            //up
            new Vector3 (0, data.floors*data.floorHeight+data.baseHeight+data.floors*data.floorWidth, 0), //4
            new Vector3 (0, data.floors*data.floorHeight+data.baseHeight+data.floors*data.floorWidth, data.length), //5
            new Vector3 (data.width, data.floors*data.floorHeight+data.baseHeight+data.floors*data.floorWidth, 0), //6
            new Vector3 (data.width, data.floors*data.floorHeight+data.baseHeight+data.floors*data.floorWidth, data.length), //7

            };

            tris.Add(0);tris.Add(1);tris.Add(5);
            tris.Add(0);tris.Add(5);tris.Add(4);

            if(!(data.hasBack == false && data.closedBack == false)){
                tris.Add(1);tris.Add(3);tris.Add(7);
                tris.Add(1);tris.Add(7);tris.Add(5);
            }

            tris.Add(3);tris.Add(2);tris.Add(6);
            tris.Add(3);tris.Add(6);tris.Add(7);

            if(!(data.hasFront == false && data.closedFront == false)){
                tris.Add(2);tris.Add(0);tris.Add(4);
                tris.Add(2);tris.Add(4);tris.Add(6);
            }
            
            verts.AddRange(vertices);

            if(data.roofEndHeight == 0f && data.roofHeight != 0f){
                //outside
                verts.Add(new Vector3(data.width/2, data.floors*data.floorHeight+data.roofHeight+data.baseHeight+data.floors*data.floorWidth, 0)); //verts.Count-4
                verts.Add(new Vector3(data.width/2, data.floors*data.floorHeight+data.roofHeight+data.baseHeight+data.floors*data.floorWidth, data.length)); //verts.Count-1

                //TRIANGLES
                if(!(data.hasFront == false && data.closedFront == false)){
                    tris.Add(4);tris.Add(verts.Count-2);tris.Add(6);
                }
                if(!(data.hasBack == false && data.closedBack == false)){
                    tris.Add(7);tris.Add(verts.Count-1);tris.Add(5);
                }
                
            }
            else{
                if(data.roofHeight != 0f) {
                    float diff = (data.width/2)*(data.roofEndHeight/data.roofHeight);

                
                    //outside
                    verts.Add(new Vector3(data.width/2-diff, data.floors*data.floorHeight+data.roofHeight-data.roofEndHeight+data.baseHeight+data.floors*data.floorWidth, 0)); //verts.Count-8
                    verts.Add(new Vector3(data.width/2+diff, data.floors*data.floorHeight+data.roofHeight-data.roofEndHeight+data.baseHeight+data.floors*data.floorWidth, 0)); //verts.Count-7

                    verts.Add(new Vector3(data.width/2-diff, data.floors*data.floorHeight+data.roofHeight-data.roofEndHeight+data.baseHeight+data.floors*data.floorWidth, data.length)); //verts.Count-6
                    verts.Add(new Vector3(data.width/2+diff, data.floors*data.floorHeight+data.roofHeight-data.roofEndHeight+data.baseHeight+data.floors*data.floorWidth, data.length)); //verts.Count-5

                    //TRIANGLES
                    if(!(data.hasFront == false && data.closedFront == false)){
                        tris.Add(4);tris.Add(verts.Count-4);tris.Add(6);
                        tris.Add(verts.Count-4);tris.Add(verts.Count-3);tris.Add(6);
                    }
                    if(!(data.hasBack == false && data.closedBack == false)){
                        tris.Add(7);tris.Add(verts.Count-1);tris.Add(5);
                        tris.Add(verts.Count-1);tris.Add(verts.Count-2);tris.Add(5);
                    }


                }

            }
            vertices = verts.ToArray();
            triangles = tris.ToArray();
            Unwrap();
            
        if(mesh != null){
            data.UpdateMesh(mesh, vertices, triangles, UV);
        }
    }


    void Unwrap(){
        uvs.Clear();
        for(int i = 0; i<vertices.Length; i++){

            uvs.Add(new Vector2((vertices[i].x+vertices[i].z)*data.outerWallsTS, vertices[i].y*data.outerWallsTS));

        }

        UV = uvs.ToArray();
    }
}
}
