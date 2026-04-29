using UnityEngine;

// ============================================================
// PickupBase.cs — Abstract base for all pickups
// MODIFIED: Added OnAnyPickupCollected event for the Treasure
// Room system. Card content lives on PickupCardUI instead.
// ============================================================
[RequireComponent(typeof(Collider))]
public abstract class PickupBase : MonoBehaviour
{
    [Header("Pickup Settings")]
    [SerializeField] private float bobSpeed = 2f;
    [SerializeField] private float bobHeight = 0.2f;
    [SerializeField] private float spinSpeed = 90f;
    [SerializeField] private GameObject collectVFXPrefab;

    // TreasureRoomController subscribes here to know when a choice is made.
    // Static event — no manual wiring between the room and the pickup needed.
    public static event System.Action<PickupBase> OnAnyPickupCollected;

    private Vector3 _startPos;

    private void Awake()
    {
        GetComponent<Collider>().isTrigger = true;
        _startPos = transform.position;
    }

    private void Update()
    {
        Vector3 pos = _startPos;
        pos.y += Mathf.Sin(Time.time * bobSpeed) * bobHeight;
        transform.position = pos;
        transform.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        OnPickedUp(other.gameObject);

        if (collectVFXPrefab != null)
        {
            GameObject vfx = Instantiate(collectVFXPrefab, transform.position, Quaternion.identity);
            Destroy(vfx, 2f);
        }

        // Notify listeners (e.g. TreasureRoomController) before destroying
        OnAnyPickupCollected?.Invoke(this);
        Destroy(gameObject);
    }

    protected abstract void OnPickedUp(GameObject player);
}