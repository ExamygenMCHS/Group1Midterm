using System.Collections;
using UnityEngine;
using UnityEngine.Events;

// Runs one delivery on the mountain road: drive the loaded truck down to the drop-off circle.
// Crashes damage the truck and the cargo; a wrecked truck is towed back to the top (the cargo keeps its damage).
// Arriving with cargo opens the wholesale market. Without cargo (the scene played on its own) the drive is practice
// and arriving just puts the truck back at the top.
public class DeliveryMission : MonoBehaviour
{
    public enum MissionState { Driving, Wrecked, Delivered }

    [SerializeField] private TruckDamage truck;
    [SerializeField] private DropOffZone dropOff;
    [SerializeField] private TruckRespawn respawn;
    [SerializeField] private string marketScene = "Wholesale Market";

    [Header("Cargo")]
    [Tooltip("Share of the cargo's value lost per point of truck damage.")]
    [SerializeField] private float cargoLossPerDamage = .004f;
    [Tooltip("The cargo is never worth less than this share of its value.")]
    [SerializeField, Range(0f, 1f)] private float worstCondition = .25f;

    [Header("Timing")]
    [SerializeField] private float towSeconds = 2.5f;
    [SerializeField] private float arriveSeconds = 1.5f;

    [SerializeField] private UnityEvent onStateChanged = new UnityEvent();
    [SerializeField] private UnityEvent onCargoChanged = new UnityEvent();

    public MissionState State { get; private set; } = MissionState.Driving;
    public UnityEvent StateChanged { get { return onStateChanged; } }
    public UnityEvent CargoChanged { get { return onCargoChanged; } }
    public TruckDamage Truck { get { return truck; } }
    /// <summary>False when the scene is played on its own with an empty truck.</summary>
    public bool HasCargo { get { return DeliveryCargo.HasCargo; } }

    private void Start()
    {
        if (truck != null)
        {
            truck.Damaged.AddListener(OnTruckDamaged);
            truck.Wrecked.AddListener(OnTruckWrecked);
        }
        if (dropOff != null)
            dropOff.TruckArrived.AddListener(OnTruckArrived);
    }

    private void OnDestroy()
    {
        if (truck != null)
        {
            truck.Damaged.RemoveListener(OnTruckDamaged);
            truck.Wrecked.RemoveListener(OnTruckWrecked);
        }
        if (dropOff != null)
            dropOff.TruckArrived.RemoveListener(OnTruckArrived);
    }

    // The only place the mission state changes.
    private void ChangeState(MissionState next)
    {
        if (next == State)
            return;
        State = next;
        onStateChanged.Invoke();
    }

    private void OnTruckDamaged(float amount)
    {
        if (State == MissionState.Delivered || !DeliveryCargo.HasCargo)
            return;
        DeliveryCargo.SetCondition(Mathf.Max(worstCondition, DeliveryCargo.Condition - amount * cargoLossPerDamage));
        onCargoChanged.Invoke();
    }

    private void OnTruckWrecked()
    {
        if (State != MissionState.Driving)
            return;
        ChangeState(MissionState.Wrecked);
        StartCoroutine(TowBack());
    }

    private IEnumerator TowBack()
    {
        yield return new WaitForSeconds(towSeconds);
        RestartAtTheTop();
    }

    private void RestartAtTheTop()
    {
        if (respawn != null) respawn.Respawn();
        if (truck != null) truck.Repair();
        ChangeState(MissionState.Driving);
    }

    private void OnTruckArrived()
    {
        if (State == MissionState.Delivered)
            return;
        if (truck != null && truck.TryGetComponent(out TruckDriveAudio audio))
            audio.PlayCheckpoint();
        if (!DeliveryCargo.HasCargo)
        {
            // Practice drive: nothing to sell, so go again from the top.
            StopAllCoroutines();
            RestartAtTheTop();
            return;
        }
        StopAllCoroutines();
        ChangeState(MissionState.Delivered);
        StartCoroutine(GoToMarket());
    }

    private IEnumerator GoToMarket()
    {
        // The driver brakes in the circle before the scene changes.
        BasicCarController car = truck != null ? truck.GetComponent<BasicCarController>() : null;
        if (car != null)
            car.Override = new BasicCarController.DriveInput(0f, 0f, 0f, 1f);
        yield return new WaitForSeconds(arriveSeconds);
        FarmTrip.Continue(marketScene);
    }
}
