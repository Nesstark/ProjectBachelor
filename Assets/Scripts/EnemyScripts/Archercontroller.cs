using UnityEngine;

// ============================================================
//  ArcherController.cs  —  Archer enemy type
//  Inherits all shared logic from BaseEnemy.
//  Only defines what is UNIQUE: flee movement, ranged attack.
// ============================================================

public class ArcherController : BaseEnemy
{
    protected override string EnemyTypeName => "Archer";

    [Header("Ranged Attack")]
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private Transform firePoint;
    [SerializeField] private float preferredRange = 8f;
    [SerializeField] private float maxShootRange = 12f;

    [Header("Flee")]
    [SerializeField] private float fleeRange = 4f;
    [SerializeField] private float fleeSpeed = 6f;

    private static readonly int HashFlee = Animator.StringToHash("Flee");

    // ─────────────────────────────────────────────────────────
    protected override void Awake()
    {
        base.Awake();
        if (firePoint == null) firePoint = transform;
    }

    // ─── Movement — flee or reposition ───────────────────────
    protected override void HandleMovement()
    {
        float dist = Vector3.Distance(transform.position, PlayerTransform.position);

        if (dist < fleeRange)
        {
            // Too close — run away
            Agent.speed = fleeSpeed;
            Vector3 fleeDir = (transform.position - PlayerTransform.position).normalized;
            Vector3 fleeTarget = transform.position + fleeDir * preferredRange;
            Agent.SetDestination(fleeTarget);
            if (animator != null) animator.SetBool(HashFlee, true);
        }
        else if (dist > preferredRange)
        {
            // Too far — move closer
            Agent.speed = Stats.Speed;
            Agent.SetDestination(PlayerTransform.position);
            if (animator != null) animator.SetBool(HashFlee, false);
        }
        else
        {
            // At preferred range — stop and face player
            Agent.speed = Stats.Speed;
            Agent.ResetPath();
            FacePlayer();
            if (animator != null) animator.SetBool(HashFlee, false);
        }
    }

    // ─── Attack — override base melee with ranged shot ───────
    protected override void TryAttack()
    {
        float dist = Vector3.Distance(transform.position, PlayerTransform.position);

        bool inShootRange = dist <= maxShootRange;
        bool notTooClose = dist >= fleeRange;

        if (inShootRange && notTooClose)
            ShootProjectile();
    }

    // ─────────────────────────────────────────────────────────
    private void ShootProjectile()
    {
        if (projectilePrefab == null)
        {
            Debug.LogWarning("[Archer] No projectile prefab assigned!");
            return;
        }

        if (animator != null) animator.SetTrigger(HashAttack);

        Vector3 dir = (PlayerTransform.position - firePoint.position);
        dir.y = 0f;
        dir.Normalize();

        GameObject proj = Instantiate(projectilePrefab, firePoint.position, Quaternion.identity);
        ArrowProjectile arrow = proj.GetComponent<ArrowProjectile>();
        if (arrow != null)
            arrow.Init(dir, Stats.Damage, gameObject);

        Debug.Log($"[Archer] Fired projectile — DMG:{Stats.Damage:F1}");
    }

    // ─────────────────────────────────────────────────────────
    private void FacePlayer()
    {
        Vector3 dir = (PlayerTransform.position - transform.position);
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.LookRotation(dir);
    }

    protected override void OnDrawGizmosSelected()
    {
        base.OnDrawGizmosSelected();

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, preferredRange);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, maxShootRange);
    }
}