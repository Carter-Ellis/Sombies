using System;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

public class GameManager : NetworkBehaviour
{
    [SerializeField] private NetworkManagerUI networkManagerUI;
    [SerializeField] private bool useRelay = true;

    async void Start()
    {
        try
        {
            // Initialize Unity Services (Required for 2024+)
            await UnityServices.InitializeAsync();

            // Sign in if not already (Relay requires an authenticated session)
            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
                Debug.Log($"Signed in as: {AuthenticationService.Instance.PlayerId}");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Services Initialization Failed: {e.Message}");
        }

        if (networkManagerUI != null)
        {
            networkManagerUI.onStartHost += StartHost;
            networkManagerUI.onStartClient += StartClient;
            networkManagerUI.onDisconnectClient += DisconnectClient;
        }
    }

    private async void StartHost()
    {
        networkManagerUI.DisableButtons();

        if (!useRelay)
        {
            StartLocalHost();
            return;
        }

        try
        {
            // 1. Create Allocation for 4 players
            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(4);

            // 2. Generate Join Code
            string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

            // 3. Show code in UI so you can give it to a friend
            networkManagerUI.DisplayJoinCode(joinCode);
            Debug.Log($"Host started! Join Code: {joinCode}");

            // 4. Configure Transport for Relay (Modern 2024+ way)
            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            transport.SetRelayServerData(AllocationUtils.ToRelayServerData(allocation, "dtls"));

            // 5. Start Host
            NetworkManager.Singleton.StartHost();
        }
        catch (RelayServiceException e)
        {
            Debug.LogError($"Relay Host Error: {e.Message}");
        }
    }

    private async void StartClient()
    {
        networkManagerUI.DisableButtons();

        if (!useRelay)
        {
            StartLocalClient();
            return;
        }

        // Grab the code typed into the InputField
        string joinCode = networkManagerUI.GetJoinCodeFromInput();

        if (string.IsNullOrEmpty(joinCode))
        {
            //If no code was entered, just re-enable buttons and exit
            networkManagerUI.EnableButtons();
            return;
        }

        try
        {
            // 1. Join Allocation
            JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(joinCode);

            // 2. Configure Transport
            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            transport.SetRelayServerData(AllocationUtils.ToRelayServerData(joinAllocation, "dtls"));

            // 3. Start Client
            NetworkManager.Singleton.StartClient();
        }
        catch (RelayServiceException e)
        {
            Debug.LogError($"Relay Join Error: {e.Message}");
        }
    }

    private void DisconnectClient()
    {
        networkManagerUI.DisableButtons();
        if (NetworkManager.Singleton != null)
        {
            networkManagerUI.EnableButtons();
            NetworkManager.Singleton.Shutdown();
        }
    }
    private void StartLocalHost()
    {
        // 1. Get the transport
        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();

        // 2. Set it to Localhost (127.0.0.1) and port 7777 (standard)
        transport.SetConnectionData("127.0.0.1", 7777);

        // 3. Just start
        NetworkManager.Singleton.StartHost();
        Debug.Log("Local Host Started (No Relay)");
    }

    private void StartLocalClient()
    {
        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        transport.SetConnectionData("127.0.0.1", 7777);
        NetworkManager.Singleton.StartClient();
        Debug.Log("Local Client Started (Skipped Relay)");
    }

}