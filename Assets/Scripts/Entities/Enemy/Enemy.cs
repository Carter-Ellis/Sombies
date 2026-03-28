using UnityEngine;

public abstract class Enemy : Entity
{

    [Header("Attack")]
    [SerializeField] private int _damageAmount = 10;
    [SerializeField] private int _knockbackForce = 5;
    [SerializeField] private float _knockbackDuration = .2f;
    [SerializeField] private int manaReward = 20;

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
        set => currentSpeed = value;
    }
    
    [SerializeField] protected float stoppingDistance = 0.5f;


    [Header("Targeting")]
    [SerializeField] private float targetUpdateInterval = 0.2f;
    private float targetUpdateTimer;
    private Transform currentTarget;

    protected Transform playerTransform;
    protected Rigidbody2D rb;

    [Header("Currency Components")]
    public int hitPrice = 1;
    public int killPrice = 5;



    protected virtual void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        currentSpeed = speed;
    }

    protected virtual void FixedUpdate()
    {
        // 1. Only look for the closest player every X seconds
        targetUpdateTimer -= Time.fixedDeltaTime;
        if (targetUpdateTimer <= 0f)
        {
            currentTarget = GetClosestPlayer();
            targetUpdateTimer = targetUpdateInterval; // Reset timer
        }

        // 2. If we have a target, move toward it
        if (currentTarget != null)
        {
            MoveTowardTarget(currentTarget);
        }
        else
        {
            rb.linearVelocity = Vector2.zero;
        }
    }

    private void MoveTowardTarget(Transform target)
    {
        float distance = Vector2.Distance(transform.position, target.position);

        if (distance > stoppingDistance)
        {
            Vector2 direction = (target.position - transform.position).normalized;
            rb.linearVelocity = direction * currentSpeed;
        }
        else
        {
            rb.linearVelocity = Vector2.zero;
        }
    }

    private Transform GetClosestPlayer()
    {
        // 1. Find all objects in the scene with the Player component
        Player[] allPlayers = Object.FindObjectsByType<Player>(FindObjectsInactive.Exclude);

        // 2. Setup variables to track the "Winner"
        Transform bestTarget = null;
        float closestDistanceSqr = Mathf.Infinity; // Use infinity so the first check always wins
        Vector3 currentPos = transform.position;

        // 3. Loop through every player found
        foreach (Player player in allPlayers)
        {
            if (player.isHidden) return null;

            // Calculate the vector between Enemy and this Player
            Vector3 directionToPlayer = player.transform.position - currentPos;

            // Use sqrMagnitude instead of Vector2.Distance for better performance
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
        Destroy(gameObject);
    }

}
