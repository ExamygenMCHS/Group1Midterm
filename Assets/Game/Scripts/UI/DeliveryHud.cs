using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The driving HUD of a delivery: the truck's condition bar, what the cargo is still worth, and a line of text for
// what is going on (drive to the circle, towed back, delivered). It only listens to the truck and the mission.
public class DeliveryHud : MonoBehaviour
{
    [SerializeField] private DeliveryMission mission;
    [SerializeField] private TruckDamage truck;
    [SerializeField] private Image healthFill;
    [SerializeField] private TMP_Text healthLabel;
    [SerializeField] private TMP_Text cargoLabel;
    [SerializeField] private TMP_Text messageLabel;
    [SerializeField] private Color intactColor = new Color(.35f, .8f, .4f);
    [SerializeField] private Color damagedColor = new Color(.95f, .75f, .2f);
    [SerializeField] private Color criticalColor = new Color(.9f, .3f, .25f);

    private void Start()
    {
        if (truck != null)
        {
            truck.Damaged.AddListener(OnDamaged);
            truck.StateChanged.AddListener(Refresh);
        }
        if (mission != null)
        {
            mission.StateChanged.AddListener(Refresh);
            mission.CargoChanged.AddListener(Refresh);
        }
        Refresh();
    }

    private void OnDestroy()
    {
        if (truck != null)
        {
            truck.Damaged.RemoveListener(OnDamaged);
            truck.StateChanged.RemoveListener(Refresh);
        }
        if (mission != null)
        {
            mission.StateChanged.RemoveListener(Refresh);
            mission.CargoChanged.RemoveListener(Refresh);
        }
    }

    private void OnDamaged(float amount)
    {
        Refresh();
    }

    private void Refresh()
    {
        if (truck != null)
        {
            if (healthFill != null)
            {
                // The bar is a plain rectangle that ends at the health share of its frame.
                RectTransform bar = healthFill.rectTransform;
                bar.anchorMax = new Vector2(truck.Health01, bar.anchorMax.y);
                healthFill.color = truck.State == TruckDamage.DamageState.Intact ? intactColor
                    : truck.State == TruckDamage.DamageState.Damaged ? damagedColor : criticalColor;
            }
            if (healthLabel != null)
                healthLabel.text = "Truck " + Mathf.CeilToInt(truck.Health01 * 100f) + "%";
        }
        if (cargoLabel != null)
        {
            int crops = DeliveryCargo.TotalCount();
            cargoLabel.text = crops == 0 ? "No cargo (practice drive)"
                : "Cargo: " + crops + (crops == 1 ? " crop, " : " crops, ") + Mathf.RoundToInt(DeliveryCargo.Condition * 100f) + "% fresh";
        }
        if (messageLabel != null && mission != null)
        {
            switch (mission.State)
            {
                case DeliveryMission.MissionState.Wrecked:
                    messageLabel.text = "The truck is wrecked. Towing it back to the top...";
                    break;
                case DeliveryMission.MissionState.Delivered:
                    messageLabel.text = "Delivered! Heading to the market...";
                    break;
                default:
                    messageLabel.text = "Drive down to the yellow circle. Mind the traffic.";
                    break;
            }
        }
    }
}
