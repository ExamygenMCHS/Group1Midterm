using UnityEngine;

//a trigger zone. Walking into it puts the player in bed, where they regain energy quickly.
//PlayerMove keeps them in bed until a movement key gets them up; they rest again the next time they walk in.
public class SleepArea : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        SetPlayerResting(other, true);
    }
    private void OnTriggerExit(Collider other)
    {
        SetPlayerResting(other, false);
    }

    private void SetPlayerResting(Collider other, bool resting)
    {
        if (other.isTrigger)//only the player's body counts, not the PlayerInteractPoint trigger in front of it
            return;
        PlayerEnergy playerEnergy = other.GetComponentInParent<PlayerEnergy>();
        if (playerEnergy != null)
            playerEnergy.SetResting(resting);
    }
}
