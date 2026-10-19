using UnityEngine;

//Visual only: slowly circles a camera around a point. Used for the 3D backdrops of the intro and store screens.
public class CameraOrbit : MonoBehaviour
{
    [SerializeField]
    private Vector3 target = Vector3.zero;
    [SerializeField]
    private float radius = 12f;
    [SerializeField]
    private float height = 5f;
    [SerializeField]
    private float lookHeight = 1f;
    [SerializeField]
    private float degreesPerSecond = 3f;
    [SerializeField]
    private float startAngle = 200f;
    [SerializeField]
    private float swingDegrees = 0f;//0 = full circle; otherwise swing back and forth this many degrees

    private float t;

    private void Start()
    {
        Place();
    }

    private void LateUpdate()
    {
        t += Time.deltaTime;
        Place();
    }

    private void Place()
    {
        float angle = swingDegrees > 0f
            ? startAngle + Mathf.Sin(t * degreesPerSecond * Mathf.Deg2Rad * 2f) * swingDegrees
            : startAngle + t * degreesPerSecond;
        float rad = angle * Mathf.Deg2Rad;
        transform.position = target + new Vector3(Mathf.Sin(rad) * radius, height, Mathf.Cos(rad) * radius);
        transform.LookAt(target + Vector3.up * lookHeight);
    }
}
