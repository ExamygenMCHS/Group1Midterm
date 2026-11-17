using UnityEngine;
using UnityEngine.UI;
using TMPro;

//the fishing HUD. It only shows while it matters: a small hint chip while the rod is out near the water ("Cast", why a
//spot cannot be fished, "Hook it!"), the line's strain bar during a fight, a "!" over the bobber when a fish bites, and
//a card with the fish that was just caught. Nothing of it is on screen while the player farms.
//It reads the FishingControl's state every frame and rebuilds a text only when what it shows changes. Everything fades
//through CanvasGroup alpha in unscaled time, and nothing here takes mouse clicks (the clicks belong to the fishing).
public class FishingHud : MonoBehaviour
{
    private enum Hint //what the hint chip says
    {
        None, Cast, BadAim, NoAim, Wait, Hook, Fight
    }

    [SerializeField]
    private FishingControl fishing;
    [Tooltip("Empty: the main camera")]
    [SerializeField]
    private Camera worldCamera;

    [Header("Hint chip")]
    [SerializeField]
    private CanvasGroup hintGroup;
    [SerializeField]
    private RectTransform hintPanel;
    [SerializeField]
    private GameObject leftClickIcon;
    [SerializeField]
    private GameObject rightClickIcon;
    [SerializeField]
    private TMP_Text hintLabel;
    [SerializeField]
    private TMP_Text baitLabel;
    [SerializeField]
    private Image baitIcon;
    [SerializeField]
    private GameObject baitGroup;//the right-click part: change the bait

    [Header("Fight bar")]
    [SerializeField]
    private CanvasGroup tensionGroup;
    [SerializeField]
    private Image tensionFill;//filled horizontally
    [SerializeField]
    private RectTransform tensionDanger;//optional: the mark where the line gets close to snapping
    [SerializeField]
    private TMP_Text tensionLabel;

    [Header("Bite alert")]
    [SerializeField]
    private RectTransform biteAlert;//the "!" bubble over the bobber
    [SerializeField]
    private float biteAlertHeight = 0.45f;//metres above the bobber

    [Header("Catch card")]
    [SerializeField]
    private CanvasGroup catchGroup;
    [SerializeField]
    private RectTransform catchPanel;
    [SerializeField]
    private Image catchIcon;
    [SerializeField]
    private TMP_Text catchName;
    [SerializeField]
    private TMP_Text catchDetail;
    [SerializeField]
    private TMP_Text catchTag;

    [Header("Look")]
    [SerializeField]
    private Color badAimColor = new Color(1f, 0.58f, 0.52f);
    [SerializeField]
    private Color calmColor = new Color(0.40f, 0.82f, 0.32f);
    [SerializeField]
    private Color strainColor = new Color(1f, 0.83f, 0.22f);
    [SerializeField]
    private Color dangerColor = new Color(0.95f, 0.22f, 0.16f);
    [SerializeField]
    private Color trophyColor = new Color(1f, 0.83f, 0.37f);
    [SerializeField]
    private Color bestColor = new Color(0.62f, 0.92f, 0.55f);
    [SerializeField]
    private float fadeSeconds = 0.15f;
    [SerializeField]
    private float waitDimSeconds = 4f;
    [SerializeField]
    private float waitDimAlpha = 0.45f;
    [SerializeField]
    private float popSeconds = 0.22f;
    [SerializeField]
    private float catchFadeDelay = 0.3f;//the card stays this long after the farmer puts the fish away

    private Hint hint = Hint.None;
    private FishingControl.AimProblem shownProblem = FishingControl.AimProblem.None;
    private BaitData shownBait;
    private int shownBaitCount = -1;
    private bool shownSurging = false;
    private Color hintColor = Color.white;
    private float hintAge = 1f;//since the chip popped in
    private float waitTime = 0f;
    private Vector2 tensionHome;
    private RectTransform tensionRect;
    private bool biteShown = true;//so the first frame hides it
    private float biteAge = 0f;
    private RectTransform biteParent;
    private Camera canvasCamera;
    private bool catchShowing = false;
    private float catchAge = 0f, catchAfter = 0f;

    private void Awake()
    {
        if (hintLabel != null)
            hintColor = hintLabel.color;
        PrepareGroup(hintGroup);
        PrepareGroup(tensionGroup);
        PrepareGroup(catchGroup);
        if (tensionGroup != null)
        {
            tensionRect = tensionGroup.transform as RectTransform;
            if (tensionRect != null)
                tensionHome = tensionRect.anchoredPosition;
        }
        if (biteAlert != null)
        {
            NoClicks(biteAlert.gameObject);
            biteParent = biteAlert.parent as RectTransform;
            Canvas canvas = biteAlert.GetComponentInParent<Canvas>();
            if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                canvasCamera = canvas.worldCamera;
        }
        if (tensionLabel != null)
            tensionLabel.text = "";
        SetBiteShown(false);
    }

    private void Start()
    {
        if (fishing != null)
            fishing.fishCaughtEvent.AddListener(OnFishCaught);
    }

    private void OnDestroy()
    {
        if (fishing != null)
            fishing.fishCaughtEvent.RemoveListener(OnFishCaught);
    }

    //starts invisible, and never catches a click
    private static void PrepareGroup(CanvasGroup group)
    {
        if (group == null)
            return;
        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;
        NoClicks(group.gameObject);
    }

    //the HUD sits over the pond: a click on it must still reach the fishing (it checks for the pointer over the UI)
    private static void NoClicks(GameObject root)
    {
        foreach (Graphic graphic in root.GetComponentsInChildren<Graphic>(true))
            graphic.raycastTarget = false;
    }

    // ---------------------------------------------------------------- the catch card

    private void OnFishCaught(FishingControl.Catch caught)
    {
        if (caught.fish == null)
            return;
        if (catchIcon != null)
        {
            Sprite sprite = caught.fish.item != null ? caught.fish.item.itemSprite : null;
            catchIcon.sprite = sprite;
            catchIcon.enabled = sprite != null;
        }
        if (catchName != null)
            catchName.text = caught.fish.fishName;
        if (catchDetail != null)
            catchDetail.text = FishData.WeightText(caught.weight) + "   $ " + caught.price;
        if (catchTag != null)
        {
            bool tagged = caught.trophy || caught.best;
            catchTag.enabled = tagged;
            catchTag.text = caught.trophy ? "Trophy catch!" : caught.best ? "New best!" : "";
            catchTag.color = caught.trophy ? trophyColor : bestColor;
        }
        catchShowing = true;
        catchAge = 0f;
        catchAfter = 0f;
    }

    // ---------------------------------------------------------------- every frame

    private void Update()
    {
        if (fishing == null)
            return;
        float dt = Time.unscaledDeltaTime;
        float time = Time.unscaledTime;
        FishingControl.FishingState state = fishing.GetFishingState();
        UpdateHint(state, dt, time);
        UpdateTension(state, dt, time);
        UpdateBiteAlert(state, dt);
        UpdateCatchCard(state, dt);
    }

    private Hint HintFor(FishingControl.FishingState state)
    {
        switch (state)
        {
            case FishingControl.FishingState.Aiming:
                if (!fishing.IsNearWater())
                    return Hint.None;
                if (!fishing.IsAimVisible())
                    return Hint.NoAim;
                return fishing.IsAimGood() ? Hint.Cast : Hint.BadAim;
            case FishingControl.FishingState.Waiting:
            case FishingControl.FishingState.Nibbling:
                return Hint.Wait;
            case FishingControl.FishingState.Biting:
                return Hint.Hook;
            case FishingControl.FishingState.Fighting:
                return Hint.Fight;
        }
        return Hint.None;//stowed, casting, landing
    }

    private void UpdateHint(FishingControl.FishingState state, float dt, float time)
    {
        Hint wanted = HintFor(state);
        FishingControl.AimProblem problem = fishing.GetAimProblem();
        if (wanted != hint || (wanted == Hint.BadAim && problem != shownProblem))
        {
            if (hint == Hint.None || (wanted == Hint.Hook && hint != Hint.Hook))
                hintAge = 0f;//pops in
            if (wanted == Hint.Wait && hint != Hint.Wait)
                waitTime = 0f;
            hint = wanted;
            shownProblem = problem;
            if (wanted != Hint.None)
                ShowHintText(wanted, problem);
        }
        bool aiming = hint == Hint.Cast || hint == Hint.BadAim || hint == Hint.NoAim;
        if (aiming)
            UpdateBaitText();

        float target = hint == Hint.None ? 0f : 1f;
        if (hint == Hint.Wait)
        {
            waitTime += dt;
            if (waitTime >= waitDimSeconds)
                target = waitDimAlpha;
        }
        if (hintGroup != null)
            hintGroup.alpha = Mathf.MoveTowards(hintGroup.alpha, target, dt / Mathf.Max(0.01f, fadeSeconds));

        if (hintPanel != null)
        {
            hintAge += dt;
            float scale = PopScale(hintAge);
            if (hint == Hint.Hook)
                scale *= 1.1f + 0.06f * Mathf.Sin(time * 11f);//"Hook it!" throbs
            hintPanel.localScale = Vector3.one * scale;
        }
    }

    private void ShowHintText(Hint wanted, FishingControl.AimProblem problem)
    {
        bool click = wanted == Hint.Cast || wanted == Hint.Wait || wanted == Hint.Hook || wanted == Hint.Fight;
        bool bait = wanted == Hint.Cast || wanted == Hint.BadAim || wanted == Hint.NoAim;
        if (leftClickIcon != null && leftClickIcon.activeSelf != click)
            leftClickIcon.SetActive(click);
        if (baitGroup != null && baitGroup.activeSelf != bait)
            baitGroup.SetActive(bait);
        if (rightClickIcon != null && rightClickIcon.activeSelf != bait)
            rightClickIcon.SetActive(bait);
        if (hintLabel == null)
            return;
        hintLabel.color = wanted == Hint.BadAim ? badAimColor : hintColor;
        switch (wanted)
        {
            case Hint.Cast:
                hintLabel.text = "Cast";
                break;
            case Hint.BadAim:
                hintLabel.text = ProblemText(problem);
                break;
            case Hint.NoAim:
                hintLabel.text = "Aim at the pond with the mouse";
                break;
            case Hint.Wait:
                hintLabel.text = "<size=75%>reel in</size>    Wait for a bite...";
                break;
            case Hint.Hook:
                hintLabel.text = "<size=125%><b>Hook it!</b></size>";
                break;
            case Hint.Fight:
                hintLabel.text = "Hold to reel   Let go when it pulls";
                break;
        }
    }

    private static string ProblemText(FishingControl.AimProblem problem)
    {
        switch (problem)
        {
            case FishingControl.AimProblem.TooCloseToShore: return "Too close to the bank";
            case FishingControl.AimProblem.TooFar: return "Too far: walk closer";
            case FishingControl.AimProblem.TooClose: return "Too close: aim farther out";
            case FishingControl.AimProblem.Ice: return "Frozen: aim at the hole in the ice";
        }
        return "Aim at the water";
    }

    //"Worm x 6" or "Lure", rebuilt only when the bait or the number of worms changes
    private void UpdateBaitText()
    {
        BaitData bait = fishing.GetBait();
        int count = fishing.GetBaitCount();
        if (bait == shownBait && count == shownBaitCount)
            return;
        shownBait = bait;
        shownBaitCount = count;
        if (baitLabel != null)
        {
            if (bait == null)
                baitLabel.text = "No bait";
            else
                baitLabel.text = bait.reusable ? bait.baitName : bait.baitName + " x " + count;
        }
        if (baitIcon != null)
        {
            Sprite sprite = bait != null && bait.item != null ? bait.item.itemSprite : null;
            baitIcon.sprite = sprite;
            baitIcon.enabled = sprite != null;
        }
    }

    // ---------------------------------------------------------------- the fight bar

    private void UpdateTension(FishingControl.FishingState state, float dt, float time)
    {
        if (tensionGroup == null)
            return;
        bool fighting = state == FishingControl.FishingState.Fighting;
        tensionGroup.alpha = Mathf.MoveTowards(tensionGroup.alpha, fighting ? 1f : 0f, dt / Mathf.Max(0.01f, fadeSeconds));
        if (tensionGroup.alpha <= 0f)
            return;
        float tension = fishing.GetTension();
        if (tensionFill != null && fighting)
        {
            tensionFill.fillAmount = tension;
            tensionFill.color = tension < 0.5f ? Color.Lerp(calmColor, strainColor, tension / 0.5f)
                : Color.Lerp(strainColor, dangerColor, (tension - 0.5f) / 0.35f);
        }
        bool danger = fighting && tension > 0.85f;
        if (tensionRect != null)
        {
            Vector2 shake = danger ? new Vector2(Mathf.Sin(time * 61f), Mathf.Sin(time * 47f + 1.3f)) * 2.5f : Vector2.zero;
            tensionRect.anchoredPosition = tensionHome + shake;
        }
        if (tensionDanger != null)
            tensionDanger.localScale = Vector3.one * (danger ? 1f + 0.15f * Mathf.Abs(Mathf.Sin(time * 12f)) : 1f);
        bool surging = fishing.IsSurging();
        if (tensionLabel != null && surging != shownSurging)
        {
            shownSurging = surging;
            tensionLabel.text = surging ? "PULLING!" : "";
            tensionLabel.color = dangerColor;
        }
    }

    // ---------------------------------------------------------------- the "!" over the bobber

    private void UpdateBiteAlert(FishingControl.FishingState state, float dt)
    {
        if (biteAlert == null)
            return;
        bool biting = state == FishingControl.FishingState.Biting;
        Camera cam = worldCamera != null ? worldCamera : Camera.main;
        Vector3 screen = Vector3.zero;
        if (biting && cam != null)
        {
            screen = cam.WorldToScreenPoint(fishing.GetCastTarget() + Vector3.up * biteAlertHeight);
            biting = screen.z > 0f;//not behind the camera
        }
        if (biting != biteShown)
        {
            SetBiteShown(biting);
            biteAge = 0f;
        }
        if (!biting)
            return;
        if (biteParent != null
            && RectTransformUtility.ScreenPointToLocalPointInRectangle(biteParent, screen, canvasCamera, out Vector2 local))
            biteAlert.anchoredPosition = local;
        biteAge += dt;
        float bob = 1f + 0.05f * Mathf.Sin(biteAge * 16f);
        biteAlert.localScale = Vector3.one * PopScale(biteAge * (popSeconds / 0.28f)) * bob;
    }

    private void SetBiteShown(bool on)
    {
        if (biteAlert == null || on == biteShown)
            return;
        biteShown = on;
        biteAlert.gameObject.SetActive(on);
    }

    // ---------------------------------------------------------------- the catch card

    private void UpdateCatchCard(FishingControl.FishingState state, float dt)
    {
        if (catchGroup == null)
            return;
        float target = 0f;
        if (catchShowing)
        {
            catchAge += dt;
            if (state != FishingControl.FishingState.Landing)
                catchAfter += dt;
            if (catchAfter >= catchFadeDelay)
                catchShowing = false;
            else
                target = 1f;
        }
        //fades out a little slower than it comes in
        float seconds = target > catchGroup.alpha ? fadeSeconds : 0.3f;
        catchGroup.alpha = Mathf.MoveTowards(catchGroup.alpha, target, dt / Mathf.Max(0.01f, seconds));
        if (catchPanel != null && catchGroup.alpha > 0f)
            catchPanel.localScale = Vector3.one * PopScale(catchAge * (popSeconds / 0.3f));
    }

    //ease out with a little overshoot, from 0.7 to 1 (the same bounce as the interaction prompt)
    private float PopScale(float age)
    {
        float k = Mathf.Clamp01(age / Mathf.Max(0.01f, popSeconds));
        const float c = 1.7f;
        float ease = 1f + (c + 1f) * Mathf.Pow(k - 1f, 3f) + c * Mathf.Pow(k - 1f, 2f);
        return Mathf.LerpUnclamped(0.7f, 1f, ease);
    }
}
