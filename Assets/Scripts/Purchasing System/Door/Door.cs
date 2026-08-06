using TMPro;
using Unity.Netcode;
using UnityEngine;

public class Door : PurchaseSystem
{
    private Vector3Int[] doorCellPositions;
    private int myDoorTypeIndex; // Remembers if this is a vertical or horizontal door

    public void Initialize(Vector3Int[] cellPositions, int doorTypeIndex)
    {
        doorCellPositions = cellPositions;
        myDoorTypeIndex = doorTypeIndex;
    }

    protected override void GrantPurchase(Entity buyer)
    {
        if (DoorTilemapManager.Instance != null)
        {
            // Pass both the coordinates and the index to the RPC
            DoorTilemapManager.Instance.OpenDoorClientRpc(doorCellPositions, myDoorTypeIndex);
        }
        else
        {
            Debug.LogError("DoorTilemapManager Instance is missing!");
        }
    }
}