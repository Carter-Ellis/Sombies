using System.Collections;
using Unity.Netcode;
using UnityEngine;
using FMOD.Studio;

public class Vendor : PurchaseSystem
{
    [Header("Vendor Settings")]
    [SerializeField] private Item[] possibleItems;
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private float displayDuration = 7.5f;

    private EventInstance _bubblingInstance;

    private void Start()
    {
        if (type == PurchaseType.CAULDRON)
        {
            StartCoroutine(StartBubblingRoutine());
        }
    }

    private IEnumerator StartBubblingRoutine()
    {
        while (!FMODUnity.RuntimeManager.IsInitialized || !FMODUnity.RuntimeManager.HaveMasterBanksLoaded)
        {
            yield return null;
        }

        while (FMODEvents.instance == null || FMODEvents.instance.bubbling.IsNull)
        {
            yield return null;
        }

        _bubblingInstance = Audio.playSFXInstance(FMODEvents.instance.bubbling, transform.position);
        if (_bubblingInstance.isValid())
        {
            FMODUnity.RuntimeManager.AttachInstanceToGameObject(_bubblingInstance, gameObject);
        }
    }

    private void OnDisable()
    {
        StopBubblingSFX();
    }

    public override void OnDestroy()
    {
        base.OnDestroy();
        StopBubblingSFX();
    }

    private void StopBubblingSFX()
    {
        if (_bubblingInstance.isValid())
        {
            _bubblingInstance.stop(STOP_MODE.ALLOWFADEOUT);
            _bubblingInstance.release();
            _bubblingInstance = default;
        }
    }

    protected override void GrantPurchase(Entity buyer)
    {
        if (FMODEvents.instance != null)
        {
            switch (type)
            {
                case PurchaseType.VENDOR:
                    Audio.PlayNetworkedSFX(FMODEvents.instance.potionBuy, transform.position);
                    break;
                case PurchaseType.CAULDRON:
                    //Play cauldron purchase sound.
                    break;
            }
            
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
