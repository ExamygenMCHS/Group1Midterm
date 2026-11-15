using System.Collections.Generic;
using UnityEngine;

namespace SHM{
[ExecuteInEditMode]
[RequireComponent(typeof(MeshFilter))]
public class roofBaseHeight : MonoBehaviour
{
    //This script will generate the mesh of the base of the roof (the sides which are not top and not bottom of roof)
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


            verts.Add(new Vector3(-data.roofOverlay, data.baseHeight+data.floorHeight*data.floors+data.floors*data.floorWidth+data.roofWidth, -data.roofOverlay)); //8
            verts.Add(new Vector3(-data.roofOverlay, data.baseHeight+data.floorHeight*data.floors+data.floors*data.floorWidth+data.roofWidth, data.roofOverlay+data.length)); //9
            verts.Add(new Vector3(data.roofOverlay+data.width, data.baseHeight+data.floorHeight*data.floors+data.floors*data.floorWidth+data.roofWidth, -data.roofOverlay)); //10
            verts.Add(new Vector3(data.roofOverlay+data.width, data.baseHeight+data.floorHeight*data.floors+data.floors*data.floorWidth+data.roofWidth, data.roofOverlay+data.length)); //11

            verts.Add(new Vector3(0, data.baseHeight+data.floorHeight*data.floors+data.floors*data.floorWidth+data.roofWidth, -data.roofOverlay));
            verts.Add(new Vector3(0, data.baseHeight+data.floorHeight*data.floors+data.floors*data.floorWidth+data.roofWidth, data.roofOverlay+data.length));
            verts.Add(new Vector3(data.width, data.baseHeight+data.floorHeight*data.floors+data.floors*data.floorWidth+data.roofWidth, -data.roofOverlay));
            verts.Add(new Vector3(data.width, data.baseHeight+data.floorHeight*data.floors+data.floors*data.floorWidth+data.roofWidth, data.roofOverlay+data.length));

            //front
            tris1.Add(2); tris1.Add(6); tris1.Add(10);
            tris1.Add(14); tris1.Add(10); tris1.Add(6);
            tris1.Add(6); tris1.Add(4); tris1.Add(14);
            tris1.Add(4); tris1.Add(12); tris1.Add(14);
            tris1.Add(4); tris1.Add(0); tris1.Add(12);
            tris1.Add(0); tris1.Add(8); tris1.Add(12);
            //left
            tris1.Add(0); tris1.Add(1); tris1.Add(8);
            tris1.Add(1); tris1.Add(9); tris1.Add(8);
            //back
            tris1.Add(1); tris1.Add(5); tris1.Add(9);
            tris1.Add(5); tris1.Add(13); tris1.Add(9);
            tris1.Add(5); tris1.Add(7); tris1.Add(13);
            tris1.Add(7); tris1.Add(15); tris1.Add(13);
            tris1.Add(7); tris1.Add(3); tris1.Add(15);
            tris1.Add(3); tris1.Add(11); tris1.Add(15);
            //right
            tris1.Add(11); tris1.Add(3); tris1.Add(2);
            tris1.Add(10); tris1.Add(11); tris1.Add(2);

            if(!data.hasFront && (rightPreCorrection == 0f && leftPreCorrection == 0f)){
                verts[0] = new Vector3(verts[0].x, verts[0].y, 0);
                verts[2] = new Vector3(verts[2].x, verts[2].y, 0);
                verts[4] = new Vector3(verts[4].x, verts[4].y, 0);
                verts[6] = new Vector3(verts[6].x, verts[6].y, 0);

                verts[8] = new Vector3(verts[0].x, verts[8].y, 0);
                verts[10] = new Vector3(verts[2].x, verts[10].y, 0);
                verts[12] = new Vector3(verts[4].x, verts[12].y, 0);
                verts[14] = new Vector3(verts[6].x, verts[14].y, 0);
            }
            else if(!data.hasFront && (rightPreCorrection != 0f || leftPreCorrection != 0f)){
                verts[0] = new Vector3(verts[0].x, verts[0].y, leftPreCorrection);
                verts[2] = new Vector3(verts[2].x, verts[2].y, rightPreCorrection);
                verts[4] = new Vector3(verts[4].x, verts[4].y, 0);
                verts[6] = new Vector3(verts[6].x, verts[6].y, 0);

                verts[8] = new Vector3(verts[0].x, verts[8].y, leftPreCorrection);
                verts[10] = new Vector3(verts[2].x, verts[10].y, rightPreCorrection);
                verts[12] = new Vector3(verts[4].x, verts[12].y, 0);
                verts[14] = new Vector3(verts[6].x, verts[14].y, 0);
            }
            if(!data.hasBack && (rightProCorrection == 0f && leftProCorrection == 0f)){
                verts[1] = new Vector3(verts[1].x, verts[1].y, data.length);
                verts[3] = new Vector3(verts[3].x, verts[3].y, data.length);
                verts[5] = new Vector3(verts[5].x, verts[5].y, data.length);
                verts[7] = new Vector3(verts[7].x, verts[7].y, data.length);

                verts[9] = new Vector3(verts[1].x, verts[9].y, data.length);
                verts[11] = new Vector3(verts[3].x, verts[11].y, data.length);
                verts[13] = new Vector3(verts[5].x, verts[13].y, data.length);
                verts[15] = new Vector3(verts[7].x, verts[15].y, data.length);
            }
            else if(!data.hasBack && (rightProCorrection != 0f || leftProCorrection != 0f)){
                verts[1] = new Vector3(verts[1].x, verts[1].y, data.length-leftProCorrection);
                verts[3] = new Vector3(verts[3].x, verts[3].y, data.length-rightProCorrection);
                verts[5] = new Vector3(verts[5].x, verts[5].y, data.length);
                verts[7] = new Vector3(verts[7].x, verts[7].y, data.length);

                verts[9] = new Vector3(verts[1].x, verts[9].y, data.length-leftProCorrection);
                verts[11] = new Vector3(verts[3].x, verts[11].y, data.length-rightProCorrection);
                verts[13] = new Vector3(verts[5].x, verts[13].y, data.length);
                verts[15] = new Vector3(verts[7].x, verts[15].y, data.length);
            }
        }
        else{
            float diff = (1/(data.width/2))*data.roofOverlay*data.roofHeight;
            //non-flat roof middle (top)
            if((data.roofEndHeight == 0 || data.roofEndLength == 0)){
                //non-flat roof edges
                verts.Add(new Vector3(-data.roofOverlay, data.baseHeight+data.floorHeight*data.floors-diff+data.floors*data.floorWidth, -data.roofOverlay)); //0
                verts.Add(new Vector3(-data.roofOverlay, data.baseHeight+data.floorHeight*data.floors-diff+data.floors*data.floorWidth, data.roofOverlay+data.length)); //1
                verts.Add(new Vector3(data.roofOverlay+data.width, data.baseHeight+data.floorHeight*data.floors-diff+data.floors*data.floorWidth, -data.roofOverlay)); //2
                verts.Add(new Vector3(data.roofOverlay+data.width, data.baseHeight+data.floorHeight*data.floors-diff+data.floors*data.floorWidth, data.roofOverlay+data.length)); //3


                verts.Add(new Vector3(data.width/2, data.baseHeight+data.floorHeight*data.floors+data.roofHeight+data.floors*data.floorWidth, -data.roofOverlay)); //4
                verts.Add(new Vector3(data.width/2, data.baseHeight+data.floorHeight*data.floors+data.roofHeight+data.floors*data.floorWidth, data.roofOverlay+data.length)); //5

                verts.Add(new Vector3(0, data.baseHeight+data.floorHeight*data.floors+data.floors*data.floorWidth, -data.roofOverlay)); //6
                verts.Add(new Vector3(0, data.baseHeight+data.floorHeight*data.floors+data.floors*data.floorWidth, data.roofOverlay+data.length)); //7
                verts.Add(new Vector3(data.width, data.baseHeight+data.floorHeight*data.floors+data.floors*data.floorWidth, -data.roofOverlay)); //8
                verts.Add(new Vector3(data.width, data.baseHeight+data.floorHeight*data.floors+data.floors*data.floorWidth, data.roofOverlay+data.length)); //9

                verts.Add(new Vector3(-data.roofOverlay, data.baseHeight+data.floorHeight*data.floors-diff+data.floors*data.floorWidth+data.roofWidth, -data.roofOverlay)); //10
                verts.Add(new Vector3(-data.roofOverlay, data.baseHeight+data.floorHeight*data.floors-diff+data.floors*data.floorWidth+data.roofWidth, data.roofOverlay+data.length)); //11
                verts.Add(new Vector3(data.roofOverlay+data.width, data.baseHeight+data.floorHeight*data.floors-diff+data.floors*data.floorWidth+data.roofWidth, -data.roofOverlay)); //2
                verts.Add(new Vector3(data.roofOverlay+data.width, data.baseHeight+data.floorHeight*data.floors-diff+data.floors*data.floorWidth+data.roofWidth, data.roofOverlay+data.length)); //13
                

                verts.Add(new Vector3(data.width/2, data.baseHeight+data.floorHeight*data.floors+data.roofHeight+data.floors*data.floorWidth+data.roofWidth, -data.roofOverlay)); //14
                verts.Add(new Vector3(data.width/2, data.baseHeight+data.floorHeight*data.floors+data.roofHeight+data.floors*data.floorWidth+data.roofWidth, data.roofOverlay+data.length)); //15

                verts.Add(new Vector3(0, data.baseHeight+data.floorHeight*data.floors+data.floors*data.floorWidth+data.roofWidth, -data.roofOverlay)); //16
                verts.Add(new Vector3(0, data.baseHeight+data.floorHeight*data.floors+data.floors*data.floorWidth+data.roofWidth, data.roofOverlay+data.length)); //17
                verts.Add(new Vector3(data.width, data.baseHeight+data.floorHeight*data.floors+data.floors*data.floorWidth+data.roofWidth, -data.roofOverlay)); //18
                verts.Add(new Vector3(data.width, data.baseHeight+data.floorHeight*data.floors+data.floors*data.floorWidth+data.roofWidth, data.roofOverlay+data.length)); //19


                //front
                tris1.Add(2); tris1.Add(8); tris1.Add(12);
                tris1.Add(8); tris1.Add(18); tris1.Add(12);
                tris1.Add(8); tris1.Add(4); tris1.Add(18);
                tris1.Add(4); tris1.Add(14); tris1.Add(18);
                tris1.Add(4); tris1.Add(6); tris1.Add(14);
                tris1.Add(6); tris1.Add(16); tris1.Add(14);
                tris1.Add(6); tris1.Add(0); tris1.Add(16);
                tris1.Add(0); tris1.Add(10); tris1.Add(16);
                //left
                tris1.Add(0); tris1.Add(1); tris1.Add(10);
                tris1.Add(1); tris1.Add(11); tris1.Add(10);
                //back
                tris1.Add(1); tris1.Add(7); tris1.Add(11);
                tris1.Add(7); tris1.Add(17); tris1.Add(11);
                tris1.Add(7); tris1.Add(5); tris1.Add(17);
                tris1.Add(5); tris1.Add(15); tris1.Add(17);
                tris1.Add(5); tris1.Add(9); tris1.Add(15);
                tris1.Add(9); tris1.Add(19); tris1.Add(15);
                tris1.Add(9); tris1.Add(3); tris1.Add(19);
                tris1.Add(3); tris1.Add(13); tris1.Add(19);
                //right
                tris1.Add(13); tris1.Add(3); tris1.Add(2);
                tris1.Add(12); tris1.Add(13); tris1.Add(2);

                if(!data.hasFront && (rightPreCorrection == 0f && leftPreCorrection == 0f)){
                    verts[0] = new Vector3(verts[0].x, verts[0].y, 0);
                    verts[2] = new Vector3(verts[2].x, verts[2].y, 0);
                    verts[4] = new Vector3(verts[4].x, verts[4].y, 0);
                    verts[6] = new Vector3(verts[6].x, verts[6].y, 0);
                    verts[8] = new Vector3(verts[8].x, verts[8].y, 0);

                    verts[10] = new Vector3(verts[0].x, verts[10].y, 0);
                    verts[12] = new Vector3(verts[2].x, verts[12].y, 0);
                    verts[14] = new Vector3(verts[4].x, verts[14].y, 0);
                    verts[16] = new Vector3(verts[6].x, verts[16].y, 0);
                    verts[18] = new Vector3(verts[8].x, verts[18].y, 0);
                }
                else if(!data.hasFront && (rightPreCorrection != 0f || leftPreCorrection != 0f)){
                    verts[0] = new Vector3(verts[0].x, verts[0].y, leftPreCorrection);
                    verts[2] = new Vector3(verts[2].x, verts[2].y, rightPreCorrection);
                    verts[4] = new Vector3(verts[4].x, verts[4].y, 0);
                    verts[6] = new Vector3(verts[6].x, verts[6].y, 0);
                    verts[8] = new Vector3(verts[8].x, verts[8].y, 0);

                    verts[10] = new Vector3(verts[0].x, verts[10].y, leftPreCorrection);
                    verts[12] = new Vector3(verts[2].x, verts[12].y, rightPreCorrection);
                    verts[14] = new Vector3(verts[4].x, verts[14].y, 0);
                    verts[16] = new Vector3(verts[6].x, verts[16].y, 0);
                    verts[18] = new Vector3(verts[8].x, verts[18].y, 0);
                }
                if(!data.hasBack && (rightProCorrection == 0f && leftProCorrection == 0f)){
                    verts[1] = new Vector3(verts[1].x, verts[1].y, data.length);
                    verts[3] = new Vector3(verts[3].x, verts[3].y, data.length);
                    verts[5] = new Vector3(verts[5].x, verts[5].y, data.length);
                    verts[7] = new Vector3(verts[7].x, verts[7].y, data.length);
                    verts[9] = new Vector3(verts[9].x, verts[9].y, data.length);

                    verts[11] = new Vector3(verts[1].x, verts[11].y, data.length);
                    verts[13] = new Vector3(verts[3].x, verts[13].y, data.length);
                    verts[15] = new Vector3(verts[5].x, verts[15].y, data.length);
                    verts[17] = new Vector3(verts[7].x, verts[17].y, data.length);
                    verts[19] = new Vector3(verts[9].x, verts[19].y, data.length);
                }
                else if(!data.hasBack && (rightProCorrection != 0f || leftProCorrection != 0f)){
                    verts[1] = new Vector3(verts[1].x, verts[1].y, data.length-leftProCorrection);
                    verts[3] = new Vector3(verts[3].x, verts[3].y, data.length-rightProCorrection);
                    verts[5] = new Vector3(verts[5].x, verts[5].y, data.length);
                    verts[7] = new Vector3(verts[7].x, verts[7].y, data.length);
                    verts[9] = new Vector3(verts[9].x, verts[9].y, data.length);

                    verts[11] = new Vector3(verts[1].x, verts[11].y, data.length-leftProCorrection);
                    verts[13] = new Vector3(verts[3].x, verts[13].y, data.length-rightProCorrection);
                    verts[15] = new Vector3(verts[5].x, verts[15].y, data.length);
                    verts[17] = new Vector3(verts[7].x, verts[17].y, data.length);
                    verts[19] = new Vector3(verts[9].x, verts[19].y, data.length);
                }
                
            }
            else{

                float prediff = (data.width/2)*(data.roofEndHeight/data.roofHeight);
                float lowWidth = prediff*data.roofOverlay/data.roofEndLength;
                float lowHeight = data.roofOverlay/data.roofEndLength*data.roofEndHeight;

                float m = data.roofHeight-data.roofEndHeight-lowHeight+diff;
                float w = data.width/2-prediff-lowWidth+data.roofOverlay;

                //non-flat roof edges
                verts.Add(new Vector3(-data.roofOverlay, data.baseHeight+data.floorHeight*data.floors-diff+data.floors*data.floorWidth, -data.roofOverlay)); //0
                verts.Add(new Vector3(-data.roofOverlay, data.baseHeight+data.floorHeight*data.floors-diff+data.floors*data.floorWidth, data.roofOverlay+data.length)); //1
                verts.Add(new Vector3(data.roofOverlay+data.width, data.baseHeight+data.floorHeight*data.floors-diff+data.floors*data.floorWidth, -data.roofOverlay)); //2
                verts.Add(new Vector3(data.roofOverlay+data.width, data.baseHeight+data.floorHeight*data.floors-diff+data.floors*data.floorWidth, data.roofOverlay+data.length)); //3


                verts.Add(new Vector3(data.width/2-prediff-lowWidth, data.baseHeight+data.floorHeight*data.floors+data.roofHeight-data.roofEndHeight-lowHeight+data.floors*data.floorWidth, -data.roofOverlay)); //4
                verts.Add(new Vector3(data.width/2-prediff-lowWidth, data.baseHeight+data.floorHeight*data.floors+data.roofHeight-data.roofEndHeight-lowHeight+data.floors*data.floorWidth, data.roofOverlay+data.length)); //5

                verts.Add(new Vector3(data.width/2+prediff+lowWidth, data.baseHeight+data.floorHeight*data.floors+data.roofHeight-data.roofEndHeight-lowHeight+data.floors*data.floorWidth, -data.roofOverlay)); //6
                verts.Add(new Vector3(data.width/2+prediff+lowWidth, data.baseHeight+data.floorHeight*data.floors+data.roofHeight-data.roofEndHeight-lowHeight+data.floors*data.floorWidth, data.roofOverlay+data.length)); //7

                //non-flat roof edges
                verts.Add(new Vector3(-data.roofOverlay, data.baseHeight+data.floorHeight*data.floors-diff+data.floors*data.floorWidth+data.roofWidth, -data.roofOverlay)); //8
                verts.Add(new Vector3(-data.roofOverlay, data.baseHeight+data.floorHeight*data.floors-diff+data.floors*data.floorWidth+data.roofWidth, data.roofOverlay+data.length)); //9
                verts.Add(new Vector3(data.roofOverlay+data.width, data.baseHeight+data.floorHeight*data.floors-diff+data.floors*data.floorWidth+data.roofWidth, -data.roofOverlay)); //10
                verts.Add(new Vector3(data.roofOverlay+data.width, data.baseHeight+data.floorHeight*data.floors-diff+data.floors*data.floorWidth+data.roofWidth, data.roofOverlay+data.length)); //11

                verts.Add(new Vector3(data.width/2-prediff-lowWidth, data.baseHeight+data.floorHeight*data.floors+data.roofHeight-data.roofEndHeight-lowHeight+data.floors*data.floorWidth+data.roofWidth, -data.roofOverlay)); //12
                verts.Add(new Vector3(data.width/2-prediff-lowWidth, data.baseHeight+data.floorHeight*data.floors+data.roofHeight-data.roofEndHeight-lowHeight+data.floors*data.floorWidth+data.roofWidth, data.roofOverlay+data.length)); //13

                verts.Add(new Vector3(data.width/2+prediff+lowWidth, data.baseHeight+data.floorHeight*data.floors+data.roofHeight-data.roofEndHeight-lowHeight+data.floors*data.floorWidth+data.roofWidth, -data.roofOverlay)); //14
                verts.Add(new Vector3(data.width/2+prediff+lowWidth, data.baseHeight+data.floorHeight*data.floors+data.roofHeight-data.roofEndHeight-lowHeight+data.floors*data.floorWidth+data.roofWidth, data.roofOverlay+data.length)); //15


                //front
                tris1.Add(2); tris1.Add(6); tris1.Add(10);
                tris1.Add(6); tris1.Add(14); tris1.Add(10);
                tris1.Add(6); tris1.Add(4); tris1.Add(14);
                tris1.Add(4); tris1.Add(12); tris1.Add(14);
                tris1.Add(4); tris1.Add(0); tris1.Add(12);
                tris1.Add(0); tris1.Add(8); tris1.Add(12);
                //left
                tris1.Add(0); tris1.Add(1); tris1.Add(8);
                tris1.Add(1); tris1.Add(9); tris1.Add(8);
                //back
                tris1.Add(1); tris1.Add(5); tris1.Add(9);
                tris1.Add(5); tris1.Add(13); tris1.Add(9);
                tris1.Add(5); tris1.Add(7); tris1.Add(13);
                tris1.Add(7); tris1.Add(15); tris1.Add(13);
                tris1.Add(7); tris1.Add(3); tris1.Add(15);
                tris1.Add(3); tris1.Add(11); tris1.Add(15);
                //right
                tris1.Add(3); tris1.Add(2); tris1.Add(11);
                tris1.Add(2); tris1.Add(10); tris1.Add(11);

                

                if(!data.hasFront && (rightPreCorrection == 0f && leftPreCorrection == 0f)){
                    verts[4] = new Vector3(data.width/2, data.baseHeight+data.floorHeight*data.floors+data.roofHeight+data.floors*data.floorWidth, 0);
                    verts[6] = verts[4];
                    verts[0] = new Vector3(verts[0].x, verts[0].y, 0);
                    verts[2] = new Vector3(verts[2].x, verts[2].y, 0);

                    verts[12] = new Vector3(data.width/2, data.baseHeight+data.floorHeight*data.floors+data.roofHeight+data.floors*data.floorWidth+data.roofWidth, 0);
                    verts[14] = verts[12];
                    verts[8] = new Vector3(verts[8].x, verts[8].y, 0);
                    verts[10] = new Vector3(verts[10].x, verts[10].y, 0);

                }
                else if(!data.hasFront && (rightPreCorrection != 0f || leftPreCorrection != 0f)){

                    verts[4] = new Vector3(data.width/2, data.baseHeight+data.floorHeight*data.floors+data.roofHeight+data.floors*data.floorWidth, 0);
                    verts[6] = verts[4];
                    verts[12] = new Vector3(data.width/2, data.baseHeight+data.floorHeight*data.floors+data.roofHeight+data.floors*data.floorWidth+data.roofWidth, 0);
                    verts[14] = verts[12];

                    //add 2 more and retriangulate
                    verts.Add(new Vector3(0, data.baseHeight+data.floorHeight*data.floors+data.floors*data.floorWidth, 0));//16
                    verts.Add(new Vector3(data.width, data.baseHeight+data.floorHeight*data.floors+data.floors*data.floorWidth, 0));//17

                    verts.Add(new Vector3(0, data.baseHeight+data.floorHeight*data.floors+data.floors*data.floorWidth+data.roofWidth, 0));//18
                    verts.Add(new Vector3(data.width, data.baseHeight+data.floorHeight*data.floors+data.floors*data.floorWidth+data.roofWidth, 0));//19

                    verts[0] = new Vector3(verts[0].x, verts[0].y, leftPreCorrection);
                    verts[2] = new Vector3(verts[2].x, verts[2].y, rightPreCorrection);

                    verts[8] = new Vector3(verts[8].x, verts[8].y, leftPreCorrection);
                    verts[10] = new Vector3(verts[10].x, verts[10].y, rightPreCorrection);
                    //tris
                    tris1.Clear();
                    //front
                    tris1.Add(2); tris1.Add(17); tris1.Add(10);
                    tris1.Add(17); tris1.Add(19); tris1.Add(10);
                    tris1.Add(17); tris1.Add(6); tris1.Add(19);
                    tris1.Add(6); tris1.Add(14); tris1.Add(19);
                    tris1.Add(6); tris1.Add(4); tris1.Add(14);
                    tris1.Add(4); tris1.Add(12); tris1.Add(14);
                    tris1.Add(4); tris1.Add(16); tris1.Add(12);
                    tris1.Add(16); tris1.Add(18); tris1.Add(12);
                    tris1.Add(16); tris1.Add(0); tris1.Add(18);
                    tris1.Add(0); tris1.Add(8); tris1.Add(18);
                    //left
                    tris1.Add(0); tris1.Add(1); tris1.Add(8);
                    tris1.Add(1); tris1.Add(9); tris1.Add(8);
                    //back
                    tris1.Add(1); tris1.Add(5); tris1.Add(9);
                    tris1.Add(5); tris1.Add(13); tris1.Add(9);
                    tris1.Add(5); tris1.Add(7); tris1.Add(13);
                    tris1.Add(7); tris1.Add(15); tris1.Add(13);
                    tris1.Add(7); tris1.Add(3); tris1.Add(15);
                    tris1.Add(3); tris1.Add(11); tris1.Add(15);
                    //right
                    tris1.Add(3); tris1.Add(2); tris1.Add(11);
                    tris1.Add(2); tris1.Add(10); tris1.Add(11);

                }

                if(!data.hasBack && (rightProCorrection == 0f && leftProCorrection == 0f)){
                    verts[5] = new Vector3(data.width/2, data.baseHeight+data.floorHeight*data.floors+data.roofHeight+data.floors*data.floorWidth, data.length);
                    verts[7] = verts[5];
                    verts[1] = new Vector3(verts[1].x, verts[1].y, data.length);
                    verts[3] = new Vector3(verts[3].x, verts[3].y, data.length);

                    verts[13] = new Vector3(data.width/2, data.baseHeight+data.floorHeight*data.floors+data.roofHeight+data.floors*data.floorWidth+data.roofWidth, data.length);
                    verts[15] = verts[13];
                    verts[9] = new Vector3(verts[9].x, verts[9].y, data.length);
                    verts[11] = new Vector3(verts[11].x, verts[11].y, data.length);
                }
                else if(!data.hasBack && (rightProCorrection != 0f || leftProCorrection != 0f)){
                    verts[5] = new Vector3(data.width/2, data.baseHeight+data.floorHeight*data.floors+data.roofHeight+data.floors*data.floorWidth, data.length);
                    verts[7] = verts[5];
                    verts[1] = new Vector3(verts[1].x, verts[1].y, data.length-leftProCorrection);
                    verts[3] = new Vector3(verts[3].x, verts[3].y, data.length-rightProCorrection);

                    verts[13] = new Vector3(data.width/2, data.baseHeight+data.floorHeight*data.floors+data.roofHeight+data.floors*data.floorWidth+data.roofWidth, data.length);
                    verts[15] = verts[13];
                    verts[9] = new Vector3(verts[9].x, verts[9].y, data.length-leftProCorrection);
                    verts[11] = new Vector3(verts[11].x, verts[11].y, data.length-rightProCorrection);

                    verts.Add(new Vector3(0, data.baseHeight+data.floorHeight*data.floors+data.floors*data.floorWidth, data.length));
                    verts.Add(new Vector3(data.width, data.baseHeight+data.floorHeight*data.floors+data.floors*data.floorWidth, data.length));

                    verts.Add(new Vector3(0, data.baseHeight+data.floorHeight*data.floors+data.floors*data.floorWidth+data.roofWidth, data.length));
                    verts.Add(new Vector3(data.width, data.baseHeight+data.floorHeight*data.floors+data.floors*data.floorWidth+data.roofWidth, data.length));
                    //tris
                    if(verts.Count > 20){
                        tris1.Clear();
                        tris1.Add(2); tris1.Add(17); tris1.Add(10);
                        tris1.Add(17); tris1.Add(19); tris1.Add(10);
                        tris1.Add(17); tris1.Add(6); tris1.Add(19);
                        tris1.Add(6); tris1.Add(14); tris1.Add(19);
                        tris1.Add(6); tris1.Add(4); tris1.Add(14);
                        tris1.Add(4); tris1.Add(12); tris1.Add(14);
                        tris1.Add(4); tris1.Add(16); tris1.Add(12);
                        tris1.Add(16); tris1.Add(18); tris1.Add(12);
                        tris1.Add(16); tris1.Add(0); tris1.Add(18);
                        tris1.Add(0); tris1.Add(8); tris1.Add(18);
                        //left
                        tris1.Add(0); tris1.Add(1); tris1.Add(8);
                        tris1.Add(1); tris1.Add(9); tris1.Add(8);
                        //back//16->20
                        tris1.Add(1); tris1.Add(20); tris1.Add(9);
                        tris1.Add(20); tris1.Add(22); tris1.Add(9);
                        tris1.Add(20); tris1.Add(5); tris1.Add(22);
                        tris1.Add(5); tris1.Add(13); tris1.Add(22);
                        tris1.Add(5); tris1.Add(7); tris1.Add(13);
                        tris1.Add(7); tris1.Add(15); tris1.Add(13);
                        tris1.Add(7); tris1.Add(21); tris1.Add(15);
                        tris1.Add(21); tris1.Add(23); tris1.Add(15);
                        tris1.Add(21); tris1.Add(3); tris1.Add(23);
                        tris1.Add(3); tris1.Add(11); tris1.Add(23);
                        //right
                        tris1.Add(3); tris1.Add(2); tris1.Add(11);
                        tris1.Add(2); tris1.Add(10); tris1.Add(11);
                    }
                    else{
                        tris1.Clear();
                        //front
                        tris1.Add(2); tris1.Add(6); tris1.Add(10);
                        tris1.Add(6); tris1.Add(14); tris1.Add(10);
                        tris1.Add(6); tris1.Add(4); tris1.Add(14);
                        tris1.Add(4); tris1.Add(12); tris1.Add(14);
                        tris1.Add(4); tris1.Add(0); tris1.Add(12);
                        tris1.Add(0); tris1.Add(8); tris1.Add(12);
                        //left
                        tris1.Add(0); tris1.Add(1); tris1.Add(8);
                        tris1.Add(1); tris1.Add(9); tris1.Add(8);
                        //back
                        tris1.Add(1); tris1.Add(16); tris1.Add(9);
                        tris1.Add(16); tris1.Add(18); tris1.Add(9);
                        tris1.Add(16); tris1.Add(5); tris1.Add(18);
                        tris1.Add(5); tris1.Add(13); tris1.Add(18);
                        tris1.Add(5); tris1.Add(7); tris1.Add(13);
                        tris1.Add(7); tris1.Add(15); tris1.Add(13);
                        tris1.Add(7); tris1.Add(17); tris1.Add(15);
                        tris1.Add(17); tris1.Add(19); tris1.Add(15);
                        tris1.Add(17); tris1.Add(3); tris1.Add(19);
                        tris1.Add(3); tris1.Add(11); tris1.Add(19);
                        //right
                        tris1.Add(3); tris1.Add(2); tris1.Add(11);
                        tris1.Add(2); tris1.Add(10); tris1.Add(11);
                    }
                }
            }
        }
        
        vertices = verts.ToArray();
        triangles = tris1.ToArray();
        Unwrap();

        if(mesh != null){
            data.UpdateMesh(mesh, vertices, triangles, UV);
        }
    }

    void Unwrap(){
        uvs.Clear();
        for(int i = 0; i<vertices.Length; i++){

            uvs.Add(new Vector2((vertices[i].x+vertices[i].z)*data.roofBaseHeightTS, vertices[i].y*data.roofBaseHeightTS));
        }
        UV = uvs.ToArray();
    }

}
}

