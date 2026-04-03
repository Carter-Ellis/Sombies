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
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += RefreshList;
            NetworkManager.Singleton.OnClientDisconnectCallback += RefreshList;
        }

        Invoke(nameof(UpdatePlayerList), 0.5f);
    }

    private void OnDisable()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= RefreshList;
            NetworkManager.Singleton.OnClientDisconnectCallback -= RefreshList;
        }
    }

    private void RefreshList(ulong id)
    {
        Invoke(nameof(UpdatePlayerList), 0.5f);
    }

    public void UpdatePlayerList()
    {
        foreach (var entry in _entryInstances)
        {
            if (entry != null) Destroy(entry);
        }
        _entryInstances.Clear();

        LobbyPlayer[] players = Object.FindObjectsByType<LobbyPlayer>(FindObjectsInactive.Include);

        foreach (LobbyPlayer player in players)
        {
            if (!player.gameObject.scene.IsValid()) continue;

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