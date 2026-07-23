using UnityEngine;

public class UniversalPotion : Item
{
    [Header("Potion Settings")]
    [SerializeField] private BUFFTYPE buffToApply;
    [SerializeField] private float buffAmount = 5f;

    protected override void OnUse(Entity entity)
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
