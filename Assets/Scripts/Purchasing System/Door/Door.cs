using TMPro;
using Unity.Netcode;
using UnityEngine;

public class Door : PurchaseSystem
{
    private Vector3Int[] doorCellPositions;
    private int myDoorTypeIndex;
    private int myTilemapIndex;

    // Add 'int tilemapIndex' and 'int setPrice' to the parameters
    public void Initialize(Vector3Int[] cellPositions, int doorTypeIndex, int tilemapIndex, int setPrice)
    {
        doorCellPositions = cellPositions;
        myDoorTypeIndex = doorTypeIndex;
        myTilemapIndex = tilemapIndex;

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
    }
}