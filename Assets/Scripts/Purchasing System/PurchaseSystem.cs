using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public abstract class PurchaseSystem : MonoBehaviour
{

    [SerializeField] protected int price;
    [SerializeField] protected Spell spell;
    [SerializeField] protected bool disableOnPurchase = true;
    protected bool hasBeenPurchased = false;
    

    public virtual void AttemptPurchase(Player buyingPlayer)
    {
        // If another player already bought this, do nothing
        if (hasBeenPurchased) return;

        // TrySpendCoins will return true if the player has enough money
        if (buyingPlayer.TrySpendCoins(price))
        {
            hasBeenPurchased = true;
            Debug.Log($"Purchase successful for {price} coins!");

            GrantPurchase(buyingPlayer);

            if (disableOnPurchase)
            {
                gameObject.SetActive(false);
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

    protected abstract void GrantPurchase(Player player);

}
