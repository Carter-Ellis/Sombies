using TMPro;
using Unity.Netcode;
using UnityEngine;

enum PurchaseType
{
    SPELL,
    MYSTERY_BOX,
    DOOR,
    VENDOR,
}   

public abstract class PurchaseSystem : NetworkBehaviour
{

    [SerializeField] protected int price;
    [SerializeField] protected Spell spell;
    [SerializeField] protected bool disableOnPurchase = true;

    protected NetworkVariable<bool> hasBeenPurchased = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    protected TextMeshPro priceTxt;

    [SerializeField] PurchaseType type;

    private void Awake()
    {
        priceTxt = GetComponentInChildren<TextMeshPro>();
        if (priceTxt != null)
        {
            priceTxt.gameObject.SetActive(false);
        }
        UpdatePriceText();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        hasBeenPurchased.OnValueChanged += OnPurchasedStateChanged;

        if (hasBeenPurchased.Value && disableOnPurchase)
        {
            gameObject.SetActive(false);
        }
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        hasBeenPurchased.OnValueChanged -= OnPurchasedStateChanged;

        if (disableOnPurchase)
        {
            gameObject.SetActive(false);
        }
    }

    private void OnPurchasedStateChanged(bool previousValue, bool newValue)
    {
        if (newValue && disableOnPurchase)
        {
            gameObject.SetActive(false);
        }
    }

    public virtual void AttemptPurchase(Entity buyer)
    {

        PlayerStats playerStats = buyer.GetComponent<PlayerStats>();

        if (playerStats == null) return;

        // TrySpendCoins will return true if the player has enough money
        if (!hasBeenPurchased.Value && playerStats.TrySpendCoins(price))
        {
            hasBeenPurchased.Value = true;

            GrantPurchase(buyer);

            if (disableOnPurchase)
            {
                NetworkObject netObj = GetComponent<NetworkObject>();
                if (netObj != null && netObj.IsSpawned)
                {
                    if (netObj.IsSceneObject == true)
                    {
                        netObj.Despawn(false);
                        gameObject.SetActive(false);
                    }
                    else
                    {
                        netObj.Despawn(true);
                    }
                }
            }
            
        }
        else
        {
            Debug.Log("Not enough coins to purchase this!");
        }
    }

    public void MakeFree()
    {
        price = 0;
    }

    protected void UpdatePriceText()
    {
        if (priceTxt == null)
        {
            Debug.LogError("Price Text component is not assigned!");
        }
        
        switch(type)
        {
            case PurchaseType.SPELL:
                priceTxt.text = "E to buy " + spell.Name + " [Cost: " + price.ToString() + "]";
                break;
            case PurchaseType.MYSTERY_BOX:
                priceTxt.text = "E to buy mystery box [Cost: " + price.ToString() + "]";
                break;
            case PurchaseType.DOOR:
                priceTxt.text = "E to unlock door [Cost: " + price.ToString() + "]";
                break;
            case PurchaseType.VENDOR:
                priceTxt.text = "E to buy from vendor [Cost: " + price.ToString() + "]";
                break;
            default:
                Debug.LogError("Unknown purchase type!");
                break;
        }  
        
    }

    public void DisplayPrice()
    {
        if (priceTxt != null)
        {
            priceTxt.gameObject.SetActive(true);
        }
    }

    public void HidePrice()
    {
        if (priceTxt != null)
        {
            priceTxt.gameObject.SetActive(false);
        }
    }

    protected abstract void GrantPurchase(Entity buyer);

}
