using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// Leaves the farm for another scene (the delivery road, the market) and comes back without losing the farm.
// The farm scene stays loaded but switched off while the player is away, so its calendar, tiles and plants are
// exactly as they were on return. Only one trip scene is loaded at a time, on top of the parked farm.
public static class FarmTrip
{
    public const string FarmScene = "The Farm";

    private static Scene home;
    private static readonly List<GameObject> parked = new List<GameObject>();
    private static CursorLockMode homeCursorLock;
    private static bool homeCursorVisible;
    private static string pending; // the trip scene being loaded, made the active scene once it is in

    /// <summary>True while the farm is parked and a trip scene is on top of it.</summary>
    public static bool IsAway { get { return parked.Count > 0; } }

    // Play Mode in this project starts without a domain reload, so the trip from the last play session must be cleared
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        parked.Clear();
        pending = null;
        home = default;
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    // Parks the scene the player is in (the farm) and opens the trip scene on top of it.
    public static void Leave(string tripScene)
    {
        if (IsAway)
        {
            Continue(tripScene);
            return;
        }
        home = SceneManager.GetActiveScene();
        homeCursorLock = Cursor.lockState;
        homeCursorVisible = Cursor.visible;
        foreach (GameObject root in home.GetRootGameObjects())
        {
            if (!root.activeSelf)
                continue;
            root.SetActive(false);
            parked.Add(root);
        }
        LoadOnTop(tripScene);
    }

    // Swaps the current trip scene for the next one (road -> market). Played without a parked farm, it is a plain load.
    public static void Continue(string tripScene)
    {
        if (!IsAway)
        {
            SceneManager.LoadScene(tripScene);
            return;
        }
        Scene current = SceneManager.GetActiveScene();
        if (current != home)
            SceneManager.UnloadSceneAsync(current);
        LoadOnTop(tripScene);
    }

    // Closes the trip scene and switches the farm back on. Played without a parked farm, it loads the farm.
    public static void Return()
    {
        Time.timeScale = 1f;
        if (!IsAway || !home.IsValid() || !home.isLoaded)
        {
            parked.Clear();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            SceneManager.LoadScene(FarmScene);
            return;
        }
        Scene trip = SceneManager.GetActiveScene();
        SceneManager.SetActiveScene(home);
        foreach (GameObject root in parked)
            if (root != null)
                root.SetActive(true);
        parked.Clear();
        Cursor.lockState = homeCursorLock;
        Cursor.visible = homeCursorVisible;
        if (trip != home)
            SceneManager.UnloadSceneAsync(trip);
    }

    private static void LoadOnTop(string tripScene)
    {
        pending = tripScene;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        SceneManager.LoadScene(tripScene, LoadSceneMode.Additive);
    }

    // The trip scene becomes the active scene, so its lighting is used and new objects land in it.
    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != pending)
            return;
        pending = null;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.SetActiveScene(scene);
    }
}
