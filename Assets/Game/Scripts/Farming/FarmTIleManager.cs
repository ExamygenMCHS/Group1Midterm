using UnityEngine;

public class FarmTIleManager:MonoBehaviour
{
    public GameObject FarmTile;
    public Transform FarmTileSpawnStartLocation;
    private Vector3 curFarmTileSpawnPos;
    
    public int numFarmTileX = 4;
    public int numFarmTileZ = 4;
    public float gapSizeBetweenTiles = 0.1f;
    
    private void Start()
    {
        curFarmTileSpawnPos=FarmTileSpawnStartLocation.position;
        curFarmTileSpawnPos.y = - FarmTile.transform.localScale.y / 2 + 0.05f;//move top of tile close to the ground surface
        int numTileGenerated = 0;
        for (int i = 0; i < numFarmTileZ; i++)
        {
            for (int j = 0; j < numFarmTileX; j++)//generate a row of tiles
            {
                GameObject curFarmTile = Instantiate(FarmTile, curFarmTileSpawnPos, Quaternion.identity);
                curFarmTileSpawnPos.x += FarmTile.transform.localScale.x + gapSizeBetweenTiles;//offset tiles
                numTileGenerated++;
                curFarmTile.name = "Farm Tile " + numTileGenerated.ToString();
                curFarmTile.transform.parent = transform;//put all the tiles under the tile manager
            }
            curFarmTileSpawnPos.z += FarmTile.transform.localScale.z + gapSizeBetweenTiles;
            curFarmTileSpawnPos.x = FarmTileSpawnStartLocation.position.x;
        }
    }
}
