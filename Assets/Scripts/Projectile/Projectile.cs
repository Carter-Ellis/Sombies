using Unity.Netcode;
using UnityEngine;

public abstract class Projectile : NetworkBehaviour
{
    [SerializeField] protected float lifetime = 3f;

    [Header("Optional Debuff")]
    [SerializeField] protected bool appliesBuff = false;
    [SerializeField] protected BUFFTYPE buffType;
    [SerializeField] protected float buffAmount;
    [SerializeField] protected float buffDuration;

    protected int damage;
    protected PlayerStats ownerStats;

    public void Initialize(PlayerStats playerStats, int damage)
    {
        ownerStats = playerStats;
        this.damage = damage;
    }

    protected virtual void Start()
    {
        if (IsServer)
        {
            Destroy(gameObject, lifetime);
        }
        
    }

    protected virtual void OnTriggerEnter2D(Collider2D collision)
    {
        if (!IsServer) return;

        if (collision.TryGetComponent(out Enemy enemy))
        {
            OnHitEnemy(enemy);
            Destroy(gameObject);
        }
        else if (collision.CompareTag("Wall"))
        {
            Destroy(gameObject);
        }


    }

    protected virtual void OnHitEnemy(Enemy enemy)
    {
        ownerStats.AddCoins(enemy.hitPrice);
        enemy.TakeDamage(damage, ownerStats);

        if (appliesBuff && enemy.TryGetComponent(out BuffManager bm))
        {
            StatBuff debuff = new StatBuff(enemy, buffType, buffAmount);
            bm.AddTemporaryBuff(debuff, buffDuration);
        }
    }
}
