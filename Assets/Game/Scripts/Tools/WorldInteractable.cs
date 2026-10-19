using System.Collections.Generic;
using UnityEngine;

//something in the world the player can use with E when standing close to it: a tool lying on the ground
//(ToolPickup) or water to fill the watering can (WaterSource). The WorldInteractor on the player picks the closest
//one in reach and shows its prompt ("E  Pick up the hoe").
public abstract class WorldInteractable : MonoBehaviour
{
    private static readonly List<WorldInteractable> all = new List<WorldInteractable>();
    public static IReadOnlyList<WorldInteractable> All { get { return all; } }

    //Play Mode in this project starts without a domain reload, so the list from the last play session must be cleared
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        all.Clear();
    }

    protected virtual void OnEnable()
    {
        all.Add(this);
    }

    protected virtual void OnDisable()
    {
        all.Remove(this);
    }

    //how far (metres on the ground) the player still is from being in reach; 0 or less = in reach
    public abstract float DistanceOutsideReach(Vector3 playerPosition);

    //the text of the popup prompt, or null when there is nothing to do here right now
    public abstract string GetPrompt(WorldInteractor player);

    //false: the prompt is only a hint (no E key is shown and E does nothing here)
    public virtual bool CanInteract(WorldInteractor player)
    {
        return true;
    }

    public abstract void Interact(WorldInteractor player);

    //the player came into reach of this object, or left it (for a highlight)
    public virtual void SetTargeted(bool targeted)
    {
    }
}
