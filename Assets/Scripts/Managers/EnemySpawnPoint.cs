using UnityEngine;

public class EnemySpawnPoint : MonoBehaviour
{
    [Header("Spawn Point Settings")]
    [Tooltip("If true, enemies can spawn here from round 1. If false, this spawnpoint requires a door/area to be unlocked.")]
    [SerializeField] private bool activeByDefault = true;

    [Tooltip("Optional direct reference to the Door required to activate this spawn point.")]
    [SerializeField] private Door linkedDoor;

    [Tooltip("Optional zone/area identifier. Unlocking a door with matching areaId will activate this spawn point.")]
    [SerializeField] private string areaId = "";

    public bool IsActive { get; private set; }
    public Door LinkedDoor => linkedDoor;
    public string AreaId => areaId;

    private void Awake()
    {
        IsActive = activeByDefault;
    }

    public void SetActive(bool active)
    {
        IsActive = active;
    }

    private void OnDrawGizmos()
    {
        bool currentActiveState = Application.isPlaying ? IsActive : activeByDefault;
        
        if (currentActiveState)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, 0.5f);
        }
        else
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, 0.5f);
        }
    }
}
