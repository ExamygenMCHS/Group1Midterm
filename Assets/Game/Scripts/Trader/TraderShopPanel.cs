using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

//Owns one reusable shop overlay. The world keeps running while keyboard-driven farm controls wait.
public class TraderShopPanel : MonoBehaviour
{
    [SerializeField] private StoreControl store;
    [SerializeField] private Canvas shopCanvas;
    [SerializeField] private UnityEngine.UI.GraphicRaycaster raycaster;
    [SerializeField] private UnityEngine.UI.Button closeButton;
    [Tooltip("Optional input or NPC movement scripts outside the player's scene to suspend during trade.")]
    [SerializeField] private Behaviour[] additionalControls;

    private static TraderShopPanel activePanel;
    private readonly List<Behaviour> suspended = new List<Behaviour>();
    private WorldInteractor player;
    private WanderingTrader trader;
    private EventSystem eventSystem;
    private GameObject previousSelection;
    private CursorLockMode previousCursorLock;
    private bool previousCursorVisible;
    private bool closing;

    public bool IsOpen { get; private set; }
    public static bool IsAnyOpen { get { return activePanel != null; } }
    public StoreControl Store { get { return store; } }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { activePanel = null; }

    private void Awake()
    {
        SetVisible(false);
        if (closeButton != null)
            closeButton.onClick.AddListener(Close);
    }

    public bool TryOpen(WorldInteractor shopper, WanderingTrader owner)
    {
        if (IsAnyOpen || !isActiveAndEnabled || shopper == null || owner == null ||
            store == null || shopCanvas == null || raycaster == null || closeButton == null ||
            FarmFund.Instance == null || EventSystem.current == null)
            return false;

        player = shopper;
        trader = owner;
        eventSystem = EventSystem.current;
        previousSelection = eventSystem.currentSelectedGameObject;
        previousCursorLock = Cursor.lockState;
        previousCursorVisible = Cursor.visible;
        activePanel = this;
        IsOpen = true;
        closing = false;

        store.PrepareShop();
        SuspendGameplay();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        SetVisible(true);
        store.RefreshShop();
        eventSystem.SetSelectedGameObject(closeButton.gameObject);
        PlayPanelSound();
        return true;
    }

    private void SuspendGameplay()
    {
        //Scene-scoped discovery happens once per opening, never every frame. It includes PlantManager
        //and the bag canvas, which are not necessarily children of the player in the existing farm scenes.
        foreach (GameObject root in player.gameObject.scene.GetRootGameObjects())
        {
            foreach (MonoBehaviour component in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (component is PlayerMove movement && movement.isActiveAndEnabled && movement.rb != null)
                    movement.StopPlayer();
                if (component is FarmBag bag && bag.isActiveAndEnabled && bag.IsBagOpen())
                    bag.SetBagOpen(false);
                if (component is InteractionPrompt prompt && prompt.isActiveAndEnabled)
                    prompt.Hide();
                if (component is PlayerMove || component is PlayerInteractPoint || component is PlantManager ||
                    component is Hotbar || component is WorldInteractor || component is FarmBag || component is TimeFastForward)
                    Suspend(component);
            }
        }
        if (additionalControls != null)
            foreach (Behaviour control in additionalControls)
                Suspend(control);
    }

    private void Suspend(Behaviour control)
    {
        if (control == null || control == this || !control.enabled || suspended.Contains(control))
            return;
        suspended.Add(control);
        control.enabled = false;
    }

    private void Update()
    {
        if (!IsOpen)
            return;
        if (player == null || !player.gameObject.activeInHierarchy || trader == null || !trader.isActiveAndEnabled ||
            eventSystem == null || !eventSystem.isActiveAndEnabled || shopCanvas == null || !shopCanvas.enabled ||
            (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame))
            Close();
    }

    public void Close()
    {
        if (!IsOpen)
            return;
        IsOpen = false;
        closing = true;
        SetVisible(false);
        PlayPanelSound();
        //Keep gameplay disabled for the rest of this Update. A UI submit/close key must not also
        //till, plant, or reopen the shop on the frame the button receives it.
    }

    private void PlayPanelSound()
    {
        if (player != null && player.TryGetComponent(out FarmPlayerAudio playerAudio))
            playerAudio.PlayUiClick();
    }

    private void LateUpdate()
    {
        if (closing)
            ReleaseControls();
    }

    private void SetVisible(bool visible)
    {
        if (shopCanvas != null)
            shopCanvas.enabled = visible;
        if (raycaster != null)
            raycaster.enabled = visible;
    }

    private void ReleaseControls()
    {
        if (activePanel != this)
            return;
        IsOpen = false;
        closing = false;
        SetVisible(false);
        foreach (Behaviour control in suspended)
            if (control != null)
                control.enabled = true;
        suspended.Clear();
        Cursor.lockState = previousCursorLock;
        Cursor.visible = previousCursorVisible;
        if (eventSystem != null)
            eventSystem.SetSelectedGameObject(previousSelection != null && previousSelection.activeInHierarchy ? previousSelection : null);
        activePanel = null;
        player = null;
        trader = null;
    }

    private void OnDisable() { ReleaseControls(); }

    private void OnDestroy()
    {
        ReleaseControls();
        if (closeButton != null)
            closeButton.onClick.RemoveListener(Close);
    }
}
