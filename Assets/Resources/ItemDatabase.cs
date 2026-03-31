using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ItemDatabase", menuName = "Items/Database")]
public class ItemDatabase : ScriptableObject
{
    public List<Item> allItems;

    private static ItemDatabase _instance;
    public static ItemDatabase Instance
    {
        get
        {
            if (_instance == null)
            {
                // Must be in a folder named "Resources"
                _instance = Resources.Load<ItemDatabase>("ItemDatabase");

                if (_instance == null)
                    Debug.LogError("ItemDatabase asset not found in Resources folder!");
            }
            return _instance;
        }
    }

    public Item GetItemByID(int id)
    {
        return allItems.Find(item => item.itemID == id);
    }
}