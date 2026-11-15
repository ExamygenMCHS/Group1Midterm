using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// The sale at the end of a delivery. The market lists what came off the truck, pays a wholesale bonus over the
// trader's price, and pays less for cargo shaken up on the way. Selling adds the money to the Farm Fund; then the
// player goes back to the farm.
public class WholesaleMarket : MonoBehaviour
{
    public enum MarketState { Offer, Sold, Empty }

    [Tooltip("How much more the market pays than the trader on the farm (1.5 = half as much again).")]
    [SerializeField] private float wholesaleBonus = 1.5f;
    [SerializeField] private TMP_Text receiptLabel;
    [SerializeField] private TMP_Text totalLabel;
    [SerializeField] private TMP_Text fundLabel;
    [SerializeField] private Button sellButton;
    [SerializeField] private Button homeButton;
    [SerializeField] private AudioSource saleAudio;
    [SerializeField] private AudioClip saleSound;
    [SerializeField] private UnityEvent onSold = new UnityEvent();

    public MarketState State { get; private set; } = MarketState.Offer;
    public float WholesaleBonus { get { return wholesaleBonus; } }
    public UnityEvent Sold { get { return onSold; } }
    /// <summary>What the cargo on the truck pays right now.</summary>
    public int Offer { get { return DeliveryCargo.TotalValue(wholesaleBonus); } }

    private void Start()
    {
        // The driving camera locks the cursor; the market is all buttons.
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Time.timeScale = 1f;
        if (sellButton != null) sellButton.onClick.AddListener(Sell);
        if (homeButton != null) homeButton.onClick.AddListener(GoHome);
        State = DeliveryCargo.HasCargo ? MarketState.Offer : MarketState.Empty;
        Refresh();
    }

    private void OnDestroy()
    {
        if (sellButton != null) sellButton.onClick.RemoveListener(Sell);
        if (homeButton != null) homeButton.onClick.RemoveListener(GoHome);
    }

    // The only place the market state changes.
    private void ChangeState(MarketState next)
    {
        if (next == State)
            return;
        State = next;
        Refresh();
    }

    public void Sell()
    {
        if (State != MarketState.Offer || FarmFund.Instance == null)
            return;
        int total = Offer;
        string receipt = Receipt();
        FarmFund.Instance.AddFund(total);
        DeliveryCargo.Clear();
        if (saleAudio != null && saleSound != null)
            saleAudio.PlayOneShot(saleSound);
        ChangeState(MarketState.Sold);
        // Keep the list of what was sold on screen under the new heading.
        if (receiptLabel != null) receiptLabel.text = receipt;
        if (totalLabel != null) totalLabel.text = "Sold for $ " + total;
        onSold.Invoke();
    }

    public void GoHome()
    {
        // Leaving without selling keeps the crops: they go back into the inventory.
        if (DeliveryCargo.HasCargo && FarmFund.Instance != null)
            DeliveryCargo.Unload(FarmFund.Instance.Inventory);
        FarmTrip.Return();
    }

    private string Receipt()
    {
        var text = new StringBuilder();
        foreach (DeliveryCargo.Line line in DeliveryCargo.Lines)
        {
            if (line.crop == null) continue;
            text.Append(line.count).Append(" x ").Append(line.crop.NameFor(line.count))
                .Append("   $ ").Append(DeliveryCargo.LineValue(line, wholesaleBonus)).Append('\n');
        }
        int fresh = Mathf.RoundToInt(DeliveryCargo.Condition * 100f);
        text.Append('\n').Append("Wholesale price: x ").Append(wholesaleBonus.ToString("0.##"))
            .Append('\n').Append("Freshness: ").Append(fresh).Append('%');
        return text.ToString();
    }

    private void Refresh()
    {
        if (receiptLabel != null)
            receiptLabel.text = State == MarketState.Empty ? "The truck is empty. Bring a harvest next time." : Receipt();
        if (totalLabel != null)
            totalLabel.text = State == MarketState.Offer ? "The market offers $ " + Offer : "";
        if (fundLabel != null)
            fundLabel.text = "Fund: $ " + (FarmFund.Instance != null ? FarmFund.Instance.fund : 0);
        if (sellButton != null)
            sellButton.gameObject.SetActive(State == MarketState.Offer);
        if (homeButton != null)
        {
            TMP_Text label = homeButton.GetComponentInChildren<TMP_Text>();
            if (label != null)
                label.text = State == MarketState.Offer ? "Keep the crops and go home" : "Back to the Farm";
        }
    }
}
