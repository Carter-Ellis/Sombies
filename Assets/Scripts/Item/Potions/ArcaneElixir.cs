using UnityEngine;

public class ArcaneElixir : Item
{
    [SerializeField] private int manaRestoreAmount = 25;
    [SerializeField] private int healAmount = 20;

    protected override void OnUse(Entity entity)
    {
        entity.Heal(healAmount);

        if (entity is PlayerStats stats)
        {
            stats.Mana += manaRestoreAmount;
        }

        IsUsed = true;

    }
}
