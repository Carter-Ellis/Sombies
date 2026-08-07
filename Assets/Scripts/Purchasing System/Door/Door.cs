using TMPro;
using Unity.Netcode;
using UnityEngine;

public class Door : PurchaseSystem
{
    private Vector3Int[] doorCellPositions;
    private int myDoorTypeIndex;

    // Add 'int setPrice' to the parameters
    public void Initialize(Vector3Int[] cellPositions, int doorTypeIndex, int setPrice)
    {
        doorCellPositions = cellPositions;
        myDoorTypeIndex = doorTypeIndex;

        // Set the price in the base PurchaseSystem class
        price = setPrice;

        // Force the text to update with the new dynamic price!
        UpdatePriceText();
    }

    protected override void GrantPurchase(Entity buyer)
    {
        if (DoorTilemapManager.Instance != null)
        {
            DoorTilemapManager.Instance.OpenDoorRpc(doorCellPositions, myDoorTypeIndex);
        }
        else
        {
            Debug.LogError("DoorTilemapManager Instance is missing!");
        }
    }
}