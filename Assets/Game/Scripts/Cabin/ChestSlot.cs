using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

//one square in the chest window (built by ChestPanel): the icon and count of a stack of seeds or crops.
//Left click moves the whole stack to the other side (bag <-> chest), right click moves one.
public class ChestSlot : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    public Image frame;
    public Image icon;
    public TMP_Text count;

    public PlantData Crop { get; private set; }
    public bool Harvest { get; private set; }//true: harvested crops, false: seeds
    public int Amount { get; private set; }

    public Action<ChestSlot, bool> clicked;//the slot, true = move the whole stack
    public Action<ChestSlot> hovered;//null slot = the pointer left

    private Color frameColor;
    private Color hoverColor;

    public void SetColors(Color normal, Color hover)
    {
        frameColor = normal;
        hoverColor = hover;
        frame.color = normal;
    }

    public void Show(PlantData crop, bool harvest, int amount)
    {
        Crop = crop;
        Harvest = harvest;
        Amount = amount;
        bool filled = crop != null && amount > 0;
        Sprite sprite = !filled ? null : harvest ? (crop.harvestItem != null ? crop.harvestItem.itemSprite : crop.icon)
                                                 : (crop.seedItem != null ? crop.seedItem.itemSprite : crop.seedIcon);
        icon.sprite = sprite;
        icon.enabled = sprite != null;
        count.text = filled && amount > 1 ? amount.ToString() : "";
    }

    public bool IsEmpty()
    {
        return Crop == null || Amount <= 0;
    }

    //"Pumpkin Seeds x4", "Pumpkins x2"
    public string Label()
    {
        if (IsEmpty())
            return "";
        string name = Harvest ? Crop.NameFor(Amount) : Crop.cropName + (Amount == 1 ? " Seed" : " Seeds");
        return name + "  x" + Amount;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (IsEmpty() || clicked == null)
            return;
        if (eventData.button == PointerEventData.InputButton.Left)
            clicked(this, true);
        else if (eventData.button == PointerEventData.InputButton.Right)
            clicked(this, false);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        frame.color = IsEmpty() ? frameColor : hoverColor;
        if (hovered != null)
            hovered(this);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        frame.color = frameColor;
        if (hovered != null)
            hovered(null);
    }
}
