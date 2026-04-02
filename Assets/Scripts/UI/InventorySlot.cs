using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class InventorySlot : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private Image iconImage;
    [SerializeField] private Image frameImage;
    [SerializeField] private TextMeshProUGUI keyLabelText; // Drag your Z/X/C text here

    public void UpdateSlot(Item item, bool isSelected, string keyLabel)
    {
        if (keyLabelText != null)
            keyLabelText.text = keyLabel;

        // Handle Item Visuals
        if (item != null)
        {
            iconImage.sprite = item.ItemIcon;
            iconImage.color = item.ItemColor;
            iconImage.enabled = true;
        }
        else
        {
            iconImage.enabled = false;
        }

        if (frameImage != null)
            frameImage.color = isSelected ? Color.yellow : Color.white;
    }
}