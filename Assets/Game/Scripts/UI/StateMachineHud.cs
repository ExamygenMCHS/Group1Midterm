using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

//a small line on the HUD that shows the game's state machines at work: the player's state, the tool in the hand
//and the season. It is updated only by their events (playerStateChangedEvent, toolChangedEvent, newSeasonEvent),
//so it also shows that the event set-up works. Tab hides or shows it.
public class StateMachineHud : MonoBehaviour
{
    [SerializeField]
    private PlayerStateControl playerStateControl;
    [SerializeField]
    private ToolManager toolManager;
    [SerializeField]
    private GameCalendar calendar;
    [SerializeField]
    private TMP_Text label;
    [SerializeField]
    private GameObject panel;
    [SerializeField]
    private Key toggleKey = Key.Tab;

    private void Start()
    {
        playerStateControl.playerStateChangedEvent.AddListener(Refresh);
        toolManager.toolChangedEvent.AddListener(Refresh);
        calendar.newSeasonEvent.AddListener(Refresh);
        Refresh();
    }

    private void OnDestroy()
    {
        if (playerStateControl != null)
            playerStateControl.playerStateChangedEvent.RemoveListener(Refresh);
        if (toolManager != null)
            toolManager.toolChangedEvent.RemoveListener(Refresh);
        if (calendar != null)
            calendar.newSeasonEvent.RemoveListener(Refresh);
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current[toggleKey].wasPressedThisFrame && panel != null)
            panel.SetActive(!panel.activeSelf);
    }

    public void Refresh()
    {
        const string key = "<color=#FFD45E>";
        const string end = "</color>";
        label.text = "Player: " + key + Spaced(playerStateControl.GetPlayerState().ToString()) + end
            + "\nTool: " + key + ToolName(toolManager.GetCurTool()) + end
            + "\nSeason: " + key + calendar.season + end;
    }

    private static string ToolName(ToolManager.ToolType tool)
    {
        return tool == ToolManager.ToolType.None ? "None" : Spaced(tool.ToString());
    }

    //"WateringCan" -> "Watering Can"
    private static string Spaced(string name)
    {
        var builder = new System.Text.StringBuilder();
        for (int i = 0; i < name.Length; i++)
        {
            if (i > 0 && char.IsUpper(name[i]))
                builder.Append(' ');
            builder.Append(name[i]);
        }
        return builder.ToString();
    }
}
