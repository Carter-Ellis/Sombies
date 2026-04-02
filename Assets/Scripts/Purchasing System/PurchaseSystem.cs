using UnityEngine;
using Unity.Netcode;

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

    protected abstract void GrantPurchase(Entity buyer);

}
