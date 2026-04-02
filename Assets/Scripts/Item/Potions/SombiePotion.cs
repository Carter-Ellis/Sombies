using System.Collections;
using UnityEngine;

public class SombiePotion : Item
{
    public override void Use(Entity entity)
    {
        Debug.Log("Player used Sombie Potion.");
        if (entity.Buffs != null)
        {
            StealthBuff hideBuff = new StealthBuff(entity);
            entity.Buffs.AddTemporaryBuff(hideBuff, duration);

            IsUsed = true;
        }
    }
}
