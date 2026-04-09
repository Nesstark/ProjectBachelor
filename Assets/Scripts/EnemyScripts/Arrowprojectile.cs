using UnityEngine;

// ============================================================
//  ArrowProjectile.cs
//  ── Prefab Setup ──────────────────────────────────────────
//   1. GameObject → 3D Object → Sphere
//   2. Remove MeshCollider
//   3. Add Rigidbody  →  Use Gravity: OFF, Freeze Rotation XYZ
//   4. Add SphereCollider  →  Is Trigger: ON
//   5. Attach this script
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
    private GameObject _spawner;   // the archer that fired this — ignored on collision

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
    /// <summary>
    /// Call immediately after Instantiate.
    /// Pass the Archer's GameObject so the projectile ignores it.
    /// </summary>
    public void Init(Vector3 direction, float damage, GameObject spawner)
    {
        _damage = damage;
        _spawner = spawner;

        Vector3 flatDir = new Vector3(direction.x, 0f, direction.z).normalized;
        _rb.linearVelocity = flatDir * speed;

        Debug.Log($"[Arrow] Launched — dir:{flatDir}  dmg:{damage}  ignoring:{spawner.name}");
        Destroy(gameObject, lifetime);
    }

    // ─────────────────────────────────────────────────────────
    private void OnTriggerEnter(Collider other)
    {
        if (_hasHit) return;

        // Always ignore the archer that fired this
        if (_spawner != null && other.gameObject == _spawner) return;
        if (_spawner != null && other.transform.IsChildOf(_spawner.transform)) return;

        // Ignore other projectiles
        if (other.GetComponent<ArrowProjectile>() != null) return;

        // Never destroy when passing through enemies or other archers
        if (other.CompareTag("Enemy")) return;

        if (other.CompareTag("Player"))
        {
            _hasHit = true;
            Debug.Log($"[Arrow] Hit Player — {_damage:F1} dmg");

            AudioManager.Instance?.Play("FireballHit");

            GameManager.Instance?.ApplyDamageToPlayer(_damage);
            Destroy(gameObject);
            return;
        }

        // Hit a wall or any solid non-trigger object — destroy
        if (!other.isTrigger)
        {
            _hasHit = true;
            Debug.Log($"[Arrow] Hit '{other.name}' — destroyed");
            Destroy(gameObject);
        }
    }
}