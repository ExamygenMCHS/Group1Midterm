using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

//the part of the pond a fishing line can be cast into. The water's outline is a polygon on the ground (baked from the
//pond's water mesh by the FishingBuilder); a cast is only legal inside it and at least Shore Margin away from its
//edge, so nobody fishes in the sand of the bank. The dark middle of the pond counts as deep water, and the lily pads
//as cover (some fish like those spots better). In winter the pond freezes and only the fishing hole in the ice is open.
public class FishingWater : MonoBehaviour
{
    public enum CastCheck //why a spot can or cannot be fished
    {
        Ok, NotWater, TooCloseToShore, Ice
    }

    [Header("Outlines: points on the ground, in this object's local x and z")]
    [SerializeField]
    private Vector2[] shoreOutline;
    [SerializeField]
    private Vector2[] deepOutline;
    [SerializeField]
    private float shoreMargin = 0.4f;//metres a cast must stay away from the bank
    [SerializeField]
    private float surfaceHeight = 0.05f;//height of the water above this object

    [Header("Cover")]
    [SerializeField]
    private Transform[] lilyPads;
    [SerializeField]
    private float coverRadius = 0.7f;

    [Header("Winter (optional)")]
    [SerializeField]
    private GameCalendar calendar;
    [SerializeField]
    private Transform iceHole;//the fishing hole that opens in the ice
    [SerializeField]
    private float iceHoleRadius = 0.4f;
    [SerializeField]
    private float iceHoleSnap = 1.3f;//the aim jumps to the hole when the mouse is this close to it
    [SerializeField]
    private float iceHoleSurface = 0.075f;//height of the open water in the hole (it sits a little above the summer water)

    //this event gets fired when the pond freezes over or thaws (a line that is out has to come in)
    public UnityEvent frozenChangedEvent = new UnityEvent();

    private static readonly List<FishingWater> all = new List<FishingWater>();
    public static IReadOnlyList<FishingWater> All { get { return all; } }

    private bool frozen;

    //Play Mode in this project starts without a domain reload, so the list from the last play session must be cleared
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        all.Clear();
    }

    private void OnEnable()
    {
        all.Add(this);
    }

    private void OnDisable()
    {
        all.Remove(this);
    }

    private void Start()
    {
        if (calendar == null)
            calendar = FindFirstObjectByType<GameCalendar>();//once; not every scene has a calendar
        if (calendar != null)
        {
            calendar.newSeasonEvent.AddListener(CheckSeason);
            frozen = calendar.IsWinter();
        }
    }

    private void OnDestroy()
    {
        if (calendar != null)
            calendar.newSeasonEvent.RemoveListener(CheckSeason);
    }

    //called when a new season starts: open water in the warm seasons, ice with a fishing hole in winter
    private void CheckSeason()
    {
        bool freeze = calendar.IsWinter();
        if (freeze == frozen)
            return;
        frozen = freeze;
        frozenChangedEvent.Invoke();//make announcement to all listeners
    }

    // ---------------------------------------------------------------- API for the FishingControl

    public bool IsFrozen()
    {
        return frozen;
    }

    //the water surface at a point on the ground
    public Vector3 OnSurface(Vector3 point)
    {
        return new Vector3(point.x, GetSurfaceY(), point.z);
    }

    public float GetSurfaceY()
    {
        //in winter the only water is in the hole, whose disc is a little higher: the bobber and its rings float on that
        return transform.position.y + (frozen && iceHole != null ? iceHoleSurface : surfaceHeight);
    }

    public float GetShoreMargin()
    {
        return shoreMargin;
    }

    //can a line be cast to this spot?
    public CastCheck Check(Vector3 point)
    {
        if (frozen)
        {
            if (iceHole != null && Flat(point - iceHole.position).magnitude <= iceHoleRadius)
                return CastCheck.Ok;
            return Contains(shoreOutline, ToLocal(point)) ? CastCheck.Ice : CastCheck.NotWater;
        }
        Vector2 local = ToLocal(point);
        if (!Contains(shoreOutline, local))
            return CastCheck.NotWater;
        return DistanceToEdge(shoreOutline, local) < shoreMargin ? CastCheck.TooCloseToShore : CastCheck.Ok;
    }

    //where the aim goes for a mouse position: in winter it jumps to the fishing hole when the mouse is close to it
    public Vector3 SnapAim(Vector3 point)
    {
        if (frozen && iceHole != null && Flat(point - iceHole.position).magnitude <= iceHoleSnap)
            return OnSurface(iceHole.position);
        return OnSurface(point);
    }

    //metres on the ground from a position to the open water (0 when standing over it)
    public float DistanceToWater(Vector3 position)
    {
        if (frozen && iceHole != null)
            return Mathf.Max(0f, Flat(position - iceHole.position).magnitude - iceHoleRadius);
        Vector2 local = ToLocal(position);
        return Contains(shoreOutline, local) ? 0f : DistanceToEdge(shoreOutline, local);
    }

    public bool IsDeep(Vector3 point)
    {
        return deepOutline != null && deepOutline.Length >= 3 && Contains(deepOutline, ToLocal(point));
    }

    //true next to a lily pad that is out this season
    public bool IsNearCover(Vector3 point)
    {
        if (lilyPads == null)
            return false;
        foreach (Transform pad in lilyPads)
            if (pad != null && pad.gameObject.activeInHierarchy && Flat(point - pad.position).magnitude <= coverRadius)
                return true;
        return false;
    }

    //a spot in the open water about 'distance' metres from 'around' (where a fish shows up before it swims to the bait).
    //'turn' from 0 to 1 picks the direction; directions that leave the water are pulled back toward the bait.
    public Vector3 PointAround(Vector3 around, float distance, float turn)
    {
        if (frozen)//under the ice the fish comes straight up to the hole
            return OnSurface(around);
        for (int attempt = 0; attempt < 8; attempt++)
        {
            float angle = (turn + attempt * 0.381966f) * Mathf.PI * 2f;//golden-ratio steps: the tries spread around the bait
            for (float reach = distance; reach > 0.3f; reach *= 0.7f)
            {
                Vector3 candidate = around + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * reach;
                Vector2 local = ToLocal(candidate);
                if (Contains(shoreOutline, local) && DistanceToEdge(shoreOutline, local) >= shoreMargin * 0.5f)
                    return OnSurface(candidate);
            }
        }
        return OnSurface(around);
    }

    //keeps a point over the water, at least 'margin' metres from the bank (a hooked fish cannot swim onto the grass)
    public Vector3 KeepInWater(Vector3 point, Vector3 fallback, float margin)
    {
        if (frozen)
            return OnSurface(fallback);
        Vector2 local = ToLocal(point);
        if (Contains(shoreOutline, local) && DistanceToEdge(shoreOutline, local) >= margin)
            return OnSurface(point);
        //walk back toward the fallback (a legal spot) until the point is in the water again
        for (int step = 1; step <= 8; step++)
        {
            Vector3 candidate = Vector3.Lerp(point, fallback, step / 8f);
            local = ToLocal(candidate);
            if (Contains(shoreOutline, local) && DistanceToEdge(shoreOutline, local) >= margin)
                return OnSurface(candidate);
        }
        return OnSurface(fallback);
    }

    // ---------------------------------------------------------------- polygon helpers

    private Vector2 ToLocal(Vector3 world)
    {
        Vector3 local = transform.InverseTransformPoint(world);
        return new Vector2(local.x, local.z);
    }

    private static Vector3 Flat(Vector3 v)
    {
        return new Vector3(v.x, 0f, v.z);
    }

    //even-odd rule: a ray from the point crosses the outline an odd number of times when the point is inside
    private static bool Contains(Vector2[] outline, Vector2 p)
    {
        if (outline == null || outline.Length < 3)
            return false;
        bool inside = false;
        for (int i = 0, j = outline.Length - 1; i < outline.Length; j = i++)
        {
            Vector2 a = outline[i], b = outline[j];
            if ((a.y > p.y) != (b.y > p.y) && p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x)
                inside = !inside;
        }
        return inside;
    }

    private static float DistanceToEdge(Vector2[] outline, Vector2 p)
    {
        float best = float.MaxValue;
        for (int i = 0, j = outline.Length - 1; i < outline.Length; j = i++)
        {
            Vector2 a = outline[j], b = outline[i];
            Vector2 ab = b - a;
            float t = ab.sqrMagnitude > 1e-8f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
            best = Mathf.Min(best, (p - (a + ab * t)).magnitude);
        }
        return best;
    }

    private void OnDrawGizmosSelected()
    {
        DrawOutline(shoreOutline, new Color(0.3f, 0.7f, 1f));
        DrawOutline(deepOutline, new Color(0.1f, 0.3f, 0.8f));
    }

    private void DrawOutline(Vector2[] outline, Color color)
    {
        if (outline == null || outline.Length < 2)
            return;
        Gizmos.color = color;
        for (int i = 0, j = outline.Length - 1; i < outline.Length; j = i++)
            Gizmos.DrawLine(transform.TransformPoint(new Vector3(outline[j].x, surfaceHeight, outline[j].y)),
                transform.TransformPoint(new Vector3(outline[i].x, surfaceHeight, outline[i].y)));
    }
}
