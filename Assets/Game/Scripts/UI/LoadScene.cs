using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LoadScene:MonoBehaviour
{
    [SerializeField]
    private AudioSource confirmSound;
    private bool loading;

    public void LadScenebyName(string name)
    {
        if (loading)
            return;
        if (confirmSound == null || confirmSound.clip == null)
        {
            SceneManager.LoadScene(name);
            return;
        }

        loading = true;
        StartCoroutine(PlayConfirmationAndLoad(name));
    }

    private IEnumerator PlayConfirmationAndLoad(string name)
    {
        //let the short cue finish before the title scene unloads its source
        confirmSound.Play();
        yield return new WaitForSecondsRealtime(confirmSound.clip.length);
        SceneManager.LoadScene(name);
    }
}
