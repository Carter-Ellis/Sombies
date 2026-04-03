using Unity.Netcode;
using Unity.Collections;

public class LobbyPlayer : NetworkBehaviour
{
    // Regular string is dynamic and slow
    public NetworkVariable<FixedString32Bytes> PlayerName = new NetworkVariable<FixedString32Bytes>(
        "Player",
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner
    );

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            PlayerName.Value = $"Player {OwnerClientId}";
        }
    }
}