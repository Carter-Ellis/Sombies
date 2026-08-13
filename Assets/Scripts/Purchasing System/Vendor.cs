using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class Vendor : PurchaseSystem
{
    [Header("Vendor Settings")]
    [SerializeField] private Item[] possibleItems;
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private float displayDuration = 7.5f;
    protected override void GrantPurchase(Entity buyer)
    {
        if (FMODEvents.instance != null)
        {
            Audio.PlayNetworkedSFX(FMODEvents.instance.potionBuy, transform.position);
        }
        StartCoroutine(Sequence());
    }

    private IEnumerator Sequence()
    {
        // 1. Pick and Spawn
        int randomIndex = Random.Range(0, possibleItems.Length);
        Item spawnedItem = Instantiate(possibleItems[randomIndex], spawnPoint.position, spawnPoint.rotation);

        spawnedItem.GetComponent<NetworkObject>().Spawn();

        // 2. Wait
        // During this time, hasBeenPurchased is TRUE, 
        // so the player can't buy another box use yet.
        float timer = 0;
        while (timer < displayDuration)
        {
            // If the player picked up the spell, it will be destroyed or disabled.
            if (spawnedItem == null || !spawnedItem.GetComponent<NetworkObject>().IsSpawned)
            {
                break; // Player grabbed it early!
            }
            timer += Time.deltaTime;
            yield return null;
        }

        // 3. Clean up
        if (spawnedItem != null && spawnedItem.GetComponent<NetworkObject>().IsSpawned)
        {
            spawnedItem.GetComponent<NetworkObject>().Despawn();
        }

        // 4. Reset the Box
        Debug.Log("Box is ready for another spin!");
        _hasBeenPurchased.Value = false;
    }

}
