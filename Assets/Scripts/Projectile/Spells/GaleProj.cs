using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class GaleProj : Projectile
{
    [Header("Boomerang Settings")]
    [SerializeField] private float outwardDuration = 0.5f; // Duration before turning back
    [SerializeField] private float catchRadius = 0.8f;      // Distance to player to catch/despawn

    private bool isReturning = false;
    private float returnTimer = 0f;
    private HashSet<Enemy> hitEnemies = new HashSet<Enemy>();
    private Rigidbody2D rb;

    protected override void Start()
    {
        base.Start();
        rb = GetComponent<Rigidbody2D>();
    }

    private void FixedUpdate()
    {
        returnTimer += Time.fixedDeltaTime;

        if (!isReturning && returnTimer >= outwardDuration)
        {
            SetReturning();
        }

        if (isReturning)
        {
            PlayerStats targetStats = GetOwnerStats();

            if (targetStats != null && targetStats.transform != null)
            {
                Vector2 targetPos = targetStats.transform.position;
                Vector2 currentPos = rb != null ? rb.position : (Vector2)transform.position;
                Vector2 dirToPlayer = (targetPos - currentPos).normalized;

                if (rb != null)
                {
                    rb.linearVelocity = dirToPlayer * _launchSpeed.Value;
                }
                else
                {
                    transform.position = Vector2.MoveTowards(transform.position, targetPos, _launchSpeed.Value * Time.fixedDeltaTime);
                }

                float angle = Mathf.Atan2(dirToPlayer.y, dirToPlayer.x) * Mathf.Rad2Deg;
                transform.rotation = Quaternion.Euler(0f, 0f, angle);

                if (IsServer && Vector2.Distance(currentPos, targetPos) <= catchRadius)
                {
                    Destroy(gameObject);
                }
            }
            else if (IsServer)
            {
                // Despawn if owner player no longer exists
                Destroy(gameObject);
            }
        }
    }

    protected override void OnTriggerEnter2D(Collider2D collision)
    {
        if (!IsServer)
        {
            if (collision.CompareTag("Wall"))
            {
                SetReturning();
            }
            return;
        }

        Enemy enemy = collision.GetComponentInParent<Enemy>();
        if (enemy != null)
        {
            // Piercing: Damage each enemy ONCE per leg (outward / returning)
            if (hitEnemies.Add(enemy))
            {
                OnHitEnemy(enemy);
            }
        }
        else if (collision.CompareTag("Wall"))
        {
            if (!isReturning)
            {
                // Immediately switch to return mode upon hitting a wall
                SetReturning();
            }
            else
            {
                // Despawn if hitting a wall while already returning
                Destroy(gameObject);
            }
        }
    }

    private void OnTriggerStay2D(Collider2D collision)
    {
        if (!IsServer) return;

        Enemy enemy = collision.GetComponentInParent<Enemy>();
        if (enemy != null && hitEnemies.Add(enemy))
        {
            OnHitEnemy(enemy);
        }
    }

    private void SetReturning()
    {
        if (!isReturning)
        {
            isReturning = true;
            hitEnemies.Clear();
        }
    }
}
