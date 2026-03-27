using UnityEngine;

public class EntityStats : MonoBehaviour
{
    [Header("Movement")]
    public float baseSpeed = 5f;
    public float currentSpeed;

    [Header("Health")]
    public int maxHealth = 100;
    public int currentHealth;

    private void Awake()
    {
        currentSpeed = baseSpeed;
        currentHealth = maxHealth;
    }
}
