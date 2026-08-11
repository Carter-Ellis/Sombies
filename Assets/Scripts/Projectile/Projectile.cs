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

    protected NetworkVariable<float> _launchSpeed = new NetworkVariable<float>(
        15f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    protected int damage;
    protected PlayerStats ownerStats;

    public void Initialize(PlayerStats playerStats, int damage, float speed = 15f)
    {
        ownerStats = playerStats;
        this.damage = damage;
        if (IsServer)
        {
            _launchSpeed.Value = speed;
        }
        ApplyVelocity();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        _launchSpeed.OnValueChanged += OnLaunchSpeedChanged;

        if (!IsServer)
        {
            // Disable NetworkTransform on client so client physics moves projectile locally without network interpolation lag
            if (TryGetComponent<Unity.Netcode.Components.NetworkTransform>(out var netTransform))
            {
                netTransform.enabled = false;
            }
        }

        ApplyVelocity();
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        _launchSpeed.OnValueChanged -= OnLaunchSpeedChanged;
    }

    private void OnLaunchSpeedChanged(float oldVal, float newVal)
    {
        ApplyVelocity();
    }

    protected void ApplyVelocity()
    {
        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = transform.right * _launchSpeed.Value;
        }
    }

    protected virtual void Start()
    {
        if (IsServer)
        {
            Destroy(gameObject, lifetime);
        }
        ApplyVelocity();
    }

    protected virtual void OnTriggerEnter2D(Collider2D collision)
    {
        if (!IsServer)
        {
            if (collision.TryGetComponent(out Enemy _) || collision.CompareTag("Wall"))
            {
                SpriteRenderer sr = GetComponent<SpriteRenderer>();
                if (sr != null) sr.enabled = false;

                Collider2D col = GetComponent<Collider2D>();
                if (col != null) col.enabled = false;
            }
            return;
        }

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

        Vector2 knockbackDir = (enemy.transform.position - transform.position).normalized;
        enemy.ApplyKnockback(knockbackDir * 5f, 0.15f);

        if (appliesBuff && enemy.TryGetComponent(out BuffManager bm))
        {
            StatBuff debuff = new StatBuff(enemy, buffType, buffAmount);
            bm.AddTemporaryBuff(debuff, buffDuration);
        }
    }
}
