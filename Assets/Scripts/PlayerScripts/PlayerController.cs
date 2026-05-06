using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{
    // ─── Inspector Fields ─────────────────────────────────────

    [Header("Movement")]
    [SerializeField] private float baseMoveSpeed  = 10f;
    [SerializeField] private float maxMoveSpeed   = 20f;
    [SerializeField] private float acceleration   = 80f;
    [SerializeField] private float deceleration   = 120f;
    [SerializeField] private float dashSpeed      = 18f;
    [SerializeField] private float dashDuration   = 0.15f;
    [SerializeField] private float dashCooldown   = 0.6f;

    [Header("Attack")]
    [SerializeField] private float     attackCooldown  = 0.4f;
    [SerializeField] private float     attackAngle     = 90f;
    [SerializeField] private float     baseAttackRange = 3f;
    [SerializeField] private float     maxAttackRange  = 8f;
    [SerializeField] private LayerMask enemyLayer;
    [SerializeField] private Transform attackOrigin;

    [Header("Aim Cone Visual")]
    [SerializeField] private int   aimConeSegments = 32;   // arc smoothness
    [SerializeField] private float aimConeYOffset   = 0.05f; // lifts mesh off ground

    [Header("Aim Cone – Idle (Mauve)")]
    [SerializeField] private Color coneIdleFill = new Color(0.608f, 0.420f, 0.478f, 0.15f);
    [SerializeField] private Color coneIdleEdge = new Color(0.608f, 0.420f, 0.478f, 0.65f);

    [Header("Aim Cone – Active / Aiming (Sickly Gold)")]
    [SerializeField] private Color coneActiveFill = new Color(0.784f, 0.588f, 0.165f, 0.18f);
    [SerializeField] private Color coneActiveEdge = new Color(0.784f, 0.588f, 0.165f, 0.70f);

    [Header("Aim Cone – Attack Flash (Blood Red)")]
    [SerializeField] private Color coneAttackFill = new Color(0.784f, 0.196f, 0.169f, 0.25f);
    [SerializeField] private Color coneAttackEdge = new Color(0.784f, 0.196f, 0.169f, 0.90f);
    [SerializeField] private float attackFlashDuration = 0.18f; // how long the red flash lasts
    [SerializeField] private float coneColorLerpSpeed  = 8f;    // transition smoothing speed

    // How long the aim must be stationary before cone returns to Idle.
    // Keeps the cone Active for a short moment after the player stops moving/aiming.
    [SerializeField] private float aimActiveLingerTime = 0.35f;

    [Header("Visual")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Animator       animator;

    [Header("Hit VFX")]
    [SerializeField] private GameObject hitVFXPrefab;
    [SerializeField] private GameObject slashVFXPrefab;

    [Header("Death Animation")]
    [SerializeField] private float deathFadeDuration = 0.8f;

    // ─── Private State ────────────────────────────────────────

    private Rigidbody rb;
    private Vector2   inputDir;
    private Vector3   moveDir;
    private float     moveSpeed;
    private bool      isDashing;
    private float     dashTimer;
    private float     dashCooldownTimer;
    private Vector3   lastMoveDir;
    private float     attackTimer;
    private float     _attackRange;
    private bool      isDead;
    private float     _lastKnownHp = float.MaxValue;
    private HitFlashHandler _hitFlash;
    private bool      _lastInputWasGamepad = false;
    private Vector2   _lastMousePosition   = Vector2.negativeInfinity;

    // ─── Aim State ────────────────────────────────────────────
    // aimDir is purely aim-driven; lastMoveDir remains movement-only.
    private Vector3 aimDir;
    public  Vector3 AimDir => aimDir;

    // ─── Cone Visual ──────────────────────────────────────────
    private GameObject   _coneGO;
    private MeshFilter   _coneMeshFilter;
    private MeshRenderer _coneMeshRenderer;
    private LineRenderer _coneEdgeRenderer;
    private Mesh         _coneMesh;

    // ─── Cone Colour State ────────────────────────────────────
    // Three states: Idle, Active, Attack
    // "Active" triggers when the player moves OR when the aim direction changes (both M+KB and gamepad).
    private enum ConeState { Idle, Active, Attack }
    private ConeState _coneState          = ConeState.Idle;
    private Color     _currentConeFill;
    private Color     _currentConeEdge;
    private float     _attackFlashTimer   = 0f;  // counts down after an attack
    private float     _aimLingerTimer     = 0f;  // counts down after aim/movement stops
    private Vector3   _lastAimDir;               // used to detect aim movement on gamepad

    // ─── Exposed for HUD ──────────────────────────────────────
    public float DashReadyFraction => dashCooldown > 0f
        ? Mathf.Clamp01(1f - Mathf.Max(0f, dashCooldownTimer) / dashCooldown)
        : 1f;

    public float CurrentMoveSpeed   => moveSpeed;
    public float CurrentAttackRange => _attackRange;

    public void SetMoveSpeed(float value)   => moveSpeed    = value;
    public void SetAttackRange(float value) => _attackRange = value;

    // ─── Animator Hashes ─────────────────────────────────────
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

    // ─── Unity Lifecycle ──────────────────────────────────────

    private void Awake()
    {
        rb           = GetComponent<Rigidbody>();
        lastMoveDir  = Vector3.forward;
        aimDir       = Vector3.forward;
        _lastAimDir  = Vector3.forward;

        if (attackOrigin == null) attackOrigin = transform;
        if (animator == null)    animator = GetComponentInChildren<Animator>();

        rb.constraints  = RigidbodyConstraints.FreezeRotation;
        rb.linearDamping = 0f;
        _hitFlash       = GetComponentInChildren<HitFlashHandler>();
        moveSpeed       = baseMoveSpeed;
        _attackRange    = baseAttackRange;

        BuildConeMeshObject();

        // Initialise colour to Idle
        _currentConeFill = coneIdleFill;
        _currentConeEdge = coneIdleEdge;
    }

    private IEnumerator Start()
    {
        // Wait one frame so RoomManager.Start() has time to call RestoreFromSave()
        yield return null;

        if (GM != null)
        {
            _lastKnownHp = GM.Player.CurrentHealth;  // now reads the restored value
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

    // ─── Input Callbacks ──────────────────────────────────────

    public void OnMove(InputValue value)
    {
        if (InGameSettings.Instance != null && InGameSettings.Instance.IsOpen) return;
        if (!isDead) inputDir = value.Get<Vector2>();
    }

    public void OnLook(InputValue value)
    {
        if (isDead) return;
        if (InGameSettings.Instance != null && InGameSettings.Instance.IsOpen) return;

        if (Gamepad.current != null && Gamepad.current.rightStick.IsActuated())
        {
            Vector2 stick = value.Get<Vector2>();
            if (stick.sqrMagnitude > 0.1f)
            {
                aimDir = new Vector3(stick.x, 0f, stick.y).normalized;
                _lastInputWasGamepad = true;
            }
            // Stick released — aimDir holds its last value; linger timer handles colour fade
        }
    }

    public void OnDash(InputValue value)
    {
        if (isDead) return;
        if (InGameSettings.Instance != null && InGameSettings.Instance.IsOpen) return;
        if (value.isPressed && !isDashing && dashCooldownTimer <= 0f)
            StartDash();
    }

    public void OnAttack(InputValue value)
    {
        if (isDead) return;
        if (InGameSettings.Instance != null && InGameSettings.Instance.IsOpen) return;
        if (value.Get<float>() < 0.5f) return;
        if (attackTimer > 0f) return;

        attackTimer       = attackCooldown;
        _attackFlashTimer = attackFlashDuration; // trigger Attack colour flash
        PerformAttack();
    }

    // ─── Update / FixedUpdate ─────────────────────────────────

    private void Update()
    {
        if (isDead)
        {
            SetConeVisible(false);
            return;
        }

        dashTimer         -= Time.deltaTime;
        dashCooldownTimer -= Time.deltaTime;
        attackTimer       -= Time.deltaTime;
        _attackFlashTimer -= Time.deltaTime;

        if (isDashing && dashTimer <= 0f) isDashing = false;

        // Clamp to length 1 rather than normalizing so analogue sticks
        // produce variable speed, while WASD still reaches full speed.
        Vector3 rawDir = new Vector3(inputDir.x, 0f, inputDir.y);
        moveDir = Vector3.ClampMagnitude(rawDir, 1f);
        if (moveDir.magnitude > 0.1f) lastMoveDir = moveDir;

        UpdateAimDir();
        UpdateAnimator();
        UpdateSpriteFlip();
        UpdateConeMesh();
        UpdateConeColor();  // ← colour state machine

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
        rb.linearVelocity         = new Vector3(newHorizontal.x, rb.linearVelocity.y, newHorizontal.z);
    }

    // ─── Player Aim ───────────────────────────────────────────

    private void UpdateAimDir()
    {
        // Gamepad stick takes priority while it's being pushed
        if (Gamepad.current != null && Gamepad.current.rightStick.IsActuated()) return;

        if (Camera.main == null || Mouse.current == null) return;
        Vector2 mouseScreen = Mouse.current.position.ReadValue();

        // Only hand control back to mouse if it has physically moved
        if (_lastInputWasGamepad)
        {
            if (mouseScreen == _lastMousePosition) return;
            _lastInputWasGamepad = false;
        }

        _lastMousePosition = mouseScreen;

        Ray ray   = Camera.main.ScreenPointToRay(mouseScreen);
        var plane = new Plane(Vector3.up, new Vector3(0f, transform.position.y, 0f));

        if (plane.Raycast(ray, out float dist))
        {
            Vector3 toMouse = ray.GetPoint(dist) - transform.position;
            toMouse.y = 0f;
            if (toMouse.sqrMagnitude > 0.01f)
                aimDir = toMouse.normalized;
        }
    }

    // ─── Combat ───────────────────────────────────────────────

    private void PerformAttack()
    {
        float range  = _attackRange;
        float damage = GM != null ? GM.Player.Damage : 20f;
        int   mask   = enemyLayer.value != 0 ? enemyLayer.value : ~0;

        if (animator != null) animator.SetTrigger(HashAttack);
        AudioManager.Instance?.Play("PlayerAttack");

        if (slashVFXPrefab != null)
        {
            float      yAngle   = Mathf.Atan2(aimDir.x, aimDir.z) * Mathf.Rad2Deg;
            Quaternion slashRot = Quaternion.Euler(0f, yAngle, 0f);
            GameObject slash    = Instantiate(slashVFXPrefab, attackOrigin.position, slashRot, attackOrigin);
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

            // Use aimDir (mouse/stick-driven) instead of lastMoveDir
            float dot = Vector3.Dot(aimDir, toEnemy.normalized);
            if (dot < Mathf.Cos(attackAngle * 0.5f * Mathf.Deg2Rad)) continue;

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

    // ─── Event Handlers ───────────────────────────────────────

    private void HandlePlayerHit(float currentHp, float maxHp)
    {
        if (isDead) return;

        bool wasDamaged = currentHp < _lastKnownHp;
        _lastKnownHp = currentHp;
        if (!wasDamaged) return;

        if (animator != null) animator.SetTrigger(HashHit);
        CameraShakeManager.Instance?.ShakeImpulse(CameraShakeManager.Instance.hitShakeForce);
        _hitFlash?.Flash();
        AudioManager.Instance?.Play("PlayerHit");

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
        isDead = true;
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

    // ─── Animation Events ─────────────────────────────────────

    public void OnFootstep()
    {
        if (moveDir.magnitude < 0.1f) return;
        AudioManager.Instance?.Play("Footstep");
    }

    // ─── Private Helpers ──────────────────────────────────────

    private void StartDash()
    {
        isDashing      = true;
        dashTimer      = dashDuration;
        dashCooldownTimer = dashCooldown;
        if (animator != null) animator.SetTrigger(HashDash);
        CameraShakeManager.Instance?.ShakeImpulse(CameraShakeManager.Instance.dashShakeForce);
    }

    private void UpdateAnimator()
    {
        if (animator == null) return;
        animator.SetFloat(HashSpeed,     moveDir.magnitude);
        animator.SetFloat(HashDirX,      lastMoveDir.x);
        animator.SetFloat(HashDirZ,      lastMoveDir.z);
        animator.SetBool(HashIsWalking,  moveDir.magnitude > 0.1f);
        animator.SetBool(HashFlipX,      spriteRenderer != null && spriteRenderer.flipX);
    }

    private void UpdateSpriteFlip()
    {
        if (spriteRenderer == null) return;
        // Flip based on aim direction so the sprite faces the mouse/stick
        if      (aimDir.x >  0.1f) spriteRenderer.flipX = true;
        else if (aimDir.x < -0.1f) spriteRenderer.flipX = false;
    }

    // ─── Cone Colour State Machine ────────────────────────────

    /// <summary>
    /// Determines the cone colour state each frame and smoothly lerps the
    /// material colours toward the target. Works identically for M+KB and gamepad:
    ///
    ///   Attack  — Player just pressed attack (lasts attackFlashDuration seconds).
    ///   Active  — Player is moving OR aim direction changed this frame.
    ///             Also lingers for aimActiveLingerTime after movement/aim stops.
    ///   Idle    — Player is completely still and not recently aiming.
    /// </summary>
    private void UpdateConeColor()
    {
        // ── Detect aim movement (works for both M+KB and gamepad) ──
        // For M+KB: aimDir is updated every frame by UpdateAimDir() from the mouse.
        // For gamepad: aimDir is updated in OnLook() when the stick is actuated.
        // Either way, comparing aimDir to last frame's value catches all motion.
        bool aimMoved   = Vector3.Angle(aimDir, _lastAimDir) > 0.5f;
        bool isMoving   = moveDir.magnitude > 0.1f;
        bool isActive   = isMoving || aimMoved;

        _lastAimDir = aimDir;

        // Reset linger timer whenever the player is actively doing something
        if (isActive)
            _aimLingerTimer = aimActiveLingerTime;
        else
            _aimLingerTimer -= Time.deltaTime;

        // ── Resolve state priority: Attack > Active > Idle ──
        if (_attackFlashTimer > 0f)
            _coneState = ConeState.Attack;
        else if (_aimLingerTimer > 0f)
            _coneState = ConeState.Active;
        else
            _coneState = ConeState.Idle;

        // ── Pick target colours ──
        Color targetFill, targetEdge;
        switch (_coneState)
        {
            case ConeState.Attack:
                targetFill = coneAttackFill;
                targetEdge = coneAttackEdge;
                break;
            case ConeState.Active:
                targetFill = coneActiveFill;
                targetEdge = coneActiveEdge;
                break;
            default: // Idle
                targetFill = coneIdleFill;
                targetEdge = coneIdleEdge;
                break;
        }

        // ── Smooth lerp toward target ──
        _currentConeFill = Color.Lerp(_currentConeFill, targetFill, Time.deltaTime * coneColorLerpSpeed);
        _currentConeEdge = Color.Lerp(_currentConeEdge, targetEdge, Time.deltaTime * coneColorLerpSpeed);

        _coneMeshRenderer.material.color = _currentConeFill;
        _coneEdgeRenderer.material.color = _currentConeEdge;
    }

    // ─── Cone Mesh ────────────────────────────────────────────

    /// <summary>
    /// Creates the child GameObject that holds the procedural cone mesh
    /// and an edge LineRenderer. Called once in Awake.
    /// </summary>
    private void BuildConeMeshObject()
    {
        _coneGO = new GameObject("AimConeVisual");
        _coneGO.transform.SetParent(transform, false);
        _coneGO.layer = gameObject.layer;

        // ── Filled fan mesh ───────────────────────────────────
        _coneMeshFilter   = _coneGO.AddComponent<MeshFilter>();
        _coneMeshRenderer = _coneGO.AddComponent<MeshRenderer>();
        _coneMesh         = new Mesh { name = "AimConeMesh" };
        _coneMeshFilter.mesh = _coneMesh;

        // Unlit transparent material — starts at Idle colour
        Material mat = new Material(Shader.Find("Sprites/Default"))
        {
            color        = coneIdleFill,
            renderQueue  = 3000
        };
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.EnableKeyword("_ALPHABLEND_ON");
        _coneMeshRenderer.material            = mat;
        _coneMeshRenderer.shadowCastingMode   = UnityEngine.Rendering.ShadowCastingMode.Off;
        _coneMeshRenderer.receiveShadows      = false;

        // ── Edge LineRenderer ─────────────────────────────────
        _coneEdgeRenderer                     = _coneGO.AddComponent<LineRenderer>();
        _coneEdgeRenderer.useWorldSpace       = false;
        _coneEdgeRenderer.loop                = false;
        _coneEdgeRenderer.startWidth          = 0.06f;
        _coneEdgeRenderer.endWidth            = 0.06f;
        _coneEdgeRenderer.positionCount       = 0;
        _coneEdgeRenderer.shadowCastingMode   = UnityEngine.Rendering.ShadowCastingMode.Off;

        Material edgeMat = new Material(Shader.Find("Sprites/Default"))
        {
            color = coneIdleEdge   // starts at Idle colour
        };
        _coneEdgeRenderer.material = edgeMat;
    }

    /// <summary>
    /// Rebuilds the cone mesh every frame to match the current aimDir and _attackRange.
    /// The mesh lives in world-space Y = player.y + aimConeYOffset.
    /// </summary>
    private void UpdateConeMesh()
    {
        // Keep the child object at a fixed world-space offset so it lies flat
        Vector3 origin = attackOrigin.position;
        origin.y = transform.position.y + aimConeYOffset;
        _coneGO.transform.position = origin;
        _coneGO.transform.rotation = Quaternion.identity; // mesh built in world-space directions

        int   segments = Mathf.Max(3, aimConeSegments);
        float range    = _attackRange;
        float halfDeg  = attackAngle * 0.5f;

        // ── Filled mesh ──────────────────────────────────────
        // vertices: [0] = origin, [1..segments+1] = arc points
        int       vCount = segments + 2;
        Vector3[] verts  = new Vector3[vCount];
        int[]     tris   = new int[segments * 3];

        verts[0] = Vector3.zero; // local origin

        for (int i = 0; i <= segments; i++)
        {
            float   t        = (float)i / segments;              // 0 → 1
            float   angleDeg = -halfDeg + t * attackAngle;       // -half → +half
            Vector3 dir      = Quaternion.Euler(0f, angleDeg, 0f) * aimDir;
            verts[i + 1]     = dir * range;
        }

        for (int i = 0; i < segments; i++)
        {
            int base3        = i * 3;
            tris[base3]      = 0;
            tris[base3 + 1]  = i + 1;
            tris[base3 + 2]  = i + 2;
        }

        _coneMesh.Clear();
        _coneMesh.vertices  = verts;
        _coneMesh.triangles = tris;
        _coneMesh.RecalculateNormals();

        // ── Edge line: origin → left ray → arc → right ray → origin ──
        int linePoints = segments + 3;
        _coneEdgeRenderer.positionCount = linePoints;
        _coneEdgeRenderer.SetPosition(0, Vector3.zero);
        for (int i = 0; i <= segments; i++)
            _coneEdgeRenderer.SetPosition(i + 1, verts[i + 1]);
        _coneEdgeRenderer.SetPosition(linePoints - 1, Vector3.zero);
    }

    private void SetConeVisible(bool visible)
    {
        if (_coneGO != null) _coneGO.SetActive(visible);
    }

    // ─── Gizmos ───────────────────────────────────────────────

    private void OnDrawGizmosSelected()
    {
        float   range  = Application.isPlaying ? _attackRange : baseAttackRange;
        Vector3 origin = attackOrigin != null ? attackOrigin.position : transform.position;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(origin, range);

        Vector3 dir = Application.isPlaying ? aimDir : transform.forward;
        dir.y = 0f;
        if (dir.magnitude > 0.01f)
        {
            dir.Normalize();
            float   halfAngle  = attackAngle * 0.5f;
            Vector3 leftEdge   = Quaternion.Euler(0f, -halfAngle, 0f) * dir;
            Vector3 rightEdge  = Quaternion.Euler(0f,  halfAngle, 0f) * dir;

            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(origin, origin + leftEdge  * range);
            Gizmos.DrawLine(origin, origin + rightEdge * range);
        }
    }

    // ─── Stat Modifiers ───────────────────────────────────────

    public void AddMoveSpeed(float bonus)
    {
        moveSpeed = Mathf.Min(moveSpeed + bonus, maxMoveSpeed);
        Debug.Log($"[PlayerController] Move speed → {moveSpeed:F1} (max {maxMoveSpeed})");
    }

    public void ResetMoveSpeed() => moveSpeed = baseMoveSpeed;

    public void AddAttackRange(float bonus)
    {
        _attackRange = Mathf.Min(_attackRange + bonus, maxAttackRange);
        Debug.Log($"[PlayerController] Attack range → {_attackRange:F1} (max {maxAttackRange})");
    }

    public void ResetAttackRange() => _attackRange = baseAttackRange;
}