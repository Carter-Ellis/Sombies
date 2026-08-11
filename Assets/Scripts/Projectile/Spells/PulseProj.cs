using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class PulseProj : Projectile
{
    public static PulseProj LocalChargingPulse { get; private set; }

    [Header("Charge Shot Settings")]
    [SerializeField] private float maxChargeTime = 1.5f;       // Seconds to reach 100% full charge
    [SerializeField] private float minSpeedMultiplier = 0.4f;  // Launch speed multiplier at 0% charge
    [SerializeField] private float maxSpeedMultiplier = 2.2f;  // Launch speed multiplier at 100% charge
    [SerializeField] private float minDamageMultiplier = 0.2f; // Damage multiplier at 0% charge
    [SerializeField] private float maxDamageMultiplier = 3.5f; // Damage multiplier at 100% charge
    [SerializeField] private float minAoERadius = 1.5f;        // Explosion radius at 0% charge
    [SerializeField] private float maxAoERadius = 5.0f;        // Explosion radius at 100% charge
    [SerializeField] private float minScale = 0.5f;            // Visual scale at 0% charge
    [SerializeField] private float maxScale = 2.2f;            // Visual scale at 100% charge

    private NetworkVariable<float> _chargeRatio = new NetworkVariable<float>(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private NetworkVariable<bool> _isLaunched = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private float chargeTimer = 0f;
    private bool isLocallyLaunched = false;
    private Rigidbody2D rb;

    protected override void Start()
    {
        // Do not call base.Start() so lifetime timer does NOT run while holding/charging!
        rb = GetComponent<Rigidbody2D>();

        // Ensure zero velocity while charging
        if (rb != null && !_isLaunched.Value && !isLocallyLaunched)
        {
            rb.linearVelocity = Vector2.zero;
        }
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        _chargeRatio.OnValueChanged += OnChargeRatioChanged;
        _isLaunched.OnValueChanged += OnLaunchStateChanged;

        UpdateVisualScale(_chargeRatio.Value);

        PlayerStats owner = GetOwnerStats();
        if (owner != null && owner.IsOwner)
        {
            LocalChargingPulse = this;
        }
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        _chargeRatio.OnValueChanged -= OnChargeRatioChanged;
        _isLaunched.OnValueChanged -= OnLaunchStateChanged;

        if (LocalChargingPulse == this)
        {
            LocalChargingPulse = null;
        }
    }

    private void OnChargeRatioChanged(float oldVal, float newVal)
    {
        UpdateVisualScale(newVal);
    }

    private void OnLaunchStateChanged(bool oldVal, bool newVal)
    {
        if (newVal)
        {
            isLocallyLaunched = true;
            ApplyLaunchVelocity();
        }
    }

    private void UpdateVisualScale(float ratio)
    {
        float currentScale = Mathf.Lerp(minScale, maxScale, ratio);
        transform.localScale = new Vector3(currentScale, currentScale, 1f);
    }

    private void Update()
    {
        // Anchor to player's firepoint while charging
        if (!_isLaunched.Value && !isLocallyLaunched)
        {
            PlayerStats owner = GetOwnerStats();
            if (owner != null && owner.TryGetComponent<Player>(out var player) && player.firepoint != null)
            {
                transform.position = player.firepoint.position;
                transform.rotation = player.firepoint.rotation;
            }

            if (IsServer && chargeTimer < maxChargeTime)
            {
                chargeTimer += Time.deltaTime;
                float ratio = Mathf.Clamp01(chargeTimer / maxChargeTime);
                _chargeRatio.Value = ratio;
            }
        }
    }

    public void LaunchFromClient()
    {
        if (isLocallyLaunched || _isLaunched.Value) return;

        isLocallyLaunched = true;
        ApplyLaunchVelocity();
        LaunchServerRpc();

        if (LocalChargingPulse == this)
        {
            LocalChargingPulse = null;
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void LaunchServerRpc()
    {
        if (_isLaunched.Value) return;

        _isLaunched.Value = true;
        isLocallyLaunched = true;

        ApplyLaunchVelocity();

        // Start flight lifetime countdown after projectile is launched
        Destroy(gameObject, lifetime);
    }

    public void ApplyLaunchVelocity()
    {
        float ratio = _chargeRatio.Value;
        float speedMult = Mathf.Lerp(minSpeedMultiplier, maxSpeedMultiplier, ratio);

        if (rb == null) rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = transform.right * (_launchSpeed.Value * speedMult);
        }
    }

    protected override void OnTriggerEnter2D(Collider2D collision)
    {
        // Ignore collisions while held/charging in front of the player
        if (!_isLaunched.Value && !isLocallyLaunched) return;

        if (!IsServer)
        {
            if (collision.CompareTag("Wall") || collision.GetComponentInParent<Enemy>() != null)
            {
                SpriteRenderer sr = GetComponent<SpriteRenderer>();
                if (sr != null) sr.enabled = false;

                Collider2D col = GetComponent<Collider2D>();
                if (col != null) col.enabled = false;
            }
            return;
        }

        Enemy hitEnemy = collision.GetComponentInParent<Enemy>();
        if (hitEnemy != null || collision.CompareTag("Wall"))
        {
            ExplodePulse();
            Destroy(gameObject);
        }
    }

    private void ExplodePulse()
    {
        float ratio = _chargeRatio.Value;
        float currentDamageMult = Mathf.Lerp(minDamageMultiplier, maxDamageMultiplier, ratio);
        int finalDamage = Mathf.RoundToInt(damage * currentDamageMult);
        float currentAoE = Mathf.Lerp(minAoERadius, maxAoERadius, ratio);

        Collider2D[] hitColliders = Physics2D.OverlapCircleAll(transform.position, currentAoE);
        HashSet<Enemy> damagedEnemies = new HashSet<Enemy>();

        foreach (Collider2D col in hitColliders)
        {
            Enemy enemy = col.GetComponentInParent<Enemy>();
            if (enemy != null && damagedEnemies.Add(enemy))
            {
                PlayerStats playerStats = GetOwnerStats();
                if (playerStats != null)
                {
                    playerStats.AddCoins(enemy.hitPrice);
                    enemy.TakeDamage(finalDamage, playerStats);
                }
                else
                {
                    enemy.TakeDamage(finalDamage);
                }
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.magenta;
        float ratio = _chargeRatio != null ? _chargeRatio.Value : 1f;
        float currentAoE = Mathf.Lerp(minAoERadius, maxAoERadius, ratio);
        Gizmos.DrawWireSphere(transform.position, currentAoE);
    }
}
