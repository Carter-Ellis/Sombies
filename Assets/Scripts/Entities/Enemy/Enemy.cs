using System.Collections;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.AI;

public abstract class Enemy : Entity
{
    private NetworkTransform _netTransform;
    private NetworkRigidbody2D _netRB;

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

    protected override void Awake()
    {
        base.Awake();
        _netTransform = GetComponent<NetworkTransform>();
        _netRB = GetComponent<NetworkRigidbody2D>();
    }

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
        PlayerStats[] allPlayers = Object.FindObjectsByType<PlayerStats>(FindObjectsInactive.Exclude);

        Transform bestTarget = null;
        float closestDistanceSqr = Mathf.Infinity;
        Vector3 currentPos = transform.position;

        foreach (PlayerStats playerStats in allPlayers)
        {
            if (playerStats == null || playerStats.isHidden.Value) continue;

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

        if (Time.time >= lastAttackTime + attackCooldown)
        {
            if (playerStats != null)
            {
                PlayerMovement playerMovement = playerStats.GetComponent<PlayerMovement>();

                Vector2 knockbackDirection = (playerStats.transform.position - transform.position).normalized;
                Vector2 force = knockbackDirection * KnockbackForce;

                playerMovement.ApplyKnockbackClientRpc(force, KnockbackDuration);

                playerStats.TakeDamage(DamageAmount);

                lastAttackTime = Time.time;
            }
        }
    }

    protected void TryDropItem()
    {
        if (!IsServer) return;

        if (Random.value <= _dropChance)
        {
            if (_possibleDrops != null && _possibleDrops.Length > 0)
            {
                int randomIndex = Random.Range(0, _possibleDrops.Length);
                Item itemPrefab = _possibleDrops[randomIndex];

                if (itemPrefab != null)
                {
                    Item spawnedItem = Instantiate(itemPrefab, transform.position, Quaternion.identity);

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

        rb.linearVelocity = force;

        yield return new WaitForSeconds(duration);

        rb.linearVelocity = Vector2.zero;
        isKnockedBack = false;
    }
}