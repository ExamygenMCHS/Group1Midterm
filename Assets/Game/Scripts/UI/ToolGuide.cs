using UnityEngine;
using TMPro;

//a small tip on the HUD that tells the player the next step with the tools, until they have them all:
//pick up the hoe, get the watering can, fill it at the well or the pond, get the sickle (in the harvest levels, to harvest).
//Hidden once there is nothing left to learn.
public class ToolGuide : MonoBehaviour
{
    [SerializeField]
    private Hotbar hotbar;
    [SerializeField]
    private WateringCanTank wateringCanTank;
    [SerializeField]
    private GameObject panel;
    [SerializeField]
    private TMP_Text label;

    private string shownTip = "-";//not a tip: the first update always shows or hides the panel (no tip: an empty panel stayed up)

    private void Start()
    {
        UpdateTip();
    }

    private void Update()
    {
        UpdateTip();
    }

    //the next thing to do, or null
    public string CurrentTip()
    {
        //tools first, in the order the level needs them
        foreach (ToolManager.ToolType tool in new[] { ToolManager.ToolType.Hoe, ToolManager.ToolType.WateringCan })
        {
            ToolPickup pickup = ToolPickup.Find(tool);
            if (pickup != null && !hotbar.HasItem(tool))
                return pickup.GetGuideText();
        }
        if (wateringCanTank != null && hotbar.HasItem(ToolManager.ToolType.WateringCan) && wateringCanTank.IsEmpty())
            return "Fill the watering can at the well or the pond";
        ToolPickup sickle = ToolPickup.Find(ToolManager.ToolType.Sickle);
        if (sickle != null && !hotbar.HasItem(ToolManager.ToolType.Sickle))
            return sickle.GetGuideText();
        return null;
    }

    private void UpdateTip()
    {
        string tip = CurrentTip();
        if (tip == shownTip)
            return;
        shownTip = tip;
        panel.SetActive(tip != null);
        if (tip != null)
            label.text = tip;
    }
}
