
using UnityEngine;

public class SpellPurchase : PurchaseSystem
{
    [Header("Floating Animation Settings")]
    [SerializeField] private bool enableFloating = true;
    [SerializeField] private float floatDistance = 0.15f;
    [SerializeField] private float floatSpeed = 2.5f;

    private Vector3 _initialPosition;
    private bool _isInitialPositionSet = false;

    private void Start()
    {
        SetInitialPosition();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        SetInitialPosition();
    }

    private void OnEnable()
    {
        SetInitialPosition();
    }

    private void SetInitialPosition()
    {
        _initialPosition = transform.position;
        _isInitialPositionSet = true;
    }

    private void Update()
    {
        if (!enableFloating) return;

        if (!_isInitialPositionSet)
        {
            SetInitialPosition();
        }

        float newY = _initialPosition.y + Mathf.Sin(Time.time * floatSpeed) * floatDistance;
        transform.position = new Vector3(_initialPosition.x, newY, _initialPosition.z);
    }

    protected override void GrantPurchase(Entity buyer)
    {
        Player player = buyer.GetComponent<Player>();
        if (player == null) return;

        player.AddSpell(spell);
        player._netActiveSpellID.Value = spell.spellID;
    }
}
