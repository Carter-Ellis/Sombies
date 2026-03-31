using System;
using Unity.Netcode;
using UnityEngine;

public class GameManager : NetworkBehaviour
{
    [SerializeField] private NetworkManagerUI networkManagerUI;
    void Start()
    {
        if (networkManagerUI != null)
        {
            networkManagerUI.onStartHost += StartHost;
            networkManagerUI.onStartClient += StartClient;
            networkManagerUI.onDisconnectClient += DisconnectClient;
        }
    }

    private void DisconnectClient()
    {
        networkManagerUI.DisableButtons();
        NetworkManager.Shutdown();
    }

    private void StartClient()
    {
        networkManagerUI.DisableButtons();
        NetworkManager.StartClient();
    }

    private void StartHost()
    {
        networkManagerUI.DisableButtons();
        NetworkManager.StartHost();
    }
}
