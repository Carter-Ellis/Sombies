using UnityEngine;
using UnityEngine.UI;

[CreateAssetMenu(fileName = "New Spell", menuName = "Spells/BaseSpell")]
public abstract class Spell : ScriptableObject
{
    [SerializeField] protected string _name;
    [SerializeField] protected string _description;
    [SerializeField] protected int _manaCost;
    [SerializeField] protected int damage;
    public Sprite sprite;
    public int spellID;
    public string Name => _name;
    public string Description => _description;
    public int ManaCost => _manaCost;
    public abstract void Cast(Entity entity);
}
