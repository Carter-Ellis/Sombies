using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;

public class RoundManager : NetworkBehaviour
{
    public static RoundManager Instance { get; private set; }

    // Simple states to keep the logic clean
    public enum RoundState { Waiting, Spawning, Fighting }

    [Header("Current Status")]
    public NetworkVariable<int> _netRound = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public RoundState currentState = RoundState.Fighting;
    public NetworkVariable<int> _netEnemiesRemaining = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    [Header("Spawn Settings")]
    [SerializeField] private Enemy[] enemyPrefabs;
    [SerializeField] private float timeBetweenSpawnsMinimum = 0.5f;
    [SerializeField] private float currentTimeBetweenSpawns = 10.5f;
    [SerializeField] private float delayBeforeNextRound = 5f;
    [SerializeField] private int firstRoundEnemyCount = 5;

    [SerializeField] private int maxActiveEnemies = 3;

    [Header("Player Respawn Settings")]
    [SerializeField] private Transform playerSpawnPointsParent;

    private List<Enemy> activeEnemies = new List<Enemy>();
    private EnemySpawnPoint[] spawnPoints;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        _netRound.OnValueChanged += OnRoundChanged;

        _netEnemiesRemaining.OnValueChanged += OnEnemiesChanged;

        // Only the server starts the round logic
        if (IsServer)
        {
            spawnPoints = FindObjectsByType<EnemySpawnPoint>(FindObjectsInactive.Include);

            if (spawnPoints.Length == 0)
            {
                Debug.LogError("No EnemySpawnPoints found!");
                return;
            }

            StartNextRound();
        }
    }

    public override void OnNetworkDespawn()
    {
        _netRound.OnValueChanged -= OnRoundChanged;
        _netEnemiesRemaining.OnValueChanged -= OnEnemiesChanged;

        if (IsServer)
        {
            foreach (Enemy enemy in activeEnemies)
            {
                if (enemy != null && enemy.TryGetComponent(out NetworkObject netObj) && netObj.IsSpawned)
                {
                    netObj.Despawn(true);
                }
            }
            activeEnemies.Clear();
        }

    }

    public void UnlockDoor(Door door, string areaId = "")
    {
        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            spawnPoints = FindObjectsByType<EnemySpawnPoint>(FindObjectsInactive.Include);
        }

        foreach (EnemySpawnPoint sp in spawnPoints)
        {
            if (sp == null) continue;

            // Direct door reference match
            if (sp.LinkedDoor != null && sp.LinkedDoor == door)
            {
                sp.SetActive(true);
            }
            // Area identifier match
            else if (!string.IsNullOrEmpty(areaId) && sp.AreaId == areaId)
            {
                sp.SetActive(true);
            }
            else if (door != null && !string.IsNullOrEmpty(door.AreaToUnlock) && sp.AreaId == door.AreaToUnlock)
            {
                sp.SetActive(true);
            }
        }
    }

    public void UnlockArea(string areaId)
    {
        if (string.IsNullOrEmpty(areaId)) return;

        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            spawnPoints = FindObjectsByType<EnemySpawnPoint>(FindObjectsInactive.Include);
        }

        foreach (EnemySpawnPoint sp in spawnPoints)
        {
            if (sp != null && sp.AreaId == areaId)
            {
                sp.SetActive(true);
            }
        }
    }

    private List<EnemySpawnPoint> GetActiveSpawnPoints()
    {
        List<EnemySpawnPoint> activeList = new List<EnemySpawnPoint>();
        if (spawnPoints != null)
        {
            foreach (EnemySpawnPoint sp in spawnPoints)
            {
                if (sp != null && sp.IsActive)
                {
                    activeList.Add(sp);
                }
            }
        }
        return activeList;
    }

    private void SpawnEnemy()
    {
        if (!IsServer) return;

        List<EnemySpawnPoint> activeSpawnPoints = GetActiveSpawnPoints();
        if (activeSpawnPoints.Count == 0)
        {
            Debug.LogWarning("No active EnemySpawnPoints available! Falling back to all spawn points.");
            if (spawnPoints != null)
            {
                activeSpawnPoints.AddRange(spawnPoints);
            }
        }

        if (activeSpawnPoints.Count == 0)
        {
            Debug.LogError("No EnemySpawnPoints found in scene!");
            return;
        }

        Enemy prefab = enemyPrefabs[Random.Range(0, enemyPrefabs.Length)];
        Transform spawnPos = activeSpawnPoints[Random.Range(0, activeSpawnPoints.Count)].transform;

        Enemy newEnemy = Instantiate(prefab, spawnPos.position, Quaternion.identity);

        NetworkObject netObj = newEnemy.GetComponent<NetworkObject>();
        if (netObj != null)
        {
            netObj.Spawn(); // This tells all clients to create this enemy
        }

        newEnemy.SetManager(this);
        activeEnemies.Add(newEnemy);
    }

    private void OnRoundChanged(int oldVal, int newVal)
    {
        // This runs on EVERYONE whenever the server changes netRound.Value
        if (UIManager.Instance != null)
        {
            UIManager.Instance.UpdateRound(newVal);
        }
    }

    private void OnEnemiesChanged(int oldVal, int newVal)
    {
        if (UIManager.Instance != null)
        {
            UIManager.Instance.UpdateEnemyCount(newVal);
        }
    }

    public void StartNextRound()
    {
        if (!IsServer) return;

        if (currentState == RoundState.Waiting || currentState == RoundState.Spawning) return;

        _netRound.Value++;
        currentTimeBetweenSpawns = Mathf.Max(timeBetweenSpawnsMinimum, currentTimeBetweenSpawns - 0.5f);

        RespawnDeadPlayersServer();

        StartCoroutine(SpawnRoundRoutine());
    }

    public List<Vector3> GetSpawnPositionsFromParent()
    {
        List<Vector3> positions = new List<Vector3>();

        if (playerSpawnPointsParent == null)
        {
            GameObject findParent = GameObject.Find("Player Spawnpoints");
            if (findParent == null) findParent = GameObject.Find("PlayerSpawnPoints");
            if (findParent == null) findParent = GameObject.Find("playerspawnpoints");
            if (findParent != null) playerSpawnPointsParent = findParent.transform;
        }

        if (playerSpawnPointsParent != null)
        {
            for (int i = 0; i < playerSpawnPointsParent.childCount; i++)
            {
                Transform child = playerSpawnPointsParent.GetChild(i);
                if (child != null && child != playerSpawnPointsParent)
                {
                    positions.Add(child.position);
                }
            }
        }

        return positions;
    }

    public Vector3 GetRandomPlayerSpawnPosition()
    {
        List<Vector3> list = GetSpawnPositionsFromParent();
        if (list.Count > 0)
        {
            return list[Random.Range(0, list.Count)];
        }
        return Vector3.zero;
    }

    private void RespawnDeadPlayersServer()
    {
        if (!IsServer) return;

        List<Vector3> availableSpawnPositions = GetSpawnPositionsFromParent();

        // Shuffle spawn positions so spawning is randomized but distinct for each player
        for (int i = 0; i < availableSpawnPositions.Count; i++)
        {
            int randomIndex = Random.Range(i, availableSpawnPositions.Count);
            Vector3 temp = availableSpawnPositions[i];
            availableSpawnPositions[i] = availableSpawnPositions[randomIndex];
            availableSpawnPositions[randomIndex] = temp;
        }

        int spawnIndex = 0;

        foreach (var client in NetworkManager.Singleton.ConnectedClients.Values)
        {
            if (client.PlayerObject != null && client.PlayerObject.TryGetComponent<Player>(out var player))
            {
                ReviveController rc = client.PlayerObject.GetComponent<ReviveController>();
                if (rc != null && (rc.IsDeadSync.Value || rc.IsDownedSync.Value))
                {
                    Vector3 spawnPos = Vector3.zero;
                    if (availableSpawnPositions.Count > 0)
                    {
                        spawnPos = availableSpawnPositions[spawnIndex % availableSpawnPositions.Count];
                        spawnIndex++;
                    }
                    else
                    {
                        spawnPos = client.PlayerObject.transform.position;
                    }

                    player.RespawnForNextRoundServer(spawnPos);
                }
            }
        }
    }

    private IEnumerator SpawnRoundRoutine()
    {
        currentState = RoundState.Waiting;

        // Brief pause so the player can breathe between rounds
        yield return new WaitForSeconds(delayBeforeNextRound);

        
        int totalEnemiesToSpawn = CalculateEnemyCount();
        _netEnemiesRemaining.Value = totalEnemiesToSpawn;

        currentState = RoundState.Spawning;

        for (int i = 0; i < totalEnemiesToSpawn; i++)
        {
            yield return new WaitUntil(() => activeEnemies.Count < maxActiveEnemies);

            SpawnEnemy();
            if (i == totalEnemiesToSpawn - 1)
            {
                currentState = RoundState.Fighting;
            }
            else
            {
                yield return new WaitForSeconds(currentTimeBetweenSpawns);
            }
        }

        // All enemies are in the scene, now we switch to Fighting state
        currentState = RoundState.Fighting;
    }



    private int CalculateEnemyCount()
    {
        // Round 1: 5 enemies. Every round adds 3 more.
        // Round 10 would be 5 + (9 * 3) = 32 enemies.
        return firstRoundEnemyCount + (_netRound.Value - 1) * 3;
    }

    public void RemoveEnemy(Enemy deadEnemy)
    {
        if (!IsServer) return;

        if (activeEnemies.Contains(deadEnemy))
        {
            activeEnemies.Remove(deadEnemy);


            _netEnemiesRemaining.Value--;

            // Now we check if the round is over only when an enemy actually dies
            if (_netEnemiesRemaining.Value <= 0)
            {
                StartNextRound();
            }
        }
    }

}