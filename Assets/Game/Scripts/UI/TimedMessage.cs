using UnityEngine;
using TMPro;

//shows a short text on the UI and clears it again after a few seconds
public class TimedMessage : MonoBehaviour
{
    [SerializeField]
    private TMP_Text messageLabel;
    [SerializeField]
    private GameObject backdrop;//optional dark panel behind the text, only visible while a message is shown
    [SerializeField]
    private float showSeconds = 2f;

    private float secondsLeft = 0f;

    private void Awake()
    {
        Clear();
    }

    public void Show(string message)
    {
        messageLabel.text = message;
        //longer messages need a little more reading time
        secondsLeft = Mathf.Max(showSeconds, Mathf.Clamp(message.Length / 24f, 2f, 6f));
        if (backdrop != null)
            backdrop.SetActive(true);
    }

    private void Clear()
    {
        messageLabel.text = "";
        if (backdrop != null)
            backdrop.SetActive(false);
    }

    private void Update()
    {
        if (secondsLeft <= 0f)
            return;
        secondsLeft -= Time.unscaledDeltaTime;
        if (secondsLeft <= 0f)
            Clear();
    }
}
