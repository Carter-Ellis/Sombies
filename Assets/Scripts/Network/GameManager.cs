using System;
using System.Collections.Generic;
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
    [SerializeField] private string sceneToLoad = "SampleScene";
    [SerializeField] private GameObject playerPrefab;
    private Dictionary<ulong, string> clientNames = new Dictionary<ulong, string>();

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.NetworkConfig.ConnectionApproval = true;
        }

    }

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
            networkManagerUI.onStartGame += StartGame;
        }

        if (IsServer)
        {
            NetworkManager.Singleton.SceneManager.OnLoadEventCompleted += OnSceneLoaded;
        }
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            NetworkManager.Singleton.SceneManager.OnLoadEventCompleted += OnSceneLoaded;
        }
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer && NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.SceneManager.OnLoadEventCompleted -= OnSceneLoaded;
        }
    }

    private void ApprovalCheck(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
    {
        // Check if host
        if (request.ClientNetworkId == NetworkManager.ServerClientId)
        {
            response.Approved = true;
            response.CreatePlayerObject = true;
            response.Pending = false;
            return;
        }

        string payloadName = System.Text.Encoding.UTF8.GetString(request.Payload);

        Debug.Log($"Server received approval request for Client: {request.ClientNetworkId} with name: {payloadName}");

        if (string.IsNullOrEmpty(payloadName))
        {
            payloadName = "Player " + (clientNames.Count + 1);
        }

        clientNames[request.ClientNetworkId] = payloadName;

        response.Approved = true;
        response.CreatePlayerObject = true;
        response.Pending = false;
    }

    private async void StartHost()
    {
        networkManagerUI.DisableButtons();

        string myName = networkManagerUI.GetPlayerName();

        // If host did not put name, assign default "Player 1"
        if (string.IsNullOrEmpty(myName))
        {
            myName = "Player 1";
        }

        clientNames[NetworkManager.ServerClientId] = myName;

        NetworkManager.Singleton.ConnectionApprovalCallback = ApprovalCheck;

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

        string myName = networkManagerUI.GetPlayerName();

        NetworkManager.Singleton.NetworkConfig.ConnectionData = System.Text.Encoding.UTF8.GetBytes(myName);

        NetworkManager.Singleton.ConnectionApprovalCallback = ApprovalCheck;

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

    public void StartGame()
    {
        if (IsServer) 
        {
            NetworkManager.Singleton.SceneManager.LoadScene(sceneToLoad, UnityEngine.SceneManagement.LoadSceneMode.Single);
        }
    }

    private void OnSceneLoaded(string sceneName, UnityEngine.SceneManagement.LoadSceneMode loadMode, List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
    {
        if (!IsServer || sceneName != sceneToLoad) return;

        foreach (ulong clientId in clientsCompleted)
        {
            // 1. Find the "Lobby" version of this player
            if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var networkClient))
            {
                var oldPlayerObject = networkClient.PlayerObject;
                if (oldPlayerObject != null)
                {
                    // Despawn the Lobby object (it will disappear for everyone)
                    oldPlayerObject.Despawn(true);
                }
            }

            // 2. Instantiate the "Actual Gameplay" version (your existing code)
            GameObject playerInstance = Instantiate(playerPrefab);
            var playerScript = playerInstance.GetComponent<Player>();

            if (playerScript != null && clientNames.TryGetValue(clientId, out string savedName))
            {
                playerScript.playerName.Value = savedName;
            }

            // 3. Re-assign this as the official PlayerObject for this client
            playerInstance.GetComponent<NetworkObject>().SpawnAsPlayerObject(clientId, true);
        }
    }

    public bool GetSavedName(ulong clientId, out string name)
    {
        return clientNames.TryGetValue(clientId, out name);
    }

}