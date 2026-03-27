using UnityEngine;

public abstract class Projectile : MonoBehaviour
{
    
    [SerializeField] protected float lifetime = 3f;
    protected int damage;
    protected Player owner;

    public void Initialize(Player player, int damage)
    {
        owner = player;
        this.damage = damage;
    }

    protected virtual void Start()
    {
        Destroy(gameObject, lifetime);
    }

    protected virtual void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.TryGetComponent(out Enemy enemy))
        {
            OnHitEnemy(enemy);
            Destroy(gameObject);
            return;
        }
        
    }

    protected virtual void OnHitEnemy(Enemy enemy)
    {
        owner.AddCoins(enemy.hitPrice);
        enemy.TakeDamage(damage, owner);
    }
}
