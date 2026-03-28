using UnityEngine;
using UnityEngine.SceneManagement;

public class Entity : MonoBehaviour
{
    [Header("Base Entity Health")]
    [SerializeField] protected int _maxHealth = 100;
    [SerializeField] protected int _health;

    public virtual int MaxHealth
    {
        get => _maxHealth;
        set => _maxHealth = value;
    }

    public virtual int Health
    {
        get => _health;
        set
        {
            _health = Mathf.Clamp(value, 0, MaxHealth);
            if (_health <= 0)
            {
                Die();
            }
        }
    }

    [Header("Movement")]
    public virtual float BaseWalkSpeed { get; }
    public virtual float BaseSprintSpeed { get; }
    public virtual float WalkSpeed { get; set; }
    public virtual float SprintSpeed { get; set; }

    public BuffManager Buffs { get; private set; }

    protected virtual void Awake()
    {
        Buffs = GetComponent<BuffManager>();
    }

    public virtual void TakeDamage(int amount)
    {
        Health -= amount;
        Debug.Log($"{gameObject.name} took damage! Current health: {Health}");
    }

    public virtual void Heal(int amount)
    {
        Health += amount;
        Debug.Log($"{gameObject.name} healed! Current health: {Health}");
    }

    // Can be overriden
    public virtual void Die()
    {
        gameObject.SetActive(false);

        // This is temp 
        SceneManager.LoadScene("MainMenu");
    }
}
