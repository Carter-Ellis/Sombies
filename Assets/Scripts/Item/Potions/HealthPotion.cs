using UnityEngine;

public class HealthPotion : Item
{
    [SerializeField] private int healAmount = 20;

    public override void Use(Entity entity)
    {
        entity.Heal(healAmount);
        IsUsed = true;
    }

}
