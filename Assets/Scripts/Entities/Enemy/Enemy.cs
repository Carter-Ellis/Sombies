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

    [Header("Wander Settings")]
    [SerializeField] protected float wanderRadius = 6f;
    [SerializeField] protected float wanderWaitTimeMin = 1f;
    [SerializeField] protected float wanderWaitTimeMax = 3f;
    [SerializeField] protected float wanderStuckTimeout = 4f;

    private bool _isWanderWaiting = false;
    private float _wanderWaitTimer = 0f;
    private float _wanderMoveTimer = 0f;
    private Vector3 _wanderTarget;
    private bool _wasChasingLastFrame = false;

    protected Transform playerTransform;
    protected NavMeshAgent agent;

    [Header("Currency Components")]
    public int hitPrice = 1;
    public int killPrice = 5;
    [SerializeField, Range(0f, 1f)] private float sharedKillCoinPercentage = 0.10f;

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
                _wasChasingLastFrame = true;
                if (agent.isOnNavMesh)
                {
                    agent.SetDestination(currentTarget.position);
                    rb.linearVelocity = agent.desiredVelocity;
                    agent.nextPosition = transform.position;

                    RotateTowards(currentTarget.position);
                }
            }
            else
            {
                UpdateWanderLogic();
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

    private void UpdateWanderLogic()
    {
        if (!agent.isOnNavMesh)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        if (_wasChasingLastFrame)
        {
            _wasChasingLastFrame = false;
            _isWanderWaiting = false;
            PickNewWanderTarget();
            return;
        }

        if (_isWanderWaiting)
        {
            rb.linearVelocity = Vector2.zero;
            agent.nextPosition = transform.position;
            _wanderWaitTimer -= Time.deltaTime;

            if (_wanderWaitTimer <= 0f)
            {
                _isWanderWaiting = false;
                PickNewWanderTarget();
            }
        }
        else
        {
            _wanderMoveTimer += Time.deltaTime;

            bool reachedDestination = !agent.hasPath || agent.remainingDistance <= stoppingDistance || agent.pathStatus == NavMeshPathStatus.PathInvalid;
            bool isStuck = _wanderMoveTimer >= wanderStuckTimeout;

            if (reachedDestination || isStuck)
            {
                _isWanderWaiting = true;
                _wanderWaitTimer = Random.Range(wanderWaitTimeMin, wanderWaitTimeMax);
                rb.linearVelocity = Vector2.zero;
                if (agent.hasPath)
                {
                    agent.ResetPath();
                }
            }
            else
            {
                agent.SetDestination(_wanderTarget);
                rb.linearVelocity = agent.desiredVelocity;
                agent.nextPosition = transform.position;

                if (rb.linearVelocity.sqrMagnitude > 0.01f)
                {
                    RotateTowards(transform.position + (Vector3)rb.linearVelocity);
                }
            }
        }
    }

    protected virtual void RotateTowards(Vector3 targetPos)
    {
        Vector2 dir = (targetPos - transform.position);
        if (dir.sqrMagnitude > 0.001f)
        {
            float targetAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

            if (rb != null)
            {
                rb.MoveRotation(targetAngle);
            }
            transform.rotation = Quaternion.Euler(0, 0, targetAngle);
        }
    }

    private void PickNewWanderTarget()
    {
        _wanderMoveTimer = 0f;
        Vector3 randomDirection = Random.insideUnitCircle * wanderRadius;
        Vector3 searchPos = transform.position + randomDirection;

        if (NavMesh.SamplePosition(searchPos, out NavMeshHit hit, wanderRadius, NavMesh.AllAreas))
        {
            _wanderTarget = hit.position;
        }
        else
        {
            _wanderTarget = transform.position;
        }

        if (agent.isOnNavMesh)
        {
            agent.SetDestination(_wanderTarget);
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

        if (FMODEvents.instance != null)
        {
            Audio.PlayNetworkedSFX(FMODEvents.instance.enemyHurt, transform.position);
        }
    }

    public void TakeDamage(int amount, PlayerStats playerStats)
    {
        Health -= amount;
        FlashRedRpc();

        if (FMODEvents.instance != null)
        {
            Audio.PlayNetworkedSFX(FMODEvents.instance.enemyHurt, transform.position);
        }

        if (Health <= 0)
        {
            if (playerStats != null)
            {
                playerStats.AddCoins(killPrice);
                playerStats.AddMana(manaReward);

                int sharedCoinAmount = Mathf.Max(1, Mathf.FloorToInt(killPrice * sharedKillCoinPercentage));
                if (NetworkManager.Singleton != null)
                {
                    foreach (var client in NetworkManager.Singleton.ConnectedClients.Values)
                    {
                        if (client.PlayerObject != null && client.PlayerObject.TryGetComponent<PlayerStats>(out var otherStats))
                        {
                            if (otherStats != playerStats)
                            {
                                otherStats.AddCoins(sharedCoinAmount);
                            }
                        }
                    }
                }
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