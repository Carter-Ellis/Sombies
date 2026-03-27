using UnityEngine;

public class HealthPotion : Item
{
    [SerializeField] private int healAmount = 20;

    public override void Use(Player player)
    {
        player.Health += healAmount;
    }

}
