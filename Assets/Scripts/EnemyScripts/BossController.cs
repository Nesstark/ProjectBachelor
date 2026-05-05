using System.Collections;
using UnityEngine;

// ============================================================
// BossController.cs — Boss enemy type
// ── Behaviour ─────────────────────────────────────────────
// • Runs a randomised "moveset" of attack patterns:
//     Normal  — single aimed projectile
//     Burst   — N fast shots at the player
//     Circle  — 360° spray of projectiles
// • Melee charges when the player gets too close
// • Two independent timers — moveset and melee run simultaneously
// ============================================================

public class BossController : BaseEnemy
{
    protected override string EnemyTypeName => "Boss";

    // ─── Attack Pattern Enum ──────────────────────────────
    private enum AttackPattern { Normal, Burst, Circle }

    // ─── Ranged (shared) ─────────────────────────────────
    [Header("Ranged Attack")]
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private Transform firePoint;
    [SerializeField] private float maxShootRange = 20f;

    // ─── Normal Shot ──────────────────────────────────────
    [Header("Normal Shot")]
    [SerializeField] private float normalShootInterval = 0.8f;

    // ─── Burst Shot ───────────────────────────────────────
    [Header("Burst Shot")]
    [SerializeField] public int   burstShotCount       = 4;
    [SerializeField] public float burstInterval        = 0.15f;   // seconds between burst shots
    [SerializeField] public float burstCooldown        = 2f;      // wait before next pattern pick

    // ─── Circle Shot ──────────────────────────────────────
    [Header("Circle Shot")]
    [SerializeField] public int   circleShotsPerDegree = 1;       // shots per degree step (raises density)
    [SerializeField] public float circleDegreeStep     = 20f;     // degrees between each shot
    [SerializeField] public float circleCooldown       = 3f;      // wait before next pattern pick

    // ─── Melee ────────────────────────────────────────────
    [Header("Melee Attack")]
    [SerializeField] private float meleeDamageMultiplier = 2f;

    // ─── Movement ─────────────────────────────────────────
    [Header("Movement")]
    [SerializeField] private float chargeRange = 5f;
    [SerializeField] private float chargeSpeed = 8f;

    // ─── Moveset weights (must sum to 100) ────────────────
    [Header("Moveset Weights (0–100, must sum to 100)")]
    [SerializeField] [Range(0, 100)] private int weightNormal = 60;
    [SerializeField] [Range(0, 100)] private int weightBurst  = 25;
    [SerializeField] [Range(0, 100)] private int weightCircle = 15;

    // ─── Private ──────────────────────────────────────────
    private bool _attackRoutineRunning;

    private static readonly int HashShoot  = Animator.StringToHash("Shoot");
    private static readonly int HashCharge = Animator.StringToHash("Charge");

    // ─────────────────────────────────────────────────────
    protected override void Awake()
    {
        base.Awake();
        if (firePoint == null) firePoint = transform;
    }

    protected override void Start()
    {
        base.Start();
        StartCoroutine(AttackMovesetRoutine());
    }

    // ─── Update ───────────────────────────────────────────
    protected override void Update()
    {
        if (IsDead || PlayerTransform == null) return;

        HandleMovement();
        TickAttackCycle();   // melee — inherited timer
        UpdateAnimator();
        UpdateSpriteFlip();
    }

    // ─── Movement — charge when close, otherwise approach ─
    protected override void HandleMovement()
    {
        float dist = Vector3.Distance(transform.position, PlayerTransform.position);

        if (dist <= chargeRange)
        {
            Agent.speed = chargeSpeed;
            if (animator != null) animator.SetBool(HashCharge, true);
        }
        else
        {
            Agent.speed = Stats.Speed;
            if (animator != null) animator.SetBool(HashCharge, false);
        }

        Agent.SetDestination(PlayerTransform.position);
    }

    // ─── Melee — hits harder than base ────────────────────
    protected override void TryAttack()
    {
        float dist = Vector3.Distance(transform.position, PlayerTransform.position);
        if (dist > meleeRange) return;

        float meleeDmg = Stats.Damage * meleeDamageMultiplier;
        Debug.Log($"[Boss] MELEE — {meleeDmg:F1} dmg");
        GM?.ApplyDamageToPlayer(meleeDmg);
        if (animator != null) animator.SetTrigger(HashAttack);
    }

    // ─── Moveset Coroutine ────────────────────────────────
    // Picks a weighted random pattern, executes it, then repeats.
    private IEnumerator AttackMovesetRoutine()
    {
        // Small startup delay so the boss doesn't fire on frame 1
        yield return new WaitForSeconds(0.5f);

        while (!IsDead)
        {
            if (PlayerTransform == null) { yield return null; continue; }

            AttackPattern pattern = PickPattern();

            switch (pattern)
            {
                case AttackPattern.Normal:
                    Debug.Log("[Boss] MOVESET → Normal Shot");
                    ShootProjectileAtPlayer();
                    yield return new WaitForSeconds(normalShootInterval);
                    break;

                case AttackPattern.Burst:
                    Debug.Log("[Boss] MOVESET → Burst Shot");
                    yield return StartCoroutine(DoBurst());
                    yield return new WaitForSeconds(burstCooldown);
                    break;

                case AttackPattern.Circle:
                    Debug.Log("[Boss] MOVESET → 360° Shot");
                    DoCircleShot();
                    yield return new WaitForSeconds(circleCooldown);
                    break;
            }
        }
    }

    // ─── Weighted random pattern picker ───────────────────
    private AttackPattern PickPattern()
    {
        int total = weightNormal + weightBurst + weightCircle;
        if (total <= 0) return AttackPattern.Normal; // safe fallback

        int roll = Random.Range(0, total);

        if (roll < weightNormal)                         return AttackPattern.Normal;
        if (roll < weightNormal + weightBurst)           return AttackPattern.Burst;
        return AttackPattern.Circle;
    }

    // ─── Normal: single aimed shot ────────────────────────
    private void ShootProjectileAtPlayer()
    {
        if (!ValidatePrefab()) return;
        if (animator != null) animator.SetTrigger(HashShoot);

        Vector3 dir = AimAtPlayer();
        SpawnProjectile(dir, Stats.Damage);
        Debug.Log($"[Boss] SHOT (Normal) — DMG:{Stats.Damage:F1}");
    }

    // ─── Burst: N fast shots aimed at the player ──────────
    private IEnumerator DoBurst()
    {
        if (!ValidatePrefab()) yield break;

        for (int i = 0; i < burstShotCount; i++)
        {
            if (IsDead || PlayerTransform == null) yield break;

            if (animator != null) animator.SetTrigger(HashShoot);

            Vector3 dir = AimAtPlayer();
            SpawnProjectile(dir, Stats.Damage);
            Debug.Log($"[Boss] SHOT (Burst {i + 1}/{burstShotCount}) — DMG:{Stats.Damage:F1}");

            yield return new WaitForSeconds(burstInterval);
        }
    }

    // ─── Circle: 360° ring of projectiles ─────────────────
    private void DoCircleShot()
    {
        if (!ValidatePrefab()) return;
        if (animator != null) animator.SetTrigger(HashShoot);

        // circleShotsPerDegree controls density.
        // e.g. step=20°, shotsPerDegree=1 → 18 projectiles
        //      step=20°, shotsPerDegree=2 → 36 projectiles (two per position)
        int shotsFired = 0;
        for (float angle = 0f; angle < 360f; angle += circleDegreeStep)
        {
            for (int s = 0; s < Mathf.Max(1, circleShotsPerDegree); s++)
            {
                // Offset repeated shots slightly so they don't stack exactly
                float offsetAngle = angle + (s * (circleDegreeStep / Mathf.Max(1, circleShotsPerDegree)));
                float rad = offsetAngle * Mathf.Deg2Rad;
                Vector3 dir = new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad));

                SpawnProjectile(dir, Stats.Damage);
                shotsFired++;
            }
        }

        Debug.Log($"[Boss] SHOT (Circle) — {shotsFired} projectiles fired");
    }

    // ─── Helpers ──────────────────────────────────────────
    private Vector3 AimAtPlayer()
    {
        Vector3 dir = PlayerTransform.position - firePoint.position;
        dir.y = 0f;
        return dir.normalized;
    }

    private void SpawnProjectile(Vector3 direction, float damage)
    {
        GameObject proj = Instantiate(projectilePrefab, firePoint.position, Quaternion.identity);
        ArrowProjectile arrow = proj.GetComponent<ArrowProjectile>();
        if (arrow != null)
            arrow.Init(direction, damage, gameObject);
    }

    private bool ValidatePrefab()
    {
        if (projectilePrefab != null) return true;
        Debug.LogWarning("[Boss] No projectile prefab assigned!");
        return false;
    }

    // ─── Gizmos ───────────────────────────────────────────
    protected override void OnDrawGizmosSelected()
    {
        base.OnDrawGizmosSelected();

        // Charge range — orange
        Gizmos.color = new Color(1f, 0.4f, 0f);
        Gizmos.DrawWireSphere(transform.position, chargeRange);

        // Max shoot range — red
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, maxShootRange);
    }
}