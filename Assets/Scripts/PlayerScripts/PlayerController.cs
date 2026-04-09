using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed     = 10f;
    [SerializeField] private float acceleration  = 80f;
    [SerializeField] private float deceleration  = 120f;
    [SerializeField] private float dashSpeed     = 18f;
    [SerializeField] private float dashDuration  = 0.15f;
    [SerializeField] private float dashCooldown  = 0.6f;

    [Header("Attack")]
    [SerializeField] private float     attackCooldown = 0.4f;
    [SerializeField] private float attackAngle = 90f;
    [SerializeField] private LayerMask enemyLayer;
    [SerializeField] private Transform attackOrigin;

    [Header("Visual")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Animator       animator;

    [Header("Hit VFX")]
    [SerializeField] private GameObject hitVFXPrefab;
    [SerializeField] private GameObject slashVFXPrefab;

    [Header("Death Animation")]
    [SerializeField] private float deathFadeDuration = 0.8f;

    // ─── Private State ────────────────────────────────────────
    private Rigidbody       rb;
    private Vector2         inputDir;
    private Vector3         moveDir;
    private bool            isDashing;
    private float           dashTimer;
    private float           dashCooldownTimer;
    private Vector3         lastMoveDir;
    private float           attackTimer;
    private bool            isDead;
    private float           _lastKnownHp = float.MaxValue;
    private HitFlashHandler _hitFlash;

    // ─── Exposed for HUD ─────────────────────────────────────
    // 1.0 = dash fully ready, 0.0 = just used / recharging
    public float DashReadyFraction => dashCooldown > 0f
        ? Mathf.Clamp01(1f - Mathf.Max(0f, dashCooldownTimer) / dashCooldown)
        : 1f;

    private static readonly int HashSpeed     = Animator.StringToHash("Speed");
    private static readonly int HashDirX      = Animator.StringToHash("DirX");
    private static readonly int HashDirZ      = Animator.StringToHash("DirZ");
    private static readonly int HashDash      = Animator.StringToHash("Dash");
    private static readonly int HashAttack    = Animator.StringToHash("attack");
    private static readonly int HashIsWalking = Animator.StringToHash("isWalking");
    private static readonly int HashFlipX     = Animator.StringToHash("FlipX");
    private static readonly int HashHit       = Animator.StringToHash("Hit");
    private static readonly int HashDeath     = Animator.StringToHash("Death");

    private GameManager GM => GameManager.Instance;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        lastMoveDir = Vector3.forward;
        if (attackOrigin == null) attackOrigin = transform;
        if (animator == null) animator = GetComponentInChildren<Animator>();
        rb.constraints   = RigidbodyConstraints.FreezeRotation;
        rb.linearDamping = 0f;
        _hitFlash = GetComponentInChildren<HitFlashHandler>();
    }

    private void Start()
    {
        if (GM != null)
        {
            _lastKnownHp = GM.Player.CurrentHealth;
            GM.OnPlayerDied.AddListener(HandlePlayerDied);
            GM.OnPlayerHealthChanged.AddListener(HandlePlayerHit);
        }
        else
        {
            Debug.LogError("[Player] GameManager not found!");
        }

        if (enemyLayer.value == 0)
            Debug.LogWarning("[Player] enemyLayer not set — will hit ALL layers as fallback.");
    }

    private void OnDestroy()
    {
        GM?.OnPlayerDied.RemoveListener(HandlePlayerDied);
        GM?.OnPlayerHealthChanged.RemoveListener(HandlePlayerHit);
    }

    public void OnMove(InputValue value)
    {
        if (!isDead) inputDir = value.Get<Vector2>();
    }

    public void OnDash(InputValue value)
    {
        if (isDead) return;
        if (value.isPressed && !isDashing && dashCooldownTimer <= 0f)
            StartDash();
    }

    public void OnAttack(InputValue value)
    {
        if (isDead) return;
        if (value.Get<float>() < 0.5f) return;
        if (attackTimer > 0f) return;
        attackTimer = attackCooldown;
        PerformAttack();
    }

    private void Update()
    {
        if (isDead) return;

        dashTimer         -= Time.deltaTime;
        dashCooldownTimer -= Time.deltaTime;
        attackTimer       -= Time.deltaTime;

        if (isDashing && dashTimer <= 0f) isDashing = false;

        moveDir = new Vector3(inputDir.x, 0f, inputDir.y).normalized;
        if (moveDir.magnitude > 0.1f) lastMoveDir = moveDir;

        UpdateAnimator();
        UpdateSpriteFlip();

        CameraShakeManager.Instance?.SetRunningShake(rb.linearVelocity.magnitude, moveSpeed);
    }

    private void FixedUpdate()
    {
        if (isDead) return;

        if (isDashing)
        {
            rb.linearVelocity = lastMoveDir * dashSpeed;
            return;
        }

        Vector3 targetVelocity    = moveDir * moveSpeed;
        Vector3 currentHorizontal = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        float   accel             = moveDir.magnitude > 0.1f ? acceleration : deceleration;
        Vector3 newHorizontal     = Vector3.MoveTowards(currentHorizontal, targetVelocity, accel * Time.fixedDeltaTime);
        rb.linearVelocity = new Vector3(newHorizontal.x, rb.linearVelocity.y, newHorizontal.z);
    }

    private void PerformAttack()
    {
        float range  = GM != null ? GM.AttackRange   : 3f;
        float damage = GM != null ? GM.Player.Damage : 20f;
        int   mask   = enemyLayer.value != 0 ? enemyLayer.value : ~0;

        if (animator != null) animator.SetTrigger(HashAttack);

        AudioManager.Instance.Play("PlayerAttack");

        if (slashVFXPrefab != null)
        {
            float yAngle = Mathf.Atan2(lastMoveDir.x, lastMoveDir.z) * Mathf.Rad2Deg;
            Quaternion slashRot = Quaternion.Euler(0f, yAngle, 0f);
            GameObject slash = Instantiate(slashVFXPrefab, attackOrigin.position, slashRot, attackOrigin);
            Destroy(slash, 0.5f);
        }

        Collider[] hits = Physics.OverlapSphere(attackOrigin.position, range, mask);
        if (hits.Length == 0) return;

        Collider closest  = null;
        float    bestDist = Mathf.Infinity;

        foreach (Collider hit in hits)
        {
            if (hit.gameObject == gameObject) continue;

            Vector3 toEnemy = hit.transform.position - transform.position;
            toEnemy.y = 0f;

            if (lastMoveDir.magnitude > 0.1f)
            {
                float dot = Vector3.Dot(lastMoveDir.normalized, toEnemy.normalized);
                if (dot < Mathf.Cos(attackAngle * 0.5f * Mathf.Deg2Rad)) continue;
            }

            float dist = toEnemy.magnitude;
            if (dist < bestDist) { bestDist = dist; closest = hit; }
        }

        if (closest == null) return;

        BaseEnemy enemy = closest.GetComponentInParent<BaseEnemy>();
        if (enemy != null)
        {
            enemy.TakeDamage(damage);
            CameraShakeManager.Instance?.ShakeImpulse(CameraShakeManager.Instance.hitShakeForce);
            Debug.Log($"[Player] HIT '{closest.name}' for {damage:F1}");
            return;
        }

        Debug.LogWarning($"[Player] '{closest.name}' has no BaseEnemy component!");
    }

    private void HandlePlayerHit(float currentHp, float maxHp)
    {
        if (isDead) return;

        bool wasDamaged = currentHp < _lastKnownHp;
        _lastKnownHp    = currentHp;
        if (!wasDamaged) return;

        if (animator != null) animator.SetTrigger(HashHit);
        CameraShakeManager.Instance?.ShakeImpulse(CameraShakeManager.Instance.hitShakeForce);
        _hitFlash?.Flash();

        AudioManager.Instance.Play("PlayerHit");

        if (hitVFXPrefab != null)
        {
            Vector3 towardCam = Camera.main != null
                ? (Camera.main.transform.position - transform.position).normalized
                : Vector3.up;
            Vector3    vfxPos = transform.position + towardCam * 0.5f;
            GameObject vfx    = Instantiate(hitVFXPrefab, vfxPos, Quaternion.identity);
            Destroy(vfx, 2f);
        }
    }

    private void HandlePlayerDied()
    {
        if (isDead) return;
        isDead            = true;
        inputDir          = Vector2.zero;
        rb.linearVelocity = Vector3.zero;
        Debug.Log("[Player] Died — playing death sequence.");
        StartCoroutine(DeathSequence());
    }

    private IEnumerator DeathSequence()
    {
        if (animator != null) animator.SetTrigger(HashDeath);

        float   elapsed    = 0f;
        Vector3 startScale = spriteRenderer != null
            ? spriteRenderer.transform.localScale
            : Vector3.one;

        while (elapsed < deathFadeDuration)
        {
            elapsed += Time.deltaTime;
            float t  = elapsed / deathFadeDuration;

            if (spriteRenderer != null)
            {
                spriteRenderer.color                = new Color(1f, 1f - t, 1f - t, 1f - t);
                spriteRenderer.transform.localScale = Vector3.Lerp(startScale, Vector3.zero, t);
            }

            yield return null;
        }

        CutoutObject cutout = FindFirstObjectByType<CutoutObject>();
        if (cutout != null) cutout.enabled = false;

        Destroy(gameObject);
    }

    public void OnFootstep()
    {
        if (moveDir.magnitude < 0.1f) return; // don't play if sliding to a stop
        AudioManager.Instance.Play("Footstep");
    }

    private void StartDash()
    {
        isDashing         = true;
        dashTimer         = dashDuration;
        dashCooldownTimer = dashCooldown;
        if (animator != null) animator.SetTrigger(HashDash);
        CameraShakeManager.Instance?.ShakeImpulse(CameraShakeManager.Instance.dashShakeForce);
    }

    private void UpdateAnimator()
    {
        if (animator == null) return;
        animator.SetFloat(HashSpeed,    moveDir.magnitude);
        animator.SetFloat(HashDirX,     lastMoveDir.x);
        animator.SetFloat(HashDirZ,     lastMoveDir.z);
        animator.SetBool(HashIsWalking, moveDir.magnitude > 0.1f);
        animator.SetBool(HashFlipX,     spriteRenderer != null && spriteRenderer.flipX);
    }

    private void UpdateSpriteFlip()
    {
        if (spriteRenderer == null) return;
        if      (lastMoveDir.x >  0.1f) spriteRenderer.flipX = true;
        else if (lastMoveDir.x < -0.1f) spriteRenderer.flipX = false;
    }

    private void OnDrawGizmosSelected()
    {
        float range = Application.isPlaying && GM != null ? GM.AttackRange : 3f;
        Vector3 origin = attackOrigin != null ? attackOrigin.position : transform.position;

        // Sphere showing max range
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(origin, range);

        // Cone edges showing the angle window
        Vector3 forward = Application.isPlaying ? lastMoveDir : transform.forward;
        forward.y = 0f;
        if (forward.magnitude > 0.01f)
        {
            forward.Normalize();
            float halfAngle = attackAngle * 0.5f;
            Vector3 leftEdge  = Quaternion.Euler(0f, -halfAngle, 0f) * forward;
            Vector3 rightEdge = Quaternion.Euler(0f,  halfAngle, 0f) * forward;

            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(origin, origin + leftEdge  * range);
            Gizmos.DrawLine(origin, origin + rightEdge * range);
        }
    }
}