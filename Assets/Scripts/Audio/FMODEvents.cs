using UnityEngine;
using FMODUnity;
public class FMODEvents : MonoBehaviour
{

    [field: Header("Sequences")]
    [field: SerializeField] public EventReference welcomeSequence { get; private set; }

    public static FMODEvents instance { get; private set; }

    private void Awake()
    {
        if (instance != null)
        {
            Debug.LogError("Found more than one Fmod Events script in the scene.");
        }

        instance = this;

    }
}
