using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using System.Collections.Generic;

// ============================================================
//  AIPlayerAgent.cs  —  Reinforcement Learning Player Agent
//
//  HOW IT WORKS (Carrot & Stick / RL):
//  ─────────────────────────────────────────────────────────
//  Every training step the agent:
//    1. OBSERVES  — reads a limited "window" of the world
//                   (only what a real player could see)
//    2. ACTS      — outputs move/dash/attack decisions
//    3. RECEIVES  — a reward (carrot) or penalty (stick)
//    4. LEARNS    — the PPO algorithm adjusts its neural
//                   network weights to maximise future reward
//
//  SETUP:
//  ─────────────────────────────────────────────────────────
//  1. Install com.unity.ml-agents via Package Manager
//  2. Add this component to your Player prefab
//     (remove or disable PlayerController — this replaces it)
//  3. Add a "Behaviour Parameters" component
//       Vector Observation Size : 47   (see CollectObservations)
//       Continuous Actions      : 2    (moveX, moveZ)
//       Discrete Branches       : 2    (dash, attack)
//       Branch 0 size           : 2    (0=no dash, 1=dash)
//       Branch 1 size           : 2    (0=no attack, 1=attack)
//  4. Add a "Decision Requester" component → Decision Period: 5
//  5. Add a "Ray Perception Sensor 3D" for walls/env sensing
//  6. Assign all Inspector references below
//  7. Run training:  mlagents-learn config/agent.yaml --run-id=run1
// ============================================================

[RequireComponent(typeof(Rigidbody))]
public class AIPlayerAgent : Agent
{
    // ─── Inspector References ────────────────────────────────
    [Header("Movement")]
    [SerializeField] private float moveSpeed    = 10f;
    [SerializeField] private float acceleration = 80f;
    [SerializeField] private float deceleration = 120f;
    [SerializeField] private float dashSpeed    = 24f;
    [SerializeField] private float dashDuration = 0.12f;
    [SerializeField] private float dashCooldown = 0.5f;

    [Header("Attack")]
    [SerializeField] private float attackCooldown = 0.4f;
    [SerializeField] private LayerMask enemyLayer;
    [SerializeField] private Transform attackOrigin;

    [Header("Perception")]
    [Tooltip("How far the agent can 'see' enemies and the exit")]
    [SerializeField] private float visionRadius = 12f;
    [Tooltip("Max enemies tracked in observation vector (keep stable)")]
    [SerializeField] private int maxTrackedEnemies = 5;

    [Header("Level Goal")]
    [SerializeField] private Transform exitDoor;
    [Tooltip("How close to exit counts as 'found it'")]
    [SerializeField] private float exitDiscoveryRadius = 6f;
    [SerializeField] private float exitCompleteRadius  = 1.5f;

    [Header("Visual")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Animator animator;

    // ─── Reward Tuning (Carrot & Stick) ──────────────────────
    [Header("Rewards  (+carrots)")]
    [SerializeField] private float rewardKillEnemy      = 1.0f;   // killed an enemy
    [SerializeField] private float rewardDiscoverExit   = 0.5f;   // first time it sees the exit
    [SerializeField] private float rewardReachExit      = 5.0f;   // completes the level
    [SerializeField] private float rewardExploreNew     = 0.02f;  // steps into unexplored area
    [SerializeField] private float rewardSurvivePerSec  = 0.005f; // small bonus just for staying alive

    [Header("Penalties  (-sticks)")]
    [SerializeField] private float penaltyTakeDamage    = 0.3f;   // per hit point lost
    [SerializeField] private float penaltyDie           = 3.0f;   // episode ends
    [SerializeField] private float penaltyIdlePerSec    = 0.01f;  // standing still too long
    [SerializeField] private float penaltyTimePerSec    = 0.001f; // small time pressure
    [SerializeField] private float penaltyRevisitCell   = 0.005f; // stepping into already-visited cell

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

    private float _lastHealth;
    private float _idleTimer;
    private float _episodeTime;

    // Exploration grid — tracks which cells the agent has visited
    private HashSet<Vector2Int> _visitedCells = new HashSet<Vector2Int>();
    private const float CELL_SIZE = 3f;

    // Spawn & fall death
    private Vector3 _spawnPosition;
    [Header("Fall Death")]
    [Tooltip("If the agent drops below this Y value the episode ends immediately")]
    [SerializeField] private float fallDeathY = -5f;

    private GameManager GM => GameManager.Instance;

    // Animator hashes
    private static readonly int HashSpeed  = Animator.StringToHash("Speed");
    private static readonly int HashDirX   = Animator.StringToHash("DirX");
    private static readonly int HashDirZ   = Animator.StringToHash("DirZ");
    private static readonly int HashDash   = Animator.StringToHash("Dash");
    private static readonly int HashAttack = Animator.StringToHash("Attack");

    // =========================================================
    //  AGENT LIFECYCLE
    // =========================================================

    public override void Initialize()
    {
        _rb = GetComponent<Rigidbody>();
        _rb.constraints = RigidbodyConstraints.FreezePositionY
                        | RigidbodyConstraints.FreezeRotation;
        _rb.linearDamping = 0f;

        if (attackOrigin == null) attackOrigin = transform;
        _lastMoveDir   = Vector3.forward;
        _spawnPosition = transform.position;   // remember where we started

        // Subscribe to damage events so we can dish out stick penalties
        if (GM != null)
        {
            GM.OnPlayerHealthChanged.AddListener(OnHealthChanged);
            GM.OnPlayerDied.AddListener(OnPlayerDied);
        }
    }

    // Called at the start of every training episode
    public override void OnEpisodeBegin()
    {
        // ── Reset the full game state ──────────────────────────
        GM?.ResetPlayer();                          // restore health, damage, level
        RoomManager.Instance?.ResetDungeon();       // restart dungeon from room 1

        // ── Reset physics and snap back to spawn ───────────────
        _rb.linearVelocity  = Vector3.zero;
        _rb.angularVelocity = Vector3.zero;
        transform.position  = _spawnPosition;

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

    // =========================================================
    //  OBSERVATIONS  — "What the agent can see"
    // =========================================================
    //
    //  Total observation size = 47 floats
    //  ─────────────────────────────────────────────────────
    //  Self (5):
    //    [0]   normalised health
    //    [1-2] normalised room position (x,z)
    //    [3]   dash cooldown fraction (0=ready, 1=on cooldown)
    //    [4]   attack cooldown fraction
    //
    //  Exit (3):
    //    [5]   exit known? (0/1)
    //    [6-7] direction to exit, normalised (x,z)
    //
    //  Enemies (maxTrackedEnemies × 7 = 5×7 = 35 floats):
    //    per enemy slot:
    //    [0] present? (0/1) — slot may be empty
    //    [1] distance, normalised 0-1 over visionRadius
    //    [2-3] direction to enemy (x,z)
    //    [4] enemy type  (0=Warrior, 0.33=Archer, 0.66=Rogue, 1=Boss)
    //    [5] enemy health fraction
    //    [6] is enemy in melee attack range? (0/1)
    //
    //  NOTE: The agent has NO knowledge of enemies outside visionRadius
    //        and cannot read enemy scripts directly — it only gets the
    //        sensor data below, just like a real player would have to react.
    // =========================================================

    public override void CollectObservations(VectorSensor sensor)
    {
        // ── Self ──────────────────────────────────────────────
        float healthFraction = GM != null
            ? GM.Player.CurrentHealth / GM.Player.MaxHealth
            : 1f;
        sensor.AddObservation(healthFraction);                        // [0]

        // Normalise position within a reasonable room bound (±30 units)
        sensor.AddObservation(transform.position.x / 30f);           // [1]
        sensor.AddObservation(transform.position.z / 30f);           // [2]

        sensor.AddObservation(Mathf.Clamp01(_dashCooldownTimer / dashCooldown));    // [3]
        sensor.AddObservation(Mathf.Clamp01(_attackTimer / attackCooldown));        // [4]

        // ── Exit ──────────────────────────────────────────────
        if (_exitDiscovered && exitDoor != null)
        {
            sensor.AddObservation(1f);                                // [5] known
            Vector3 toExit = (exitDoor.position - transform.position);
            toExit.y = 0f;
            toExit = toExit.sqrMagnitude > 0f ? toExit.normalized : Vector3.zero;
            sensor.AddObservation(toExit.x);                          // [6]
            sensor.AddObservation(toExit.z);                          // [7]
        }
        else
        {
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);
        }

        // ── Nearby Enemies (only within visionRadius) ─────────
        List<EnemyObservation> visible = GetVisibleEnemies();

        // Fill exactly maxTrackedEnemies slots — pad with zeros if fewer
        for (int i = 0; i < maxTrackedEnemies; i++)
        {
            if (i < visible.Count)
            {
                EnemyObservation e = visible[i];
                sensor.AddObservation(1f);                            // present
                sensor.AddObservation(e.normalizedDist);              // distance
                sensor.AddObservation(e.dirX);                        // direction x
                sensor.AddObservation(e.dirZ);                        // direction z
                sensor.AddObservation(e.typeCode);                    // type
                sensor.AddObservation(e.healthFraction);              // hp
                sensor.AddObservation(e.inMeleeRange ? 1f : 0f);     // reachable now?
            }
            else
            {
                // Empty slot — seven zeros
                sensor.AddObservation(0f); sensor.AddObservation(0f);
                sensor.AddObservation(0f); sensor.AddObservation(0f);
                sensor.AddObservation(0f); sensor.AddObservation(0f);
                sensor.AddObservation(0f);
            }
        }
    }

    // =========================================================
    //  ACTIONS  — "What the agent decides to do"
    // =========================================================
    //
    //  Continuous[0] = move X  (-1 to +1)
    //  Continuous[1] = move Z  (-1 to +1)
    //  Discrete[0]   = dash    (0=no, 1=yes)
    //  Discrete[1]   = attack  (0=no, 1=yes)
    // =========================================================

    public override void OnActionReceived(ActionBuffers actions)
    {
        if (_isDead) return;

        // ── Fall death check ──────────────────────────────────
        if (transform.position.y < fallDeathY)
        {
            FallDeath();
            return;
        }

        float inputX = actions.ContinuousActions[0];
        float inputZ = actions.ContinuousActions[1];
        bool wantDash   = actions.DiscreteActions[0] == 1;
        bool wantAttack = actions.DiscreteActions[1] == 1;

        // ── Movement vector ────────────────────────────────────
        _moveDir = new Vector3(inputX, 0f, inputZ);
        if (_moveDir.magnitude > 1f) _moveDir.Normalize();
        if (_moveDir.magnitude > 0.1f) _lastMoveDir = _moveDir;

        // ── Dash ──────────────────────────────────────────────
        if (wantDash && !_isDashing && _dashCooldownTimer <= 0f)
            StartDash();

        // ── Attack ────────────────────────────────────────────
        if (wantAttack && _attackTimer <= 0f)
        {
            _attackTimer = attackCooldown;
            PerformAttack();
        }

        // ── Step rewards ──────────────────────────────────────
        AddReward(rewardSurvivePerSec  * Time.fixedDeltaTime);   // alive = good
        AddReward(-penaltyTimePerSec   * Time.fixedDeltaTime);   // time pressure

        // Idle penalty — discourages the agent from camping
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
            AddReward(rewardExploreNew);          // carrot — new ground
        else
            AddReward(-penaltyRevisitCell);       // stick  — already been here

        // ── Exit proximity check ──────────────────────────────
        CheckExitProximity();

        UpdateTimers();
        UpdatePhysics();
        UpdateAnimator();
        UpdateSpriteFlip();
    }

    // =========================================================
    //  HEURISTIC  — keyboard override for human testing
    //  Press Play, set Behaviour Type to "Heuristic Only"
    //  and you can control the agent manually to verify rewards
    // =========================================================
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        ActionSegment<float> cont = actionsOut.ContinuousActions;
        ActionSegment<int>   disc = actionsOut.DiscreteActions;

        var kb = UnityEngine.InputSystem.Keyboard.current;
        var mouse = UnityEngine.InputSystem.Mouse.current;

        Vector2 move = kb != null
            ? new Vector2(
                (kb.dKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed ? 1f : 0f),
                (kb.wKey.isPressed ? 1f : 0f) - (kb.sKey.isPressed ? 1f : 0f))
            : Vector2.zero;

        cont[0] = move.x;
        cont[1] = move.y;
        disc[0] = kb != null && kb.spaceKey.wasPressedThisFrame ? 1 : 0;
        disc[1] = mouse != null && mouse.leftButton.isPressed ? 1 : 0;
    }

    // =========================================================
    //  PHYSICS
    // =========================================================
    private void FixedUpdate()
    {
        if (_isDead) return;

        if (_isDashing)
        {
            _rb.linearVelocity = _lastMoveDir * dashSpeed;
            return;
        }

        Vector3 target = _moveDir * moveSpeed;
        Vector3 current = new Vector3(_rb.linearVelocity.x, 0f, _rb.linearVelocity.z);
        float accel = _moveDir.magnitude > 0.1f ? acceleration : deceleration;
        Vector3 next = Vector3.MoveTowards(current, target, accel * Time.fixedDeltaTime);
        _rb.linearVelocity = new Vector3(next.x, _rb.linearVelocity.y, next.z);
    }

    // =========================================================
    //  ATTACK
    // =========================================================
    private void PerformAttack()
    {
        float range  = GM != null ? GM.AttackRange : 3f;
        float damage = GM != null ? GM.Player.Damage : 20f;
        int   mask   = enemyLayer.value != 0 ? enemyLayer.value : ~0;

        if (animator != null) animator.SetTrigger(HashAttack);

        Collider[] hits = Physics.OverlapSphere(attackOrigin.position, range, mask);
        Collider closest = null;
        float bestDist = Mathf.Infinity;

        foreach (Collider hit in hits)
        {
            if (hit.gameObject == gameObject) continue;
            Vector3 toEnemy = hit.transform.position - transform.position;
            toEnemy.y = 0f;

            if (_lastMoveDir.magnitude > 0.1f)
            {
                float dot = Vector3.Dot(_lastMoveDir.normalized, toEnemy.normalized);
                if (dot < 0.3f) continue;
            }

            float dist = toEnemy.magnitude;
            if (dist < bestDist) { bestDist = dist; closest = hit; }
        }

        if (closest == null) return;

        BaseEnemy enemy = closest.GetComponentInParent<BaseEnemy>();
        if (enemy != null)
        {
            // Register the hit so we can reward the kill later via TakeDamage callback
            enemy.TakeDamage(damage);
            Debug.Log($"[AIAgent] HIT '{closest.name}' for {damage:F1}");
        }
    }

    // =========================================================
    //  REWARD CALLBACKS
    // =========================================================

    // Called by GameManager.OnPlayerHealthChanged
    private void OnHealthChanged(float current, float max)
    {
        float delta = _lastHealth - current;
        if (delta > 0f)
        {
            // We took damage — apply stick proportional to HP lost
            float penalty = delta * penaltyTakeDamage;
            AddReward(-penalty);
            Debug.Log($"[AIAgent] STICK  took {delta:F1} dmg  penalty:{penalty:F3}");
        }
        _lastHealth = current;
    }

    // Called by GameManager.OnPlayerDied
    private void OnPlayerDied()
    {
        if (_isDead) return;
        _isDead = true;

        AddReward(-penaltyDie);
        Debug.Log("[AIAgent] STICK  died  penalty:" + penaltyDie);
        EndEpisode();       // tells ML-Agents to restart and train
    }

    // Called when agent falls off the map
    public void FallDeath()
    {
        if (_isDead) return;
        _isDead = true;

        AddReward(-penaltyDie);
        Debug.Log("[AIAgent] Fell off map — ending episode");
        EndEpisode();
    }

    // Called when an enemy's health reaches 0 (hook into BaseEnemy.Die via event or override)
    // Attach this to enemies at spawn or wire via EnemyKilledEvent
    public void OnEnemyKilled(float xpReward)
    {
        AddReward(rewardKillEnemy);
        Debug.Log($"[AIAgent] CARROT  enemy killed  reward:{rewardKillEnemy:F2}");
    }

    // =========================================================
    //  EXIT LOGIC
    // =========================================================
    private void CheckExitProximity()
    {
        if (exitDoor == null) return;

        float dist = Vector3.Distance(transform.position, exitDoor.position);

        // Discovery — first time the agent gets close enough to "see" the exit
        if (!_exitDiscovered && dist <= exitDiscoveryRadius)
        {
            _exitDiscovered = true;
            AddReward(rewardDiscoverExit);
            Debug.Log("[AIAgent] CARROT  exit discovered  reward:" + rewardDiscoverExit);
        }

        // Completion — agent walks through the exit
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

    private void UpdatePhysics() { /* driven by FixedUpdate */ }

    private void UpdateAnimator()
    {
        if (animator == null) return;
        animator.SetFloat(HashSpeed, _moveDir.magnitude);
        animator.SetFloat(HashDirX,  _lastMoveDir.x);
        animator.SetFloat(HashDirZ,  _lastMoveDir.z);
    }

    private void UpdateSpriteFlip()
    {
        if (spriteRenderer == null) return;
        if (_lastMoveDir.x >  0.1f) spriteRenderer.flipX = true;
        else if (_lastMoveDir.x < -0.1f) spriteRenderer.flipX = false;
    }

    private Vector2Int WorldToCell(Vector3 pos)
        => new Vector2Int(Mathf.FloorToInt(pos.x / CELL_SIZE),
                          Mathf.FloorToInt(pos.z / CELL_SIZE));

    // =========================================================
    //  ENEMY PERCEPTION
    //  Sorted by distance — closest enemies fill the first slots.
    //  Enemies behind solid walls (Physics.Linecast) are hidden.
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

            // Line-of-sight check — walls block perception
            if (Physics.Linecast(transform.position + Vector3.up * 0.5f,
                                  col.transform.position + Vector3.up * 0.5f,
                                  LayerMask.GetMask("Wall", "Obstacle")))
                continue;    // can't see through walls

            BaseEnemy enemy = col.GetComponentInParent<BaseEnemy>();
            float hpFraction = 1f;
            float typeCode   = 0f;

            if (enemy != null)
            {
                // Read type code — agent learns type-specific behaviour from this
                string typeName = enemy.GetType().Name.ToLower();
                if      (typeName.Contains("archer")) typeCode = 0.33f;
                else if (typeName.Contains("rogue"))  typeCode = 0.66f;
                else if (typeName.Contains("boss"))   typeCode = 1.00f;
                else                                  typeCode = 0.00f;  // warrior/elite
            }

            Vector3 dir = dist > 0f ? toEnemy / dist : Vector3.zero;

            result.Add(new EnemyObservation
            {
                normalizedDist = dist / visionRadius,
                dirX           = dir.x,
                dirZ           = dir.z,
                typeCode       = typeCode,
                healthFraction = hpFraction,
                inMeleeRange   = dist <= (GM != null ? GM.AttackRange : 3f)
            });
        }

        // Sort by distance — closest threats fill observation slots first
        result.Sort((a, b) => a.normalizedDist.CompareTo(b.normalizedDist));

        if (result.Count > maxTrackedEnemies)
            result.RemoveRange(maxTrackedEnemies, result.Count - maxTrackedEnemies);

        return result;
    }

    // =========================================================
    //  GIZMOS — visualise perception radius in the Editor
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
    }

    private void OnDestroy()
    {
        if (GM != null)
        {
            GM.OnPlayerHealthChanged.RemoveListener(OnHealthChanged);
            GM.OnPlayerDied.RemoveListener(OnPlayerDied);
        }
    }
}

// ─── Helper struct — passed between perception and observation ────────────────
public struct EnemyObservation
{
    public float normalizedDist;
    public float dirX, dirZ;
    public float typeCode;
    public float healthFraction;
    public bool  inMeleeRange;
}