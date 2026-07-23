using UnityEngine;

public class ManaPotion : Item
{
    [SerializeField] private int manaRestoreAmount = 25;

    protected override void OnUse(Entity entity)
    {
        if (entity is PlayerStats stats)
        {
            stats.Mana += manaRestoreAmount;
            IsUsed = true;
        }
    }
}
