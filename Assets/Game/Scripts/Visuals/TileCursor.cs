using UnityEngine;

//Visual only: a glowing bracket that hovers over the tile the player is about to work on
//(the same tile the example highlights through its edge emission), so the target is easy to see.
public class TileCursor : MonoBehaviour
{
    [SerializeField]
    private PlayerInteractPoint playerInteractPoint;
    [SerializeField]
    private Transform cursor;
    [SerializeField]
    private float heightAboveTile = 0.04f;
    [SerializeField]
    private float pulse = 0.05f;
    [SerializeField]
    private float followSpeed = 25f;

    private GameObject shownTile;

    private void LateUpdate()
    {
        GameObject tile = playerInteractPoint != null ? playerInteractPoint.GetCurTile() : null;
        if (tile == null)
        {
            if (cursor.gameObject.activeSelf)
                cursor.gameObject.SetActive(false);
            shownTile = null;
            return;
        }
        Vector3 target = tile.transform.position + Vector3.up * (tile.transform.lossyScale.y * 0.5f + heightAboveTile);
        if (!cursor.gameObject.activeSelf || shownTile == null)
        {
            cursor.gameObject.SetActive(true);
            cursor.position = target;//appear in place, then glide between tiles
        }
        shownTile = tile;
        cursor.position = Vector3.Lerp(cursor.position, target, 1f - Mathf.Exp(-followSpeed * Time.deltaTime));
        float s = 1f + Mathf.Sin(Time.time * 6f) * pulse;
        cursor.localScale = new Vector3(s, 1f, s);
    }
}
