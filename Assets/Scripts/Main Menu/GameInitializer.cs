using UnityEngine;

public class GameInitializer : MonoBehaviour
{
    [SerializeField] private GameObject uiManagerPrefab;

    void Awake()
    {
        // Check if a UIManager already exists
        if (UIManager.Instance == null)
        {
            Instantiate(uiManagerPrefab);
        }
    }
}