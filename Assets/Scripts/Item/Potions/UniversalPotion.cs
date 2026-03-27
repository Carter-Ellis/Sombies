using UnityEngine;

public class UniversalPotion : Item
{
    [Header("Potion Settings")]
    [SerializeField] private BUFFTYPE buffToApply;
    [SerializeField] private float buffAmount = 5f;

    public override void Use(Player player)
    {
        BuffManager buffs = player.GetComponent<BuffManager>();

        if (buffs != null)
        {
            StatBuff newBuff = new StatBuff(player, buffToApply, buffAmount);

            ApplyTimeEffect(player,
                () => buffs.AddBuff(newBuff),
                () => buffs.RemoveBuff(newBuff));
        }
    }
}
