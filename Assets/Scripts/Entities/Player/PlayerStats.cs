using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UIElements;

public class PlayerStats : Entity
{
    [Header("Network Stats")]
    public NetworkVariable<int> _netCoins = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> _netMana = new NetworkVariable<int>(100, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> _netMaxMana = new NetworkVariable<int>(100, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<bool> isHidden = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public int Coins
    {
        get => _netCoins.Value;
        protected set
        {
            if (!IsServer) return;
            _netCoins.Value = value;
        }
    }

    public int MaxMana
    {
        get => _netMaxMana.Value;
        set
        {
            if (!IsServer) return;
            _netMaxMana.Value = value;

            Mana = value;
        }
    }

    public int Mana
    {
        get => _netMana.Value;
        set
        {
            if (!IsServer) return;
            _netMana.Value = Mathf.Clamp(value, 0, MaxMana);
        }
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (IsOwner)
        {
            if (UIManager.Instance != null)
            {
                UIManager.Instance.SetHUDVisibility(true);
                UpdatePlayerUI();
            }
        }

        _netMana.OnValueChanged += OnManaChanged;
        _netCoins.OnValueChanged += OnCoinsChanged;

    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        _netMana.OnValueChanged -= OnManaChanged;
        _netCoins.OnValueChanged -= OnCoinsChanged;
    }

    protected override void OnHealthChanged(int oldValue, int newValue)
    {
        base.OnHealthChanged(oldValue, newValue);
        if (IsOwner)
        {
            UpdatePlayerUI();
        }
    }

    private void OnManaChanged(int oldVal, int newVal)
    {
        if (IsOwner)
        {
            UpdatePlayerUI();
        }
    }

    public void AddMana(int amount)
    {
        if (amount < 0) return;   
        Mana += amount;
    }

    private void OnCoinsChanged(int oldVal, int newVal)
    {
        if (IsOwner)
        {
            UpdatePlayerUI();
        }
    }

    public bool TrySpendCoins(int price)
    {
        if (Coins < price)
        {
            return false;
        }
        Coins -= price;
        return true;
    }

    public void AddCoins(int amount)
    {
        if (amount < 0) return;   
        Coins += amount;
    }

    private void UpdatePlayerUI()
    {
        if (IsOwner && UIManager.Instance != null)
        {
            UIManager.Instance.UpdateHUD(Health, MaxHealth, Mana, Coins);
        }
    }

    public override void Die()
    {
        if (!IsServer) return;

        if (TryGetComponent<Player>(out var playerController))
        {
            playerController.OnDeathTriggered();
        }
    }

}
