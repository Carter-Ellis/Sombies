using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Entity : NetworkBehaviour
{
    [Header("Network Sync")]
    // This variable handles the heavy lifting of syncing across the network.
    [SerializeField] protected NetworkVariable<int> _netHealth = new NetworkVariable<int>(100,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    [Header("Base Entity Health")]
    [SerializeField] protected int _maxHealth = 100;
    

    public virtual int MaxHealth
    {
        get => _maxHealth;
        set => _maxHealth = value;
    }

    public virtual int Health
    {
        get => _netHealth.Value;
        set
        {
            if (IsServer)
            {
                _netHealth.Value = Mathf.Clamp(value, 0, MaxHealth);
                

                if (_netHealth.Value <= 0)
                {
                    Die();
                }
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
        else
        {
            OnHealthChanged(0, _netHealth.Value);
        }
    }

    public override void OnNetworkDespawn()
    {
        // Cleanup to prevent memory leaks
        _netHealth.OnValueChanged -= OnHealthChanged;
    }

    // This method fires on EVERY client whenever the server changes the health
    protected virtual void OnHealthChanged(int previousValue, int newValue)
    {
        Debug.Log($"{gameObject.name} health update: {newValue}");
        // You can trigger hurt animations or UI updates here
    }

    public virtual void TakeDamage(int amount)
    {
        Health -= amount;
        Debug.Log($"{gameObject.name} took damage! Current health: {Health}");
    }

    public virtual void Heal(int amount)
    {
        Health += amount;
        Debug.Log($"{gameObject.name} healed! Current health: {Health}");
    }

    public virtual void Die()
    {
        if (IsServer)
        {
            // In Netcode, use Despawn to remove the object for everyone
            GetComponent<NetworkObject>().Despawn();
        }
        else
        {
            // Local fallback logic if needed
            gameObject.SetActive(false);
        }
    }
}