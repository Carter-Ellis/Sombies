using System.Collections;
using UnityEngine;

public class SombiePotion : Item
{
    public override void Use(Player player)
    {
        ApplyTimeEffect(player, () => player.isHidden = true, () => player.isHidden = false);
    }
}
