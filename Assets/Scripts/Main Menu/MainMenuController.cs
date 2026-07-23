using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuController : MonoBehaviour
{

    private void Awake()
    {
        Audio.playSFX(FMODEvents.instance.welcomeSequence, Vector3.zero);
    }

    public void StartGame()
    {
        // This loads the next scene in your Build Settings
        SceneManager.LoadScene("SampleScene");
    }

    public void QuitGame()
    {
        Application.Quit();
        Debug.Log("Game Exited");
    }
}