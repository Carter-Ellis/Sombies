using UnityEngine;
using UnityEngine.UI;
using System;
using TMPro; // Needed for modern text and input fields

public class NetworkManagerUI : MonoBehaviour
{
    [Header("Button References")]
    [SerializeField] private Button hostBtn;
    [SerializeField] private Button clientBtn;
    [SerializeField] private Button disconnectBtn;
    [SerializeField] private Button startGameBtn;

    [Header("Lobby UI References")]
    [SerializeField] private GameObject lobbyPanel;
    [SerializeField] private TMP_InputField nameInputField;

    [Header("Relay UI References")]
    [SerializeField] private TMP_InputField joinInputField; // Drag your InputField here
    [SerializeField] private TextMeshProUGUI joinCodeText;   // Drag a TextMeshPro text here

    public event Action onStartHost, onStartClient, onDisconnectClient, onStartGame;

    private void Awake()
    {
        ShowStartButton(false);
        ShowLobbyUI(false);
    }

    private void Start()
    {
        hostBtn.onClick.AddListener(() => {
            onStartHost?.Invoke();
            ShowLobbyUI(true);
            ShowStartButton(true);
        });

        clientBtn.onClick.AddListener(() => {
            onStartClient?.Invoke();
            ShowLobbyUI(true);
            ShowStartButton(false);
        });

        disconnectBtn.onClick.AddListener(() => {
            onDisconnectClient?.Invoke();
            ShowLobbyUI(false);
            ShowStartButton(false);
        });

        startGameBtn.onClick.AddListener(() => {
            onStartGame?.Invoke();
        });
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

    // --- NEW RELAY HELPER METHODS ---

    public void DisplayJoinCode(string code)
    {
        if (joinCodeText != null)
        {
            joinCodeText.text = $"Join Code: {code}";
        }
    }

    public string GetJoinCodeFromInput()
    {
        if (joinInputField != null)
        {
            return joinInputField.text.Trim(); // Trim removes accidental spaces
        }
        return "";
    }

    public void ShowStartButton(bool isVisible)
    {
        if (startGameBtn != null)
        {
            startGameBtn.gameObject.SetActive(isVisible);
        }
    }

    public void ShowLobbyUI(bool isVisible)
    {
        if (lobbyPanel != null)
        {
            lobbyPanel.SetActive(isVisible);
        }

    }

    public string GetPlayerName()
    {
        return nameInputField.text;
    }

}