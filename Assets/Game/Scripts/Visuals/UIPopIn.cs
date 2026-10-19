using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

//Visual only: a UI element pops in with a little bounce when it is shown, optionally with a burst of confetti.
//Used on the win panel and the title. It does not touch the buttons' behaviour.
public class UIPopIn : MonoBehaviour
{
    [SerializeField]
    private float seconds = 0.45f;
    [SerializeField]
    private float startScale = 0.6f;
    [SerializeField]
    private int confettiCount = 0;
    [SerializeField]
    private Sprite confettiSprite;
    [SerializeField]
    private Color[] confettiColors = { new Color(1f, 0.8f, 0.2f), new Color(0.45f, 0.8f, 0.3f), new Color(0.95f, 0.4f, 0.35f), new Color(0.4f, 0.65f, 0.95f), new Color(1f, 1f, 1f) };

    private class Bit
    {
        public RectTransform rect;
        public Image image;
        public Vector2 velocity;
        public float spin;
    }

    private float age;
    private readonly List<Bit> bits = new List<Bit>();
    private RectTransform confettiLayer;

    private void OnEnable()
    {
        age = 0f;
        transform.localScale = Vector3.one * startScale;
        if (confettiCount > 0 && confettiSprite != null)
            SpawnConfetti();
    }

    private void OnDisable()
    {
        transform.localScale = Vector3.one;
        ClearConfetti();
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        age += dt;
        float k = Mathf.Clamp01(age / seconds);
        const float c = 1.7f;
        float ease = 1f + (c + 1f) * Mathf.Pow(k - 1f, 3f) + c * Mathf.Pow(k - 1f, 2f);
        transform.localScale = Vector3.one * Mathf.LerpUnclamped(startScale, 1f, ease);

        if (bits.Count == 0)
            return;
        float fade = Mathf.Clamp01((3.2f - age) / 1f);
        foreach (Bit b in bits)
        {
            b.velocity += new Vector2(0f, -1100f) * dt;
            b.velocity *= 1f - 0.6f * dt;
            b.rect.anchoredPosition += b.velocity * dt;
            b.rect.Rotate(0f, 0f, b.spin * dt);
            Color col = b.image.color;
            col.a = fade;
            b.image.color = col;
        }
        if (age > 3.2f)
            ClearConfetti();
    }

    private void SpawnConfetti()
    {
        ClearConfetti();
        var layer = new GameObject("Confetti", typeof(RectTransform));
        confettiLayer = layer.GetComponent<RectTransform>();
        confettiLayer.SetParent(transform, false);
        confettiLayer.anchorMin = confettiLayer.anchorMax = new Vector2(0.5f, 0.5f);
        confettiLayer.anchoredPosition = Vector2.zero;
        confettiLayer.sizeDelta = Vector2.zero;
        for (int i = 0; i < confettiCount; i++)
        {
            var go = new GameObject("Bit", typeof(RectTransform), typeof(Image));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(confettiLayer, false);
            rect.sizeDelta = new Vector2(Random.Range(10f, 18f), Random.Range(6f, 10f));
            rect.anchoredPosition = new Vector2(Random.Range(-60f, 60f), Random.Range(40f, 120f));
            rect.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
            var image = go.GetComponent<Image>();
            image.sprite = confettiSprite;
            image.color = confettiColors[Random.Range(0, confettiColors.Length)];
            image.raycastTarget = false;
            float angle = Random.Range(20f, 160f) * Mathf.Deg2Rad;
            float speed = Random.Range(500f, 1000f);
            bits.Add(new Bit { rect = rect, image = image, velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed, spin = Random.Range(-540f, 540f) });
        }
    }

    private void ClearConfetti()
    {
        bits.Clear();
        if (confettiLayer != null)
            Destroy(confettiLayer.gameObject);
        confettiLayer = null;
    }
}
