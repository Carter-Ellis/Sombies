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

    [Header("Audio")]
    [SerializeField] private GameObject audioManagerPrefab;

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
        }
    }

    private void OnDisable()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }
    }

    private async void Start()
    {
        await EnsureServicesInitialized();

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

    private async System.Threading.Tasks.Task EnsureServicesInitialized()
    {
        try
        {
            if (UnityServices.State != ServicesInitializationState.Initialized)
            {
                var options = new InitializationOptions();
                string profile = "Player_" + UnityEngine.Random.Range(10000, 999999);
                options.SetProfile(profile);

                await UnityServices.InitializeAsync(options);
            }

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
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            NetworkManager.Singleton.SceneManager.OnLoadEventCompleted += OnSceneLoaded;

            // Spawn or instantiate the Audio Manager safely
            if (audioManagerPrefab != null && FindAnyObjectByType<Audio>() == null)
            {
                GameObject audioInstance = Instantiate(audioManagerPrefab);
                if (audioInstance.TryGetComponent(out NetworkObject netObj))
                {
                    netObj.Spawn();
                }
            }
        }
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer && NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.SceneManager.OnLoadEventCompleted -= OnSceneLoaded;
        }
    }

    private string GeneratePlayerName(string requestedName)
    {
        string trimmed = string.IsNullOrWhiteSpace(requestedName) ? "" : requestedName.Trim();

        // If requested name is empty or generic default (e.g. "Player", "Player 1"), generate next available "Player X"
        if (string.IsNullOrEmpty(trimmed) || trimmed.Equals("Player", StringComparison.OrdinalIgnoreCase) || trimmed.Equals("Player 1", StringComparison.OrdinalIgnoreCase))
        {
            int number = 1;
            while (IsNameTaken($"Player {number}"))
            {
                number++;
            }
            return $"Player {number}";
        }

        // If custom name is requested, ensure uniqueness
        if (IsNameTaken(trimmed))
        {
            int number = 2;
            while (IsNameTaken($"{trimmed} ({number})"))
            {
                number++;
            }
            return $"{trimmed} ({number})";
        }

        return trimmed;
    }

    private bool IsNameTaken(string name)
    {
        foreach (var pair in clientNames)
        {
            if (string.Equals(pair.Value, name, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
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

        string rawName = "";
        if (request.Payload != null && request.Payload.Length > 0)
        {
            rawName = System.Text.Encoding.UTF8.GetString(request.Payload);
        }

        string assignedName = GeneratePlayerName(rawName);

        Debug.Log($"Server received approval request for Client: {request.ClientNetworkId} with requested name: '{rawName}', assigned: '{assignedName}'");

        clientNames[request.ClientNetworkId] = assignedName;

        response.Approved = true;
        response.CreatePlayerObject = true;
        response.Pending = false;
    }

    private async void StartHost()
    {
        networkManagerUI.DisableButtons();

        await EnsureServicesInitialized();

        string rawName = networkManagerUI.GetPlayerName();
        string myName = GeneratePlayerName(rawName);

        clientNames[NetworkManager.ServerClientId] = myName;

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.NetworkConfig.ConnectionApproval = true;
            NetworkManager.Singleton.ConnectionApprovalCallback = ApprovalCheck;
        }

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
        catch (Exception e)
        {
            Debug.LogError($"Relay Host Error: {e.Message}");
            networkManagerUI.EnableButtons();
            networkManagerUI.ShowLobbyUI(false);
        }
    }

    private async void StartClient()
    {
        networkManagerUI.DisableButtons();

        await EnsureServicesInitialized();

        string myName = networkManagerUI.GetPlayerName();

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.NetworkConfig.ConnectionData = System.Text.Encoding.UTF8.GetBytes(myName ?? "");
        }

        if (!useRelay)
        {
            StartLocalClient();
            return;
        }

        // Grab the code typed into the InputField
        string joinCode = networkManagerUI.GetJoinCodeFromInput();
        if (!string.IsNullOrEmpty(joinCode))
        {
            joinCode = joinCode.Trim().ToUpper();
        }

        if (string.IsNullOrEmpty(joinCode))
        {
            Debug.LogWarning("No join code entered into input field!");
            networkManagerUI.EnableButtons();
            networkManagerUI.ShowLobbyUI(false);
            return;
        }

        try
        {
            Debug.Log($"Client attempting to join Relay allocation with code: '{joinCode}'");

            // 1. Join Allocation
            JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(joinCode);

            // 2. Configure Transport
            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            transport.SetRelayServerData(AllocationUtils.ToRelayServerData(joinAllocation, "dtls"));

            // 3. Start Client
            bool started = NetworkManager.Singleton.StartClient();
            Debug.Log($"NetworkManager.StartClient() result: {started}");
        }
        catch (Exception e)
        {
            Debug.LogError($"Relay Join Error: {e.Message}");
            networkManagerUI.EnableButtons();
            networkManagerUI.ShowLobbyUI(false);
        }
    }

    private void OnClientDisconnected(ulong clientId)
    {
        if (NetworkManager.Singleton != null)
        {
            if (IsServer)
            {
                clientNames.Remove(clientId);
            }

            if (!NetworkManager.Singleton.IsServer && clientId == NetworkManager.Singleton.LocalClientId)
            {
                Debug.LogWarning($"Client disconnected or failed to connect. Reason: {NetworkManager.Singleton.DisconnectReason}");
                if (networkManagerUI != null)
                {
                    networkManagerUI.EnableButtons();
                    networkManagerUI.ShowLobbyUI(false);
                }
            }
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

    // --- NEW: Added Restart Logic ---
    public void RestartGame()
    {
        if (IsServer)
        {
            NetworkManager.Singleton.SceneManager.LoadScene(sceneToLoad, UnityEngine.SceneManagement.LoadSceneMode.Single);
        }
    }

    private void OnGUI()
    {

        if (IsServer && UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == sceneToLoad)
        {
            if (GUI.Button(new Rect(10, 10, 150, 40), "Restart Game"))
            {
                RestartGame();
            }
        }
    }
    // --------------------------------

    private void OnSceneLoaded(string sceneName, UnityEngine.SceneManagement.LoadSceneMode loadMode, List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
    {
        if (!IsServer || sceneName != sceneToLoad) return;

        foreach (ulong clientId in clientsCompleted)
        {
            // 1. Find the current version of this player (Lobby OR previous round)
            if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var networkClient))
            {
                var oldPlayerObject = networkClient.PlayerObject;
                if (oldPlayerObject != null)
                {
                    // Despawn the old object (it will disappear for everyone)
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