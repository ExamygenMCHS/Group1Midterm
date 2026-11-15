using UnityEngine;
namespace SHM{
public class ColorRandomizer : MonoBehaviour
{
    public Color[] colors;
    public Gradient continous;
    [Tooltip("If true this will generate color from the gradient.")]
    public bool useContinous = false;

    void Start()
    {
        ChangeColor();
    }

    void ChangeColor(){
        Material[] mats = GetComponent<Renderer>().materials;
        if(!useContinous){
            int select = Random.Range(0, colors.Length-1);
            for(int i = 0; i<mats.Length; i++){
                mats[i].color = colors[select];
            }
            //gameObject.GetComponent<MeshRenderer>().material.color = colors[select];
        }
        else{
            for(int i = 0; i<mats.Length; i++){
                mats[i].color = continous.Evaluate(Random.value);
            }
            //gameObject.GetComponent<MeshRenderer>().material.color = continous.Evaluate(Random.value);
        }
    }


}
}
