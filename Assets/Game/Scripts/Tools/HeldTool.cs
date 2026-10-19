using UnityEngine;

//sits on the empty parent of every tool prefab. The ToolManager shows and hides the tool through it:
//the tool grows into the hand with a small overshoot, and shrinks away when it is put down.
//It can also run the tool's own effect, like the water pouring out of the watering can.
//the farmer now also carries the tool in the hotbar while walking around; a long tool is then held upright
//(carryRotation) and turns into its working grip when an action starts.
public class HeldTool : MonoBehaviour
{
    public ToolManager.ToolType toolType;
    [SerializeField]
    private ParticleSystem useEffect;//optional: water from the can's rose, seeds from the bag
    [SerializeField]
    private float popSeconds = 0.1f;
    [SerializeField]
    private Vector3 carryRotation = Vector3.zero;//the tool's rotation in the hand while it is only carried
    [SerializeField]
    private float carryBlendSeconds = 0.12f;

    private float showTime = 0f;
    private float hideTime = -1f;//>= 0 while shrinking away
    private bool carried = false;
    private float carry = 0f;//0 = working grip, 1 = carried

    public void Show()
    {
        gameObject.SetActive(true);
        showTime = 0f;
        hideTime = -1f;
        transform.localScale = Vector3.one * 0.01f;
    }

    public void PutAway()
    {
        SetEffectPlaying(false);
        if (!gameObject.activeInHierarchy)
        {
            gameObject.SetActive(false);
            return;
        }
        hideTime = 0f;
    }

    //carried (idle, walking) or in use (an action)
    public void SetCarried(bool on)
    {
        carried = on;
        if (on)
            SetEffectPlaying(false);//the can stays in the hand after watering: no more water
    }

    public void SetEffectPlaying(bool on)
    {
        if (useEffect == null || useEffect.isEmitting == on)
            return;
        if (on)
            useEffect.Play();
        else
            useEffect.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }

    public void EmitEffect(int count)
    {
        if (useEffect != null)
            useEffect.Emit(count);
    }

    private void Update()
    {
        float pop = Mathf.Max(0.01f, popSeconds);
        //blend between the carry pose and the working grip (no blend while the tool pops into the hand)
        float carryTarget = carried ? 1f : 0f;
        carry = showTime < pop ? carryTarget : Mathf.MoveTowards(carry, carryTarget, Time.deltaTime / Mathf.Max(0.01f, carryBlendSeconds));
        transform.localRotation = Quaternion.Slerp(Quaternion.identity, Quaternion.Euler(carryRotation), Mathf.SmoothStep(0f, 1f, carry));

        if (hideTime >= 0f)
        {
            hideTime += Time.deltaTime;
            float k = Mathf.Clamp01(1f - hideTime / pop);
            transform.localScale = Vector3.one * Mathf.Max(0.01f, k);
            if (k <= 0f)
            {
                hideTime = -1f;
                transform.localScale = Vector3.one;
                gameObject.SetActive(false);
            }
            return;
        }
        if (showTime < pop)
        {
            showTime += Time.deltaTime;
            float t = Mathf.Clamp01(showTime / pop);
            //ease out with a little overshoot
            float scale = 1f + 2.2f * Mathf.Pow(t - 1f, 3f) + 1.2f * Mathf.Pow(t - 1f, 2f);
            transform.localScale = Vector3.one * Mathf.Max(0.01f, scale);
        }
    }
}
