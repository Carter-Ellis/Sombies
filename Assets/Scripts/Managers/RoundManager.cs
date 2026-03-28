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

    public void StartNextRound()
    {
        currentRound++;
        currentTimeBetweenSpawns = Mathf.Max(timeBetweenSpawnsMinimum, currentTimeBetweenSpawns - 0.5f);
        Debug.Log($"Starting Round {currentRound}");
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
        Enemy prefab = enemyPrefabs[Random.Range(0, enemyPrefabs.Length)];
        Transform spawnPos = spawnPoints[Random.Range(0, spawnPoints.Length)].transform;

        Enemy newEnemy = Instantiate(prefab, spawnPos.position, Quaternion.identity);
        newEnemy.SetManager(this);
        activeEnemies.Add(newEnemy);
    }

    private int CalculateEnemyCount()
    {
        // Round 1: 5 enemies. Every round adds 3 more.
        // Round 10 would be 5 + (9 * 3) = 32 enemies.
        return firstRoundEnemyCount + (currentRound - 1) * 3;
    }

    public void RemoveEnemy(Enemy deadEnemy)
    {

        if (activeEnemies.Contains(deadEnemy))
        {
            activeEnemies.Remove(deadEnemy);
        }

        enemiesRemainingToKill--;
            

        // Now we check if the round is over only when an enemy actually dies
        if (enemiesRemainingToKill <= 0)
        {
            StartNextRound();
        }
    }

}