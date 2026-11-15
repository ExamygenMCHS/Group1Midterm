using UnityEngine;
namespace SHM{
[ExecuteInEditMode]
public class Stairs : MonoBehaviour
{
    [Tooltip("One stairstep, that is going to be duplicated and used as a base element")]
    public GameObject stairstep;
    [Tooltip("The starting position of the stair (the transform of the parent objects will affect that)")]
    public Transform start;
    [Tooltip("The ending position of the stair (the transform of the parent objects will affect that)")]
    public Transform end;
    [Tooltip("The space between two stairstep")]
    public float spacing = 1f;
    [Tooltip("The scale of each stairstep")]
    public Vector3 scale = new Vector3(1,1,1);
    [Tooltip("The rotation offset of each stairstep")]
    public Vector3 rotation = new Vector3(0,0,0);
    [Tooltip("The offset of each stairstep from the given start and end point")]
    public Vector3 offset = new Vector3(0,0,0);
    [Tooltip("The roll effect in angles (if you would want a non-linear stair)")]
    public float roll = 0f;
    [Tooltip("Unpack prefab before updating!")]
    public bool UPDATE = false;

    void Start()
    {
        if(UPDATE)
            PlaceElements();

    }

    void Update(){
        if(UPDATE){
            Clear();
            PlaceElements();
            UPDATE = false;
            
        }
    }

    public void PlaceElements(){
        Clear();
        Vector3 distanceBetween = (end.position-start.position);
        Quaternion angle = Quaternion.Euler(0, Mathf.Atan(distanceBetween.x / distanceBetween.z)*Mathf.Rad2Deg, 0);
        float length = Mathf.Sqrt(Mathf.Pow(distanceBetween.x, 2) + Mathf.Pow(distanceBetween.y, 2) + Mathf.Pow(distanceBetween.z, 2)) / spacing;
        Vector3 pos = start.position;
        for(int i=0; i<(int)length; i++){
            GameObject newGO = Instantiate(stairstep, transform.position+pos+distanceBetween/length+offset, angle*Quaternion.Euler(rotation), transform);
            newGO.transform.localScale = scale;
            newGO.transform.rotation *= Quaternion.Euler(0, roll/length*i, 0);
            pos = pos+distanceBetween/length;
        }
    }

    void Clear(){
        for(int i = 0; i<transform.childCount; i++){
            DestroyImmediate(transform.GetChild(i).gameObject);
        }
    }
}
}
