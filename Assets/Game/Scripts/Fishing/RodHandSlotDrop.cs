using UnityEngine;
using UnityEngine.EventSystems;

//sits on the bag's hand slot at the bottom left: a lure or the worms dropped there while the farmer holds the fishing
//rod are loaded into the rod (the item in the hand takes no raycasts, so the slot catches the drop). Anything else
//dropped there goes back where it came from, as before.
public class RodHandSlotDrop : MonoBehaviour, IDropHandler
{
    [SerializeField]
    private FarmBag bag;

    public void OnDrop(PointerEventData eventData)
    {
        if (bag != null && eventData.pointerDrag != null)
            bag.TryDropOnHand(eventData.pointerDrag);
    }
}
