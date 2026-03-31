using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "SpellDatabase", menuName = "Spells/Database")]
public class SpellDatabase : ScriptableObject
{
    public List<Spell> allSpells;

    private static SpellDatabase _instance;
    public static SpellDatabase Instance
    {
        get
        {
            if (_instance == null)
            {
                // Make sure your Database asset is in a folder named "Resources"
                _instance = Resources.Load<SpellDatabase>("SpellDatabase");
            }
            return _instance;
        }
    }

    public Spell GetSpellByID(int id)
    {
        return allSpells.Find(s => s.spellID == id);
    }
}