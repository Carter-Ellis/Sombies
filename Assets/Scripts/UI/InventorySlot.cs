using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class InventorySlot : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private Image iconImage;
    [SerializeField] private Image frameImage;
    [SerializeField] private TextMeshProUGUI keyLabelText;
    [SerializeField] private Color slotColor = Color.white;

    [Header("Selection Colors")]
    [SerializeField] private Color selectedIconColor = Color.white;
    [SerializeField] private Color unselectedIconColor = new Color(0.45f, 0.45f, 0.45f, 0.6f);
    [SerializeField] private Color selectedFrameColor = Color.white;
    [SerializeField] private Color unselectedFrameColor = new Color(0.3f, 0.3f, 0.3f, 0.5f);

    [Header("Selection Scale & Animation")]
    [SerializeField] private float selectedScale = 1.25f;
    [SerializeField] private float unselectedScale = 0.85f;
    [SerializeField] private float transitionSpeed = 12f;

    private Vector3 _targetScale = Vector3.one;
    private Color _targetIconColor = Color.white;
    private Color _targetFrameColor = Color.white;

    private void Update()
    {
        if (iconImage != null && iconImage.enabled)
        {
            iconImage.transform.localScale = Vector3.Lerp(iconImage.transform.localScale, _targetScale, Time.deltaTime * transitionSpeed);
            iconImage.color = Color.Lerp(iconImage.color, _targetIconColor, Time.deltaTime * transitionSpeed);
        }

        if (frameImage != null)
        {
            frameImage.color = Color.Lerp(frameImage.color, _targetFrameColor, Time.deltaTime * transitionSpeed);
        }
    }

    public void UpdateSlot(Item item, bool isSelected, string keyLabel)
    {
        if (keyLabelText != null)
        {
            keyLabelText.text = keyLabel;
            keyLabelText.gameObject.SetActive(true);
        }

        if (item != null)
        {
            if (iconImage != null)
            {
                iconImage.sprite = item.ItemIcon;
                Color baseColor = item.ItemColor;
                _targetIconColor = isSelected ? baseColor : new Color(baseColor.r * 0.5f, baseColor.g * 0.5f, baseColor.b * 0.5f, baseColor.a * 0.6f);
                _targetScale = isSelected ? Vector3.one * selectedScale : Vector3.one * unselectedScale;
                iconImage.enabled = true;
            }
        }
        else
        {
            if (iconImage != null)
            {
                iconImage.enabled = false;
            }
            _targetScale = Vector3.one * unselectedScale;
        }

        _targetFrameColor = isSelected ? selectedFrameColor : unselectedFrameColor;
    }

    public void UpdateSlot(Spell spell, bool isSelected, string keyLabel)
    {
        if (spell != null)
        {
            if (keyLabelText != null)
            {
                keyLabelText.text = keyLabel;
                keyLabelText.gameObject.SetActive(true);
            }

            if (iconImage != null)
            {
                iconImage.sprite = spell.sprite;
                _targetIconColor = isSelected ? selectedIconColor : unselectedIconColor;
                _targetScale = isSelected ? Vector3.one * selectedScale : Vector3.one * unselectedScale;
                iconImage.enabled = spell.sprite != null;
            }
        }
        else
        {
            if (keyLabelText != null)
            {
                keyLabelText.gameObject.SetActive(false);
            }

            if (iconImage != null)
            {
                iconImage.enabled = false;
            }
            _targetScale = Vector3.one * unselectedScale;
        }

        _targetFrameColor = isSelected ? selectedFrameColor : unselectedFrameColor;
    }
}