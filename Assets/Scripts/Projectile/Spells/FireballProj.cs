using System.Collections.Generic;
using UnityEngine;

public class FireballProj : Projectile
{
    [Header("Splash Damage Settings")]
    [SerializeField] private float splashRadius = 3.5f;
    [SerializeField] private int splashDamage = 30;

    protected override void OnTriggerEnter2D(Collider2D collision)
    {
        if (!IsServer) return;

        Enemy hitEnemy = collision.GetComponentInParent<Enemy>();

        if (hitEnemy != null)
        {
            OnHitEnemy(hitEnemy);
            ExplodeSplashDamage(hitEnemy);
            Destroy(gameObject);
        }
        else if (collision.CompareTag("Wall"))
        {
            ExplodeSplashDamage(null);
            Destroy(gameObject);
        }
    }

    private void ExplodeSplashDamage(Enemy directHitEnemy)
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, splashRadius);
        HashSet<Enemy> damagedEnemies = new HashSet<Enemy>();

        if (directHitEnemy != null)
        {
            damagedEnemies.Add(directHitEnemy);
        }

        foreach (Collider2D hit in hits)
        {
            Enemy nearbyEnemy = hit.GetComponentInParent<Enemy>();
            if (nearbyEnemy != null && damagedEnemies.Add(nearbyEnemy))
            {
                if (ownerStats != null)
                {
                    ownerStats.AddCoins(nearbyEnemy.hitPrice);
                }

                nearbyEnemy.TakeDamage(splashDamage, ownerStats);

                if (appliesBuff && nearbyEnemy.TryGetComponent(out BuffManager bm))
                {
                    StatBuff debuff = new StatBuff(nearbyEnemy, buffType, buffAmount);
                    bm.AddTemporaryBuff(debuff, buffDuration);
                }
            }
        }

        int splashCount = damagedEnemies.Count - (directHitEnemy != null ? 1 : 0);
        Debug.Log($"[Fireball Splash] Exploded at {transform.position} with radius {splashRadius}. Hit {splashCount} nearby splash targets.");
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, splashRadius);
    }
}
