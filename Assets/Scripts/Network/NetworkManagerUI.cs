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

    [Header("Relay UI References")]
    [SerializeField] private TMP_InputField joinInputField; // Drag your InputField here
    [SerializeField] private TextMeshProUGUI joinCodeText;   // Drag a TextMeshPro text here

    public event Action onStartHost, onStartClient, onDisconnectClient;

    private void Start()
    {
        hostBtn.onClick.AddListener(() => onStartHost?.Invoke());
        clientBtn.onClick.AddListener(() => onStartClient?.Invoke());
        disconnectBtn.onClick.AddListener(() => onDisconnectClient?.Invoke());

        EnableButtons();

        // Clear the code text on start
        if (joinCodeText != null) joinCodeText.text = "";
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
            Debug.Log($"Displaying Code: {code}");
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
}