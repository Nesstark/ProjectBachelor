using UnityEngine;

// ============================================================
//  PickupBase.cs — Abstract base for all pickups
//  Attach to a trigger collider GameObject.
//  Subclasses only implement OnPickedUp().
// ============================================================
[RequireComponent(typeof(Collider))]
public abstract class PickupBase : MonoBehaviour
{
    [Header("Pickup Settings")]
    [SerializeField] private float bobSpeed    = 2f;
    [SerializeField] private float bobHeight   = 0.2f;
    [SerializeField] private float spinSpeed   = 90f;
    [SerializeField] private GameObject collectVFXPrefab;

    private Vector3 _startPos;

    private void Awake()
    {
        GetComponent<Collider>().isTrigger = true;
        _startPos = transform.position;
    }

    private void Update()
    {
        // Bob up and down
        Vector3 pos = _startPos;
        pos.y += Mathf.Sin(Time.time * bobSpeed) * bobHeight;
        transform.position = pos;

        // Spin
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

        Destroy(gameObject);
    }

    protected abstract void OnPickedUp(GameObject player);
}