using System.Collections.Generic;
using UnityEngine;

namespace SHM{
[ExecuteInEditMode]
public class TextureFlip : MonoBehaviour
{
    Vector2[] uvsOld;
    List<Vector2> uvsNew = new List<Vector2>();
    Mesh mesh;
    
    public bool flip = true;
    bool flipped = false;

    [Tooltip("The scale to resize the texture")]
    public Vector2 TextureScale = new Vector2(1,1);
    [Tooltip("The offset to move the texture")]
    public Vector2 TextureOffset = new Vector2(1,1);

    Vector2 sample1 = new Vector2(1,1);
    Vector2 sample2 = new Vector2(1,1);

    void Start()
    {
        mesh = gameObject.GetComponent<MeshFilter>().sharedMesh;

        Flip();
        flipped = true;
    }

    void Update(){
        if(Application.isEditor){//remove this statement if you also want to update it in playmode
            if(!flipped && flip){
                Flip();
                flipped = true;
            }
            else if(flipped && !flip){
                Flip();
                flipped = false;
            }
            if(sample1 != TextureScale){
                ScaleUV();
                sample1 = TextureScale;
            }
            if(sample2 != TextureOffset){
                OffsetUV();
                sample2 = TextureOffset;
            }
        }
    }

    void Flip(){
        uvsNew.Clear();
        mesh = GetComponent<MeshFilter>().sharedMesh;
        uvsOld = mesh.uv;
        for(int i = 0; i < uvsOld.Length; i++){
            uvsNew.Add(new Vector2(uvsOld[i].y, uvsOld[i].x));
        }
        Vector3[] verts = mesh.vertices;
        int[] tris = mesh.triangles;

        mesh.Clear();
        mesh.vertices = verts;
        mesh.triangles = tris;
        mesh.uv = uvsNew.ToArray();
        mesh.RecalculateNormals();
    }

    void ScaleUV(){
        uvsNew.Clear();
        mesh = GetComponent<MeshFilter>().sharedMesh;
        uvsOld = mesh.uv;
        for(int i = 0; i < uvsOld.Length; i++){
            uvsNew.Add(new Vector2(uvsOld[i].x * TextureScale.x/sample1.x, uvsOld[i].y * TextureScale.y/sample1.y));
        }
        Vector3[] verts = mesh.vertices;
        int[] tris = mesh.triangles;

        mesh.Clear();
        mesh.vertices = verts;
        mesh.triangles = tris;
        mesh.uv = uvsNew.ToArray();
        mesh.RecalculateNormals();
    }

    void OffsetUV(){
        uvsNew.Clear();
        mesh = GetComponent<MeshFilter>().sharedMesh;
        uvsOld = mesh.uv;
        for(int i = 0; i < uvsOld.Length; i++){
            uvsNew.Add(new Vector2(uvsOld[i].x + TextureOffset.x-sample2.x, uvsOld[i].y + TextureOffset.y-sample2.y));
        }
        Vector3[] verts = mesh.vertices;
        int[] tris = mesh.triangles;

        mesh.Clear();
        mesh.vertices = verts;
        mesh.triangles = tris;
        mesh.uv = uvsNew.ToArray();
        mesh.RecalculateNormals();
    }
}
}
