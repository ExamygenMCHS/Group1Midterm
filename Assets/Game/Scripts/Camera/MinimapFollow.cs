using UnityEngine;

//keeps the mini-map camera above the player (from the week 5 mini-map tutorial)
public class MinimapFollow : MonoBehaviour
{
    [SerializeField]
    private Transform player;
    [SerializeField]
    private bool rotateWithPlayer = false;//off = the top of the map is always north (+z)

    //LateUpdate runs after all the Update calls, so the camera moves after the player has moved
    private void LateUpdate()
    {
        Vector3 newPosition = player.position;
        newPosition.y = transform.position.y;//keep the camera's own height
        transform.position = newPosition;

        if (rotateWithPlayer)
            transform.rotation = Quaternion.Euler(90f, player.eulerAngles.y, 0f);
    }
}
