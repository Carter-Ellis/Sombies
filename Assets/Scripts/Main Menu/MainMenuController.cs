using UnityEngine;
using UnityEngine.SceneManagement;
using FMODUnity;
public class MainMenuController : MonoBehaviour
{
    [SerializeField] private EventReference welcomeSequence;

    private void Awake()
    {
        AudioManager.Instance.PlayOneShot(welcomeSequence, Vector3.zero);
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