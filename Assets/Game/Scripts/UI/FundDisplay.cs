using UnityEngine;
using TMPro;

//keeps the "Fund: $ 0" UI text showing the current fund
//the fund lives on the Farm Fund object (FarmFund), which survives scene loads
public class FundDisplay : MonoBehaviour
{
    [SerializeField]
    private TMP_Text fundLabel;

    private FarmFund farmFund;

    private void Start()
    {
        farmFund = FarmFund.Instance;
        if (farmFund != null)
            farmFund.fundChangedEvent.AddListener(UpdateLabel);
        UpdateLabel();
    }
    private void OnDestroy()
    {
        if (farmFund != null)
            farmFund.fundChangedEvent.RemoveListener(UpdateLabel);
    }

    private void UpdateLabel()
    {
        int fund = farmFund != null ? farmFund.fund : 0;
        fundLabel.text = "Fund: $ " + fund.ToString();
    }
}
