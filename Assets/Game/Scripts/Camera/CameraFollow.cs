using UnityEngine;

public class CameraFollow:MonoBehaviour
{
    public GameObject player;

    //how far the camera stays behind the player (the example used 3). Together with the camera's own height and tilt
    //this decides where on the screen the player is; the farm scenes use a value that keeps the farmer near the middle.
    [SerializeField]
    private float distanceBehind = 3f;

    //while the player rests in the sleep area the camera holds still on this point (the bed) while the farmer
    //lies in bed (the body is locked until a movement key gets them up). Empty = always follow the player.
    [SerializeField]
    private Transform restFocus;
    [SerializeField]
    private float restBlendSeconds = 0.5f;

    private PlayerEnergy playerEnergy;
    private float restBlend = 0f;//0 = following the player, 1 = holding on the bed

    void Start()
    {
        playerEnergy = player.GetComponent<PlayerEnergy>();
    }

    //LateUpdate instead of Update, so the camera reads the player's position after it moved this frame (no jitter)
    void LateUpdate()
    {
        Vector3 follow = PositionFor(player.transform.position);

        //ease over to the bed when resting starts and back to the player when it ends
        bool resting = restFocus != null && playerEnergy != null && playerEnergy.IsResting();
        restBlend = Mathf.MoveTowards(restBlend, resting ? 1f : 0f, Time.deltaTime / Mathf.Max(0.01f, restBlendSeconds));
        if (restBlend <= 0f)
            transform.position = follow;
        else
            transform.position = Vector3.Lerp(follow, PositionFor(restFocus.position), Mathf.SmoothStep(0f, 1f, restBlend));
    }

    //the camera keeps its own height and looks at the target from distanceBehind
    private Vector3 PositionFor(Vector3 target)
    {
        return new Vector3(target.x, transform.position.y, target.z - distanceBehind);
    }
}
