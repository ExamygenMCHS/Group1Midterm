using UnityEngine;

//a bait lying in the world, free, once per game (the old lure on the grass next to the fishing rod). Standing next to
//it shows "E  Pick up the old lure"; E puts it into the bag like any item (it is not loaded into the rod: the player
//drags it onto the rod). The Farm Fund's inventory remembers that it was taken, so it is not lying here again after a
//scene load. Like the tool pickups, an arrow bobs above it until it is picked up.
public class BaitPickup : WorldInteractable
{
    [SerializeField]
    private BaitData bait;
    [Tooltip("Remembered once taken, so the bait is only ever picked up once in a game")]
    [SerializeField]
    private string pickupId = "Old Lure by the pond";
    [SerializeField]
    private string promptText = "Pick up the old lure";
    [SerializeField]
    private string pickedUpText = "You picked up an old lure! Drag it onto the fishing rod.";
    [SerializeField]
    private float reach = 1.1f;//metres from this object's position (on the ground)
    [SerializeField]
    private Transform model;//shrinks away when the bait is picked up
    [SerializeField]
    private Transform marker;//arrow above the bait
    [SerializeField]
    private float markerBob = 0.12f;
    [SerializeField]
    private AudioSource pickupSound;

    private Vector3 markerHome;
    private Vector3 markerScale = Vector3.one;
    private float markerGrow = 1f;
    private Vector3 modelHome;
    private Vector3 modelScale = Vector3.one;
    private bool targeted = false;
    private float takenTime = -1f;//>= 0 after the bait was picked up (the model shrinks away)
    private Transform cameraTransform;

    private void Start()
    {
        //taken earlier in this game: it is not lying here any more
        if (FarmFund.Instance != null && FarmFund.Instance.Inventory.IsPickupTaken(pickupId))
        {
            gameObject.SetActive(false);
            return;
        }
        if (marker != null)
        {
            markerHome = marker.localPosition;
            markerScale = marker.localScale;
        }
        if (model != null)
        {
            modelHome = model.localPosition;
            modelScale = model.localScale;
        }
        if (Camera.main != null)
            cameraTransform = Camera.main.transform;
    }

    public bool IsTaken()
    {
        return takenTime >= 0f;
    }

    public BaitData GetBait()
    {
        return bait;
    }

    public override float DistanceOutsideReach(Vector3 playerPosition)
    {
        Vector3 offset = playerPosition - transform.position;
        offset.y = 0f;
        return offset.magnitude - reach;
    }

    public override string GetPrompt(WorldInteractor player)
    {
        return IsTaken() ? null : promptText;
    }

    public override void Interact(WorldInteractor player)
    {
        if (IsTaken() || FarmFund.Instance == null || bait == null)
            return;
        FarmInventory inventory = FarmFund.Instance.Inventory;
        if (inventory.IsPickupTaken(pickupId))
            return;
        takenTime = 0f;
        inventory.MarkPickupTaken(pickupId);
        inventory.AddBait(bait, 1);//into the bag like any item
        Hotbar hotbar = player.GetHotbar();
        if (hotbar != null)
            hotbar.Tell(pickedUpText);
        if (pickupSound != null)
            pickupSound.Play();
        if (marker != null)
            marker.gameObject.SetActive(false);
    }

    public override void SetTargeted(bool on)
    {
        targeted = on;
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        if (takenTime >= 0f)
        {
            //hop up a little and shrink away, then the object is gone
            takenTime += dt;
            float k = Mathf.Clamp01(takenTime / 0.25f);
            if (model != null)
            {
                model.localScale = modelScale * (1f - k);
                model.localPosition = modelHome + Vector3.up * Mathf.Sin(k * Mathf.PI) * 0.25f;
            }
            if (k >= 1f && (pickupSound == null || !pickupSound.isPlaying))
                Destroy(gameObject);
            return;
        }
        if (marker == null)
            return;
        //the arrow bobs above the bait (faster and bigger while the player is in reach) and always faces the camera
        float speed = targeted ? 6f : 3f;
        marker.localPosition = markerHome + Vector3.up * Mathf.Sin(Time.time * speed) * markerBob;
        markerGrow = Mathf.Lerp(markerGrow, targeted ? 1.25f : 1f, 1f - Mathf.Exp(-10f * dt));
        marker.localScale = markerScale * markerGrow;
        if (cameraTransform != null)
            marker.rotation = Quaternion.LookRotation(marker.position - cameraTransform.position, Vector3.up);
    }
}
