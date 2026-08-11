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

    [Header("Health Scaling")]
    [SerializeField] protected int healthIncreasePerRound = 10;
    [SerializeField] protected int maxScaledHealth = 200;

    [Header("Attack")]
    [SerializeField] private int _damageAmount = 10;

    [SerializeField] protected int damageIncreasePerRound = 2;
    [SerializeField] protected int maxDamage = 40;

    protected int currentDamage;

    [SerializeField] private int _knockbackForce = 5;
    [SerializeField] private float _knockbackDuration = .2f;
    [SerializeField] private int manaReward = 20;
    [SerializeField] protected float attackCooldown = 1f;
    protected float lastAttackTime;
    public int DamageAmount => currentDamage;
    public int KnockbackForce => _knockbackForce;
    public float KnockbackDuration => _knockbackDuration;

    [Header("Movement")]
    [SerializeField] protected float speed = 2f;

    [SerializeField] protected float speedIncreasePerRound = 0.25f;
    [SerializeField] protected float maxSpeed = 7f;

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

    [Header("Visual Feedback")]
    protected SpriteRenderer spriteRenderer;
    protected Color originalColor = Color.white;
    private Coroutine flashCoroutine;

    protected override void Awake()
    {
        base.Awake();
        _netTransform = GetComponent<NetworkTransform>();
        _netRB = GetComponent<NetworkRigidbody2D>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            originalColor = spriteRenderer.color;
        }
    }

    protected virtual void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        rb = GetComponent<Rigidbody2D>();

        agent.updateRotation = false;
        agent.updateUpAxis = false;
        agent.updatePosition = false;

        int currentRound = roundManager != null ? roundManager._netRound.Value : 1;

        int scaledHealth = MaxHealth + (healthIncreasePerRound * (currentRound - 1));
        MaxHealth = Mathf.Min(scaledHealth, maxScaledHealth);

        float scaledSpeed = speed + (speedIncreasePerRound * (currentRound - 1));
        currentSpeed = Mathf.Min(scaledSpeed, maxSpeed);

        agent.speed = currentSpeed;
        agent.stoppingDistance = stoppingDistance;

        int scaledDamage = _damageAmount + (damageIncreasePerRound * (currentRound - 1));
        currentDamage = Mathf.Min(scaledDamage, maxDamage);

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

    public override void TakeDamage(int amount)
    {
        Health -= amount;
        FlashRedRpc();
    }

    public void TakeDamage(int amount, PlayerStats playerStats)
    {
        Health -= amount;
        FlashRedRpc();

        if (Health <= 0)
        {
            if (playerStats != null)
            {
                playerStats.AddCoins(killPrice);
                playerStats.AddMana(manaReward);
            }
        }
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void FlashRedRpc()
    {
        if (spriteRenderer != null)
        {
            if (flashCoroutine != null) StopCoroutine(flashCoroutine);
            flashCoroutine = StartCoroutine(FlashRoutine());
        }
    }

    private IEnumerator FlashRoutine()
    {
        spriteRenderer.color = Color.red;
        yield return new WaitForSeconds(0.1f);
        if (spriteRenderer != null)
        {
            spriteRenderer.color = originalColor;
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
                float totalWeight = 0f;
                foreach (Item itemPrefab in _possibleDrops)
                {
                    if (itemPrefab != null)
                    {
                        totalWeight += itemPrefab.DropWeight;
                    }
                }

                float randomValue = Random.Range(0f, totalWeight);
                float cumulativeWeight = 0f;
                Item selectedItemPrefab = null;

                foreach (Item itemPrefab in _possibleDrops)
                {
                    if (itemPrefab == null) continue;

                    cumulativeWeight += itemPrefab.DropWeight;
                    if (randomValue <= cumulativeWeight)
                    {
                        selectedItemPrefab = itemPrefab;
                        break;
                    }
                }

                if (selectedItemPrefab != null)
                {
                    Item spawnedItem = Instantiate(selectedItemPrefab, transform.position, Quaternion.identity);

                    NetworkObject netObj = spawnedItem.GetComponent<NetworkObject>();
                    if (netObj != null)
                    {
                        netObj.Spawn();
                    }
                    else
                    {
                        Debug.LogError($"Item {selectedItemPrefab.name} is missing a NetworkObject component!");
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