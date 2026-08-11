using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using Unity.Netcode;
using System.Linq;

[System.Serializable]
public struct DoorTilePair
{
    public string doorName;
    public TileBase closedTile;
    public TileBase openTile;
    public int price;
    public string areaToUnlock;
}

[System.Serializable]
public struct DoorTilemapConfig
{
    public Tilemap tilemap;
    [Tooltip("If > 0, overrides the price for all doors painted on this tilemap. If 0, uses the price from DoorTilePair.")]
    public int priceOverride;
    [Tooltip("The Area ID (e.g. 'Room2') unlocked when purchasing doors on this tilemap.")]
    public string areaToUnlock;
}

public class DoorTilemapManager : NetworkBehaviour
{
    public static DoorTilemapManager Instance;

    private static readonly Vector3Int[] Directions = { Vector3Int.up, Vector3Int.down, Vector3Int.left, Vector3Int.right };

    [Header("Tilemap References")]
    [SerializeField] private DoorTilemapConfig[] doorTilemaps;

    // This array replaces the single tile references!
    [SerializeField] private DoorTilePair[] doorTypes;

    [Header("Network Prefabs")]
    [SerializeField] private GameObject doorTriggerPrefab;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Duplicate DoorTilemapManager detected in scene! Destroying duplicate.");
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;
        ScanAndSpawnDoors();
    }

    private void ScanAndSpawnDoors()
    {
        if (doorTilemaps == null || doorTilemaps.Length == 0 || doorTypes == null || doorTypes.Length == 0) return;

        // Build O(1) lookup dictionary for closed door tiles
        Dictionary<TileBase, int> closedTileToTypeIndex = new Dictionary<TileBase, int>();
        for (int i = 0; i < doorTypes.Length; i++)
        {
            if (doorTypes[i].closedTile != null)
            {
                closedTileToTypeIndex[doorTypes[i].closedTile] = i;
            }
        }

        HashSet<Tilemap> processedTilemaps = new HashSet<Tilemap>();
        HashSet<Vector3Int> visitedTiles = new HashSet<Vector3Int>();

        for (int t = 0; t < doorTilemaps.Length; t++)
        {
            Tilemap tilemap = doorTilemaps[t].tilemap;
            if (tilemap == null || !processedTilemaps.Add(tilemap)) continue;

            int priceOverride = doorTilemaps[t].priceOverride;
            BoundsInt bounds = tilemap.cellBounds;
            TileBase[] allTiles = tilemap.GetTilesBlock(bounds);
            int width = bounds.size.x;
            int totalCells = allTiles.Length;

            for (int index = 0; index < totalCells; index++)
            {
                TileBase tile = allTiles[index];
                
                // Fast O(1) check: skip empty tiles and non-door tiles instantly
                if (tile == null || !closedTileToTypeIndex.TryGetValue(tile, out int doorTypeIndex)) continue;

                int x = index % width;
                int y = index / width;
                Vector3Int cellPos = new Vector3Int(bounds.xMin + x, bounds.yMin + y, 0);

                if (visitedTiles.Contains(cellPos)) continue;

                List<Vector3Int> doorGroup = GetConnectedTiles(tilemap, cellPos, visitedTiles, doorTypes[doorTypeIndex].closedTile);

                int finalPrice = (priceOverride > 0) ? priceOverride : doorTypes[doorTypeIndex].price;
                string areaFromTilemap = doorTilemaps[t].areaToUnlock;
                string finalAreaToUnlock = !string.IsNullOrEmpty(areaFromTilemap) ? areaFromTilemap : doorTypes[doorTypeIndex].areaToUnlock;

                SpawnDoorTrigger(tilemap, doorGroup, doorTypeIndex, t, finalPrice, finalAreaToUnlock);
            }
        }
    }

    private List<Vector3Int> GetConnectedTiles(Tilemap tilemap, Vector3Int startPos, HashSet<Vector3Int> visitedTiles, TileBase targetTile)
    {
        List<Vector3Int> group = new List<Vector3Int>();
        Queue<Vector3Int> queue = new Queue<Vector3Int>();

        queue.Enqueue(startPos);
        visitedTiles.Add(startPos);

        while (queue.Count > 0)
        {
            Vector3Int current = queue.Dequeue();
            group.Add(current);

            foreach (Vector3Int dir in Directions)
            {
                Vector3Int neighbor = current + dir;
                // Only connect if it is the EXACT same type of closed door tile
                if (!visitedTiles.Contains(neighbor) && tilemap.GetTile(neighbor) == targetTile)
                {
                    visitedTiles.Add(neighbor);
                    queue.Enqueue(neighbor);
                }
            }
        }
        return group;
    }

    private void SpawnDoorTrigger(Tilemap tilemap, List<Vector3Int> doorCells, int doorTypeIndex, int tilemapIndex, int doorPrice, string areaToUnlock = "")
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

        Vector3 minWorld = tilemap.GetCellCenterWorld(new Vector3Int(minX, minY, 0));
        Vector3 maxWorld = tilemap.GetCellCenterWorld(new Vector3Int(maxX, maxY, 0));
        Vector3 centerWorld = (minWorld + maxWorld) / 2f;

        GameObject doorObj = Instantiate(doorTriggerPrefab, centerWorld, Quaternion.identity);
        doorObj.GetComponent<NetworkObject>().Spawn();

        // Calculate size based on tiles
        float width = (maxX - minX + 1) * tilemap.cellSize.x;
        float height = (maxY - minY + 1) * tilemap.cellSize.y;

        BoxCollider2D col = doorObj.GetComponent<BoxCollider2D>();
        if (col != null)
        {
            col.isTrigger = true;
            // Expand trigger size slightly so players walking up to solid wall tiles touch the trigger
            col.size = new Vector2(width + 0.6f, height + 0.6f);
        }

        // --- Dynamic NavMesh Obstacle Sizing for Horizontal & Vertical Doors ---
        UnityEngine.AI.NavMeshObstacle obstacle = doorObj.GetComponent<UnityEngine.AI.NavMeshObstacle>();
        if (obstacle != null)
        {
            bool isHorizontal = (maxX - minX) >= (maxY - minY);
            float obstacleWidth, obstacleHeight;

            if (isHorizontal)
            {
                obstacleWidth = width;
                obstacleHeight = 0.4f;
            }
            else
            {
                obstacleWidth = 0.4f;
                obstacleHeight = height;
            }

            obstacle.size = new Vector3(obstacleWidth, obstacleHeight, 1f);
        }

        // Send the index of the door pair, tilemap index, price, and area identifier to the Door script
        doorObj.GetComponent<Door>().Initialize(doorCells.ToArray(), doorTypeIndex, tilemapIndex, doorPrice, areaToUnlock);
    }

    [Rpc(SendTo.ClientsAndHost)]
    public void OpenDoorRpc(Vector3Int[] cellPositions, int doorTypeIndex, int tilemapIndex)
    {
        if (doorTilemaps == null || tilemapIndex < 0 || tilemapIndex >= doorTilemaps.Length) return;
        Tilemap targetTilemap = doorTilemaps[tilemapIndex].tilemap;
        if (targetTilemap == null) return;

        TileBase openTile = doorTypes[doorTypeIndex].openTile;

        // Play 3D door open sound for all connected clients and host
        if (cellPositions != null && cellPositions.Length > 0 && FMODEvents.instance != null)
        {
            Vector3 soundPos = targetTilemap.GetCellCenterWorld(cellPositions[0]);
            Audio.playSFX(FMODEvents.instance.doorOpen, soundPos);
        }

        // Loop 1: Change visuals and clear colliders so the doorway becomes passable
        print("Opening door on tilemap: " + targetTilemap.name);
        foreach (Vector3Int pos in cellPositions)
        {
            targetTilemap.SetTile(pos, openTile);
            targetTilemap.SetColliderType(pos, Tile.ColliderType.None);
            targetTilemap.RefreshTile(pos);
        }

        // Force CompositeCollider2D to regenerate physics geometry immediately
        CompositeCollider2D compositeCol = targetTilemap.GetComponent<CompositeCollider2D>();
        if (compositeCol != null)
        {
            compositeCol.GenerateGeometry();
        }

        // Refresh 2D Shadow Caster
        UnityEngine.Rendering.Universal.ShadowCaster2D shadowCaster = targetTilemap.GetComponent<UnityEngine.Rendering.Universal.ShadowCaster2D>();
        if (shadowCaster == null) shadowCaster = GetComponent<UnityEngine.Rendering.Universal.ShadowCaster2D>();
        if (shadowCaster != null)
        {
            shadowCaster.enabled = false;
            shadowCaster.enabled = true;
        }
    }
}