using UnityEngine;

// ============================================================
//  ArrowProjectile.cs  —  Rigidbody version
//
//  ── Prefab Setup ──────────────────────────────────────────
//   1. GameObject → 3D Object → Sphere
//   2. Remove the MeshCollider Unity adds automatically
//   3. Add Rigidbody
//        • Use Gravity      : OFF
//        • Is Kinematic     : OFF
//        • Interpolate      : Interpolate
//        • Freeze Rotation  : X Y Z all ON
//   4. Add SphereCollider
//        • Is Trigger       : ON
//        • Radius           : 0.25
//   5. Attach this script
//   6. Save as prefab → assign to ArcherController
// ============================================================

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(SphereCollider))]
public class ArrowProjectile : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private float speed = 12f;
    [SerializeField] private float lifetime = 4f;

    private Rigidbody _rb;
    private float _damage;
    private bool _hasHit;

    // ─────────────────────────────────────────────────────────
    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _rb.useGravity = false;
        _rb.isKinematic = false;
        _rb.constraints = RigidbodyConstraints.FreezeRotationX
                        | RigidbodyConstraints.FreezeRotationY
                        | RigidbodyConstraints.FreezeRotationZ;

        GetComponent<SphereCollider>().isTrigger = true;
    }

    // ─────────────────────────────────────────────────────────
    /// <summary>Called by ArcherController immediately after Instantiate.</summary>
    public void Init(Vector3 direction, float damage)
    {
        _damage = damage;

        Vector3 flatDir = new Vector3(direction.x, 0f, direction.z).normalized;
        _rb.linearVelocity = flatDir * speed;

        Debug.Log($"[Arrow] Launched — dir:{flatDir}  speed:{speed}  dmg:{damage}");
        Destroy(gameObject, lifetime);
    }

    // ─────────────────────────────────────────────────────────
    private void OnTriggerEnter(Collider other)
    {
        if (_hasHit) return;
        if (other.GetComponent<ArrowProjectile>() != null) return;

        if (other.CompareTag("Player"))
        {
            _hasHit = true;
            Debug.Log($"[Arrow] Hit Player — {_damage:F1} dmg");
            GameManager.Instance?.ApplyDamageToPlayer(_damage);
            Destroy(gameObject);
            return;
        }

        if (!other.CompareTag("Enemy") && !other.CompareTag("Archer"))
        {
            _hasHit = true;
            Debug.Log($"[Arrow] Hit '{other.name}' — destroyed");
            Destroy(gameObject);
        }
    }
}