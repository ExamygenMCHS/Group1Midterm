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

    [Tooltip("When above 0 the message is a narrow box this wide that wraps its text and grows downward, instead of one long line")]
    [SerializeField]
    private float maxWidth = 0f;
    [SerializeField]
    private float fontSize = 20f;
    [Tooltip("Gap from the top of the screen to the box, so it can sit below the day and season display")]
    [SerializeField]
    private float topOffset = 120f;

    private const float PadX = 12f;
    private const float PadY = 8f;

    private float secondsLeft = 0f;
    private float nextRejectionSoundAt;

    private void Awake()
    {
        if (maxWidth > 0f)
            SetUpBox();
        Clear();
    }

    //a fixed-width box hanging from its top edge: text wraps inside it and the box height follows the text
    private void SetUpBox()
    {
        RectTransform root = (RectTransform)transform;
        root.anchoredPosition = new Vector2(root.anchoredPosition.x, -topOffset);
        RectTransform label = messageLabel.rectTransform;
        label.anchorMin = Vector2.zero;
        label.anchorMax = Vector2.one;
        label.offsetMin = new Vector2(PadX, PadY);
        label.offsetMax = new Vector2(-PadX, -PadY);
        messageLabel.textWrappingMode = TextWrappingModes.Normal;
        messageLabel.enableAutoSizing = false;
        messageLabel.fontSize = fontSize;
        messageLabel.alignment = TextAlignmentOptions.Center;
    }

    private void FitBoxToText(string message)
    {
        float textHeight = messageLabel.GetPreferredValues(message, maxWidth - 2f * PadX, 0f).y;
        ((RectTransform)transform).sizeDelta = new Vector2(maxWidth, textHeight + 2f * PadY);
    }

    public void Show(string message)
    {
        if (maxWidth > 0f)
            FitBoxToText(message);
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
