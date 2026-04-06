using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using TMPro;

public class LobbyUI : MonoBehaviour
{
    [SerializeField] private GameObject playerEntryPrefab;
    [SerializeField] private Transform container;

    private List<GameObject> _entryInstances = new List<GameObject>();

    private void OnEnable()
    {
        LobbyPlayer.OnAnyPlayerSpawned += RefreshUI;
        LobbyPlayer.OnAnyPlayerDespawned += RefreshUI;

        RefreshUI();
    }

    private void OnDisable()
    {
        LobbyPlayer.OnAnyPlayerSpawned -= RefreshUI;
        LobbyPlayer.OnAnyPlayerDespawned -= RefreshUI;
    }

    private void RefreshUI()
    {
        // Give Netcode a tiny moment to finish internal registration
        CancelInvoke(nameof(UpdatePlayerList));
        Invoke(nameof(UpdatePlayerList), 0.1f);
    }

    public void UpdatePlayerList()
    {
        // Cleanup existing UI entries
        foreach (var entry in _entryInstances)
        {
            if (entry != null) Destroy(entry);
        }
        _entryInstances.Clear();

        // Find all LobbyPlayers in the scene
        LobbyPlayer[] players = Object.FindObjectsByType<LobbyPlayer>(FindObjectsInactive.Include);

        foreach (LobbyPlayer player in players)
        {
            // Ensure the player is actually spawned and in the active scene
            if (!player.IsSpawned || !player.gameObject.scene.IsValid()) continue;

            GameObject newEntry = Instantiate(playerEntryPrefab, container);
            var text = newEntry.GetComponentInChildren<TextMeshProUGUI>();

            if (text != null)
            {
                string nameValue = player.PlayerName.Value.ToString();
                text.text = string.IsNullOrEmpty(nameValue) ? "Connecting..." : nameValue;
            }

            _entryInstances.Add(newEntry);
        }
    }
}