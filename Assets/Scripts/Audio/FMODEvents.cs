using UnityEngine;
using FMODUnity;

public class FMODEvents : MonoBehaviour
{
    [field: Header("Sequences")]
    [field: SerializeField] public EventReference welcomeSequence { get; set; }

    [field: Header("Mystery Box")]
    [field: SerializeField] public EventReference mysteryBoxOpen { get; set; }

    [field: Header("Melee")]
    [field: SerializeField] public EventReference meleeAttack { get; set; }

    [field: Header("Revive")]
    [field: SerializeField] public EventReference downed { get; set; }
    [field: SerializeField] public EventReference reviveSequence { get; set; }

    [field: Header("Player")]
    [field: SerializeField] public EventReference playerHurt { get; set; }

    [field: Header("Potion")]
    [field: SerializeField] public EventReference swig { get; set; }
    [field: SerializeField] public EventReference potionBounce { get; set; }

    [field: Header("Music")]
    [field: SerializeField] public EventReference sombieStyle { get; set; }

    [field: Header("Ambience")]
    [field: SerializeField] public EventReference stormAmbience { get; set; }
    [field: SerializeField] public EventReference fireCrackling { get; set; }

    [field: Header("Door")]
    [field: SerializeField] public EventReference doorOpen { get; set; }

    [field: Header("ItemPickup")]
    [field: SerializeField] public EventReference itemPickup { get; set; }

    [field: Header("Throw")]
    [field: SerializeField] public EventReference itemThrow { get; set; }

    [field: Header("Potion Vendor")]
    [field: SerializeField] public EventReference potionBuy { get; set; }

    [field: Header("Player Walk")]
    [field: SerializeField] public EventReference walkWood { get; set; }

    [field: Header("Enemy")]
    [field: SerializeField] public EventReference enemyHurt { get; set; }

    [field: Header("Cauldron")]
    [field: SerializeField] public EventReference bubbling { get; set; }


    public static FMODEvents instance { get; private set; }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Debug.LogWarning("Found more than one FMODEvents script in the scene; destroying duplicate.");
            Destroy(gameObject);
            return;
        }

        instance = this;
    }
}
