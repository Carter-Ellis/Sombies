using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class ThornProj : Projectile
{
    [Header("Thorn Burst Settings")]
    [SerializeField] private int projectileCount = 12; // Number of thorns in 360 degree burst
    [SerializeField] private bool isBurstMaster = true; // True for initial object spawned by spell cast

    private HashSet<Enemy> hitEnemies = new HashSet<Enemy>();

    protected override void Start()
    {
        base.Start();

        if (IsServer && isBurstMaster)
        {
            SpawnRadialBurst();
            Destroy(gameObject);
        }
    }

    private void SpawnRadialBurst()
    {
        if (projectileCount <= 0) return;

        float angleStep = 360f / projectileCount;

        for (int i = 0; i < projectileCount; i++)
        {
            float angle = i * angleStep;
            Quaternion rotation = Quaternion.Euler(0f, 0f, angle);

            GameObject thornObj = Instantiate(gameObject, transform.position, rotation);

            if (thornObj.TryGetComponent(out ThornProj thornProj))
            {
                thornProj.isBurstMaster = false;
                thornProj.Initialize(ownerStats, damage, _launchSpeed.Value);
            }

            if (thornObj.TryGetComponent(out NetworkObject netObj))
            {
                netObj.Spawn();
            }
        }
    }

    protected override void OnTriggerEnter2D(Collider2D collision)
    {
        if (!IsServer)
        {
            if (collision.CompareTag("Wall"))
            {
                SpriteRenderer sr = GetComponent<SpriteRenderer>();
                if (sr != null) sr.enabled = false;

                Collider2D col = GetComponent<Collider2D>();
                if (col != null) col.enabled = false;
            }
            return;
        }

        Enemy enemy = collision.GetComponentInParent<Enemy>();
        if (enemy != null)
        {
            // Piercing: Damage each enemy ONCE and continue flying through!
            if (hitEnemies.Add(enemy))
            {
                OnHitEnemy(enemy);
            }
        }
        else if (collision.CompareTag("Wall"))
        {
            // Stop and despawn on wall impact
            Destroy(gameObject);
        }
    }
}
