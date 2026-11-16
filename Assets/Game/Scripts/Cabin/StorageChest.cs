using UnityEngine;

//the storage chest in the cabin. E opens its window (ChestPanel) and the lid swings up while it is open.
//What is put in the chest stays in the Farm Fund's inventory (FarmInventory.chest), apart from the bag, so it is
//safe from the trader's store and the wholesale market, and is still there after a trip away from the farm.
public class StorageChest : WorldInteractable
{
    [SerializeField, Min(0.1f)]
    private float reach = 1.4f;
    [SerializeField]
    private string promptText = "Open the chest";
    [SerializeField]
    private Transform lid;//turns around its local X axis (the hinge at the back)
    [SerializeField]
    private float openAngle = -70f;
    [SerializeField]
    private float lidSpeed = 360f;//degrees per second
    [SerializeField]
    private AudioSource chestAudio;
    [SerializeField]
    private AudioClip openSound;
    [SerializeField]
    private AudioClip closeSound;
    [SerializeField]
    private ChestPanel.Style windowStyle = new ChestPanel.Style();

    private ChestPanel window;
    private float lidAngle;
    private bool wasOpen;

    public bool IsOpen { get { return window != null && window.IsOpen && window.Owner == this; } }
    public ChestPanel Window { get { return window; } }

    public override float DistanceOutsideReach(Vector3 playerPosition)
    {
        Vector3 offset = playerPosition - transform.position;
        offset.y = 0f;
        return offset.magnitude - Mathf.Max(0.1f, reach);
    }

    public override string GetPrompt(WorldInteractor player)
    {
        return FarmFund.Instance != null && !ChestPanel.IsAnyOpen && !TraderShopPanel.IsAnyOpen ? promptText : null;
    }

    public override void Interact(WorldInteractor player)
    {
        Open(player);
    }

    public bool Open(WorldInteractor player)
    {
        if (window == null)
            window = ChestPanel.Create(transform, windowStyle);
        return window.TryOpen(player, this);
    }

    private void Update()
    {
        bool open = IsOpen;
        if (open != wasOpen)
        {
            wasOpen = open;
            AudioClip clip = open ? openSound : closeSound;
            if (chestAudio != null && clip != null)
                chestAudio.PlayOneShot(clip);
        }
        if (lid == null)
            return;
        float target = open ? openAngle : 0f;
        if (Mathf.Approximately(lidAngle, target))
            return;
        lidAngle = Mathf.MoveTowards(lidAngle, target, lidSpeed * Time.unscaledDeltaTime);
        lid.localRotation = Quaternion.Euler(lidAngle, 0f, 0f);
    }
}
