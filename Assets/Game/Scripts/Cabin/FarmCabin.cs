using System.Collections.Generic;
using UnityEngine;

//the log cabin where the farmer sleeps and keeps the storage chest.
//The farm camera looks down from the south, so once the farmer steps inside, the roof and the front (south) wall
//would hide them: those parts disappear while the farmer is indoors and come back when they leave.
//The front door swings open when the farmer comes close and closes again behind them.
public class FarmCabin : MonoBehaviour
{
    [Tooltip("Hidden while the player is inside (the roof, the front wall and everything above the eaves)")]
    [SerializeField]
    private Transform[] hideWhenInside;
    [Tooltip("The inside of the cabin, in the cabin's local space")]
    [SerializeField]
    private Bounds interior = new Bounds(Vector3.zero, Vector3.one);
    [Tooltip("Height of the walls (cabin local space): weather below this, inside the walls, is kept out")]
    [SerializeField]
    private float wallHeight = 3.15f;

    [Header("Door")]
    [SerializeField]
    private Transform doorHinge;//turns around its local Y axis
    [SerializeField]
    private float doorOpenAngle = -105f;
    [SerializeField]
    private float doorOpenDistance = 1.6f;
    [SerializeField]
    private float doorSpeed = 300f;//degrees per second

    private readonly List<Renderer> hidden = new List<Renderer>();
    private Transform player;
    private bool inside;
    private float doorAngle;

    public bool PlayerInside { get { return inside; } }
    public Bounds Interior { get { return interior; } }
    public Transform DoorHinge { get { return doorHinge; } }

    private void Awake()
    {
        if (hideWhenInside != null)
            foreach (Transform part in hideWhenInside)
                if (part != null)
                    hidden.AddRange(part.GetComponentsInChildren<Renderer>(true));
    }

    private void OnDisable()
    {
        SetInside(false);
    }

    private void Update()
    {
        if (player == null)
        {
            PlayerMove move = FindAnyObjectByType<PlayerMove>();
            if (move == null)
                return;
            player = move.transform;
        }
        SetInside(Contains(player.position));

        if (doorHinge == null)
            return;
        Vector3 offset = player.position - doorHinge.position;
        offset.y = 0f;
        bool near = offset.magnitude < doorOpenDistance;
        doorAngle = Mathf.MoveTowards(doorAngle, near ? doorOpenAngle : 0f, doorSpeed * Time.deltaTime);
        doorHinge.localRotation = Quaternion.Euler(0f, doorAngle, 0f);
    }

    //true when a world position is inside the walls (height does not matter)
    public bool Contains(Vector3 worldPosition)
    {
        Vector3 local = transform.InverseTransformPoint(worldPosition);
        local.y = interior.center.y;
        return interior.Contains(local);
    }

    //true when a world position is indoors: inside the walls and below the top of them (weather stays out of here)
    public bool IsSheltered(Vector3 worldPosition)
    {
        Vector3 local = transform.InverseTransformPoint(worldPosition);
        if (local.y > wallHeight)
            return false;
        local.y = interior.center.y;
        return interior.Contains(local);
    }

    private void SetInside(bool value)
    {
        if (value == inside)
            return;
        inside = value;
        foreach (Renderer part in hidden)
            if (part != null)
                part.enabled = !inside;
    }
}
