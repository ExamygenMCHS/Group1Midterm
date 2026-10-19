using UnityEngine;
using UnityEngine.UI;
using TMPro;

//the energy bar on the 2D canvas. It only displays, the energy itself lives in PlayerEnergy.
public class EnergyBar : MonoBehaviour
{
    [SerializeField]
    private PlayerEnergy playerEnergy;
    [SerializeField]
    private Image barFill;//Image Type = Filled, Fill Method = Horizontal
    [SerializeField]
    private TMP_Text energyLabel;
    [SerializeField]
    private TimedMessage hudMessage;

    [SerializeField]
    private Color normalColor = new Color(0.25f, 0.8f, 0.3f);
    [SerializeField]
    private Color lowColor = new Color(0.95f, 0.75f, 0.2f);
    [SerializeField]
    private Color flashColor = new Color(0.9f, 0.15f, 0.15f);
    [SerializeField]
    private float lowEnergyFraction = 0.25f;//the bar turns to the low color at or below this fraction
    [SerializeField]
    private float flashSeconds = 0.5f;//how long the bar flashes when the player is out of energy

    private float flashSecondsLeft = 0f;
    private int shownEnergy = -1;
    private bool shownResting = false;

    private void OnEnable()
    {
        playerEnergy.notEnoughEnergyEvent.AddListener(OnNotEnoughEnergy);
    }
    private void OnDisable()
    {
        playerEnergy.notEnoughEnergyEvent.RemoveListener(OnNotEnoughEnergy);
    }

    private void OnNotEnoughEnergy()
    {
        flashSecondsLeft = flashSeconds;
        if (playerEnergy.GetCurEnergy() < 1f)
            hudMessage.Show("Out of energy! Rest in the Sleep Area.");
        else
            hudMessage.Show("Not enough energy! Rest in the Sleep Area.");
    }

    private void Update()
    {
        float energyFraction = playerEnergy.GetCurEnergy() / playerEnergy.GetMaxEnergy();
        barFill.fillAmount = energyFraction;

        if (flashSecondsLeft > 0f)
        {
            flashSecondsLeft -= Time.deltaTime;
            barFill.color = flashColor;
        }
        else if (energyFraction <= lowEnergyFraction)
            barFill.color = lowColor;
        else
            barFill.color = normalColor;

        //only rebuild the text when the number on screen changes
        int energy = Mathf.FloorToInt(playerEnergy.GetCurEnergy());
        bool resting = playerEnergy.IsResting();
        if (energy != shownEnergy || resting != shownResting)
        {
            shownEnergy = energy;
            shownResting = resting;
            energyLabel.text = "Energy  " + energy.ToString() + " / " + Mathf.FloorToInt(playerEnergy.GetMaxEnergy()).ToString()
                + (resting ? "   Zzz..." : "");
        }
    }
}
