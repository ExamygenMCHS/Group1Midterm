using UnityEngine;
using UnityEngine.UI;
using TMPro;

//the watering can's water, shown under the energy bar only while the can is in the player's hand.
//It slides in and pushes the state line under it down; it flashes when the player tries to water with an empty can.
//Display only: the water itself lives in WateringCanTank.
public class WaterMeter : MonoBehaviour
{
    [SerializeField]
    private Hotbar hotbar;
    [SerializeField]
    private WateringCanTank wateringCanTank;
    [SerializeField]
    private CanvasGroup group;
    [SerializeField]
    private Image barFill;//Image Type = Filled, Fill Method = Horizontal
    [SerializeField]
    private TMP_Text label;
    [SerializeField]
    private RectTransform pushDown;//optional: the HUD element right under the meter (moves down while the meter shows)
    [SerializeField]
    private float pushDistance = 34f;
    [SerializeField]
    private float showSeconds = 0.18f;

    [SerializeField]
    private Color normalColor = new Color(0.32f, 0.66f, 1f);
    [SerializeField]
    private Color lowColor = new Color(0.55f, 0.8f, 1f);
    [SerializeField]
    private Color flashColor = new Color(0.9f, 0.15f, 0.15f);
    [SerializeField]
    private float flashSeconds = 0.6f;

    private float shown = 0f;//0 hidden, 1 shown
    private float flashLeft = 0f;
    private Vector2 pushHome;
    private int shownWater = -1;

    private void Start()
    {
        if (wateringCanTank != null)
            wateringCanTank.canEmptyEvent.AddListener(OnCanEmpty);
        if (pushDown != null)
            pushHome = pushDown.anchoredPosition;
        shown = IsCanInHand() ? 1f : 0f;
        Apply();
    }

    private void OnDestroy()
    {
        if (wateringCanTank != null)
            wateringCanTank.canEmptyEvent.RemoveListener(OnCanEmpty);
    }

    private void OnCanEmpty()
    {
        flashLeft = flashSeconds;
    }

    public bool IsCanInHand()
    {
        return hotbar != null && wateringCanTank != null && hotbar.IsHolding(ToolManager.ToolType.WateringCan);
    }

    public bool IsShown()
    {
        return shown > 0.5f;
    }

    private void Update()
    {
        float target = IsCanInHand() ? 1f : 0f;
        shown = Mathf.MoveTowards(shown, target, Time.unscaledDeltaTime / Mathf.Max(0.01f, showSeconds));
        if (flashLeft > 0f)
            flashLeft -= Time.unscaledDeltaTime;
        Apply();
    }

    private void Apply()
    {
        float ease = Mathf.SmoothStep(0f, 1f, shown);
        group.alpha = ease;
        group.transform.localScale = new Vector3(1f, Mathf.Lerp(0.6f, 1f, ease), 1f);
        if (pushDown != null)
            pushDown.anchoredPosition = pushHome + Vector2.down * pushDistance * ease;
        if (shown <= 0f || wateringCanTank == null)
            return;

        int water = wateringCanTank.GetWater();
        int capacity = wateringCanTank.GetCapacity();
        barFill.fillAmount = (float)water / capacity;
        barFill.color = flashLeft > 0f ? flashColor : (water <= capacity / 4 ? lowColor : normalColor);
        if (water != shownWater)
        {
            shownWater = water;
            label.text = water > 0 ? "Water  " + water + " / " + capacity : "Empty!  Fill it at the well or pond";
        }
    }
}
