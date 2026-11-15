using System.Collections.Generic;
using UnityEngine;

namespace SHM{
[ExecuteInEditMode]
[RequireComponent(typeof(MeshFilter))]
public class roofBase : MonoBehaviour
{
    //This script will generate the mesh of the base of the roof (the bottom part)
    house data;

    Mesh mesh;
    Vector3[] vertices;
    List<Vector3> verts = new List<Vector3>();
    int[] triangles;
    List<int> tris1 = new List<int>();
    Vector2[] UV;
    List<Vector2> uvs = new List<Vector2>();
    
    void Start()
    {
        mesh = new Mesh();
        GetComponent<MeshFilter>().mesh = mesh;
    }



    public void Draw(){
        data = transform.parent.parent.gameObject.GetComponent<house>();
        verts.Clear();
        tris1.Clear();
        uvs.Clear();
        float leftPreCorrection;
        float rightPreCorrection;
        float leftProCorrection;
        float rightProCorrection;

        if(data.madeAngle == 180f){
            leftPreCorrection = 0f;
            rightPreCorrection = 0f;
        }
        else{
            if(data.madeAngle < 180f){
                leftPreCorrection = 0f;
                rightPreCorrection = data.roofOverlay / (Mathf.Tan(0.5f*data.madeAngle*Mathf.Deg2Rad));
            }
            else{
                leftPreCorrection = data.roofOverlay / (Mathf.Tan(0.5f*(360-data.madeAngle)*Mathf.Deg2Rad));
                rightPreCorrection = 0f;
            }

        }
        if(data.prepareAngle == 180f){
            leftProCorrection = 0f;
            rightProCorrection = 0f;
        }
        else{
            if(data.prepareAngle < 180f){
                leftProCorrection = 0f;
                rightProCorrection = data.roofOverlay / (Mathf.Tan(0.5f*data.prepareAngle*Mathf.Deg2Rad));
            }
            else{
                leftProCorrection = data.roofOverlay / (Mathf.Tan(0.5f*(360-data.prepareAngle)*Mathf.Deg2Rad));
                rightProCorrection = 0f;
            }

        }

        if(data.roofHeight == 0f){
            //flat roof
            verts.Add(new Vector3(-data.roofOverlay, data.baseHeight+data.floorHeight*data.floors+data.floors*data.floorWidth, -data.roofOverlay)); //0
            verts.Add(new Vector3(-data.roofOverlay, data.baseHeight+data.floorHeight*data.floors+data.floors*data.floorWidth, data.roofOverlay+data.length)); //1
            verts.Add(new Vector3(data.roofOverlay+data.width, data.baseHeight+data.floorHeight*data.floors+data.floors*data.floorWidth, -data.roofOverlay)); //2
            verts.Add(new Vector3(data.roofOverlay+data.width, data.baseHeight+data.floorHeight*data.floors+data.floors*data.floorWidth, data.roofOverlay+data.length)); //3

            verts.Add(new Vector3(0, data.baseHeight+data.floorHeight*data.floors+data.floors*data.floorWidth, -data.roofOverlay));
            verts.Add(new Vector3(0, data.baseHeight+data.floorHeight*data.floors+data.floors*data.floorWidth, data.roofOverlay+data.length));
            verts.Add(new Vector3(data.width, data.baseHeight+data.floorHeight*data.floors+data.floors*data.floorWidth, -data.roofOverlay));
            verts.Add(new Vector3(data.width, data.baseHeight+data.floorHeight*data.floors+data.floors*data.floorWidth, data.roofOverlay+data.length));

            tris1.Add(0); tris1.Add(4); tris1.Add(1);
            tris1.Add(1); tris1.Add(4); tris1.Add(5);
            tris1.Add(4); tris1.Add(6); tris1.Add(5);
            tris1.Add(5); tris1.Add(6); tris1.Add(7);
            tris1.Add(6); tris1.Add(2); tris1.Add(7);
            tris1.Add(7); tris1.Add(2); tris1.Add(3);

            if(!data.hasFront && (rightPreCorrection == 0f && leftPreCorrection == 0f)){
                verts[0] = new Vector3(verts[0].x, verts[0].y, 0);
                verts[2] = new Vector3(verts[2].x, verts[2].y, 0);
                verts[4] = new Vector3(verts[4].x, verts[4].y, 0);
                verts[6] = new Vector3(verts[6].x, verts[6].y, 0);
            }
            else if(!data.hasFront && (rightPreCorrection != 0f || leftPreCorrection != 0f)){
                verts[0] = new Vector3(verts[0].x, verts[0].y, leftPreCorrection);
                verts[2] = new Vector3(verts[2].x, verts[2].y, rightPreCorrection);
                verts[4] = new Vector3(verts[4].x, verts[4].y, 0);
                verts[6] = new Vector3(verts[6].x, verts[6].y, 0);
            }
            if(!data.hasBack && (rightProCorrection == 0f && leftProCorrection == 0f)){
                verts[1] = new Vector3(verts[1].x, verts[1].y, data.length);
                verts[3] = new Vector3(verts[3].x, verts[3].y, data.length);
                verts[5] = new Vector3(verts[5].x, verts[5].y, data.length);
                verts[7] = new Vector3(verts[7].x, verts[7].y, data.length);
            }
            else if(!data.hasBack && (rightProCorrection != 0f || leftProCorrection != 0f)){
                verts[1] = new Vector3(verts[1].x, verts[1].y, data.length-leftProCorrection);
                verts[3] = new Vector3(verts[3].x, verts[3].y, data.length-rightProCorrection);
                verts[5] = new Vector3(verts[5].x, verts[5].y, data.length);
                verts[7] = new Vector3(verts[7].x, verts[7].y, data.length);
            }
            for(int i = 0; i<verts.Count; i++){
                uvs.Add(new Vector2(verts[i].z*data.roofTS, verts[i].x*data.roofTS));
            }
        }
        else{
            float diff = (1/(data.width/2))*data.roofOverlay*data.roofHeight;

            //non-flat roof edges
            verts.Add(new Vector3(-data.roofOverlay, data.baseHeight+data.floorHeight*data.floors-diff+data.floors*data.floorWidth, -data.roofOverlay)); //0
            verts.Add(new Vector3(-data.roofOverlay, data.baseHeight+data.floorHeight*data.floors-diff+data.floors*data.floorWidth, data.roofOverlay+data.length)); //1
            verts.Add(new Vector3(data.roofOverlay+data.width, data.baseHeight+data.floorHeight*data.floors-diff+data.floors*data.floorWidth, -data.roofOverlay)); //2
            verts.Add(new Vector3(data.roofOverlay+data.width, data.baseHeight+data.floorHeight*data.floors-diff+data.floors*data.floorWidth, data.roofOverlay+data.length)); //3

            //non-flat roof middle (top)
            if((data.roofEndHeight == 0 || data.roofEndLength == 0)){
                verts.Add(new Vector3(data.width/2, data.baseHeight+data.floorHeight*data.floors+data.roofHeight+data.floors*data.floorWidth, -data.roofOverlay)); //4
                verts.Add(new Vector3(data.width/2, data.baseHeight+data.floorHeight*data.floors+data.roofHeight+data.floors*data.floorWidth, data.roofOverlay+data.length)); //5

                verts.Add(new Vector3(0, data.baseHeight+data.floorHeight*data.floors+data.floors*data.floorWidth, -data.roofOverlay)); //6
                verts.Add(new Vector3(0, data.baseHeight+data.floorHeight*data.floors+data.floors*data.floorWidth, data.roofOverlay+data.length)); //7
                verts.Add(new Vector3(data.width, data.baseHeight+data.floorHeight*data.floors+data.floors*data.floorWidth, -data.roofOverlay)); //8
                verts.Add(new Vector3(data.width, data.baseHeight+data.floorHeight*data.floors+data.floors*data.floorWidth, data.roofOverlay+data.length)); //9

                tris1.Add(0); tris1.Add(6); tris1.Add(1);
                tris1.Add(1); tris1.Add(6); tris1.Add(7);
                tris1.Add(6); tris1.Add(4); tris1.Add(7);
                tris1.Add(7); tris1.Add(4); tris1.Add(5);
                
                tris1.Add(4); tris1.Add(8); tris1.Add(5);
                tris1.Add(8); tris1.Add(9); tris1.Add(5);
                tris1.Add(8); tris1.Add(2); tris1.Add(9);
                tris1.Add(9); tris1.Add(2); tris1.Add(3);

                if(!data.hasFront && (rightPreCorrection == 0f && leftPreCorrection == 0f)){
                    verts[0] = new Vector3(verts[0].x, verts[0].y, 0);
                    verts[2] = new Vector3(verts[2].x, verts[2].y, 0);
                    verts[4] = new Vector3(verts[4].x, verts[4].y, 0);
                    verts[6] = new Vector3(verts[6].x, verts[6].y, 0);
                    verts[8] = new Vector3(verts[8].x, verts[8].y, 0);
                }
                else if(!data.hasFront && (rightPreCorrection != 0f || leftPreCorrection != 0f)){
                    verts[0] = new Vector3(verts[0].x, verts[0].y, leftPreCorrection);
                    verts[2] = new Vector3(verts[2].x, verts[2].y, rightPreCorrection);
                    verts[4] = new Vector3(verts[4].x, verts[4].y, 0);
                    verts[6] = new Vector3(verts[6].x, verts[6].y, 0);
                    verts[8] = new Vector3(verts[8].x, verts[8].y, 0);
                }
                if(!data.hasBack && (rightProCorrection == 0f && leftProCorrection == 0f)){
                    verts[1] = new Vector3(verts[1].x, verts[1].y, data.length);
                    verts[3] = new Vector3(verts[3].x, verts[3].y, data.length);
                    verts[5] = new Vector3(verts[5].x, verts[5].y, data.length);
                    verts[7] = new Vector3(verts[7].x, verts[7].y, data.length);
                    verts[9] = new Vector3(verts[9].x, verts[9].y, data.length);
                }
                else if(!data.hasBack && (rightProCorrection != 0f || leftProCorrection != 0f)){
                    verts[1] = new Vector3(verts[1].x, verts[1].y, data.length-leftProCorrection);
                    verts[3] = new Vector3(verts[3].x, verts[3].y, data.length-rightProCorrection);
                    verts[5] = new Vector3(verts[5].x, verts[5].y, data.length);
                    verts[7] = new Vector3(verts[7].x, verts[7].y, data.length);
                    verts[9] = new Vector3(verts[9].x, verts[9].y, data.length);
                }

                uvs.Add(new Vector2(verts[0].z*data.roofTS, 0));
                uvs.Add(new Vector2(verts[1].z*data.roofTS, 0));
                uvs.Add(new Vector2(verts[2].z*data.roofTS, 0));
                uvs.Add(new Vector2(verts[3].z*data.roofTS, 0));
                uvs.Add(new Vector2(verts[4].z*data.roofTS, Mathf.Sqrt(Mathf.Pow(verts[4].z, 2) + Mathf.Pow(verts[4].y, 2))*data.roofTS));
                uvs.Add(new Vector2(verts[5].z*data.roofTS, Mathf.Sqrt(Mathf.Pow(verts[4].z, 2) + Mathf.Pow(verts[4].y, 2))*data.roofTS));

                uvs.Add(new Vector2(verts[6].z*data.roofTS, (data.roofOverlay/(data.roofOverlay+data.width/2))*uvs[4].y));
                uvs.Add(new Vector2(verts[7].z*data.roofTS, uvs[6].y));
                uvs.Add(new Vector2(verts[8].z*data.roofTS, uvs[6].y));
                uvs.Add(new Vector2(verts[9].z*data.roofTS, uvs[6].y));
                
            }
            else{

                float prediff = (data.width/2)*(data.roofEndHeight/data.roofHeight);
                float lowWidth = prediff*data.roofOverlay/data.roofEndLength;
                float lowHeight = data.roofOverlay/data.roofEndLength*data.roofEndHeight;

                float m = data.roofHeight-data.roofEndHeight-lowHeight+diff;
                float w = data.width/2-prediff-lowWidth+data.roofOverlay;

                verts.Add(new Vector3(data.width/2, data.baseHeight+data.floorHeight*data.floors+data.roofHeight+data.floors*data.floorWidth, data.roofEndLength)); //4
                verts.Add(new Vector3(data.width/2, data.baseHeight+data.floorHeight*data.floors+data.roofHeight+data.floors*data.floorWidth, data.length-data.roofEndLength)); //5

                verts.Add(new Vector3(data.width/2-prediff-lowWidth, data.baseHeight+data.floorHeight*data.floors+data.roofHeight-data.roofEndHeight-lowHeight+data.floors*data.floorWidth, -data.roofOverlay)); //6
                verts.Add(new Vector3(data.width/2-prediff-lowWidth, data.baseHeight+data.floorHeight*data.floors+data.roofHeight-data.roofEndHeight-lowHeight+data.floors*data.floorWidth, data.roofOverlay+data.length)); //7

                verts.Add(new Vector3(data.width/2+prediff+lowWidth, data.baseHeight+data.floorHeight*data.floors+data.roofHeight-data.roofEndHeight-lowHeight+data.floors*data.floorWidth, -data.roofOverlay)); //8
                verts.Add(new Vector3(data.width/2+prediff+lowWidth, data.baseHeight+data.floorHeight*data.floors+data.roofHeight-data.roofEndHeight-lowHeight+data.floors*data.floorWidth, data.roofOverlay+data.length)); //9
                //alternative for UV
                verts.Add(verts[4]); //10
                verts.Add(verts[5]); //11
                verts.Add(verts[6]); //12
                verts.Add(verts[7]); //13
                verts.Add(verts[8]); //14
                verts.Add(verts[9]); //15

                //left side
                tris1.Add(0); tris1.Add(7); tris1.Add(1);
                tris1.Add(0); tris1.Add(6); tris1.Add(7);
                tris1.Add(6); tris1.Add(5); tris1.Add(7);
                tris1.Add(6); tris1.Add(4); tris1.Add(5);
                //rigth side
                tris1.Add(2); tris1.Add(3); tris1.Add(9);
                tris1.Add(2); tris1.Add(9); tris1.Add(8);
                tris1.Add(8); tris1.Add(5); tris1.Add(4);
                tris1.Add(8); tris1.Add(9); tris1.Add(5);
                //front
                tris1.Add(12); tris1.Add(14); tris1.Add(10);
                //back
                tris1.Add(15); tris1.Add(13); tris1.Add(11);

                if(!data.hasFront && (rightPreCorrection == 0f && leftPreCorrection == 0f)){
                    verts[6] = new Vector3(data.width/2, data.baseHeight+data.floorHeight*data.floors+data.roofHeight+data.floors*data.floorWidth, 0);
                    verts[8] = verts[6];
                    verts[12] = verts[6];
                    verts[14] = verts[6];
                    verts[0] = new Vector3(verts[0].x, verts[0].y, 0);
                    verts[2] = new Vector3(verts[2].x, verts[2].y, 0);

                }
                else if(!data.hasFront && (rightPreCorrection != 0f || leftPreCorrection != 0f)){

                    verts[6] = new Vector3(data.width/2, data.baseHeight+data.floorHeight*data.floors+data.roofHeight+data.floors*data.floorWidth, 0);
                    verts[8] = verts[6];
                    verts[12] = verts[6];
                    verts[14] = verts[6];
                    //add 2 more and retriangulate
                    verts.Add(new Vector3(0, data.baseHeight+data.floorHeight*data.floors+data.floors*data.floorWidth, 0));
                    verts.Add(new Vector3(data.width, data.baseHeight+data.floorHeight*data.floors+data.floors*data.floorWidth, 0));

                    verts[0] = new Vector3(verts[0].x, verts[0].y, leftPreCorrection);
                    verts[2] = new Vector3(verts[2].x, verts[2].y, rightPreCorrection);
                    //tris
                    tris1.Clear();
                    tris1.Add(0); tris1.Add(16); tris1.Add(1);
                    tris1.Add(1); tris1.Add(16); tris1.Add(7);
                    tris1.Add(16); tris1.Add(6); tris1.Add(7);
                    tris1.Add(6); tris1.Add(5); tris1.Add(7);
                    tris1.Add(6); tris1.Add(4); tris1.Add(5);
                    //rigth side
                    tris1.Add(2); tris1.Add(3); tris1.Add(17);
                    tris1.Add(17); tris1.Add(3); tris1.Add(9);
                    tris1.Add(8); tris1.Add(17); tris1.Add(9);
                    tris1.Add(8); tris1.Add(5); tris1.Add(4);
                    tris1.Add(8); tris1.Add(9); tris1.Add(5);
                    //back
                    tris1.Add(15); tris1.Add(13); tris1.Add(11);

                }

                if(!data.hasBack && (rightProCorrection == 0f && leftProCorrection == 0f)){
                    verts[7] = new Vector3(data.width/2, data.baseHeight+data.floorHeight*data.floors+data.roofHeight+data.floors*data.floorWidth, data.length);
                    verts[9] = verts[7];
                    verts[13] = verts[7];
                    verts[15] = verts[7];
                    verts[1] = new Vector3(verts[1].x, verts[1].y, data.length);
                    verts[3] = new Vector3(verts[3].x, verts[3].y, data.length);
                }
                else if(!data.hasBack && (rightProCorrection != 0f || leftProCorrection != 0f)){
                    verts[7] = new Vector3(data.width/2, data.baseHeight+data.floorHeight*data.floors+data.roofHeight+data.floors*data.floorWidth, data.length);
                    verts[9] = verts[7];
                    verts[13] = verts[7];
                    verts[15] = verts[7];
                    verts.Add(new Vector3(0, data.baseHeight+data.floorHeight*data.floors+data.floors*data.floorWidth, data.length));
                    verts.Add(new Vector3(data.width, data.baseHeight+data.floorHeight*data.floors+data.floors*data.floorWidth, data.length));
                    
                    verts[1] = new Vector3(verts[1].x, verts[1].y, data.length-leftProCorrection);
                    verts[3] = new Vector3(verts[3].x, verts[3].y, data.length-rightProCorrection);
                    //tris
                    if(verts.Count > 18){
                        tris1.Clear();
                        tris1.Add(0); tris1.Add(16); tris1.Add(1);
                        tris1.Add(1); tris1.Add(16); tris1.Add(18);
                        tris1.Add(16); tris1.Add(6); tris1.Add(18);
                        tris1.Add(18); tris1.Add(6); tris1.Add(7);
                        //rigth side
                        tris1.Add(2); tris1.Add(3); tris1.Add(17);
                        tris1.Add(17); tris1.Add(3); tris1.Add(19);
                        tris1.Add(8); tris1.Add(17); tris1.Add(19);
                        tris1.Add(8); tris1.Add(19); tris1.Add(9);
                    }
                    else{
                        tris1.Clear();
                        tris1.Add(0); tris1.Add(16); tris1.Add(1);
                        tris1.Add(6); tris1.Add(16); tris1.Add(0);
                        tris1.Add(16); tris1.Add(6); tris1.Add(7);
                        tris1.Add(6); tris1.Add(5); tris1.Add(7);
                        tris1.Add(6); tris1.Add(4); tris1.Add(5);
                        //rigth side
                        tris1.Add(2); tris1.Add(3); tris1.Add(17);
                        tris1.Add(17); tris1.Add(8); tris1.Add(2);
                        tris1.Add(8); tris1.Add(17); tris1.Add(9);
                        tris1.Add(8); tris1.Add(5); tris1.Add(4);
                        tris1.Add(8); tris1.Add(9); tris1.Add(5);
                        //front
                        tris1.Add(12); tris1.Add(14); tris1.Add(10);
                    }

                }

                uvs.Add(new Vector2(verts[0].z*data.roofTS, 0));//0
                uvs.Add(new Vector2(verts[1].z*data.roofTS, 0));//1
                uvs.Add(new Vector2(verts[2].z*data.roofTS, 0));//2
                uvs.Add(new Vector2(verts[3].z*data.roofTS, 0));//3

                uvs.Add(new Vector2(verts[4].z*data.roofTS, Mathf.Sqrt(Mathf.Pow(data.width/2+data.roofOverlay, 2) + Mathf.Pow(data.roofHeight+diff, 2))*data.roofTS));
                uvs.Add(new Vector2(verts[5].z*data.roofTS, uvs[4].y));
                if(!data.hasFront){
                    uvs.Add(new Vector2(verts[6].z*data.roofTS, uvs[4].y)); //6
                }
                else{
                    uvs.Add(new Vector2(verts[6].z*data.roofTS, Mathf.Sqrt(Mathf.Pow(m, 2)+Mathf.Pow(w, 2))*data.roofTS)); //6
                }
                if(!data.hasBack){
                    uvs.Add(new Vector2(verts[7].z*data.roofTS, uvs[4].y));//7
                }
                else{
                    uvs.Add(new Vector2(verts[7].z*data.roofTS, Mathf.Sqrt(Mathf.Pow(m, 2)+Mathf.Pow(w, 2))*data.roofTS));//7
                }

                uvs.Add(uvs[6]);//8(6)
                uvs.Add(uvs[7]);//9(7)
                //little triangles (front and back)
                uvs.Add(new Vector2(verts[10].x*data.roofTS, Mathf.Sqrt(Mathf.Pow(data.roofEndHeight+lowHeight, 2) + Mathf.Pow(data.roofEndLength+data.roofOverlay, 2))*data.roofTS));//10
                uvs.Add(new Vector2(verts[11].x*data.roofTS, Mathf.Sqrt(Mathf.Pow(data.roofEndHeight+lowHeight, 2) + Mathf.Pow(data.roofEndLength+data.roofOverlay, 2))*data.roofTS));//11
                uvs.Add(new Vector2(verts[12].x*data.roofTS, verts[12].z*data.roofTS));//12
                uvs.Add(new Vector2(verts[13].x*data.roofTS, verts[12].z*data.roofTS));//13
                uvs.Add(new Vector2(verts[14].x*data.roofTS, verts[14].z*data.roofTS));//14
                uvs.Add(new Vector2(verts[15].x*data.roofTS, verts[14].z*data.roofTS));//15

                if(verts.Count > 16){
                    uvs.Add(new Vector2(verts[16].z*data.roofTS, Mathf.Sqrt(Mathf.Pow(data.roofOverlay, 2) + Mathf.Pow(diff, 2))*data.roofTS));//16
                    uvs.Add(new Vector2(verts[17].z*data.roofTS, uvs[16].y));//17
                }
                if(verts.Count > 18){
                    uvs.Add(new Vector2(verts[18].z*data.roofTS, Mathf.Sqrt(Mathf.Pow(data.roofOverlay, 2) + Mathf.Pow(diff, 2))*data.roofTS));//16
                    uvs.Add(new Vector2(verts[19].z*data.roofTS, uvs[18].y));//17
                }
                

            }


        }
        
        vertices = verts.ToArray();
        triangles = tris1.ToArray();
        UV = uvs.ToArray();

        if(mesh != null){
            data.UpdateMesh(mesh, vertices, triangles, UV);
        }
    }
}
}
