using System.Collections;
using UnityEngine;

public class SombiePotion : Item
{
    public override void Use(Player player)
    {
        Debug.Log("Player used Sombie Potion.");
        if (player.Buffs != null)
        {
            StealthBuff hideBuff = new StealthBuff(player);
            Debug.Log("Applying Sombie Potion buff to player for 10 seconds.");
            player.Buffs.AddTemporaryBuff(hideBuff, duration);

            IsUsed = true;
        }
    }
}
