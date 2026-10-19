using UnityEngine;
using UnityEngine.InputSystem;

//sits on the player. Every frame it looks for the closest WorldInteractable in reach (a tool on the ground,
//the well, the pond), shows its popup prompt above the hotbar, and uses it when E is pressed.
//E also plants and harvests (PlantManager); when a prompt is up, E belongs to the prompt.
[DefaultExecutionOrder(-10)]//decides who gets E before the PlantManager reads it
public class WorldInteractor : MonoBehaviour
{
    [SerializeField]
    private Hotbar hotbar;
    [SerializeField]
    private WateringCanTank wateringCanTank;
    [SerializeField]
    private PlayerStateControl playerStateControl;
    [SerializeField]
    private InteractionPrompt prompt;
    [SerializeField]
    private Key interactKey = Key.E;

    private WorldInteractable target;
    private int usedFrame = -1;

    private void Update()
    {
        WorldInteractable closest = FindClosest();
        if (closest != target)
        {
            if (target != null)
                target.SetTargeted(false);
            target = closest;
            if (target != null)
                target.SetTargeted(true);
        }

        string text = target != null && CanAct() ? target.GetPrompt(this) : null;
        bool usable = text != null && target.CanInteract(this);
        if (prompt != null)
        {
            if (text != null)
                prompt.Show(text, usable);
            else
                prompt.Hide();
        }
        if (usable && Keyboard.current != null && Keyboard.current[interactKey].wasPressedThisFrame)
            Interact();
    }

    //uses the object in reach (what E does); returns false when there is nothing to use
    public bool Interact()
    {
        if (target == null || !CanAct() || target.GetPrompt(this) == null || !target.CanInteract(this))
            return false;
        usedFrame = Time.frameCount;
        target.Interact(this);
        return true;
    }

    //true when E belongs to the prompt this frame (the PlantManager then leaves it alone)
    public bool ClaimsInteractKey()
    {
        if (usedFrame == Time.frameCount)
            return true;
        return target != null && CanAct() && target.GetPrompt(this) != null && target.CanInteract(this);
    }

    private WorldInteractable FindClosest()
    {
        WorldInteractable best = null;
        float bestDistance = 0f;
        foreach (WorldInteractable thing in WorldInteractable.All)
        {
            float distance = thing.DistanceOutsideReach(transform.position);
            if (distance > 0f || thing.GetPrompt(this) == null)
                continue;
            if (best == null || distance < bestDistance)
            {
                best = thing;
                bestDistance = distance;
            }
        }
        return best;
    }

    //not in the middle of an action, not in bed
    private bool CanAct()
    {
        return playerStateControl == null || playerStateControl.CanStartAction();
    }

    public WorldInteractable GetTarget()
    {
        return target;
    }
    public Hotbar GetHotbar()
    {
        return hotbar;
    }
    public WateringCanTank GetWateringCanTank()
    {
        return wateringCanTank;
    }
}
