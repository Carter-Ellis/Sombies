using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class RoundManager : MonoBehaviour
{
    // Simple states to keep the logic clean
    public enum RoundState { Waiting, Spawning, Fighting }

    [Header("Current Status")]
    public int currentRound = 0;
    public RoundState currentState = RoundState.Waiting;

    [Header("Spawn Settings")]
    [SerializeField] private Enemy[] enemyPrefabs;
    [SerializeField] private float timeBetweenSpawns = 0.5f;
    [SerializeField] private float delayBeforeNextRound = 5f;
    [SerializeField] private int firstRoundEnemyCount = 5;

    private List<Enemy> activeEnemies = new List<Enemy>();
    private EnemySpawnPoint[] spawnPoints;

    void Start()
    {
        spawnPoints = FindObjectsByType<EnemySpawnPoint>(FindObjectsInactive.Exclude);

        if (spawnPoints.Length == 0)
        {
            Debug.LogError("No EnemySpawnPoints found in the scene!");
            return;
        }

        StartNextRound();
    }

    void Update()
    {
        // We only care about checking for the end of the round if we are currently fighting
        if (currentState != RoundState.Fighting) return;

        // Clean the list of any null references (enemies that were destroyed)
        activeEnemies.RemoveAll(e => e == null);

        // If the list is empty, the round is over!
        if (activeEnemies.Count == 0)
        {
            StartNextRound();
        }
    }

    public void StartNextRound()
    {
        currentRound++;
        Debug.Log($"Starting Round {currentRound}");
        StartCoroutine(SpawnRoundRoutine());
    }

    private IEnumerator SpawnRoundRoutine()
    {
        currentState = RoundState.Waiting;

        // Brief pause so the player can breathe between rounds
        yield return new WaitForSeconds(delayBeforeNextRound);

        currentState = RoundState.Spawning;

        int enemiesToSpawn = CalculateEnemyCount();

        for (int i = 0; i < enemiesToSpawn; i++)
        {
            SpawnEnemy();
            yield return new WaitForSeconds(timeBetweenSpawns);
        }

        // All enemies are in the scene, now we switch to Fighting state
        currentState = RoundState.Fighting;
    }

    private void SpawnEnemy()
    {
        Enemy prefab = enemyPrefabs[Random.Range(0, enemyPrefabs.Length)];
        Transform spawnPos = spawnPoints[Random.Range(0, spawnPoints.Length)].transform;

        Enemy newEnemy = Instantiate(prefab, spawnPos.position, Quaternion.identity);
        activeEnemies.Add(newEnemy);
    }

    private int CalculateEnemyCount()
    {
        // Round 1: 5 enemies. Every round adds 3 more.
        // Round 10 would be 5 + (9 * 3) = 32 enemies.
        return firstRoundEnemyCount + (currentRound - 1) * 3;
    }
}