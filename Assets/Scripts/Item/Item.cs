using UnityEngine;

public abstract class Item : MonoBehaviour
{
    [SerializeField] protected string _itemName;
    [SerializeField] protected string _itemDescription;
    protected bool _isUsed;
    public string ItemName => _itemName;
    public string ItemDescription => _itemDescription;
    public bool IsUsed
    {
        get => _isUsed;
        protected set => _isUsed = value;
    }

    public abstract void Use(Player player);
}
