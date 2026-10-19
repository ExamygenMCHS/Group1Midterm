using UnityEngine;

//Visual only: dresses the farmer in what was bought in the store. The golden Speed Boots appear on the farmer's
//feet once the player owns them; the speed itself is applied by FarmBalance.
public class FarmerUpgradeVisual : MonoBehaviour
{
    [SerializeField]
    private GameObject[] speedBoots;
    [SerializeField]
    private ParticleSystem sparkle;//a few glints at the heels while running in the boots

    private bool shown;

    private void Start()
    {
        if (FarmFund.Instance != null)
            FarmFund.Instance.Inventory.inventoryChangedEvent.AddListener(UpdateLook);
        UpdateLook();
    }

    private void OnDestroy()
    {
        if (FarmFund.Instance != null)
            FarmFund.Instance.Inventory.inventoryChangedEvent.RemoveListener(UpdateLook);
    }

    private void UpdateLook()
    {
        shown = FarmBalance.HasSpeedBoots();
        foreach (GameObject boot in speedBoots)
            if (boot != null)
                boot.SetActive(shown);
        if (sparkle != null)
        {
            if (shown)
                sparkle.Play();
            else
                sparkle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }
}
