using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using System.Collections.Generic;

// ============================================================
//  AIPlayerAgent.cs  —  Reinforcement Learning Player Agent  (v2)
//
//  CHANGES FROM v1:
//  ─────────────────────────────────────────────────────────
//  • Pickup awareness : observes 3 nearest pickups (type + dir + dist)
//  • Pickup rewards   : carrot for collecting each pickup type;
//                       permanent stat effects applied locally
//  • Stat tracking    : _currentAttackRange / _currentMoveSpeed
//                       replace the missing GM.AttackRange reference
//  • Instant respawn  : EndEpisode() fires immediately on death —
//                       DeathScreenUI is bypassed entirely
//  • ResetForTraining : OnEpisodeBegin calls RoomManager.ResetForTraining()
//                       which rebuilds the dungeon from scratch each episode
//
//  OBSERVATION VECTOR = 58 floats  (update Behaviour Parameters!)
//  ─────────────────────────────────────────────────────────
//  Self       [0-4]    health, pos.x, pos.z, dash cd, attack cd
//  Exit       [5-7]    known?, dir.x, dir.z
//  Enemies  5×7=[8-42] present, dist, dir.x, dir.z, type, hp, inMelee
//  Pickups  3×5=[43-57] present, dist, dir.x, dir.z, type
//
//  BEHAVIOUR PARAMETERS (update in Inspector):
//    Vector Observation Size : 58
//    Continuous Actions      : 2  (moveX, moveZ)
//    Discrete Branches       : 2
//      Branch 0 size         : 2  (0=no dash,   1=dash)
//      Branch 1 size         : 2  (0=no attack, 1=attack)
//
//  MULTIPLE INSTANCES (faster training):
//  ─────────────────────────────────────────────────────────
//  The easiest way to gather steps faster is to run several
//  separate Unity builds all connected to the same Python trainer:
//
//    mlagents-learn config/agent.yaml --run-id=run1 --num-envs=4
//
//  Each build is an independent process; no scene changes needed.
//  Alternatively, set up a build with --inference-device=cpu,
//  launch 4-8 copies, and they all feed the same trainer.
//
//  If you want multiple agents inside ONE scene (parallel envs),
//  that requires refactoring all singletons into local references
//  per environment — a larger architectural change. Start with
//  separate builds; it is just as fast and far simpler.
// ============================================================

[RequireComponent(typeof(Rigidbody))]
public class AIPlayerAgent : Agent
{
    // ─── Movement ────────────────────────────────────────────
    [Header("Movement")]
    [SerializeField] private float baseMoveSpeed = 10f;
    [SerializeField] private float maxMoveSpeed  = 20f;
    [SerializeField] private float acceleration  = 80f;
    [SerializeField] private float deceleration  = 120f;
    [SerializeField] private float dashSpeed     = 24f;
    [SerializeField] private float dashDuration  = 0.12f;
    [SerializeField] private float dashCooldown  = 0.5f;

    // ─── Attack ──────────────────────────────────────────────
    [Header("Attack")]
    [SerializeField] private float     attackCooldown  = 0.4f;
    [SerializeField] private float     attackAngle     = 90f;
    [SerializeField] private float     baseAttackRange = 3f;
    [SerializeField] private float     maxAttackRange  = 8f;
    [SerializeField] private LayerMask enemyLayer;
    [SerializeField] private Transform attackOrigin;

    [Header("Hit VFX")]
    [SerializeField] private GameObject slashVFXPrefab;

    // ─── Perception ──────────────────────────────────────────
    [Header("Perception")]
    [Tooltip("How far the agent can see enemies, pickups, and the exit")]
    [SerializeField] private float visionRadius     = 12f;
    [SerializeField] private int   maxTrackedEnemies = 5;   // keep at 5; changing breaks obs vector
    [SerializeField] private int   maxTrackedPickups = 3;   // keep at 3; changing breaks obs vector

    // ─── Level Goal ──────────────────────────────────────────
    [Header("Level Goal")]
    [SerializeField] private Transform exitDoor;
    [SerializeField] private float     exitDiscoveryRadius = 6f;
    [SerializeField] private float     exitCompleteRadius  = 1.5f;

    // ─── Visual ──────────────────────────────────────────────
    [Header("Visual")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Animator       animator;

    // ─── Rewards ─────────────────────────────────────────────
    [Header("Rewards  (+carrots)")]
    [SerializeField] private float rewardKillEnemy       = 1.0f;
    [SerializeField] private float rewardDiscoverExit    = 0.5f;
    [SerializeField] private float rewardReachExit       = 5.0f;
    [SerializeField] private float rewardExploreNew      = 0.02f;
    [SerializeField] private float rewardSurvivePerSec   = 0.005f;
    [SerializeField] private float rewardPickupHealth    = 0.3f;  // consumable heal
    [SerializeField] private float rewardPickupPermanent = 0.5f;  // speed / armor / range

    [Header("Penalties  (-sticks)")]
    [SerializeField] private float penaltyTakeDamage  = 0.3f;
    [SerializeField] private float penaltyDie         = 3.0f;
    [SerializeField] private float penaltyIdlePerSec  = 0.01f;
    [SerializeField] private float penaltyTimePerSec  = 0.001f;
    [SerializeField] private float penaltyRevisitCell = 0.005f;

    // ─── Fall Death ──────────────────────────────────────────
    [Header("Fall Death")]
    [SerializeField] private float fallDeathY = -5f;

    // ─── Private Runtime State ────────────────────────────────
    private Rigidbody _rb;
    private Vector3   _moveDir;
    private bool      _isDashing;
    private float     _dashTimer;
    private float     _dashCooldownTimer;
    private float     _attackTimer;
    private Vector3   _lastMoveDir;
    private bool      _isDead;
    private bool      _exitDiscovered;
    private float     _lastHealth;
    private float     _idleTimer;
    private float     _episodeTime;

    // Local stat mirrors — updated when pickups are collected.
    // These replace the old GM.AttackRange reference which doesn't exist.
    private float _currentAttackRange;
    private float _currentMoveSpeed;

    // Exploration grid
    private readonly HashSet<Vector2Int> _visitedCells = new HashSet<Vector2Int>();
    private const float CELL_SIZE = 3f;

    private Vector3 _spawnPosition;

    private GameManager GM => GameManager.Instance;

    // Animator hashes — match PlayerController exactly
    private static readonly int HashSpeed     = Animator.StringToHash("Speed");
    private static readonly int HashDirX      = Animator.StringToHash("DirX");
    private static readonly int HashDirZ      = Animator.StringToHash("DirZ");
    private static readonly int HashDash      = Animator.StringToHash("Dash");
    private static readonly int HashAttack    = Animator.StringToHash("attack");
    private static readonly int HashIsWalking = Animator.StringToHash("isWalking");
    private static readonly int HashFlipX     = Animator.StringToHash("FlipX");

    // =========================================================
    //  AGENT LIFECYCLE
    // =========================================================

    public override void Initialize()
    {
        _rb = GetComponent<Rigidbody>();
        _rb.constraints   = RigidbodyConstraints.FreezeRotation;
        _rb.linearDamping = 0f;

        if (attackOrigin == null) attackOrigin = transform;
        _lastMoveDir        = Vector3.forward;
        _spawnPosition      = transform.position;
        _currentAttackRange = baseAttackRange;
        _currentMoveSpeed   = baseMoveSpeed;

        if (GM != null)
        {
            GM.OnPlayerHealthChanged.AddListener(OnHealthChanged);
            GM.OnPlayerDied.AddListener(OnPlayerDied);
        }

        // Subscribe to the static pickup event so we receive rewards and
        // can apply permanent stat bonuses locally (PlayerController is disabled).
        PickupBase.OnAnyPickupCollected += OnPickupCollected;
    }

    /// <summary>
    /// Called by ML-Agents at the start of every training episode.
    /// Resets the full game state so the agent trains on a fresh dungeon.
    /// </summary>
    public override void OnEpisodeBegin()
    {
        // Reset player stats (health, damage, XP etc.)
        GM?.ResetPlayer();

        // Reset local stat mirrors to base values
        _currentAttackRange = baseAttackRange;
        _currentMoveSpeed   = baseMoveSpeed;

        // Rebuild the dungeon: new seed, destroy all enemies/pickups, reload start room
        RoomManager.Instance?.ResetForTraining();

        // Reset physics
        _rb.linearVelocity  = Vector3.zero;
        _rb.angularVelocity = Vector3.zero;
        transform.position  = _spawnPosition;

        // Reset all episode state
        _isDead            = false;
        _exitDiscovered    = false;
        _isDashing         = false;
        _dashTimer         = 0f;
        _dashCooldownTimer = 0f;
        _attackTimer       = 0f;
        _idleTimer         = 0f;
        _episodeTime       = 0f;
        _moveDir           = Vector3.zero;
        _lastMoveDir       = Vector3.forward;
        _visitedCells.Clear();

        _lastHealth = GM != null ? GM.Player.CurrentHealth : 100f;
    }

    private void OnDestroy()
    {
        if (GM != null)
        {
            GM.OnPlayerHealthChanged.RemoveListener(OnHealthChanged);
            GM.OnPlayerDied.RemoveListener(OnPlayerDied);
        }
        PickupBase.OnAnyPickupCollected -= OnPickupCollected;
    }

    // =========================================================
    //  OBSERVATIONS  (58 floats)
    // =========================================================

    public override void CollectObservations(VectorSensor sensor)
    {
        // ── Self [0-4] ─────────────────────────────────────────
        float healthFraction = GM != null
            ? GM.Player.CurrentHealth / GM.Player.MaxHealth : 1f;
        sensor.AddObservation(healthFraction);
        sensor.AddObservation(transform.position.x / 30f);
        sensor.AddObservation(transform.position.z / 30f);
        sensor.AddObservation(Mathf.Clamp01(_dashCooldownTimer / dashCooldown));
        sensor.AddObservation(Mathf.Clamp01(_attackTimer / attackCooldown));

        // ── Exit [5-7] ─────────────────────────────────────────
        if (_exitDiscovered && exitDoor != null)
        {
            Vector3 toExit = exitDoor.position - transform.position;
            toExit.y = 0f;
            toExit = toExit.sqrMagnitude > 0f ? toExit.normalized : Vector3.zero;
            sensor.AddObservation(1f);
            sensor.AddObservation(toExit.x);
            sensor.AddObservation(toExit.z);
        }
        else
        {
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);
        }

        // ── Enemies [8-42] (5 × 7 = 35) ───────────────────────
        List<EnemyObservation> visible = GetVisibleEnemies();
        for (int i = 0; i < maxTrackedEnemies; i++)
        {
            if (i < visible.Count)
            {
                EnemyObservation e = visible[i];
                sensor.AddObservation(1f);
                sensor.AddObservation(e.normalizedDist);
                sensor.AddObservation(e.dirX);
                sensor.AddObservation(e.dirZ);
                sensor.AddObservation(e.typeCode);
                sensor.AddObservation(e.healthFraction);
                sensor.AddObservation(e.inMeleeRange ? 1f : 0f);
            }
            else
            {
                for (int j = 0; j < 7; j++) sensor.AddObservation(0f);
            }
        }

        // ── Pickups [43-57] (3 × 5 = 15) ──────────────────────
        List<PickupObservation> pickups = GetNearbyPickups();
        for (int i = 0; i < maxTrackedPickups; i++)
        {
            if (i < pickups.Count)
            {
                PickupObservation p = pickups[i];
                sensor.AddObservation(1f);
                sensor.AddObservation(p.normalizedDist);
                sensor.AddObservation(p.dirX);
                sensor.AddObservation(p.dirZ);
                sensor.AddObservation(p.typeCode);
            }
            else
            {
                for (int j = 0; j < 5; j++) sensor.AddObservation(0f);
            }
        }
    }

    // =========================================================
    //  ACTIONS
    //  Continuous[0] = moveX,  Continuous[1] = moveZ
    //  Discrete[0]   = dash,   Discrete[1]   = attack
    // =========================================================

    public override void OnActionReceived(ActionBuffers actions)
    {
        if (_isDead) return;

        if (transform.position.y < fallDeathY)
        {
            FallDeath();
            return;
        }

        float inputX    = actions.ContinuousActions[0];
        float inputZ    = actions.ContinuousActions[1];
        bool  wantDash  = actions.DiscreteActions[0] == 1;
        bool  wantAtk   = actions.DiscreteActions[1] == 1;

        _moveDir = new Vector3(inputX, 0f, inputZ);
        if (_moveDir.magnitude > 1f) _moveDir.Normalize();
        if (_moveDir.magnitude > 0.1f) _lastMoveDir = _moveDir;

        if (wantDash && !_isDashing && _dashCooldownTimer <= 0f)
            StartDash();

        if (wantAtk && _attackTimer <= 0f)
        {
            _attackTimer = attackCooldown;
            PerformAttack();
        }

        // ── Per-step rewards ──────────────────────────────────
        AddReward( rewardSurvivePerSec * Time.fixedDeltaTime);
        AddReward(-penaltyTimePerSec   * Time.fixedDeltaTime);

        if (_moveDir.magnitude < 0.1f)
        {
            _idleTimer += Time.fixedDeltaTime;
            if (_idleTimer > 1.5f)
                AddReward(-penaltyIdlePerSec * Time.fixedDeltaTime);
        }
        else
        {
            _idleTimer = 0f;
        }

        // ── Exploration reward / revisit penalty ──────────────
        Vector2Int cell = WorldToCell(transform.position);
        if (_visitedCells.Add(cell))
            AddReward(rewardExploreNew);
        else
            AddReward(-penaltyRevisitCell);

        CheckExitProximity();
        UpdateTimers();
        UpdateAnimator();
        UpdateSpriteFlip();
    }

    // =========================================================
    //  HEURISTIC — human keyboard override for testing
    //  Set Behaviour Type to "Heuristic Only" in Play mode.
    // =========================================================
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        ActionSegment<float> cont = actionsOut.ContinuousActions;
        ActionSegment<int>   disc = actionsOut.DiscreteActions;

        var kb    = UnityEngine.InputSystem.Keyboard.current;
        var mouse = UnityEngine.InputSystem.Mouse.current;

        Vector2 move = kb != null
            ? new Vector2(
                (kb.dKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed ? 1f : 0f),
                (kb.wKey.isPressed ? 1f : 0f) - (kb.sKey.isPressed ? 1f : 0f))
            : Vector2.zero;

        cont[0] = move.x;
        cont[1] = move.y;
        disc[0] = kb    != null && kb.spaceKey.wasPressedThisFrame ? 1 : 0;
        disc[1] = mouse != null && mouse.leftButton.isPressed       ? 1 : 0;
    }

    // =========================================================
    //  PHYSICS — matches PlayerController.FixedUpdate
    // =========================================================
    private void FixedUpdate()
    {
        if (_isDead) return;

        if (_isDashing)
        {
            _rb.linearVelocity = _lastMoveDir * dashSpeed;
            return;
        }

        Vector3 target  = _moveDir * _currentMoveSpeed;
        Vector3 current = new Vector3(_rb.linearVelocity.x, 0f, _rb.linearVelocity.z);
        float   accel   = _moveDir.magnitude > 0.1f ? acceleration : deceleration;
        Vector3 next    = Vector3.MoveTowards(current, target, accel * Time.fixedDeltaTime);
        _rb.linearVelocity = new Vector3(next.x, _rb.linearVelocity.y, next.z);
    }

    // =========================================================
    //  ATTACK — matches PlayerController.PerformAttack
    // =========================================================
    private void PerformAttack()
    {
        float range  = _currentAttackRange;
        float damage = GM != null ? GM.Player.Damage : 20f;
        int   mask   = enemyLayer.value != 0 ? enemyLayer.value : ~0;

        if (animator != null) animator.SetTrigger(HashAttack);
        AudioManager.Instance?.Play("PlayerAttack");

        if (slashVFXPrefab != null)
        {
            float      yAngle = Mathf.Atan2(_lastMoveDir.x, _lastMoveDir.z) * Mathf.Rad2Deg;
            Quaternion rot    = Quaternion.Euler(0f, yAngle, 0f);
            GameObject slash  = Instantiate(slashVFXPrefab, attackOrigin.position, rot, attackOrigin);
            Destroy(slash, 0.5f);
        }

        Collider[] hits    = Physics.OverlapSphere(attackOrigin.position, range, mask);
        Collider   closest = null;
        float      bestDist = Mathf.Infinity;

        foreach (Collider hit in hits)
        {
            if (hit.gameObject == gameObject) continue;

            Vector3 toEnemy = hit.transform.position - transform.position;
            toEnemy.y = 0f;

            if (_lastMoveDir.magnitude > 0.1f)
            {
                float dot = Vector3.Dot(_lastMoveDir.normalized, toEnemy.normalized);
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
            Debug.Log($"[AIAgent] HIT '{closest.name}' for {damage:F1}");
        }
    }

    // =========================================================
    //  REWARD CALLBACKS
    // =========================================================

    private void OnHealthChanged(float current, float max)
    {
        float delta = _lastHealth - current;
        if (delta > 0f)
        {
            AddReward(-delta * penaltyTakeDamage);
            Debug.Log($"[AIAgent] STICK  took {delta:F1} dmg  penalty:{delta * penaltyTakeDamage:F3}");
        }
        _lastHealth = current;
    }

    /// <summary>
    /// GameManager fires OnPlayerDied. We end the episode immediately —
    /// no death screen, no button presses, instant next episode.
    /// DeathScreenUI.OnPlayerDied() checks for this component and skips itself.
    /// </summary>
    private void OnPlayerDied()
    {
        if (_isDead) return;
        _isDead = true;

        AddReward(-penaltyDie);
        Debug.Log("[AIAgent] STICK  died  penalty:" + penaltyDie);
        EndEpisode();   // ML-Agents calls OnEpisodeBegin immediately after this
    }

    public void FallDeath()
    {
        if (_isDead) return;
        _isDead = true;

        AddReward(-penaltyDie);
        Debug.Log("[AIAgent] Fell off map — ending episode");
        EndEpisode();
    }

    public void OnEnemyKilled(float xpReward)
    {
        AddReward(rewardKillEnemy);
        Debug.Log($"[AIAgent] CARROT  enemy killed  reward:{rewardKillEnemy:F2}");
    }

    /// <summary>
    /// Fires whenever any PickupBase is collected anywhere in the scene.
    /// We filter by proximity so we only react to pickups the agent itself grabbed.
    /// Permanent stat effects (speed, range) are applied locally here because
    /// PlayerController is disabled and SpeedPickup/RangePickup would silently
    /// find no PlayerController to call.
    /// Health and Armor pickups apply themselves through GameManager as normal.
    /// </summary>
    private void OnPickupCollected(PickupBase pickup)
    {
        // PickupBase fires this BEFORE Destroy(), so pickup.transform is still valid.
        if (pickup == null) return;
        float dist = Vector3.Distance(transform.position, pickup.transform.position);
        if (dist > 2.5f) return;  // wasn't collected by this agent

        switch (pickup)
        {
            case HealthPickup _:
                // GameManager.HealPlayer() already ran inside HealthPickup.OnPickedUp.
                AddReward(rewardPickupHealth);
                Debug.Log($"[AIAgent] CARROT  health pickup  reward:{rewardPickupHealth:F2}");
                break;

            case SpeedPickup sp:
                // PlayerController is disabled, so we apply the bonus ourselves.
                _currentMoveSpeed = Mathf.Min(_currentMoveSpeed + sp.SpeedBonus, maxMoveSpeed);
                AddReward(rewardPickupPermanent);
                Debug.Log($"[AIAgent] CARROT  speed pickup  +{sp.SpeedBonus}  speed→{_currentMoveSpeed:F1}");
                break;

            case ArmorPickup _:
                // GameManager.AddDamageReduction() already ran inside ArmorPickup.OnPickedUp.
                AddReward(rewardPickupPermanent);
                Debug.Log($"[AIAgent] CARROT  armor pickup  reward:{rewardPickupPermanent:F2}");
                break;

            case RangePickup rp:
                // Apply the bonus ourselves since PlayerController is disabled.
                _currentAttackRange = Mathf.Min(_currentAttackRange + rp.RangeBonus, maxAttackRange);
                AddReward(rewardPickupPermanent);
                Debug.Log($"[AIAgent] CARROT  range pickup  +{rp.RangeBonus}  range→{_currentAttackRange:F1}");
                break;
        }
    }

    // =========================================================
    //  EXIT LOGIC
    // =========================================================
    private void CheckExitProximity()
    {
        if (exitDoor == null) return;

        float dist = Vector3.Distance(transform.position, exitDoor.position);

        if (!_exitDiscovered && dist <= exitDiscoveryRadius)
        {
            _exitDiscovered = true;
            AddReward(rewardDiscoverExit);
            Debug.Log("[AIAgent] CARROT  exit discovered  reward:" + rewardDiscoverExit);
        }

        if (dist <= exitCompleteRadius)
        {
            AddReward(rewardReachExit);
            Debug.Log("[AIAgent] CARROT  level complete  reward:" + rewardReachExit);
            EndEpisode();
        }
    }

    // =========================================================
    //  HELPERS
    // =========================================================

    private void StartDash()
    {
        _isDashing         = true;
        _dashTimer         = dashDuration;
        _dashCooldownTimer = dashCooldown;
        if (animator != null) animator.SetTrigger(HashDash);
        CameraShakeManager.Instance?.ShakeImpulse(CameraShakeManager.Instance.dashShakeForce);
    }

    private void UpdateTimers()
    {
        _episodeTime       += Time.deltaTime;
        _dashCooldownTimer -= Time.deltaTime;
        _attackTimer       -= Time.deltaTime;

        if (_isDashing)
        {
            _dashTimer -= Time.deltaTime;
            if (_dashTimer <= 0f) _isDashing = false;
        }
    }

    private void UpdateAnimator()
    {
        if (animator == null) return;
        animator.SetFloat(HashSpeed,    _moveDir.magnitude);
        animator.SetFloat(HashDirX,     _lastMoveDir.x);
        animator.SetFloat(HashDirZ,     _lastMoveDir.z);
        animator.SetBool(HashIsWalking, _moveDir.magnitude > 0.1f);
        animator.SetBool(HashFlipX,     spriteRenderer != null && spriteRenderer.flipX);
    }

    private void UpdateSpriteFlip()
    {
        if (spriteRenderer == null) return;
        if      (_lastMoveDir.x >  0.1f) spriteRenderer.flipX = true;
        else if (_lastMoveDir.x < -0.1f) spriteRenderer.flipX = false;
    }

    private Vector2Int WorldToCell(Vector3 pos)
        => new Vector2Int(Mathf.FloorToInt(pos.x / CELL_SIZE),
                          Mathf.FloorToInt(pos.z / CELL_SIZE));

    // =========================================================
    //  ENEMY PERCEPTION
    // =========================================================
    private List<EnemyObservation> GetVisibleEnemies()
    {
        List<EnemyObservation> result = new List<EnemyObservation>();
        int mask = enemyLayer.value != 0 ? enemyLayer.value : ~0;

        Collider[] nearby = Physics.OverlapSphere(transform.position, visionRadius, mask);

        foreach (Collider col in nearby)
        {
            if (col.gameObject == gameObject) continue;

            Vector3 toEnemy = col.transform.position - transform.position;
            toEnemy.y = 0f;
            float dist = toEnemy.magnitude;

            // Skip enemies behind walls
            if (Physics.Linecast(transform.position + Vector3.up * 0.5f,
                                  col.transform.position + Vector3.up * 0.5f,
                                  LayerMask.GetMask("Wall", "Obstacle")))
                continue;

            BaseEnemy enemy    = col.GetComponentInParent<BaseEnemy>();
            float     typeCode = 0f;

            if (enemy != null)
            {
                string typeName = enemy.GetType().Name.ToLower();
                if      (typeName.Contains("archer")) typeCode = 0.33f;
                else if (typeName.Contains("rogue"))  typeCode = 0.66f;
                else if (typeName.Contains("boss"))   typeCode = 1.00f;
                // Warrior / Elite stay at 0f
            }

            Vector3 dir = dist > 0f ? toEnemy / dist : Vector3.zero;

            result.Add(new EnemyObservation
            {
                normalizedDist = Mathf.Clamp01(dist / visionRadius),
                dirX           = dir.x,
                dirZ           = dir.z,
                typeCode       = typeCode,
                healthFraction = 1f,   // BaseEnemy.Stats is protected; use 1f as safe default
                inMeleeRange   = dist <= _currentAttackRange
            });
        }

        result.Sort((a, b) => a.normalizedDist.CompareTo(b.normalizedDist));
        if (result.Count > maxTrackedEnemies)
            result.RemoveRange(maxTrackedEnemies, result.Count - maxTrackedEnemies);

        return result;
    }

    // =========================================================
    //  PICKUP PERCEPTION
    // =========================================================
    private List<PickupObservation> GetNearbyPickups()
    {
        List<PickupObservation> result = new List<PickupObservation>();

        // OverlapSphere with no layer mask to catch all colliders, then filter by component.
        Collider[] nearby = Physics.OverlapSphere(transform.position, visionRadius);
        foreach (Collider col in nearby)
        {
            PickupBase pickup = col.GetComponent<PickupBase>();
            if (pickup == null) continue;

            Vector3 toPickup = col.transform.position - transform.position;
            toPickup.y = 0f;
            float   dist = toPickup.magnitude;
            Vector3 dir  = dist > 0f ? toPickup / dist : Vector3.zero;

            // Encode pickup type as a normalised float the network can distinguish
            float typeCode = pickup switch
            {
                HealthPickup _ => 0.00f,  // consumable — high priority when low HP
                SpeedPickup  _ => 0.33f,  // permanent speed boost
                ArmorPickup  _ => 0.66f,  // permanent damage reduction
                RangePickup  _ => 1.00f,  // permanent attack range boost
                _              => 0.50f   // unknown
            };

            result.Add(new PickupObservation
            {
                normalizedDist = Mathf.Clamp01(dist / visionRadius),
                dirX           = dir.x,
                dirZ           = dir.z,
                typeCode       = typeCode
            });
        }

        result.Sort((a, b) => a.normalizedDist.CompareTo(b.normalizedDist));
        if (result.Count > maxTrackedPickups)
            result.RemoveRange(maxTrackedPickups, result.Count - maxTrackedPickups);

        return result;
    }

    // =========================================================
    //  GIZMOS
    // =========================================================
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, visionRadius);

        if (exitDoor != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(exitDoor.position, exitCompleteRadius);
            Gizmos.color = new Color(0f, 1f, 0f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, exitDiscoveryRadius);
        }

        float   range   = Application.isPlaying ? _currentAttackRange : baseAttackRange;
        Vector3 origin  = attackOrigin != null ? attackOrigin.position : transform.position;
        Vector3 forward = Application.isPlaying ? _lastMoveDir : transform.forward;
        forward.y = 0f;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(origin, range);

        if (forward.magnitude > 0.01f)
        {
            forward.Normalize();
            float   halfAngle = attackAngle * 0.5f;
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(origin, origin + Quaternion.Euler(0f, -halfAngle, 0f) * forward * range);
            Gizmos.DrawLine(origin, origin + Quaternion.Euler(0f,  halfAngle, 0f) * forward * range);
        }
    }
}

// ─── Helper structs ───────────────────────────────────────────────────────────

public struct EnemyObservation
{
    public float normalizedDist;
    public float dirX, dirZ;
    public float typeCode;
    public float healthFraction;
    public bool  inMeleeRange;
}

public struct PickupObservation
{
    public float normalizedDist;
    public float dirX, dirZ;
    public float typeCode;
}