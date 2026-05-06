using System.Collections;
using UnityEngine;

// ============================================================
// BossController.cs — Boss enemy type
// ── Behaviour ─────────────────────────────────────────────
// • Runs a randomised "moveset" of attack patterns:
//     Normal  — single aimed projectile
//     Burst   — N fast shots at the player
//     Circle  — 360° spray (with ! warning ping)
// • Each attack has its own projectile speed variable
// • Melee charges when the player gets too close
// ============================================================

public class BossController : BaseEnemy
{
    protected override string EnemyTypeName => "Boss";

    private enum AttackPattern { Normal, Burst, Circle }

    // ─── Ranged (shared) ─────────────────────────────────
    [Header("Ranged Attack")]
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private Transform  firePoint;
    [SerializeField] private float      maxShootRange = 20f;

    // ─── Normal Shot ──────────────────────────────────────
    [Header("Normal Shot")]
    [Tooltip("Seconds between each normal shot")]
    [SerializeField] private float normalShootInterval   = 0.8f;
    [Tooltip("Speed of the normal shot projectile")]
    [SerializeField] public  float normalProjectileSpeed = 10f;

    // ─── Burst Shot ───────────────────────────────────────
    [Header("Burst Shot")]
    [SerializeField] public int   burstShotCount       = 4;
    [SerializeField] public float burstInterval        = 0.15f;
    [Tooltip("Speed of each burst projectile")]
    [SerializeField] public float burstProjectileSpeed = 10f;
    [Tooltip("Cooldown after the burst finishes before the next pattern is picked")]
    [SerializeField] public float burstCooldown        = 2f;

    // ─── Circle Shot ──────────────────────────────────────
    [Header("Circle Shot")]
    [SerializeField] public int   circleShotsPerDegree  = 1;
    [SerializeField] public float circleDegreeStep      = 20f;
    [Tooltip("Speed of each circle projectile — lower = slower ring, easier to dodge outward")]
    [SerializeField] public float circleProjectileSpeed = 5f;
    [Tooltip("Cooldown after the circle fires before the next pattern is picked")]
    [SerializeField] public float circleCooldown        = 3f;

    [Header("Circle Shot Warning Ping")]
    [Tooltip("Same ! ping prefab used on the Rogue — assign the same asset here")]
    [SerializeField] private GameObject exclamationPingPrefab;
    [Tooltip("Offset above the Boss's transform where the ping spawns")]
    [SerializeField] private Vector3    pingOffset         = new Vector3(0f, 2.2f, 0f);
    [Tooltip("Uniform scale multiplier applied to the ping — 1 = prefab default, 2 = double size")]
    [SerializeField] private float      pingScale          = 1f;
    [Tooltip("How long the ! ping is shown before the circle fires")]
    [SerializeField] private float      circlePingDuration = 0.8f;

    // ─── Melee ────────────────────────────────────────────
    [Header("Melee Attack")]
    [SerializeField] private float meleeDamageMultiplier = 2f;

    // ─── Movement ─────────────────────────────────────────
    [Header("Movement")]
    [SerializeField] private float chargeRange = 5f;
    [SerializeField] private float chargeSpeed = 8f;

    // ─── Moveset Weights ──────────────────────────────────
    [Header("Moveset Weights (any ratio works)")]
    [SerializeField] [Range(0, 100)] private int weightNormal = 60;
    [SerializeField] [Range(0, 100)] private int weightBurst  = 25;
    [SerializeField] [Range(0, 100)] private int weightCircle = 15;

    // ─── Private ──────────────────────────────────────────
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

    protected override void Update()
    {
        if (IsDead || PlayerTransform == null) return;

        HandleMovement();
        TickAttackCycle();
        UpdateAnimator();
        UpdateSpriteFlip();
    }

    // ─── Movement ─────────────────────────────────────────
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

    // ─── Melee ────────────────────────────────────────────
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
    private IEnumerator AttackMovesetRoutine()
    {
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
                    yield return StartCoroutine(DoCircleShot());
                    yield return new WaitForSeconds(circleCooldown);
                    break;
            }
        }
    }

    // ─── Weighted random pattern picker ───────────────────
    private AttackPattern PickPattern()
    {
        int total = weightNormal + weightBurst + weightCircle;
        if (total <= 0) return AttackPattern.Normal;

        int roll = Random.Range(0, total);

        if (roll < weightNormal)               return AttackPattern.Normal;
        if (roll < weightNormal + weightBurst) return AttackPattern.Burst;
        return AttackPattern.Circle;
    }

    // ─── Normal: single aimed shot ────────────────────────
    private void ShootProjectileAtPlayer()
    {
        if (!ValidatePrefab()) return;
        if (animator != null) animator.SetTrigger(HashShoot);

        AudioManager.Instance.Play("EnemyFireball");
        SpawnProjectile(AimAtPlayer(), Stats.Damage, normalProjectileSpeed);
        Debug.Log($"[Boss] SHOT (Normal) — DMG:{Stats.Damage:F1}  SPD:{normalProjectileSpeed}");
    }

    // ─── Burst: N fast shots aimed at the player ──────────
    private IEnumerator DoBurst()
    {
        if (!ValidatePrefab()) yield break;

        for (int i = 0; i < burstShotCount; i++)
        {
            if (IsDead || PlayerTransform == null) yield break;

            if (animator != null) animator.SetTrigger(HashShoot);

            AudioManager.Instance.Play("EnemyFireball");
            SpawnProjectile(AimAtPlayer(), Stats.Damage, burstProjectileSpeed);
            Debug.Log($"[Boss] SHOT (Burst {i + 1}/{burstShotCount}) — DMG:{Stats.Damage:F1}  SPD:{burstProjectileSpeed}");

            yield return new WaitForSeconds(burstInterval);
        }
    }

    // ─── Circle: ! ping → 360° ring ───────────────────────
    private IEnumerator DoCircleShot()
    {
        // ── Step 1: Spawn and scale the ! ping ─────────────
        GameObject ping = null;
        if (exclamationPingPrefab != null)
        {
            ping = Instantiate(exclamationPingPrefab, transform.position + pingOffset, Quaternion.identity);
            ping.transform.SetParent(transform, worldPositionStays: true);
            ping.transform.localScale = Vector3.one * pingScale;

            AudioManager.Instance.Play("BossAlert");

            Debug.Log("[Boss] Circle ping spawned!");
        }
        else
        {
            Debug.LogWarning("[Boss] exclamationPingPrefab is NULL — assign it in the Inspector!");
        }

        // ── Step 2: Hold while ping is visible ─────────────
        yield return new WaitForSeconds(circlePingDuration);
        if (ping != null) Destroy(ping);

        // ── Step 3: Fire the ring ──────────────────────────
        if (!ValidatePrefab()) yield break;
        if (animator != null) animator.SetTrigger(HashShoot);

        int shotsFired = 0;
        for (float angle = 0f; angle < 360f; angle += circleDegreeStep)
        {
            AudioManager.Instance.Play("EnemyFireball");
            
            for (int s = 0; s < Mathf.Max(1, circleShotsPerDegree); s++)
            {
                float offsetAngle = angle + (s * (circleDegreeStep / Mathf.Max(1, circleShotsPerDegree)));
                float rad         = offsetAngle * Mathf.Deg2Rad;
                Vector3 dir       = new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad));

                SpawnProjectile(dir, Stats.Damage, circleProjectileSpeed);
                shotsFired++;
            }
        }

        Debug.Log($"[Boss] SHOT (Circle) — {shotsFired} projectiles  SPD:{circleProjectileSpeed}");
    }

    // ─── Helpers ──────────────────────────────────────────
    private Vector3 AimAtPlayer()
    {
        Vector3 dir = PlayerTransform.position - firePoint.position;
        dir.y = 0f;
        return dir.normalized;
    }

    private void SpawnProjectile(Vector3 direction, float damage, float speed)
    {
        GameObject proj       = Instantiate(projectilePrefab, firePoint.position, Quaternion.identity);
        ArrowProjectile arrow = proj.GetComponent<ArrowProjectile>();
        if (arrow != null)
            arrow.Init(direction, damage, gameObject, speed);
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

        Gizmos.color = new Color(1f, 0.4f, 0f);
        Gizmos.DrawWireSphere(transform.position, chargeRange);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, maxShootRange);
    }
}