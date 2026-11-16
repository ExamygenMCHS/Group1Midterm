using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

//the controls window: L (or the key set below) opens and closes a panel in the middle of the screen with every control
//of the scene, Esc also closes it. It replaces the block of instructions that used to sit on the HUD, which now shows a
//single line ("Press L to show controls") in the hint label.
//The panel builds itself when the scene starts, so a scene only needs this component (on its HUD canvas) and the list
//of controls filled in the Inspector. With a Tool Guide set, the panel also shows the player's next step.
public class ControlsPanel : MonoBehaviour
{
    [System.Serializable]
    public class Control
    {
        public string keys;//"WASD", "1-8 / wheel"
        public string action;//"move", "hold an item"
    }

    [SerializeField]
    private Key toggleKey = Key.L;
    [Tooltip("Optional: the HUD text that shows the one-line hint")]
    [SerializeField]
    private TMP_Text hintLabel;
    [Tooltip("Optional: the font of the panel (empty: TextMeshPro's default font)")]
    [SerializeField]
    private TMP_FontAsset font;
    [Tooltip("Optional: the next step (pick up the hoe, fill the can...) shows at the bottom of the panel")]
    [SerializeField]
    private ToolGuide toolGuide;
    [SerializeField]
    private string title = "Controls";
    [SerializeField]
    private Control[] controls =
    {
        new Control { keys = "WASD", action = "move" },
        new Control { keys = "1-8 / wheel", action = "hold an item" },
        new Control { keys = "Space", action = "till / water" },
        new Control { keys = "E", action = "plant / harvest / use" },
        new Control { keys = "Q", action = "next seeds" },
        new Control { keys = "B", action = "open the bag" },
        new Control { keys = "F (hold)", action = "fast-forward time" },
        new Control { keys = "Tab", action = "state info" },
    };

    [Tooltip("Sorting order of the window's canvas: above the bag, the hotbar and the time display")]
    [SerializeField]
    private int sortingOrder = 100;

    private const string Gold = "#FFD45E";

    private GameObject root;
    private TMP_Text body;

    //the one-line hint, in the same gold as the old controls hint
    public string HintText()
    {
        return "Press " + Hotbar.KeyTag(KeyName()) + " to show controls";
    }

    private string KeyName()
    {
        return toggleKey.ToString();
    }

    private void Start()
    {
        if (hintLabel != null)
            hintLabel.text = HintText();
        Build();
        SetOpen(false);
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return;
        if (keyboard[toggleKey].wasPressedThisFrame)
            SetOpen(!IsOpen());
        else if (IsOpen() && keyboard.escapeKey.wasPressedThisFrame)
            SetOpen(false);
    }

    public bool IsOpen()
    {
        return root != null && root.activeSelf;
    }

    public void SetOpen(bool open)
    {
        if (root == null)
            return;
        if (open)
            body.text = BodyText();//the next step may have changed since the last time
        root.SetActive(open);
        root.transform.SetAsLastSibling();//in front of the rest of the HUD
    }

    private string BodyText()
    {
        var text = new System.Text.StringBuilder();
        foreach (Control control in controls)
            text.Append("<color=").Append(Gold).Append("><b>").Append(control.keys).Append("</b></color>")
                .Append("<pos=38%>").Append(control.action).Append('\n');
        string tip = toolGuide != null ? toolGuide.CurrentTip() : null;
        if (!string.IsNullOrEmpty(tip))
            text.Append("\n<color=").Append(Gold).Append(">Next step:</color> ").Append(tip).Append('\n');
        text.Append("\n<size=80%><alpha=#AA>Press ").Append(KeyName()).Append(" or Esc to close</size>");
        return text.ToString();
    }

    // ---------------------------------------------------------------- the panel builds itself

    private void Build()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        Transform parent = canvas != null ? canvas.transform : transform;
        //a dark screen behind the panel, over the whole canvas
        root = new GameObject("Controls Panel", typeof(RectTransform), typeof(Image));
        RectTransform rootRect = (RectTransform)root.transform;
        rootRect.SetParent(parent, false);
        Stretch(rootRect);
        //its own canvas sorted above the rest of the HUD (the bag and the hotbar have a canvas of their own)
        Canvas overlay = root.AddComponent<Canvas>();
        overlay.overrideSorting = true;
        overlay.sortingOrder = sortingOrder;
        root.AddComponent<GraphicRaycaster>();
        Image dim = root.GetComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.55f);
        dim.raycastTarget = true;//the HUD under the panel cannot be clicked while it is open

        GameObject panel = new GameObject("Panel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        RectTransform panelRect = (RectTransform)panel.transform;
        panelRect.SetParent(rootRect, false);
        panelRect.anchorMin = panelRect.anchorMax = panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(560f, 0f);
        panel.GetComponent<Image>().color = new Color(0.09f, 0.08f, 0.07f, 0.94f);
        VerticalLayoutGroup layout = panel.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(36, 36, 26, 28);
        layout.spacing = 14f;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        ContentSizeFitter fitter = panel.GetComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        TMP_Text heading = Label(panelRect, "Title", title, 36f, TextAlignmentOptions.Center);
        heading.fontStyle = FontStyles.Bold;
        heading.color = new Color(1f, 0.83f, 0.37f);
        body = Label(panelRect, "Body", "", 26f, TextAlignmentOptions.TopLeft);
        body.lineSpacing = 18f;
    }

    private TMP_Text Label(RectTransform parent, string name, string text, float size, TextAlignmentOptions alignment)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI label = go.GetComponent<TextMeshProUGUI>();
        if (font != null)
            label.font = font;
        label.text = text;
        label.fontSize = size;
        label.alignment = alignment;
        label.color = new Color(0.95f, 0.95f, 0.92f);
        label.raycastTarget = false;
        label.richText = true;
        return label;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
}
