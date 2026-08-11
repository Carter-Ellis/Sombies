using TMPro;
using Unity.Netcode;
using UnityEngine;

public class Door : PurchaseSystem
{
    [Header("Spawn Points & Unlocking")]
    [Tooltip("Direct references to enemy spawn points unlocked when this door is purchased.")]
    [SerializeField] private EnemySpawnPoint[] linkedSpawnPoints;

    [Tooltip("Optional zone/area identifier to unlock when this door is purchased.")]
    [SerializeField] private string areaToUnlock = "";

    private Vector3Int[] doorCellPositions;
    private int myDoorTypeIndex;
    private int myTilemapIndex;

    public string AreaToUnlock => areaToUnlock;

    public void Initialize(Vector3Int[] cellPositions, int doorTypeIndex, int tilemapIndex, int setPrice, string areaToUnlock = "")
    {
        doorCellPositions = cellPositions;
        myDoorTypeIndex = doorTypeIndex;
        myTilemapIndex = tilemapIndex;
        if (!string.IsNullOrEmpty(areaToUnlock))
        {
            this.areaToUnlock = areaToUnlock;
        }

        // Set and sync the price in the base PurchaseSystem class
        SetPrice(setPrice);
    }

    protected override void GrantPurchase(Entity buyer)
    {
        if (DoorTilemapManager.Instance != null)
        {
            DoorTilemapManager.Instance.OpenDoorRpc(doorCellPositions, myDoorTypeIndex, myTilemapIndex);
        }
        else
        {
            Debug.LogError("DoorTilemapManager Instance is missing!");
        }

        // Activate directly linked spawn points on this door instance
        if (linkedSpawnPoints != null)
        {
            foreach (EnemySpawnPoint sp in linkedSpawnPoints)
            {
                if (sp != null)
                {
                    sp.SetActive(true);
                }
            }
        }

        // Notify RoundManager to unlock spawn points referencing this door or matching areaToUnlock
        if (RoundManager.Instance != null)
        {
            RoundManager.Instance.UnlockDoor(this, areaToUnlock);
        }
    }
}