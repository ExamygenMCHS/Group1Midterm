using UnityEngine;
using UnityEngine.InputSystem;

//while the player holds the fast-forward key (F) the in-game clock runs faster.
//Only the clock speeds up (days, plant growth, seasons, drying tiles, and the energy regained while sleeping in bed),
//not the player, so it never makes a level easier; it is there to watch plants grow and seasons change without a long wait.
public class TimeFastForward : MonoBehaviour
{
    [SerializeField]
    private DayNightControl dayNightControl;
    [SerializeField]
    private Key fastForwardKey = Key.F;
    [SerializeField]
    private float fastForwardSpeed = 6f;

    private bool fastForwarding = false;

    private void Update()
    {
        fastForwarding = Keyboard.current != null && Keyboard.current[fastForwardKey].isPressed;
        dayNightControl.timeSpeed = fastForwarding ? fastForwardSpeed : 1f;
    }

    private void OnDisable()
    {
        fastForwarding = false;
        if (dayNightControl != null)
            dayNightControl.timeSpeed = 1f;
    }

    public bool IsFastForwarding()
    {
        return fastForwarding;
    }
}
