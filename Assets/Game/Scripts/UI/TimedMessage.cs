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
    [SerializeField]
    private AudioSource rejectionSound;

    private float secondsLeft = 0f;
    private float nextRejectionSoundAt;

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

    public void ShowRejection(string message)
    {
        Show(message);
        if (rejectionSound == null || Time.unscaledTime < nextRejectionSoundAt)
            return;
        rejectionSound.Play();
        nextRejectionSoundAt = Time.unscaledTime + 0.4f;
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
