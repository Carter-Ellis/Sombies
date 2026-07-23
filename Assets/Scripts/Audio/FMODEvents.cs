using UnityEngine;
using FMODUnity;
public class FMODEvents : MonoBehaviour
{

    [field: Header("Sequences")]
    [field: SerializeField] public EventReference welcomeSequence { get; private set; }

    [field: Header("Mystery Box")]
    [field: SerializeField] public EventReference mysteryBoxOpen { get; private set; }

    [field: Header("Melee")]
    [field: SerializeField] public EventReference meleeAttack { get; private set; }

    [field: Header("Revive")]
    [field: SerializeField] public EventReference downed { get; private set; }
    [field: SerializeField] public EventReference reviveSequence { get; private set; }

    [field: Header("Player")]
    [field: SerializeField] public EventReference playerHurt { get; private set; }

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
