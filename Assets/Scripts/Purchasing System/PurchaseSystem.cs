using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public abstract class PurchaseSystem : MonoBehaviour
{

    [SerializeField] protected int price;
    [SerializeField] protected Spell spell;

    private bool hasBeenPurchased = false;

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

            // Disable the object so it can't be interacted with anymore
            gameObject.SetActive(false);
        }
        else
        {
            Debug.Log("Not enough coins to purchase this!");
        }
    }

    protected abstract void GrantPurchase(Player player);

}
