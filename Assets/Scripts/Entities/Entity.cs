using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public abstract class Entity : NetworkBehaviour
{
    [Header("Network Sync")]
    [SerializeField]
    protected NetworkVariable<int> _netHealth = new NetworkVariable<int>(100,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    [SerializeField]
    protected NetworkVariable<int> _netMaxHealth = new NetworkVariable<int>(100,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public virtual int MaxHealth
    {
        get => _netMaxHealth.Value;
        set
        {
            if (!IsServer) return;
            _netMaxHealth.Value = value;

            Health = value;
        }
    }

    public virtual int Health
    {
        get => _netHealth.Value;
        protected set
        {
            if (!IsServer) return;

            _netHealth.Value = Mathf.Clamp(value, 0, MaxHealth);


            if (_netHealth.Value <= 0)
            {
                Die();
            }

        }
    }

    [Header("Movement")]
    [SerializeField] protected float _baseWalkSpeed = 5f;
    [SerializeField] protected float _baseSprintSpeed = 7f;

    public virtual float BaseWalkSpeed => _baseWalkSpeed;
    public virtual float BaseSprintSpeed => _baseSprintSpeed;
    public virtual float WalkSpeed { get; set; }
    public virtual float SprintSpeed { get; set; }

    public BuffManager Buffs { get; private set; }

    protected virtual void Awake()
    {
        Buffs = GetComponent<BuffManager>();
        WalkSpeed = BaseWalkSpeed;
        SprintSpeed = BaseSprintSpeed;
    }

    public override void OnNetworkSpawn()
    {
        _netHealth.OnValueChanged += OnHealthChanged;

        if (IsServer)
        {
            _netHealth.Value = MaxHealth;
        }

        OnHealthChanged(_netHealth.Value, _netHealth.Value);

    }

    public override void OnNetworkDespawn()
    {
        _netHealth.OnValueChanged -= OnHealthChanged;
    }

    protected virtual void OnHealthChanged(int previousValue, int newValue)
    {

    }

    public virtual void TakeDamage(int amount)
    {
        Audio.playSFX(FMODEvents.instance.playerHurt, transform.position);
        Health -= amount;
    }

    public virtual void Heal(int amount)
    {
        Health += amount;
    }

    public virtual void SetHealth(int amount)
    {
        Health = amount;
    }

    public virtual void Die()
    {
        if (!IsServer) return;

        GetComponent<NetworkObject>().Despawn();

    }
}