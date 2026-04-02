using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

public abstract class Enemy : Entity
{
    private RoundManager roundManager;

    [Header("Attack")]
    [SerializeField] private int _damageAmount = 10;
    [SerializeField] private int _knockbackForce = 5;
    [SerializeField] private float _knockbackDuration = .2f;
    [SerializeField] private int manaReward = 20;
    [SerializeField] protected float attackCooldown = 1f;
    protected float lastAttackTime;
    public int DamageAmount => _damageAmount;
    public int KnockbackForce => _knockbackForce;
    public float KnockbackDuration => _knockbackDuration;

    [Header("Movement")]
    [SerializeField] protected float speed = 2f;
    protected float currentSpeed = 2f;

    public override float BaseWalkSpeed => speed;
    public override float WalkSpeed
    {
        get => currentSpeed;
        set
        {
            currentSpeed = value;
            if (agent != null) agent.speed = currentSpeed;
        }
    }

    [SerializeField] protected float stoppingDistance = 0.5f;

    [Header("Targeting")]
    [SerializeField] private float targetUpdateInterval = 0.2f;
    private float targetUpdateTimer;
    private Transform currentTarget;

    protected Transform playerTransform;
    protected NavMeshAgent agent;

    [Header("Currency Components")]
    public int hitPrice = 1;
    public int killPrice = 5;

    [Header("Components")]
    protected Rigidbody2D rb;

    [Header("Loot Drops")]
    [SerializeField, Range(0f, 1f)] private float _dropChance = 0.25f;
    [SerializeField] private Item[] _possibleDrops;

    [Header("States")]
    protected bool isKnockedBack = false;

    protected virtual void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        rb = GetComponent<Rigidbody2D>();


        agent.updateRotation = false;
        agent.updateUpAxis = false;

        agent.updatePosition = false;

        agent.speed = speed;
        agent.stoppingDistance = stoppingDistance;

        currentSpeed = speed;

        NavMeshHit hit;
        if (NavMesh.SamplePosition(transform.position, out hit, 2f, NavMesh.AllAreas))
        {
            agent.Warp(hit.position);
        }
    }

    protected virtual void Update()
    {
        if (!IsServer) return;

        targetUpdateTimer -= Time.deltaTime;
        if (targetUpdateTimer <= 0f)
        {
            currentTarget = GetClosestPlayer();
            targetUpdateTimer = targetUpdateInterval;
        }

        if (!isKnockedBack)
        {
            if (currentTarget != null)
            {
                if (agent.isOnNavMesh)
                {
                    agent.SetDestination(currentTarget.position);
                    rb.linearVelocity = agent.desiredVelocity;
                    agent.nextPosition = transform.position;
                }
            }
            else
            {
                // If we have no target, we MUST clear the velocity and the agent path
                rb.linearVelocity = Vector2.zero;

                if (agent.isOnNavMesh && agent.hasPath)
                {
                    agent.ResetPath();
                }
            }
        }
        else
        {
            if (agent.isOnNavMesh)
            {
                agent.nextPosition = transform.position;
            }
        }
    }

    public void SetManager(RoundManager manager)
    {
        roundManager = manager;
    }

    private Transform GetClosestPlayer()
    {
        // Ensure we are finding all active player scripts in the network
        PlayerStats[] allPlayers = Object.FindObjectsByType<PlayerStats>(FindObjectsInactive.Exclude);

        Transform bestTarget = null;
        float closestDistanceSqr = Mathf.Infinity;
        Vector3 currentPos = transform.position;

        foreach (PlayerStats playerStats in allPlayers)
        {
            // 1. Skip if player is hidden/dead
            if (playerStats == null || playerStats.isHidden.Value) continue;

            // 2. Extra safety: Check if the player actually has a NetworkObject and is spawned
            var netObj = playerStats.GetComponent<NetworkObject>();
            if (netObj == null || !netObj.IsSpawned) continue;

            Vector3 directionToPlayer = playerStats.transform.position - currentPos;
            float dSqrToPlayer = directionToPlayer.sqrMagnitude;

            if (dSqrToPlayer < closestDistanceSqr)
            {
                closestDistanceSqr = dSqrToPlayer;
                bestTarget = playerStats.transform;
            }
        }

        return bestTarget;
    }

    public void TakeDamage(int amount, PlayerStats playerStats)
    {
        Health -= amount;

        if (Health <= 0)
        {
            playerStats.AddCoins(killPrice);
            playerStats.AddMana(manaReward);
        }
    }

    public override void Die()
    {
        if (!IsServer) return;

        TryDropItem();

        if (roundManager != null)
        {
            roundManager.RemoveEnemy(this);
        }
        Destroy(gameObject);
    }

    protected void TryDamagePlayer(Collider2D collider)
    {
        PlayerStats playerStats = collider.GetComponent<PlayerStats>();
        if (playerStats != null && playerStats.isHidden.Value) return;

        // Check if enough time has passed since the last attack
        if (Time.time >= lastAttackTime + attackCooldown)
        {
            

            if (playerStats != null)
            {
                PlayerMovement playerMovement = playerStats.GetComponent<PlayerMovement>();

                // Calculate direction and apply knockback
                Vector2 knockbackDirection = (playerStats.transform.position - transform.position).normalized;
                Vector2 force = knockbackDirection * KnockbackForce;

                playerMovement.ApplyKnockbackClientRpc(force, KnockbackDuration);

                // Deal damage
                playerStats.TakeDamage(DamageAmount);

                // Record the time of this attack so the cooldown starts
                lastAttackTime = Time.time;
            }
        }
    }

    protected void TryDropItem()
    {
        // 1. Only the Server handles spawning
        if (!IsServer) return;

        if (Random.value <= _dropChance)
        {
            if (_possibleDrops != null && _possibleDrops.Length > 0)
            {
                int randomIndex = Random.Range(0, _possibleDrops.Length);
                Item itemPrefab = _possibleDrops[randomIndex];

                if (itemPrefab != null)
                {
                    // 2. Standard Instantiate first
                    Item spawnedItem = Instantiate(itemPrefab, transform.position, Quaternion.identity);

                    // 3. Get the NetworkObject and Spawn it across the network
                    NetworkObject netObj = spawnedItem.GetComponent<NetworkObject>();
                    if (netObj != null)
                    {
                        netObj.Spawn();
                    }
                    else
                    {
                        Debug.LogError($"Item {itemPrefab.name} is missing a NetworkObject component!");
                    }
                }
            }
        }
    }

    public void ApplyKnockback(Vector2 force, float duration)
    {
        StartCoroutine(KnockbackRoutine(force, duration));
    }

    private IEnumerator KnockbackRoutine(Vector2 force, float duration)
    {
        isKnockedBack = true;

        // Apply the sudden force
        rb.linearVelocity = force;

        yield return new WaitForSeconds(duration);

        // End knockback, stop sliding
        rb.linearVelocity = Vector2.zero;
        isKnockedBack = false;
    }

}