using System;
using UnityEngine;
using UnityEngine.UI; // Change this from UI Elements to UI

public class NetworkManagerUI : MonoBehaviour
{
    [Header("Button References")]
    [SerializeField] private Button hostBtn;
    [SerializeField] private Button clientBtn;
    [SerializeField] private Button disconnectBtn;

    public event Action onStartHost, onStartClient, onDisconnectClient;

    private void Start()
    {
        // In uGUI, we use onClick.AddListener
        hostBtn.onClick.AddListener(() => onStartHost?.Invoke());
        clientBtn.onClick.AddListener(() => onStartClient?.Invoke());
        disconnectBtn.onClick.AddListener(() => onDisconnectClient?.Invoke());

        EnableButtons();
    }

    public void DisableButtons()
    {
        hostBtn.interactable = false;
        clientBtn.interactable = false;
        disconnectBtn.interactable = true;
    }

    public void EnableButtons()
    {
        hostBtn.interactable = true;
        clientBtn.interactable = true;
        disconnectBtn.interactable = false;
    }
}