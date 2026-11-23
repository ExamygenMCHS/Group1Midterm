using UnityEngine;

//the "ghost cast": a see-through preview of where the bobber will land while the player aims with the rod. A ring on
//the water sits exactly under the mouse, a ghost bobber floats in it, and a dotted-looking arc shows the flight from
//the rod (the same curve the real bobber flies, FishingVisuals.FlightPoint). Light blue means a click casts there,
//red means the spot cannot be fished and a click does nothing (the ring then shakes).
//Visual only, never changes gameplay. It reads the FishingControl's aim every frame.
public class CastGhost : MonoBehaviour
{
    [SerializeField]
    private FishingControl fishing;
    [SerializeField]
    private ToolManager toolManager;//for the rod tip, where the arc starts
    [SerializeField]
    private Transform marker;//flat ring and disc, 1 m across, lying on the water
    [SerializeField]
    private Renderer[] tinted;//the marker and the ghost bobber, tinted with the good or the bad colour
    [SerializeField]
    private Transform ghostBobber;
    [SerializeField]
    private LineRenderer arc;

    [Header("Look")]
    [SerializeField]
    private Color goodColor = new Color(0.55f, 0.85f, 1.0f, 0.8f);
    [SerializeField]
    private Color badColor = new Color(1.0f, 0.22f, 0.2f, 0.9f);
    [SerializeField]
    private float markerSize = 0.55f;//metres across
    [SerializeField]
    private float markerLift = 0.006f;//just above the water, so it does not flicker in it
    [SerializeField]
    private float arcWidth = 0.03f;
    [SerializeField]
    private float badArcAlpha = 0.6f;//the arc to a spot that cannot be fished is fainter
    [SerializeField]
    private int arcPoints = 22;
    [SerializeField]
    private float bobberFloat = 0.03f;//the ghost bobber's centre above the water

    [Header("Motion")]
    [SerializeField]
    private float fadeSeconds = 0.12f;
    [SerializeField]
    private float colorSeconds = 0.1f;
    [SerializeField]
    private float pulseAmount = 0.06f;
    [SerializeField]
    private float pulseHz = 2.2f;
    [SerializeField]
    private float shakeSeconds = 0.25f;
    [SerializeField]
    private float shakeMetres = 0.05f;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    private MaterialPropertyBlock block;
    private Vector3[] points;
    private Gradient arcGradient;
    private GradientColorKey[] colorKeys;
    private GradientAlphaKey[] alphaKeys;
    private AnimationCurve widthCurve;
    private GameObject rodObject;
    private FishingRod rod;
    private float alpha = 0f;//fades in and out
    private float goodness = 1f;//1 = good colour, 0 = bad colour (blends between them)
    private float shakeLeft = 0f;
    private bool shown = true;//so the first frame hides everything

    private void Awake()
    {
        block = new MaterialPropertyBlock();
        points = new Vector3[Mathf.Max(2, arcPoints)];
        colorKeys = new GradientColorKey[2];
        alphaKeys = new GradientAlphaKey[3];
        arcGradient = new Gradient();
        //widest in the middle of the flight, thin at both ends
        widthCurve = new AnimationCurve(new Keyframe(0f, 0.25f), new Keyframe(0.5f, 1f), new Keyframe(1f, 0.55f));
        if (arc != null)
        {
            arc.positionCount = points.Length;
            arc.useWorldSpace = true;
            arc.widthCurve = widthCurve;
            arc.widthMultiplier = arcWidth;
        }
    }

    private void Start()
    {
        if (fishing != null)
            fishing.castRefusedEvent.AddListener(OnCastRefused);
        SetShown(false);
    }

    private void OnDestroy()
    {
        if (fishing != null)
            fishing.castRefusedEvent.RemoveListener(OnCastRefused);
    }

    //a click on a spot that cannot be fished: the ring shakes its head
    private void OnCastRefused()
    {
        shakeLeft = shakeSeconds;
    }

    private void LateUpdate()
    {
        float dt = Time.unscaledDeltaTime;
        bool visible = fishing != null && fishing.IsAimVisible();
        //the cast has started: the real bobber takes over at once
        if (fishing == null || fishing.IsLineOut())
        {
            alpha = 0f;
            shakeLeft = 0f;
            SetShown(false);
            return;
        }
        alpha = Mathf.MoveTowards(alpha, visible ? 1f : 0f, dt / Mathf.Max(0.01f, fadeSeconds));
        if (alpha <= 0f)
        {
            SetShown(false);
            return;
        }
        SetShown(true);

        bool good = fishing.IsAimGood();
        goodness = Mathf.MoveTowards(goodness, good ? 1f : 0f, dt / Mathf.Max(0.01f, colorSeconds));
        Color color = Color.Lerp(badColor, goodColor, goodness);
        color.a *= alpha;

        //glued to the aim point (no lag), only the ring's shake moves it sideways
        Vector3 aim = fishing.GetAimPoint();
        Vector3 shake = Vector3.zero;
        if (shakeLeft > 0f)
        {
            shakeLeft = Mathf.Max(0f, shakeLeft - dt);
            float k = shakeLeft / Mathf.Max(0.01f, shakeSeconds);
            shake = Vector3.right * Mathf.Sin((1f - k) * Mathf.PI * 8f) * shakeMetres * k;
        }
        float time = Time.unscaledTime;
        if (marker != null)
        {
            float pulse = 1f + pulseAmount * goodness * Mathf.Sin(time * pulseHz * Mathf.PI * 2f);
            marker.position = aim + shake + Vector3.up * markerLift;
            marker.localScale = Vector3.one * markerSize * pulse;
        }
        if (ghostBobber != null)
            ghostBobber.position = aim + shake + Vector3.up * (bobberFloat + 0.012f * Mathf.Sin(time * 3.1f));

        block.SetColor(BaseColorId, color);
        if (tinted != null)
            foreach (Renderer target in tinted)
                if (target != null)
                    target.SetPropertyBlock(block);

        DrawArc(aim, color, Mathf.Lerp(badArcAlpha, 1f, goodness));
    }

    private void DrawArc(Vector3 aim, Color color, float strength)
    {
        if (arc == null)
            return;
        Vector3 from = ArcStart();
        for (int i = 0; i < points.Length; i++)
            points[i] = FishingVisuals.FlightPoint(from, aim, i / (float)(points.Length - 1));
        arc.SetPositions(points);
        //fainter toward the rod, so it reads as the path of the bobber, not a stick in the farmer's hand; it never fades
        //out completely there, or (seen from behind) only the falling half shows and looks like a post standing in the water
        Color solid = new Color(color.r, color.g, color.b, 1f);
        colorKeys[0] = new GradientColorKey(solid, 0f);
        colorKeys[1] = new GradientColorKey(solid, 1f);
        alphaKeys[0] = new GradientAlphaKey(color.a * strength * 0.3f, 0f);
        alphaKeys[1] = new GradientAlphaKey(color.a * strength * 0.85f, 0.4f);
        alphaKeys[2] = new GradientAlphaKey(color.a * strength, 1f);
        arcGradient.SetKeys(colorKeys, alphaKeys);
        arc.colorGradient = arcGradient;
    }

    //the rod tip when the rod is in the hand, otherwise the farmer's chest
    private Vector3 ArcStart()
    {
        GameObject current = toolManager != null ? toolManager.GetCurToolObject() : null;
        if (current != rodObject)
        {
            rodObject = current;
            rod = current != null ? current.GetComponent<FishingRod>() : null;
        }
        if (rod != null && rod.isActiveAndEnabled)
            return rod.GetTipPosition();
        return fishing.transform.position + Vector3.up * 0.3f;
    }

    private void SetShown(bool on)
    {
        if (on == shown)
            return;
        shown = on;
        if (marker != null)
            marker.gameObject.SetActive(on);
        if (ghostBobber != null)
            ghostBobber.gameObject.SetActive(on);
        if (arc != null)
            arc.enabled = on;
    }
}
