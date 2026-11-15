using System.Collections.Generic;
using UnityEngine;
namespace SHM{
[ExecuteInEditMode]
[RequireComponent(typeof(MeshFilter))]
public class baseCube : MonoBehaviour
{
    //This script will generate the mesh of the base of the house
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
                rightPreCorrection = data.baseWidth / (Mathf.Tan(0.5f*data.madeAngle*Mathf.Deg2Rad));
            }
            else{
                leftPreCorrection = data.baseWidth / (Mathf.Tan(0.5f*(360-data.madeAngle)*Mathf.Deg2Rad));
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
                rightProCorrection = data.baseWidth / (Mathf.Tan(0.5f*data.prepareAngle*Mathf.Deg2Rad));
            }
            else{
                leftProCorrection = data.baseWidth / (Mathf.Tan(0.5f*(360-data.prepareAngle)*Mathf.Deg2Rad));
                rightProCorrection = 0f;
            }

        }

        ////bottom
        if(leftPreCorrection != 0 && !data.hasFront){//0
            verts.Add(new Vector3 (-data.baseWidth, 0, leftPreCorrection));
        }
        else if(!data.hasFront){
            verts.Add(new Vector3 (-data.baseWidth, 0, 0));
        }
        else{
            verts.Add(new Vector3 (-data.baseWidth, 0, -data.baseWidth));
        }

        if(leftProCorrection != 0 && !data.hasBack){//1
            verts.Add(new Vector3 (-data.baseWidth, 0, data.length-leftProCorrection));
        }
        else if(!data.hasBack){
            verts.Add(new Vector3 (-data.baseWidth, 0, data.length));
        }
        else{
            verts.Add(new Vector3 (-data.baseWidth, 0, data.length+data.baseWidth));
        }

        if(rightPreCorrection != 0 && !data.hasFront){//2
            verts.Add(new Vector3 (data.width+data.baseWidth, 0, rightPreCorrection));
        }
        else if(!data.hasFront){
            verts.Add(new Vector3 (data.width+data.baseWidth, 0, 0));
        }
        else{
            verts.Add(new Vector3 (data.width+data.baseWidth, 0, -data.baseWidth));
        }

        if(rightProCorrection != 0 && !data.hasBack){//3
            verts.Add(new Vector3 (data.width+data.baseWidth, 0, data.length-rightProCorrection));
        }
        else if(!data.hasBack){
            verts.Add(new Vector3 (data.width+data.baseWidth, 0, data.length));
        }
        else{
            verts.Add(new Vector3 (data.width+data.baseWidth, 0, data.length+data.baseWidth));
        }

        ////top
        if(leftPreCorrection != 0 && !data.hasFront){//4
            verts.Add(new Vector3 (-data.baseWidth, data.baseOutHeight, leftPreCorrection));
        }
        else if(!data.hasFront){
            verts.Add(new Vector3 (-data.baseWidth, data.baseOutHeight, 0));
        }
        else{
            verts.Add(new Vector3 (-data.baseWidth, data.baseOutHeight, -data.baseWidth));
        }

        if(leftProCorrection != 0 && !data.hasBack){//5
            verts.Add(new Vector3 (-data.baseWidth, data.baseOutHeight, data.length-leftProCorrection));
        }
        else if(!data.hasBack){
            verts.Add(new Vector3 (-data.baseWidth, data.baseOutHeight, data.length));
        }
        else{
            verts.Add(new Vector3 (-data.baseWidth, data.baseOutHeight, data.length+data.baseWidth));
        }

        if(rightPreCorrection != 0 && !data.hasFront){//6
            verts.Add(new Vector3 (data.width+data.baseWidth, data.baseOutHeight, rightPreCorrection));
        }
        else if(!data.hasFront){
            verts.Add(new Vector3 (data.width+data.baseWidth, data.baseOutHeight, 0));
        }
        else{
            verts.Add(new Vector3 (data.width+data.baseWidth, data.baseOutHeight, -data.baseWidth));
        }

        if(rightProCorrection != 0 && !data.hasBack){//7
            verts.Add(new Vector3 (data.width+data.baseWidth, data.baseOutHeight, data.length-rightProCorrection));
        }
        else if(!data.hasBack){
            verts.Add(new Vector3 (data.width+data.baseWidth, data.baseOutHeight, data.length));
        }
        else{
            verts.Add(new Vector3 (data.width+data.baseWidth, data.baseOutHeight, data.length+data.baseWidth));
        }

        verts.Add(new Vector3 (0, data.baseHeight, 0));//8
        verts.Add(new Vector3 (0, data.baseHeight, data.length));//9
        verts.Add(new Vector3 (data.width, data.baseHeight, 0));//10
        verts.Add(new Vector3 (data.width, data.baseHeight, data.length));//11

        if(rightPreCorrection != 0 || leftPreCorrection!= 0){
            verts.Add(new Vector3 (0, 0, 0));
            verts.Add(new Vector3 (0, data.baseOutHeight, 0));
            verts.Add(new Vector3 (data.width, 0, 0));
            verts.Add(new Vector3 (data.width, data.baseOutHeight, 0));
        }
        if(rightProCorrection != 0 || leftProCorrection!= 0){
            verts.Add(new Vector3 (0, 0, data.length));
            verts.Add(new Vector3 (0, data.baseOutHeight, data.length));
            verts.Add(new Vector3 (data.width, 0, data.length));
            verts.Add(new Vector3 (data.width, data.baseOutHeight, data.length));
        }

        if(rightPreCorrection == 0 && leftPreCorrection == 0 && rightProCorrection == 0 && leftProCorrection== 0){
            tris.Add(0);tris.Add(1);tris.Add(4);
            tris.Add(1);tris.Add(5);tris.Add(4);
            tris.Add(1);tris.Add(3);tris.Add(5);
            tris.Add(3);tris.Add(7);tris.Add(5);
            tris.Add(3);tris.Add(6);tris.Add(7);
            tris.Add(3);tris.Add(2);tris.Add(6);
            tris.Add(2);tris.Add(0);tris.Add(4);
            tris.Add(2);tris.Add(4);tris.Add(6);

            tris.Add(4);tris.Add(5);tris.Add(8);
            tris.Add(5);tris.Add(9);tris.Add(8);
            tris.Add(5);tris.Add(7);tris.Add(9);
            tris.Add(7);tris.Add(11);tris.Add(9);
            tris.Add(7);tris.Add(6);tris.Add(11);
            tris.Add(6);tris.Add(10);tris.Add(11);
            tris.Add(6);tris.Add(4);tris.Add(10);
            tris.Add(4);tris.Add(8);tris.Add(10);
        }
        else if(rightPreCorrection == 0 && leftPreCorrection == 0 && (rightProCorrection != 0 || leftProCorrection != 0)){
            //just pro correction
            tris.Add(0);tris.Add(1);tris.Add(4);
            tris.Add(1);tris.Add(5);tris.Add(4);
            //correction
            tris.Add(1);tris.Add(12);tris.Add(5);
            tris.Add(12);tris.Add(13);tris.Add(5);
            tris.Add(12);tris.Add(14);tris.Add(13);
            tris.Add(14);tris.Add(15);tris.Add(13);
            tris.Add(14);tris.Add(3);tris.Add(15);
            tris.Add(3);tris.Add(7);tris.Add(15);

            tris.Add(3);tris.Add(6);tris.Add(7);
            tris.Add(3);tris.Add(2);tris.Add(6);
            tris.Add(2);tris.Add(0);tris.Add(4);
            tris.Add(2);tris.Add(4);tris.Add(6);

            tris.Add(4);tris.Add(5);tris.Add(8);
            tris.Add(5);tris.Add(9);tris.Add(8);
            //correction
            tris.Add(5);tris.Add(13);tris.Add(9);
            tris.Add(7);tris.Add(11);tris.Add(15);
            tris.Add(13);tris.Add(15);tris.Add(9);
            tris.Add(15);tris.Add(11);tris.Add(9);

            tris.Add(7);tris.Add(6);tris.Add(11);
            tris.Add(6);tris.Add(10);tris.Add(11);
            tris.Add(6);tris.Add(4);tris.Add(10);
            tris.Add(4);tris.Add(8);tris.Add(10);

        }
        else if((rightPreCorrection != 0 || leftPreCorrection != 0) && rightProCorrection == 0 && leftProCorrection== 0){
            //just pre correction
            tris.Add(0);tris.Add(1);tris.Add(4);
            tris.Add(1);tris.Add(5);tris.Add(4);
            tris.Add(1);tris.Add(3);tris.Add(5);
            tris.Add(3);tris.Add(7);tris.Add(5);
            tris.Add(3);tris.Add(6);tris.Add(7);
            tris.Add(3);tris.Add(2);tris.Add(6);
            //correction
            tris.Add(12);tris.Add(0);tris.Add(4);
            tris.Add(12);tris.Add(4);tris.Add(13);
            tris.Add(2);tris.Add(14);tris.Add(15);
            tris.Add(2);tris.Add(15);tris.Add(6);
            tris.Add(14);tris.Add(12);tris.Add(13);
            tris.Add(14);tris.Add(13);tris.Add(15);

            tris.Add(4);tris.Add(5);tris.Add(8);
            tris.Add(5);tris.Add(9);tris.Add(8);
            tris.Add(5);tris.Add(7);tris.Add(9);
            tris.Add(7);tris.Add(11);tris.Add(9);
            tris.Add(7);tris.Add(6);tris.Add(11);
            tris.Add(6);tris.Add(10);tris.Add(11);
            //correction
            tris.Add(4);tris.Add(8);tris.Add(13);
            tris.Add(15);tris.Add(13);tris.Add(8);
            tris.Add(15);tris.Add(8);tris.Add(10);
            tris.Add(6);tris.Add(15);tris.Add(10);
        }
        else{
            //pro and pre correction
            //just pre correction
            tris.Add(0);tris.Add(1);tris.Add(4);
            tris.Add(1);tris.Add(5);tris.Add(4);
            ////correction
            tris.Add(1);tris.Add(16);tris.Add(5);
            tris.Add(16);tris.Add(17);tris.Add(5);
            tris.Add(16);tris.Add(18);tris.Add(17);
            tris.Add(18);tris.Add(19);tris.Add(17);
            tris.Add(18);tris.Add(3);tris.Add(19);
            tris.Add(3);tris.Add(7);tris.Add(19);

            tris.Add(3);tris.Add(6);tris.Add(7);
            tris.Add(3);tris.Add(2);tris.Add(6);
            //correction
            tris.Add(12);tris.Add(0);tris.Add(4);
            tris.Add(12);tris.Add(4);tris.Add(13);
            tris.Add(2);tris.Add(14);tris.Add(15);
            tris.Add(2);tris.Add(15);tris.Add(6);
            tris.Add(14);tris.Add(12);tris.Add(13);
            tris.Add(14);tris.Add(13);tris.Add(15);

            tris.Add(4);tris.Add(5);tris.Add(8);
            tris.Add(5);tris.Add(9);tris.Add(8);
            ////correction
            tris.Add(5);tris.Add(17);tris.Add(9);
            tris.Add(7);tris.Add(11);tris.Add(19);
            tris.Add(17);tris.Add(19);tris.Add(9);
            tris.Add(19);tris.Add(11);tris.Add(9);


            tris.Add(7);tris.Add(6);tris.Add(11);
            tris.Add(6);tris.Add(10);tris.Add(11);
            //correction
            tris.Add(4);tris.Add(8);tris.Add(13);
            tris.Add(15);tris.Add(13);tris.Add(8);
            tris.Add(15);tris.Add(8);tris.Add(10);
            tris.Add(6);tris.Add(15);tris.Add(10);
        }
        

        
        for(int i = 0; i<verts.Count; i++){
            if(7<i && i<12){//8, 9, 10, 11 (the inner four)
                uvs.Add(new Vector2(uvs[i-4].x, (verts[i].y+data.baseWidth)*data.baseTS));
            }
            else{
                uvs.Add(new Vector2((verts[i].x+verts[i].z)*data.baseTS, verts[i].y*data.baseTS));
            }
        }

        triangles = tris.ToArray();
        vertices = verts.ToArray();
        UV = uvs.ToArray();

        if(mesh != null){
            data.UpdateMesh(mesh, vertices, triangles, UV);
        }
    }
}
}
