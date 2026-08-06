using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using Unity.Netcode;

[System.Serializable]
public struct DoorTilePair
{
    public string doorName; // Just to keep things organized in the inspector!
    public TileBase closedTile;
    public TileBase openTile;
}

public class DoorTilemapManager : NetworkBehaviour
{
    public static DoorTilemapManager Instance;

    [Header("Tilemap References")]
    [SerializeField] private Tilemap doorTilemap;

    // This array replaces the single tile references!
    [SerializeField] private DoorTilePair[] doorTypes;

    [Header("Network Prefabs")]
    [SerializeField] private GameObject doorTriggerPrefab;

    private void Awake()
    {
        if (Instance == null) Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;
        ScanAndSpawnDoors();
    }

    private void ScanAndSpawnDoors()
    {
        BoundsInt bounds = doorTilemap.cellBounds;
        HashSet<Vector3Int> visitedTiles = new HashSet<Vector3Int>();

        for (int x = bounds.xMin; x < bounds.xMax; x++)
        {
            for (int y = bounds.yMin; y < bounds.yMax; y++)
            {
                Vector3Int cellPos = new Vector3Int(x, y, 0);
                TileBase tile = doorTilemap.GetTile(cellPos);

                if (tile != null && !visitedTiles.Contains(cellPos))
                {
                    // Check if this tile matches ANY of our closed door types
                    for (int i = 0; i < doorTypes.Length; i++)
                    {
                        if (tile == doorTypes[i].closedTile)
                        {
                            // Pass the specific closed tile to the flood fill so it doesn't accidentally 
                            // connect a vertical door to a horizontal door if they touch!
                            List<Vector3Int> doorGroup = GetConnectedTiles(cellPos, visitedTiles, doorTypes[i].closedTile);

                            // Pass the index 'i' so the trigger knows which pair it belongs to
                            SpawnDoorTrigger(doorGroup, i);
                            break;
                        }
                    }
                }
            }
        }
    }

    private List<Vector3Int> GetConnectedTiles(Vector3Int startPos, HashSet<Vector3Int> visitedTiles, TileBase targetTile)
    {
        List<Vector3Int> group = new List<Vector3Int>();
        Queue<Vector3Int> queue = new Queue<Vector3Int>();

        queue.Enqueue(startPos);
        visitedTiles.Add(startPos);

        Vector3Int[] directions = { Vector3Int.up, Vector3Int.down, Vector3Int.left, Vector3Int.right };

        while (queue.Count > 0)
        {
            Vector3Int current = queue.Dequeue();
            group.Add(current);

            foreach (Vector3Int dir in directions)
            {
                Vector3Int neighbor = current + dir;
                // Only connect if it is the EXACT same type of closed door tile
                if (!visitedTiles.Contains(neighbor) && doorTilemap.GetTile(neighbor) == targetTile)
                {
                    visitedTiles.Add(neighbor);
                    queue.Enqueue(neighbor);
                }
            }
        }
        return group;
    }

    private void SpawnDoorTrigger(List<Vector3Int> doorCells, int doorTypeIndex)
    {
        int minX = int.MaxValue, minY = int.MaxValue;
        int maxX = int.MinValue, maxY = int.MinValue;

        foreach (Vector3Int pos in doorCells)
        {
            if (pos.x < minX) minX = pos.x;
            if (pos.x > maxX) maxX = pos.x;
            if (pos.y < minY) minY = pos.y;
            if (pos.y > maxY) maxY = pos.y;
        }

        Vector3 minWorld = doorTilemap.GetCellCenterWorld(new Vector3Int(minX, minY, 0));
        Vector3 maxWorld = doorTilemap.GetCellCenterWorld(new Vector3Int(maxX, maxY, 0));
        Vector3 centerWorld = (minWorld + maxWorld) / 2f;

        GameObject doorObj = Instantiate(doorTriggerPrefab, centerWorld, Quaternion.identity);
        doorObj.GetComponent<NetworkObject>().Spawn();

        BoxCollider2D col = doorObj.GetComponent<BoxCollider2D>();
        if (col != null)
        {
            float width = (maxX - minX + 1) * doorTilemap.cellSize.x;
            float height = (maxY - minY + 1) * doorTilemap.cellSize.y;
            col.size = new Vector2(width, height);
        }

        // Send the index of the door pair to the Door script
        doorObj.GetComponent<Door>().Initialize(doorCells.ToArray(), doorTypeIndex);
    }

    [ClientRpc]
    public void OpenDoorClientRpc(Vector3Int[] cellPositions, int doorTypeIndex)
    {
        // Use the index to grab the correct open tile for this specific door
        TileBase openTile = doorTypes[doorTypeIndex].openTile;

        foreach (Vector3Int pos in cellPositions)
        {
            doorTilemap.SetTile(pos, openTile);
        }
    }
}