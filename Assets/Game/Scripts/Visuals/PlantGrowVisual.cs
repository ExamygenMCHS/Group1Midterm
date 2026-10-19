using UnityEngine;

//Visual only: put on each plant stage model. The model pops up when its stage starts
//and then sways a little in the wind. Plant.cs still decides which stage is shown.
public class PlantGrowVisual : MonoBehaviour
{
    [SerializeField]
    private float popSeconds = 0.6f;
    [SerializeField]
    private float swayDegrees = 3f;
    [SerializeField]
    private float swaySpeed = 1.6f;

    private float age;
    private float seed;
    private Vector3 baseScale = Vector3.one;
    private bool scaleKnown = false;

    private void OnEnable()
    {
        if (!scaleKnown)
        {
            baseScale = transform.localScale;
            scaleKnown = true;
        }
        age = 0f;
        seed = (transform.position.x * 1.7f + transform.position.z * 2.3f) % 6.28f;
        transform.localScale = baseScale * 0.2f;
    }

    private void OnDisable()
    {
        transform.localScale = baseScale;
        transform.localRotation = Quaternion.identity;
    }

    private void Update()
    {
        age += Time.deltaTime;
        //ease-out-back: grows slightly past full size, then settles
        float k = Mathf.Clamp01(age / popSeconds);
        float c = 1.9f;
        float pop = 1f + (c + 1f) * Mathf.Pow(k - 1f, 3f) + c * Mathf.Pow(k - 1f, 2f);
        transform.localScale = baseScale * Mathf.Lerp(0.2f, 1f, pop);

        float t = Time.time * swaySpeed + seed;
        transform.localRotation = Quaternion.Euler(Mathf.Sin(t) * swayDegrees, 0f, Mathf.Sin(t * 0.77f + 1.3f) * swayDegrees * 0.7f);
    }
}
