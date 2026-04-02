using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public abstract class Entity : NetworkBehaviour
{
    [Header("Network Sync")]
    // This variable handles the heavy lifting of syncing across the network.
    [SerializeField] protected NetworkVariable<int> _netHealth = new NetworkVariable<int>(100,
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
    public virtual float BaseWalkSpeed { get; }
    public virtual float BaseSprintSpeed { get; }
    public virtual float WalkSpeed { get; set; }
    public virtual float SprintSpeed { get; set; }

    public BuffManager Buffs { get; private set; }

    protected virtual void Awake()
    {
        Buffs = GetComponent<BuffManager>();

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
        // Cleanup to prevent memory leaks
        _netHealth.OnValueChanged -= OnHealthChanged;
    }

    // This method fires on EVERY client whenever the server changes the health
    protected virtual void OnHealthChanged(int previousValue, int newValue)
    {

    }

    public virtual void TakeDamage(int amount)
    {
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