using TMPro;
using UnityEngine;
using UnityEngine.UI;

//the small marks on a bag item that has to do with bait: on the fishing rod a tiny copy of the bait loaded into it in
//the bottom right corner (with the number of worms next to it), and on a lure (or the rod holding one) a thin
//Minecraft-style durability bar along the bottom that empties from green to red. BaitBagUI adds it to the items and
//sets it; it builds its own child objects the first time and never takes a mouse click.
public class BaitBadge : MonoBehaviour
{
    private Image plate;//dark disc behind the bait, so it reads over the rod's icon
    private Image overlay;
    private TMP_Text count;
    private RectTransform bar;
    private RectTransform barFill;
    private Image barFillImage;

    private Sprite shownSprite;
    private int shownAmount = -2;
    private int shownLeft = -1, shownMax = -1;

    //makes the child objects (once)
    public void Build(TMP_FontAsset font, Material fontMaterial, Sprite plateSprite)
    {
        if (overlay != null)
            return;
        RectTransform plateRect = Part("Bait Plate", transform, new Vector2(0.54f, 0.08f), new Vector2(1.08f, 0.62f));
        plate = plateRect.gameObject.AddComponent<Image>();
        plate.sprite = plateSprite;
        plate.color = new Color(0.07f, 0.09f, 0.06f, 0.78f);
        plate.raycastTarget = false;

        RectTransform overlayRect = Part("Bait Overlay", plateRect, new Vector2(0.06f, 0.06f), new Vector2(0.94f, 0.94f));
        overlay = overlayRect.gameObject.AddComponent<Image>();
        overlay.preserveAspect = true;
        overlay.raycastTarget = false;

        //the number of worms, just left of the bait
        RectTransform countRect = Part("Bait Count", plateRect, new Vector2(-0.9f, -0.12f), new Vector2(0.08f, 0.62f));
        count = countRect.gameObject.AddComponent<TextMeshProUGUI>();
        count.font = font;
        if (fontMaterial != null)
            count.fontSharedMaterial = fontMaterial;
        count.fontSize = 13f;
        count.fontStyle = FontStyles.Bold;
        count.alignment = TextAlignmentOptions.BottomRight;
        count.textWrappingMode = TextWrappingModes.NoWrap;
        count.overflowMode = TextOverflowModes.Overflow;
        count.color = Color.white;
        count.raycastTarget = false;

        //the durability bar: a dark backing with the fill inside, along the bottom edge of the icon
        bar = Part("Durability", transform, new Vector2(0.1f, 0f), new Vector2(0.9f, 0f));
        bar.sizeDelta = new Vector2(0f, 5f);
        bar.anchoredPosition = new Vector2(0f, 1.5f);
        Image back = bar.gameObject.AddComponent<Image>();
        back.color = new Color(0.04f, 0.04f, 0.04f, 0.92f);
        back.raycastTarget = false;
        barFill = Part("Fill", bar, Vector2.zero, Vector2.one);
        barFill.offsetMin = new Vector2(1f, 1f);
        barFill.offsetMax = new Vector2(-1f, -1f);
        barFillImage = barFill.gameObject.AddComponent<Image>();
        barFillImage.raycastTarget = false;

        plateRect.gameObject.SetActive(false);
        bar.gameObject.SetActive(false);
    }

    private static RectTransform Part(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        RectTransform rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return rect;
    }

    //the bait in the rod (null: none shown); 'amount' below 1 shows no number (a lure)
    public void SetBait(Sprite sprite, int amount)
    {
        if (overlay == null || (sprite == shownSprite && amount == shownAmount))
            return;
        shownSprite = sprite;
        shownAmount = amount;
        plate.gameObject.SetActive(sprite != null);
        overlay.sprite = sprite;
        count.text = sprite != null && amount > 0 ? amount.ToString() : "";
    }

    //casts left of the most a lure can have; the bar only shows once the lure has been used
    public void SetDurability(int left, int max)
    {
        if (bar == null || (left == shownLeft && max == shownMax))
            return;
        shownLeft = left;
        shownMax = max;
        bool shown = max > 0 && left < max;
        bar.gameObject.SetActive(shown);
        if (!shown)
            return;
        float share = Mathf.Clamp01((float)left / max);
        barFill.anchorMax = new Vector2(share, 1f);
        barFillImage.color = Color.HSVToRGB(share / 3f, 0.9f, 0.95f);//green when new, yellow, red when nearly worn out
    }

    // ---------------------------------------------------------------- for the tests

    public bool IsBaitShown()
    {
        return plate != null && plate.gameObject.activeSelf;
    }
    public Sprite GetBaitSprite()
    {
        return IsBaitShown() ? overlay.sprite : null;
    }
    public string GetCountText()
    {
        return count != null ? count.text : "";
    }
    public bool IsBarShown()
    {
        return bar != null && bar.gameObject.activeSelf;
    }
    //the filled share of the durability bar (0..1)
    public float GetBarShare()
    {
        return IsBarShown() ? barFill.anchorMax.x : 1f;
    }
}
