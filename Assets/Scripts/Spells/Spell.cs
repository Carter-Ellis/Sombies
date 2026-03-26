using UnityEngine;

[CreateAssetMenu(fileName = "New Spell", menuName = "Spells/BaseSpell")]
public abstract class Spell : ScriptableObject
{
    [SerializeField] protected string _name;
    [SerializeField] protected string _description;

    public string Name => _name;
    public string Description => _description;

    public abstract void Cast(Player player);
}
