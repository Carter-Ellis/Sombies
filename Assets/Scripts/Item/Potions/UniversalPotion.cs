using UnityEngine;

public class UniversalPotion : Item
{
    [Header("Potion Settings")]
    [SerializeField] private BUFFTYPE buffToApply;
    [SerializeField] private float buffAmount = 5f;

    public override void Use(Entity entity)
    {
        BuffManager buffs = entity.GetComponent<BuffManager>();

        if (buffs != null)
        {
            StatBuff newBuff = new StatBuff(entity, buffToApply, buffAmount);

            buffs.AddTemporaryBuff(newBuff, duration);
            IsUsed = true;
        }
    }
}
