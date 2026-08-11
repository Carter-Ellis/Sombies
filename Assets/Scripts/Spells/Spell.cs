using UnityEngine;
using UnityEngine.UI;

[CreateAssetMenu(fileName = "New Spell", menuName = "Spells/BaseSpell")]
public abstract class Spell : ScriptableObject
{
    [SerializeField] protected string _name;
    [SerializeField] protected string _description;
    [SerializeField] protected int _manaCost;
    [SerializeField] protected int damage;
    [SerializeField] protected float cooldown = 0.5f;
    [SerializeField] protected bool isChargeSpell = false;

    public Sprite sprite;
    public int spellID;
    public string Name => _name;
    public string Description => _description;
    public int ManaCost => _manaCost;
    public int Damage => damage;
    public float Cooldown => cooldown;
    public bool IsChargeSpell => isChargeSpell;

    public abstract void Cast(Entity entity);
    public virtual void Cast(Entity entity, float chargeTime)
    {
        Cast(entity);
    }
}
