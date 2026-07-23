using UnityEngine;

public class HealthPotion : Item
{
    [SerializeField] private int healAmount = 20;

    protected override void OnUse(Entity entity)
    {
        entity.Heal(healAmount);
        IsUsed = true;
    }

}
