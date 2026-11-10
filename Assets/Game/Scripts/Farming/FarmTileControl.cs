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
    //the tile's edge uses the shared edge material, and the shared glow one (emission on) while the player's
    //interaction point is on the tile. Swapping shared materials keeps every tile batched, unlike a material copy per edge.
    [SerializeField]
    private Material edgeGlowMat;
    private readonly List<MeshRenderer> edgeRenderers = new List<MeshRenderer>();
    private Material edgeMat;
    private MeshRenderer tileRenderer;

    //the tile sounds; every tile plays them through one shared source (they are not positional)
    [SerializeField]
    private AudioClip stepClip;
    [SerializeField]
    private AudioClip tillClip;
    [SerializeField]
    private AudioClip waterClip;
    private static AudioSource sharedAudio;

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
        foreach (Transform edge in transform)
        {
            MeshRenderer edgeRenderer = edge.GetComponent<MeshRenderer>();
            if (edgeRenderer == null)
                continue;
            edgeRenderers.Add(edgeRenderer);
            if (edgeMat == null)
                edgeMat = edgeRenderer.sharedMaterial;
        }

        dayNightControl = GameObject.Find("DayNightController");
        dayNightControl.GetComponent<DayNightControl>().listenToDayPassEvent(this.gameObject);
    }

    private static void PlayTileSound(AudioClip clip, float volume = 1f)
    {
        if (clip == null)
            return;
        if (sharedAudio == null)//created on first use; it goes with the scene, the next scene makes a new one
        {
            sharedAudio = new GameObject("Tile Audio").AddComponent<AudioSource>();
            sharedAudio.playOnAwake = false;
        }
        sharedAudio.PlayOneShot(clip, volume);
    }

    private void SetEdgeGlow(bool glowing)
    {
        Material material = glowing && edgeGlowMat != null ? edgeGlowMat : edgeMat;
        if (material == null)
            return;
        foreach (MeshRenderer edgeRenderer in edgeRenderers)
            edgeRenderer.sharedMaterial = material;
    }

    public void UpdateTileMaterial()//called to update the tile appearance when the farm tile changes states
    {
        if (tileRenderer == null)
            tileRenderer = GetComponent<MeshRenderer>();
        switch (tileCond)
        {
            case FarmTileCond.Grass:
                tileRenderer.sharedMaterial = grassMat;
                break;
            case FarmTileCond.Tilled:
                tileRenderer.sharedMaterial = tilledMat;
                break;
            case FarmTileCond.Watered:
                tileRenderer.sharedMaterial = wateredMat;
                break;
        }
        tileCondChangedEvent.Invoke();//make announcement to all listeners
    }

    //API prepared for the player to interact with tiles when steping on it and press the space key
    public void InteractWithFarmTile()
    {
        switch (tileCond){
            case FarmTileCond.Grass://interact with the grass tile tills it
                PlayTileSound(tillClip, 0.4f);
                tileCond = FarmTileCond.Tilled;
                UpdateTileMaterial();
                daysPassedSinceLastInteraction = 0;
                break;
            case FarmTileCond.Tilled://interact with the tilled land waters it
            case FarmTileCond.Watered://repeated interaction on the watered tile does not change it to other conditions
                WaterTile();
                break;
        }
    }

    public void WaterTile()
    {
        PlayTileSound(waterClip);
        if (tileCond == FarmTileCond.Grass)
            return;//water still lands if the tile reverted to grass during the animation
        daysPassedSinceLastInteraction = 0;
        if (tileCond == FarmTileCond.Tilled)
        {
            tileCond = FarmTileCond.Watered;
            UpdateTileMaterial();
        }
    }

    //API prepared for the player interaction point to select a tile
    //When the player's interaction point (a bit in front of the player) interact with a tile,
    //this tile's material gets brighter to show the current tile that the player is about to interact with
    public void EnterTile()
    {
        SetEdgeGlow(true);
        PlayTileSound(stepClip, 0.1f);
    }
    public void ExitTile()
    {
        SetEdgeGlow(false);
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
