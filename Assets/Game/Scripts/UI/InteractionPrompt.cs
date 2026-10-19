using UnityEngine;
using TMPro;

//the popup prompt above the hotbar ("E  Pick up the hoe", "E  Fill the watering can at the well").
//The WorldInteractor shows it while the player is next to something they can use. It pops in with a small bounce.
//A prompt that is only a hint ("Hold the watering can (2) to fill it here") hides the key cap.
public class InteractionPrompt : MonoBehaviour
{
    [SerializeField]
    private RectTransform panel;
    [SerializeField]
    private TMP_Text label;
    [SerializeField]
    private GameObject keyCap;
    [SerializeField]
    private float popSeconds = 0.2f;

    private float age = 1f;
    private string shownText;

    private void Awake()
    {
        Hide();
    }

    public void Show(string text, bool withKey)
    {
        if (!panel.gameObject.activeSelf)
        {
            panel.gameObject.SetActive(true);
            age = 0f;
        }
        if (text != shownText)
        {
            shownText = text;
            label.text = text;
        }
        if (keyCap != null && keyCap.activeSelf != withKey)
            keyCap.SetActive(withKey);
    }

    public void Hide()
    {
        if (panel.gameObject.activeSelf)
            panel.gameObject.SetActive(false);
        shownText = null;
    }

    public bool IsShown()
    {
        return panel.gameObject.activeSelf;
    }

    public string GetText()
    {
        return IsShown() ? shownText : null;
    }

    private void Update()
    {
        if (age >= popSeconds)
            return;
        age += Time.unscaledDeltaTime;
        float k = Mathf.Clamp01(age / popSeconds);
        const float c = 1.7f;
        float ease = 1f + (c + 1f) * Mathf.Pow(k - 1f, 3f) + c * Mathf.Pow(k - 1f, 2f);//ease out with a little overshoot
        panel.localScale = Vector3.one * Mathf.LerpUnclamped(0.7f, 1f, ease);
    }
}
