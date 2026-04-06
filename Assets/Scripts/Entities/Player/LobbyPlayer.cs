using Unity.Netcode;
using Unity.Collections;
using System;

public class LobbyPlayer : NetworkBehaviour
{
    public static Action OnAnyPlayerSpawned;
    public static Action OnAnyPlayerDespawned;

    public NetworkVariable<FixedString32Bytes> PlayerName = new NetworkVariable<FixedString32Bytes>(
        "Player",
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            GameManager gm = FindAnyObjectByType<GameManager>();
            if (gm != null)
            {
                if (gm.GetSavedName(OwnerClientId, out string savedName))
                {
                    PlayerName.Value = savedName;
                }
            }
        }

        OnAnyPlayerSpawned?.Invoke();

        PlayerName.OnValueChanged += OnNameChanged;
    }

    private void OnNameChanged(FixedString32Bytes oldVal, FixedString32Bytes newVal)
    {
        OnAnyPlayerSpawned?.Invoke();
    }

    public override void OnNetworkDespawn()
    {
        OnAnyPlayerDespawned?.Invoke();
        PlayerName.OnValueChanged -= OnNameChanged;
    }
}