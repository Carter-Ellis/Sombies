using TMPro;
using Unity.Netcode;
using UnityEngine;

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

    private void Awake()
    {
        priceTxt = GetComponentInChildren<TextMeshPro>();
        priceTxt.gameObject.SetActive(false);
        UpdatePriceText();
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
                GetComponent<NetworkObject>().Despawn();
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
        
        if (spell == null)
        {
            // This is the mysterybox
            priceTxt.text = "E to buy mystery box [Cost: " + price.ToString() + "]";
        }
        else
        {
            // This is a spell purchase
            priceTxt.text = "E to buy " + spell.Name + " [Cost: " + price.ToString() + "]";
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
