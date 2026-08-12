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

    [field: Header("FireCrackling")]
    [field: SerializeField] public EventReference fireCrackling { get; set; }

    [field: Header("Door")]
    [field: SerializeField] public EventReference doorOpen { get; set; }

    [field: Header("ItemPickup")]
    [field: SerializeField] public EventReference itemPickup { get; set; }

    [field: Header("Throw")]
    [field: SerializeField] public EventReference itemThrow { get; set; }



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
        EnsureValidEvents();
    }

    public void EnsureValidEvents()
    {
        if (welcomeSequence.IsNull) welcomeSequence = RuntimeManager.PathToEventReference("event:/SFX/Sequences/Welcome");
        if (mysteryBoxOpen.IsNull) mysteryBoxOpen = RuntimeManager.PathToEventReference("event:/SFX/MysteryBox/MysteryBoxPurchase");
        if (meleeAttack.IsNull) meleeAttack = RuntimeManager.PathToEventReference("event:/SFX/Melee/MeleeAttack");
        if (downed.IsNull) downed = RuntimeManager.PathToEventReference("event:/SFX/Revive/Downed");
        if (reviveSequence.IsNull) reviveSequence = RuntimeManager.PathToEventReference("event:/SFX/Revive/Revive Sequence");
        if (playerHurt.IsNull) playerHurt = RuntimeManager.PathToEventReference("event:/SFX/Player/PlayerHurt");
        if (swig.IsNull) swig = RuntimeManager.PathToEventReference("event:/SFX/Potion/Swig");
        if (potionBounce.IsNull) potionBounce = RuntimeManager.PathToEventReference("event:/SFX/Potion/PotionBounce");
        if (sombieStyle.IsNull) sombieStyle = RuntimeManager.PathToEventReference("event:/Music/SombieStyle");
        if (fireCrackling.IsNull) fireCrackling = RuntimeManager.PathToEventReference("event:/SFX/Fire/FireCrackling");
        if (doorOpen.IsNull) doorOpen = RuntimeManager.PathToEventReference("event:/SFX/DoorOpen/DoorOpen");
        if (itemPickup.IsNull) itemPickup = RuntimeManager.PathToEventReference("event:/SFX/ItemPickup/ItemPickup");
        if (itemThrow.IsNull) itemThrow = RuntimeManager.PathToEventReference("event:/SFX/Throw/ItemThrow");
    }
}
