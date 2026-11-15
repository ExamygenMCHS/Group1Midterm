using System.Collections.Generic;
using UnityEngine;
namespace SHM{
[ExecuteInEditMode]
[RequireComponent(typeof(MeshFilter))]
public class innerWalls : MonoBehaviour
{    
    //This script will generate the mesh of the inside walls
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
        vertices = new Vector3[]{

            //base inside
            new Vector3 (data.wallWidth, data.baseHeight, data.wallWidth), //0
            new Vector3 (data.wallWidth, data.baseHeight, data.length-data.wallWidth), //1
            new Vector3 (data.width-data.wallWidth, data.baseHeight, data.wallWidth), //2
            new Vector3 (data.width-data.wallWidth, data.baseHeight, data.length-data.wallWidth), //3

            //up inside
            new Vector3 (data.wallWidth, data.floors*data.floorHeight+data.baseHeight+data.floors*data.floorWidth, data.wallWidth), //4
            new Vector3 (data.wallWidth, data.floors*data.floorHeight+data.baseHeight+data.floors*data.floorWidth, data.length-data.wallWidth), //5
            new Vector3 (data.width-data.wallWidth, data.floors*data.floorHeight+data.baseHeight+data.floors*data.floorWidth, data.wallWidth), //6
            new Vector3 (data.width-data.wallWidth, data.floors*data.floorHeight+data.baseHeight+data.floors*data.floorWidth, data.length-data.wallWidth) //7
            };

            tris.Add(0);tris.Add(5);tris.Add(1);
            tris.Add(0);tris.Add(4);tris.Add(5);

            if(!(data.hasBack == false && data.closedBack == false)){
                tris.Add(1);tris.Add(7);tris.Add(3);
                tris.Add(1);tris.Add(5);tris.Add(7);   
            }

            tris.Add(3);tris.Add(6);tris.Add(2);
            tris.Add(3);tris.Add(7);tris.Add(6);

            if(!(data.hasFront == false && data.closedFront == false)){
                tris.Add(2);tris.Add(4);tris.Add(0);
                tris.Add(2);tris.Add(6);tris.Add(4);
            }
            
            verts.AddRange(vertices);

            if(data.roofEndHeight == 0f && data.roofHeight != 0f){

                //inside
                verts.Add(new Vector3(data.width/2, data.floors*data.floorHeight+data.roofHeight+data.baseHeight+data.floors*data.floorWidth, data.wallWidth)); //verts.Count-2
                verts.Add(new Vector3(data.width/2, data.floors*data.floorHeight+data.roofHeight+data.baseHeight+data.floors*data.floorWidth, data.length-data.wallWidth)); //verts.Count-1
                //TRIANGLES
                if(!(data.hasFront == false && data.closedFront == false)){
                    tris.Add(6);tris.Add(verts.Count-2);tris.Add(4);
                }

                if(!(data.hasBack == false && data.closedBack == false)){
                    tris.Add(5);tris.Add(verts.Count-1);tris.Add(7);
                    
                }


            }
            else{
                if(data.roofHeight != 0f) {
                    float diff = (data.width/2)*(data.roofEndHeight/data.roofHeight);


                    //inside
                    
                    verts.Add(new Vector3(data.width/2-diff, data.floors*data.floorHeight+data.roofHeight-data.roofEndHeight+data.baseHeight+data.floors*data.floorWidth, data.wallWidth)); //verts.Count-4
                    verts.Add(new Vector3(data.width/2+diff, data.floors*data.floorHeight+data.roofHeight-data.roofEndHeight+data.baseHeight+data.floors*data.floorWidth, data.wallWidth)); //verts.Count-3
                    
                    verts.Add(new Vector3(data.width/2-diff, data.floors*data.floorHeight+data.roofHeight-data.roofEndHeight+data.baseHeight+data.floors*data.floorWidth, data.length-data.wallWidth)); //verts.Count-2
                    verts.Add(new Vector3(data.width/2+diff, data.floors*data.floorHeight+data.roofHeight-data.roofEndHeight+data.baseHeight+data.floors*data.floorWidth, data.length-data.wallWidth)); //verts.Count-1

                    //inside
                    if(!(data.hasFront == false && data.closedFront == false)){
                        tris.Add(6);tris.Add(verts.Count-4);tris.Add(4);
                        tris.Add(verts.Count-3);tris.Add(verts.Count-4);tris.Add(6);
                        
                    }

                    
                    if(!(data.hasBack == false && data.closedBack == false)){
                        tris.Add(5);tris.Add(verts.Count-1);tris.Add(7);
                        tris.Add(verts.Count-2);tris.Add(verts.Count-1);tris.Add(5);
                        
                    }

                    
                }

            }
            if(!data.hasFront && !data.closedFront){
                for(int i = 0; i<verts.Count; i++){
                    if(verts[i].z == data.wallWidth){
                        verts[i] = new Vector3(verts[i].x, verts[i].y, 0);
                    }
                }
                
            }
            if(!data.hasBack && !data.closedBack){
                for(int i = 0; i<verts.Count; i++){
                    if(verts[i].z > data.wallWidth){
                        verts[i] = new Vector3(verts[i].x, verts[i].y, data.length);
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

            uvs.Add(new Vector2((vertices[i].x+vertices[i].z)*data.innerWallsTS, vertices[i].y*data.innerWallsTS));

        }

        UV = uvs.ToArray();
    }

}
}
