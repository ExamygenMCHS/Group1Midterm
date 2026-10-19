using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Events;

public class FarmTileControl:MonoBehaviour
{
    public enum FarmTileCond //used for the switch states
    {
        Grass, Tilled, Watered
    }
    public FarmTileCond tileCond;

    //3 materials representing 3 different farm tile conditions
    public Material grassMat, tilledMat, wateredMat;
    private List<Material> curTileEdgeMaterial=new List<Material>();

    private AudioSource stepTileAudio;
    private AudioSource tillTileAudio;
    private AudioSource waterTileAudio;

    private int daysPassedSinceLastInteraction=0;
    [SerializeField]
    private GameObject dayNightControl;

    //days without interaction before the tile goes back one condition (was hard coded as 2)
    public int daysUntilRevert = 2;
    //this event gets fired every time the tile changes condition, so other objects (the win condition) can listen to it
    public UnityEvent tileCondChangedEvent = new UnityEvent();

    public void Start()
    {
        tileCond = FarmTileCond.Grass;//start with grass
        GameObject tileEdge;
        
        foreach (Transform edge in transform)
        {
            tileEdge = edge.gameObject;
            curTileEdgeMaterial.Add(tileEdge.GetComponent<MeshRenderer>().material);
        }
        //get access to the audio clips attached on the tile object
        AudioSource[] audios = gameObject.GetComponents<AudioSource>();
        stepTileAudio = audios[0];
        tillTileAudio = audios[1];
        waterTileAudio =audios[2];

        dayNightControl = GameObject.Find("DayNightController");
        dayNightControl.GetComponent<DayNightControl>().listenToDayPassEvent(this.gameObject);
    }

    public void UpdateTileMaterial()//called to update the tile appearance when the farm tile changes states
    {
        switch (tileCond)
        {
            case FarmTileCond.Grass:
                GetComponent<MeshRenderer>().material = grassMat;
                break;
            case FarmTileCond.Tilled:
                GetComponent<MeshRenderer>().material = tilledMat;
                break;
            case FarmTileCond.Watered:
                GetComponent<MeshRenderer>().material = wateredMat;
                break;
        }
        tileCondChangedEvent.Invoke();//make announcement to all listeners
    }

    //API prepared for the player to interact with tiles when steping on it and press the space key
    public void InteractWithFarmTile()
    {
        switch (tileCond){
            case FarmTileCond.Grass://interact with the grass tile tills it
                tillTileAudio.Play();
                tileCond = FarmTileCond.Tilled;
                UpdateTileMaterial();
                daysPassedSinceLastInteraction = 0;
                break;
            case FarmTileCond.Tilled://interact with the tilled land waters it
                waterTileAudio.Play();
                tileCond= FarmTileCond.Watered;
                UpdateTileMaterial();
                daysPassedSinceLastInteraction = 0;
                break;
            case FarmTileCond.Watered://repeated interaction on the watered tile does not change it to other conditions
                daysPassedSinceLastInteraction = 0;
                break;
        }
    }

    //API prepared for the player interaction point to select a tile
    //When the player's interaction point (a bit in front of the player) interact with a tile,
    //this tile's material gets brighter to show the current tile that the player is about to interact with
    public void EnterTile()
    {
        foreach(Material mat in curTileEdgeMaterial )
            mat.EnableKeyword("_EMISSION");


        //play the audio clip
        stepTileAudio.Play();
    }
    public void ExitTile()
    {
        foreach (Material mat in curTileEdgeMaterial)
            mat.DisableKeyword("_EMISSION");
    }

    //API provided for the day night counter to tell the tile that a day just passed
    public void ADayPassed()
    {
        daysPassedSinceLastInteraction++;
        if (daysPassedSinceLastInteraction >= daysUntilRevert)//enough days passed since last farm tile interaction
        {
            switch (tileCond)
            {
                case FarmTileCond.Watered: //soil dry after 2 days
                    tileCond = FarmTileCond.Tilled;
                    UpdateTileMaterial();
                    daysPassedSinceLastInteraction = 0;
                    break;
                case FarmTileCond.Tilled: //grass grows back after 2 days
                    tileCond = FarmTileCond.Grass;
                    UpdateTileMaterial();
                    daysPassedSinceLastInteraction = 0;
                    break;
            }
        }
    }

}
