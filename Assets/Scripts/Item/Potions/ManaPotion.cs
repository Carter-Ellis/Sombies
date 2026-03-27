using UnityEngine;

public class ManaPotion : Item
{
    [SerializeField] private int manaRestoreAmount = 25;

    public override void Use(Player player)
    {
        player.Mana += manaRestoreAmount;
    }
}
