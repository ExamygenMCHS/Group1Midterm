using UnityEngine;
using UnityEngine.EventSystems;

public class InventorySlot : MonoBehaviour,IDropHandler
{
    public bool isDebug = false;
    private InventoryItemDrag draggedItem;

    //A dragged item still owns its source slot until it is dropped or returned.
    public bool IsEmpty()
    {
        return transform.childCount == 0 && draggedItem == null;
    }

    public void ReserveForDrag(InventoryItemDrag item)
    {
        draggedItem = item;
    }

    public void ReleaseDrag(InventoryItemDrag item)
    {
        if (draggedItem == item)
            draggedItem = null;
    }

    public void OnDrop(PointerEventData eventData)
    {
        if (isDebug)
            Debug.Log("Drop to slot"+transform.name);
        //the dragged Item that is dropped to the slot is now saved to the variable
        GameObject droppedItem = eventData.pointerDrag;
        InventoryItemDrag drag = droppedItem != null ? droppedItem.GetComponent<InventoryItemDrag>() : null;
        if (drag == null || (draggedItem != null && draggedItem != drag))
            return;//another drag has reserved this slot

        if (transform.childCount == 1)//if this slot is occupied by another item

        {
            //move the existing object to the dragged Item's original slot
            Transform droppedItemOriginalParent = drag.getParentAfterDrag();
            transform.GetChild(0).SetParent(droppedItemOriginalParent);
        }
        
        //drop this Item to the empty slot
        drag.setParentAfterDrag(transform);

    }
}
