using UnityEngine;

//a tool lying in the world (the hoe leaning on the barrel, the watering can by the well, the sickle by the
//fence). Standing next to it shows "E  Pick up the hoe"; E puts it into the hotbar (the Farm Fund's inventory) and
//into the player's hand. A tool the player already owns (picked up in an earlier level) is not placed again.
//A bobbing arrow above it shows where it is until it is picked up.
public class ToolPickup : WorldInteractable
{
    [SerializeField]
    private ToolManager.ToolType tool = ToolManager.ToolType.Hoe;
    [SerializeField]
    [Tooltip("Where to find it, completes \"Pick up the hoe ...\" and \"Find it ...\"")]
    private string whereText = "leaning on the barrel to your left";
    [SerializeField]
    [Tooltip("What the tool does, shown after it is picked up")]
    private string usageText = "Space on grass tills it.";
    [SerializeField]
    private float reach = 1.3f;//metres from this object's position (on the ground)
    [SerializeField]
    private Transform model;//shrinks away when the tool is picked up
    [SerializeField]
    private Transform marker;//arrow above the tool
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
    private float takenTime = -1f;//>= 0 after the tool was picked up (the model shrinks away)
    private Transform cameraTransform;

    private void Start()
    {
        //the player brought this tool from an earlier level: it is not lying here any more
        if (FarmFund.Instance != null && FarmFund.Instance.Inventory.HasTool(tool))
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

    //the pickup of a tool that is still lying somewhere in this level (null if there is none)
    public static ToolPickup Find(ToolManager.ToolType tool)
    {
        foreach (WorldInteractable thing in All)
        {
            ToolPickup pickup = thing as ToolPickup;
            if (pickup != null && pickup.tool == tool && pickup.takenTime < 0f)
                return pickup;
        }
        return null;
    }

    //"Pick up the hoe leaning on the barrel to your left"
    public string GetGuideText()
    {
        return "Pick up the " + Hotbar.ItemName(tool) + " " + whereText;
    }

    //"Find it leaning on the barrel to your left."
    public string GetWhereText()
    {
        return "Find it " + whereText + ".";
    }

    public bool IsTaken()
    {
        return takenTime >= 0f;
    }

    public override float DistanceOutsideReach(Vector3 playerPosition)
    {
        Vector3 offset = playerPosition - transform.position;
        offset.y = 0f;
        return offset.magnitude - reach;
    }

    public override string GetPrompt(WorldInteractor player)
    {
        return IsTaken() ? null : "Pick up the " + Hotbar.ItemName(tool);
    }

    public override void Interact(WorldInteractor player)
    {
        if (IsTaken() || FarmFund.Instance == null)
            return;
        takenTime = 0f;
        FarmFund.Instance.Inventory.AddTool(tool);//the hotbar puts it into its slot
        Hotbar hotbar = player.GetHotbar();
        if (hotbar != null)
        {
            hotbar.SelectItem(tool);//straight into the hand
            int slot = hotbar.SlotOf(tool) + 1;
            hotbar.Tell("You picked up the " + Hotbar.ItemName(tool) + " (slot " + Hotbar.KeyTag(slot.ToString()) + ")! " + usageText);
        }
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
        //the arrow bobs above the tool (faster and bigger while the player is in reach) and always faces the camera
        float speed = targeted ? 6f : 3f;
        marker.localPosition = markerHome + Vector3.up * Mathf.Sin(Time.time * speed) * markerBob;
        markerGrow = Mathf.Lerp(markerGrow, targeted ? 1.25f : 1f, 1f - Mathf.Exp(-10f * dt));
        marker.localScale = markerScale * markerGrow;
        if (cameraTransform != null)
            marker.rotation = Quaternion.LookRotation(marker.position - cameraTransform.position, Vector3.up);
    }
}
