using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;

public class RoundManager : NetworkBehaviour
{
    // Simple states to keep the logic clean
    public enum RoundState { Waiting, Spawning, Fighting }

    [Header("Current Status")]
    public NetworkVariable<int> _netRound = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public RoundState currentState = RoundState.Fighting;
    private int enemiesRemainingToKill;

    [Header("Spawn Settings")]
    [SerializeField] private Enemy[] enemyPrefabs;
    [SerializeField] private float timeBetweenSpawnsMinimum = 0.5f;
    [SerializeField] private float currentTimeBetweenSpawns = 10.5f;
    [SerializeField] private float delayBeforeNextRound = 5f;
    [SerializeField] private int firstRoundEnemyCount = 5;

    [SerializeField] private int maxActiveEnemies = 3;

    private List<Enemy> activeEnemies = new List<Enemy>();
    private EnemySpawnPoint[] spawnPoints;

    public override void OnNetworkSpawn()
    {
        _netRound.OnValueChanged += OnRoundChanged;

        // Only the server starts the round logic
        if (IsServer)
        {
            spawnPoints = FindObjectsByType<EnemySpawnPoint>(FindObjectsInactive.Exclude);

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
    }

    private void OnRoundChanged(int oldVal, int newVal)
    {
        // This runs on EVERYONE whenever the server changes netRound.Value
        if (UIManager.Instance != null)
        {
            UIManager.Instance.UpdateRound(newVal);
        }
    }

    public void StartNextRound()
    {
        if (!IsServer) return;

        if (currentState == RoundState.Waiting || currentState == RoundState.Spawning) return;

        _netRound.Value++;
        currentTimeBetweenSpawns = Mathf.Max(timeBetweenSpawnsMinimum, currentTimeBetweenSpawns - 0.5f);

        StartCoroutine(SpawnRoundRoutine());
    }

    private IEnumerator SpawnRoundRoutine()
    {
        currentState = RoundState.Waiting;

        // Brief pause so the player can breathe between rounds
        yield return new WaitForSeconds(delayBeforeNextRound);

        
        int totalEnemiesToSpawn = CalculateEnemyCount();
        enemiesRemainingToKill = totalEnemiesToSpawn;

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

    private void SpawnEnemy()
    {
        if (!IsServer) return;

        Enemy prefab = enemyPrefabs[Random.Range(0, enemyPrefabs.Length)];
        Transform spawnPos = spawnPoints[Random.Range(0, spawnPoints.Length)].transform;

        Enemy newEnemy = Instantiate(prefab, spawnPos.position, Quaternion.identity);

        NetworkObject netObj = newEnemy.GetComponent<NetworkObject>();
        if (netObj != null)
        {
            netObj.Spawn(); // This tells all clients to create this enemy
        }

        newEnemy.SetManager(this);
        activeEnemies.Add(newEnemy);
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


            enemiesRemainingToKill--;


            // Now we check if the round is over only when an enemy actually dies
            if (enemiesRemainingToKill <= 0)
            {
                StartNextRound();
            }
        }
    }

}