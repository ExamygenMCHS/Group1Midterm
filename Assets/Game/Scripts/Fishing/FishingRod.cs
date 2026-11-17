using UnityEngine;

//sits on the fishing rod prefab next to its HeldTool. Visual only: the rod is built from a few sections, each a child of
//the one below, and bends by turning every section a little (more toward the tip). The fishing line starts at the tip.
public class FishingRod : MonoBehaviour
{
    [SerializeField]
    private Transform[] sections;//from the handle up
    [SerializeField]
    private Transform tip;//the line leaves the rod here
    [SerializeField]
    private float bendSpeed = 16f;

    private float bend = 0f;//degrees, the whole rod
    private float targetBend = 0f;

    public Vector3 GetTipPosition()
    {
        return tip != null ? tip.position : transform.position;
    }

    //how far the whole rod bends toward the line, in degrees
    public void SetBend(float degrees)
    {
        targetBend = degrees;
    }

    private void OnDisable()
    {
        bend = targetBend = 0f;
        Apply();
    }

    private void LateUpdate()
    {
        bend = Mathf.Lerp(bend, targetBend, 1f - Mathf.Exp(-bendSpeed * Time.deltaTime));
        Apply();
    }

    private void Apply()
    {
        if (sections == null || sections.Length == 0)
            return;
        //the thin sections near the tip bend more than the thick ones at the handle
        float shares = sections.Length * (sections.Length + 1) * 0.5f;
        for (int i = 0; i < sections.Length; i++)
            if (sections[i] != null)
                sections[i].localRotation = Quaternion.Euler(bend * (i + 1) / shares, 0f, 0f);
    }
}
