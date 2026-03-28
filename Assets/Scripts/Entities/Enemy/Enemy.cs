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

    protected virtual void Start()
    {
        agent = GetComponent<NavMeshAgent>();

        agent.updateRotation = false;
        agent.updateUpAxis = false;

        agent.speed = speed;
        agent.stoppingDistance = stoppingDistance;

        currentSpeed = speed;

        NavMeshHit hit;
        if (NavMesh.SamplePosition(transform.position, out hit, 2f, NavMesh.AllAreas))
        {
            agent.Warp(hit.position);
        }
        Debug.Log(agent.isOnNavMesh);
    }

    protected virtual void Update()
    {
        targetUpdateTimer -= Time.deltaTime;
        if (targetUpdateTimer <= 0f)
        {
            currentTarget = GetClosestPlayer();
            targetUpdateTimer = targetUpdateInterval;
        }

        if (currentTarget != null)
        {
            if (agent.isOnNavMesh)
            {
                agent.SetDestination(currentTarget.position);
            }
        }
        else if (agent.hasPath)
        {
            if (agent.isOnNavMesh)
            {
                agent.ResetPath();
            }
        }
    }

    public void SetManager(RoundManager manager)
    {
        roundManager = manager;
    }

    private Transform GetClosestPlayer()
    {
        Player[] allPlayers = Object.FindObjectsByType<Player>(FindObjectsInactive.Exclude);

        Transform bestTarget = null;
        float closestDistanceSqr = Mathf.Infinity;
        Vector3 currentPos = transform.position;

        foreach (Player player in allPlayers)
        {
            if (player.isHidden) continue;

            Vector3 directionToPlayer = player.transform.position - currentPos;
            float dSqrToPlayer = directionToPlayer.sqrMagnitude;

            if (dSqrToPlayer < closestDistanceSqr)
            {
                closestDistanceSqr = dSqrToPlayer;
                bestTarget = player.transform;
            }
        }

        return bestTarget;
    }

    public void TakeDamage(int amount, Player player)
    {
        Health -= amount;

        if (Health <= 0)
        {
            player.AddCoins(killPrice);
            player.AddMana(manaReward);
        }
    }

    public override void Die()
    {
        if (roundManager != null)
        {
            roundManager.RemoveEnemy(this);
        }
        Destroy(gameObject);
    }

    protected void TryDamagePlayer(Collider2D collider)
    {
        // Check if enough time has passed since the last attack
        if (Time.time >= lastAttackTime + attackCooldown)
        {
            Player player = collider.GetComponent<Player>();

            if (player != null)
            {
                PlayerMovement playerMovement = player.GetComponent<PlayerMovement>();

                // Calculate direction and apply knockback
                Vector2 knockbackDirection = (player.transform.position - transform.position).normalized;
                playerMovement.ApplyKnockback(knockbackDirection * KnockbackForce, KnockbackDuration);

                // Deal damage
                player.TakeDamage(DamageAmount);

                // Record the time of this attack so the cooldown starts
                lastAttackTime = Time.time;
            }
        }
    }

}