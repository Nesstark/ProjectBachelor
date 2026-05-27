using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using System.Collections.Generic;

// ============================================================
//  AIPlayerAgent.cs  —  Reinforcement Learning Player Agent  (v4)
//
//  CHANGES FROM v3:
//  ─────────────────────────────────────────────────────────
//  • REWARD SIMPLIFICATION — every overlapping/noisy signal stripped.
//    The full reward table is now:
//
//      CARROTS
//        +1.0   Enter a new room  (first visit only)
//        +1.0   Kill an enemy
//        +0.5   Collect any pickup  (all types unified)
//        +5.0   Reach the exit
//
//      STICKS
//        -0.001/s  Time pressure  (gentle; keeps episode from dragging)
//        -0.05/s   Lingering in a cleared room  (post grace-period)
//        -2.0      Die
//
//  • REMOVED: rewardSurvivePerSec, rewardDiscoverExit,
//             rewardExploreNewCell, rewardApproachDoor,
//             penaltyIdlePerSec, penaltyStagnationPerCheck,
//             penaltyRevisitCell, penaltyTakeDamage
//  • REMOVED: all idle detection state (_idleTimer, IdleSpeedThreshold…)
//  • REMOVED: stagnation ring-buffer (_posBuffer, _posBufferIdx…)
//  • REMOVED: world-cell hash set (_visitedCells)
//  • REMOVED: _lastDistToDoor / door-approach shaping block
//  • rewardPickupHealth / rewardPickupPermanent → single rewardPickup
//  • OnHealthChanged listener removed (penaltyTakeDamage gone)
//
//  OBSERVATION VECTOR = 72 floats  (unchanged from v3 — no re-training needed)
//  ─────────────────────────────────────────────────────────
//  Self       [0-6]    health, pos.x, pos.z, dash cd, attack cd,
//                      room_cleared, room_is_new
//  Exit       [7-9]    known?, dir.x, dir.z
//  Enemies  5x7=[10-44]
//  Pickups  3x5=[45-59]
//  Doors    4x3=[60-71]
//
//  BEHAVIOUR PARAMETERS  (same as v3 — no change required):
//    Vector Observation Size : 72
//    Continuous Actions      : 2
//    Discrete Branches       : 2  (sizes: 2, 2)
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
    [SerializeField] private float visionRadius      = 12f;
    [SerializeField] private int   maxTrackedEnemies = 5;
    [SerializeField] private int   maxTrackedPickups = 3;

    // ─── Level Goal ──────────────────────────────────────────
    [Header("Level Goal")]
    [SerializeField] private float exitDiscoveryRadius = 6f;
    [SerializeField] private float exitCompleteRadius  = 1.5f;

    // ─── Visual ──────────────────────────────────────────────
    [Header("Visual")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Animator       animator;

    // ─── Rewards ─────────────────────────────────────────────
    [Header("Rewards  (+carrots)")]
    [SerializeField] private float rewardNewRoom    = 1.0f;
    [SerializeField] private float rewardKillEnemy  = 1.0f;
    [SerializeField] private float rewardPickup     = 0.5f;
    [SerializeField] private float rewardReachExit  = 5.0f;

    [Header("Penalties  (-sticks)")]
    [SerializeField] private float penaltyTimePerSec         = 0.001f;
    [SerializeField] private float penaltyClearedRoomPerSec  = 0.05f;
    [SerializeField] private float penaltyDie                = 2.0f;

    // ─── Fall Death ──────────────────────────────────────────
    [Header("Fall Death")]
    [SerializeField] private float fallDeathY = -5f;

    // ─── Private State ────────────────────────────────────────
    private Rigidbody _rb;
    private Vector3   _moveDir;
    private bool      _isDashing;
    private float     _dashTimer;
    private float     _dashCooldownTimer;
    private float     _attackTimer;
    private Vector3   _lastMoveDir;
    private bool      _isDead;
    private bool      _exitDiscovered;
    private float     _episodeTime;
    private float     _currentAttackRange;
    private float     _currentMoveSpeed;

    // Room-level tracking
    private readonly HashSet<int> _visitedRooms   = new HashSet<int>();
    private int   _lastDungeonCell  = -999;
    private float _clearedRoomTimer = 0f;
    private const float ClearedRoomGrace = 1f;

    private Vector3 _spawnPosition;
    private GameManager GM => GameManager.Instance;

    private static readonly int HashSpeed     = Animator.StringToHash("Speed");
    private static readonly int HashDirX      = Animator.StringToHash("DirX");
    private static readonly int HashDirZ      = Animator.StringToHash("DirZ");
    private static readonly int HashDash      = Animator.StringToHash("Dash");
    private static readonly int HashAttack    = Animator.StringToHash("attack");
    private static readonly int HashIsWalking = Animator.StringToHash("isWalking");
    private static readonly int HashFlipX     = Animator.StringToHash("FlipX");

    private Transform GetExitDoor()
    {
        var exit = RoomManager.Instance?.CurrentLevelExit;
        if (exit == null || !exit.activeInHierarchy) return null;
        return exit.transform;
    }

    // =========================================================
    //  LIFECYCLE
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
            GM.OnPlayerDied.AddListener(OnPlayerDied);

        PickupBase.OnAnyPickupCollected += OnPickupCollected;
    }

    public override void OnEpisodeBegin()
    {
        GM?.ResetPlayer();
        _currentAttackRange = baseAttackRange;
        _currentMoveSpeed   = baseMoveSpeed;

        RoomManager.Instance?.ResetForTraining();

        _rb.linearVelocity  = Vector3.zero;
        _rb.angularVelocity = Vector3.zero;
        transform.position  = _spawnPosition;

        _isDead            = false;
        _exitDiscovered    = false;
        _isDashing         = false;
        _dashTimer         = 0f;
        _dashCooldownTimer = 0f;
        _attackTimer       = 0f;
        _episodeTime       = 0f;
        _moveDir           = Vector3.zero;
        _lastMoveDir       = Vector3.forward;

        _visitedRooms.Clear();
        _lastDungeonCell  = -999;
        _clearedRoomTimer = 0f;
    }

    private void OnDestroy()
    {
        if (GM != null)
            GM.OnPlayerDied.RemoveListener(OnPlayerDied);

        PickupBase.OnAnyPickupCollected -= OnPickupCollected;
    }

    // =========================================================
    //  OBSERVATIONS  (72 floats — identical layout to v3)
    // =========================================================

    public override void CollectObservations(VectorSensor sensor)
    {
        int  dungeonCell = RoomManager.Instance?.CurrentCellPublic ?? -1;
        bool roomCleared = dungeonCell >= 0 && RoomManager.Instance.IsRoomCleared(dungeonCell);
        bool roomIsNew   = dungeonCell >= 0 && !_visitedRooms.Contains(dungeonCell);

        // Self [0-6]
        sensor.AddObservation(GM != null ? GM.Player.CurrentHealth / GM.Player.MaxHealth : 1f);
        sensor.AddObservation(transform.position.x / 30f);
        sensor.AddObservation(transform.position.z / 30f);
        sensor.AddObservation(Mathf.Clamp01(_dashCooldownTimer / dashCooldown));
        sensor.AddObservation(Mathf.Clamp01(_attackTimer / attackCooldown));
        sensor.AddObservation(roomCleared ? 1f : 0f);
        sensor.AddObservation(roomIsNew   ? 1f : 0f);

        // Exit [7-9]
        Transform exit = GetExitDoor();
        if (_exitDiscovered && exit != null)
        {
            Vector3 toExit = exit.position - transform.position;
            toExit.y = 0f;
            toExit = toExit.sqrMagnitude > 0f ? toExit.normalized : Vector3.zero;
            sensor.AddObservation(1f);
            sensor.AddObservation(toExit.x);
            sensor.AddObservation(toExit.z);
        }
        else
        {
            sensor.AddObservation(0f); sensor.AddObservation(0f); sensor.AddObservation(0f);
        }

        // Enemies [10-44]
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
            else { for (int j = 0; j < 7; j++) sensor.AddObservation(0f); }
        }

        // Pickups [45-59]
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
            else { for (int j = 0; j < 5; j++) sensor.AddObservation(0f); }
        }

        // Doors [60-71] — always observe all 4 cardinal doors ─────────────────
        // Each door: [dirX, dirZ, isUnlocked]  →  4 × 3 = 12 floats
        AddDoorObservations(sensor);
    }

    private static readonly Direction[] _cardinalDirs =
        { Direction.North, Direction.South, Direction.East, Direction.West };

private void AddDoorObservations(VectorSensor sensor)
{
    var room = RoomManager.Instance?.CurrentRoom;

    foreach (Direction dir in _cardinalDirs)
    {
        if (room != null && room.HasDoor(dir))
        {
            Vector3 doorPos = room.GetDoorPosition(dir);
            Vector3 toDir   = doorPos - transform.position;
            toDir.y = 0f;
            float dist = toDir.magnitude;

            float dirX       = dist > 0f ? toDir.x / dist : 0f;
            float dirZ       = dist > 0f ? toDir.z / dist : 0f;
            float isUnlocked = room.IsDoorUnlocked(dir) ? 1f : 0f;

            sensor.AddObservation(dirX);
            sensor.AddObservation(dirZ);
            sensor.AddObservation(isUnlocked);
        }
        else
        {
            // No door in this direction — pad with zeros
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);
        }
    }
}

    // =========================================================
    //  ACTIONS
    // =========================================================

    public override void OnActionReceived(ActionBuffers actions)
    {
        if (_isDead) return;
        if (transform.position.y < fallDeathY) { FallDeath(); return; }

        float inputX   = actions.ContinuousActions[0];
        float inputZ   = actions.ContinuousActions[1];
        bool  wantDash = actions.DiscreteActions[0] == 1;
        bool  wantAtk  = actions.DiscreteActions[1] == 1;

        _moveDir = new Vector3(inputX, 0f, inputZ);
        if (_moveDir.magnitude > 1f) _moveDir.Normalize();
        if (_moveDir.magnitude > 0.1f) _lastMoveDir = _moveDir;

        if (wantDash && !_isDashing && _dashCooldownTimer <= 0f) StartDash();
        if (wantAtk  && _attackTimer <= 0f) { _attackTimer = attackCooldown; PerformAttack(); }

        // ── Time pressure ─────────────────────────────────────
        AddReward(-penaltyTimePerSec * Time.fixedDeltaTime);

        // ── Room tracking ─────────────────────────────────────
        int dungeonCell = RoomManager.Instance?.CurrentCellPublic ?? -1;

        if (dungeonCell != _lastDungeonCell)
        {
            _lastDungeonCell  = dungeonCell;
            _clearedRoomTimer = 0f;

            if (dungeonCell >= 0 && _visitedRooms.Add(dungeonCell))
            {
                AddReward(rewardNewRoom);
                Debug.Log($"[AIAgent] CARROT  new room cell:{dungeonCell}  reward:{rewardNewRoom:F2}");
            }
        }

        // ── Lingering in cleared room ─────────────────────────
        bool roomCleared = dungeonCell >= 0
                        && dungeonCell != 35           // start room exempt
                        && RoomManager.Instance.IsRoomCleared(dungeonCell);
        if (roomCleared)
        {
            _clearedRoomTimer += Time.fixedDeltaTime;
            if (_clearedRoomTimer > ClearedRoomGrace)
                AddReward(-penaltyClearedRoomPerSec * Time.fixedDeltaTime);
        }
        else
        {
            _clearedRoomTimer = 0f;
        }

        CheckExitProximity();
        UpdateTimers();
        UpdateAnimator();
        UpdateSpriteFlip();
    }

    // =========================================================
    //  HEURISTIC
    // =========================================================
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var cont  = actionsOut.ContinuousActions;
        var disc  = actionsOut.DiscreteActions;
        var kb    = UnityEngine.InputSystem.Keyboard.current;
        var mouse = UnityEngine.InputSystem.Mouse.current;

        Vector2 move = kb != null
            ? new Vector2((kb.dKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed ? 1f : 0f),
                          (kb.wKey.isPressed ? 1f : 0f) - (kb.sKey.isPressed ? 1f : 0f))
            : Vector2.zero;

        cont[0] = move.x;
        cont[1] = move.y;
        disc[0] = kb    != null && kb.spaceKey.wasPressedThisFrame ? 1 : 0;
        disc[1] = mouse != null && mouse.leftButton.isPressed       ? 1 : 0;
    }

    // =========================================================
    //  PHYSICS
    // =========================================================
    private void FixedUpdate()
    {
        if (_isDead) return;
        if (_isDashing)
        {
            _rb.linearVelocity = new Vector3(
                _lastMoveDir.x * dashSpeed, _rb.linearVelocity.y, _lastMoveDir.z * dashSpeed);
            return;
        }
        Vector3 target  = _moveDir * _currentMoveSpeed;
        Vector3 current = new Vector3(_rb.linearVelocity.x, 0f, _rb.linearVelocity.z);
        float   accel   = _moveDir.magnitude > 0.1f ? acceleration : deceleration;
        Vector3 next    = Vector3.MoveTowards(current, target, accel * Time.fixedDeltaTime);
        _rb.linearVelocity = new Vector3(next.x, _rb.linearVelocity.y, next.z);
    }

    // =========================================================
    //  ATTACK
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
            float yAngle = Mathf.Atan2(_lastMoveDir.x, _lastMoveDir.z) * Mathf.Rad2Deg;
            GameObject slash = Instantiate(slashVFXPrefab, attackOrigin.position,
                                           Quaternion.Euler(0f, yAngle, 0f), attackOrigin);
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
        }
    }

    // =========================================================
    //  REWARD CALLBACKS
    // =========================================================

    private void OnPlayerDied()
    {
        if (_isDead) return;
        _isDead = true;
        AddReward(-penaltyDie);
        Debug.Log($"[AIAgent] STICK  player died  penalty:{penaltyDie:F2}");
        EndEpisode();
    }

    public void FallDeath()
    {
        if (_isDead) return;
        _isDead = true;
        AddReward(-penaltyDie);
        Debug.Log($"[AIAgent] STICK  fall death  penalty:{penaltyDie:F2}");
        EndEpisode();
    }

    public void OnEnemyKilled(float xpReward)
    {
        AddReward(rewardKillEnemy);
        Debug.Log($"[AIAgent] CARROT  enemy killed  reward:{rewardKillEnemy:F2}");
    }

    private void OnPickupCollected(PickupBase pickup)
    {
        if (pickup == null) return;
        if (Vector3.Distance(transform.position, pickup.transform.position) > 2.5f) return;

        // All pickup types give the same reward — differentiation via observations is enough.
        // SpeedPickup and RangePickup still update the agent's stats as before.
        switch (pickup)
        {
            case SpeedPickup sp:
                _currentMoveSpeed = Mathf.Min(_currentMoveSpeed + sp.SpeedBonus, maxMoveSpeed);
                break;
            case RangePickup rp:
                _currentAttackRange = Mathf.Min(_currentAttackRange + rp.RangeBonus, maxAttackRange);
                break;
        }

        AddReward(rewardPickup);
        Debug.Log($"[AIAgent] CARROT  pickup {pickup.GetType().Name}  reward:{rewardPickup:F2}");
    }

    // =========================================================
    //  EXIT LOGIC
    // =========================================================
    private void CheckExitProximity()
    {
        Transform exit = GetExitDoor();
        if (exit == null) return;

        float dist = Vector3.Distance(transform.position, exit.position);

        // Discovery is tracked for the observation vector but gives no reward.
        if (!_exitDiscovered && dist <= exitDiscoveryRadius)
            _exitDiscovered = true;

        if (dist <= exitCompleteRadius)
        {
            AddReward(rewardReachExit);
            Debug.Log($"[AIAgent] CARROT  exit reached  reward:{rewardReachExit:F2}");
            EndEpisode();
        }
    }

    // =========================================================
    //  HELPERS
    // =========================================================
    private void StartDash()
    {
        _isDashing = true; _dashTimer = dashDuration; _dashCooldownTimer = dashCooldown;
        if (animator != null) animator.SetTrigger(HashDash);
        CameraShakeManager.Instance?.ShakeImpulse(CameraShakeManager.Instance.dashShakeForce);
    }

    private void UpdateTimers()
    {
        _episodeTime       += Time.deltaTime;
        _dashCooldownTimer -= Time.deltaTime;
        _attackTimer       -= Time.deltaTime;
        if (_isDashing) { _dashTimer -= Time.deltaTime; if (_dashTimer <= 0f) _isDashing = false; }
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
        => new Vector2Int(Mathf.FloorToInt(pos.x / 3f),
                          Mathf.FloorToInt(pos.z / 3f));

    // =========================================================
    //  ENEMY PERCEPTION
    // =========================================================
    private List<EnemyObservation> GetVisibleEnemies()
    {
        var result = new List<EnemyObservation>();
        int mask = enemyLayer.value != 0 ? enemyLayer.value : ~0;
        foreach (Collider col in Physics.OverlapSphere(transform.position, visionRadius, mask))
        {
            if (col.gameObject == gameObject) continue;
            Vector3 toEnemy = col.transform.position - transform.position; toEnemy.y = 0f;
            float dist = toEnemy.magnitude;
            if (Physics.Linecast(transform.position + Vector3.up * 0.5f,
                                  col.transform.position + Vector3.up * 0.5f,
                                  LayerMask.GetMask("Wall", "Obstacle"))) continue;

            BaseEnemy enemy = col.GetComponentInParent<BaseEnemy>();
            float typeCode  = 0f;
            if (enemy != null)
            {
                string t = enemy.GetType().Name.ToLower();
                if      (t.Contains("archer")) typeCode = 0.33f;
                else if (t.Contains("rogue"))  typeCode = 0.66f;
                else if (t.Contains("boss"))   typeCode = 1.00f;
            }
            Vector3 dir = dist > 0f ? toEnemy / dist : Vector3.zero;
            result.Add(new EnemyObservation
            {
                normalizedDist = Mathf.Clamp01(dist / visionRadius),
                dirX = dir.x, dirZ = dir.z,
                typeCode = typeCode, healthFraction = 1f,
                inMeleeRange = dist <= _currentAttackRange
            });
        }
        result.Sort((a, b) => a.normalizedDist.CompareTo(b.normalizedDist));
        if (result.Count > maxTrackedEnemies) result.RemoveRange(maxTrackedEnemies, result.Count - maxTrackedEnemies);
        return result;
    }

    // =========================================================
    //  PICKUP PERCEPTION
    // =========================================================
    private List<PickupObservation> GetNearbyPickups()
    {
        var result = new List<PickupObservation>();
        foreach (Collider col in Physics.OverlapSphere(transform.position, visionRadius))
        {
            PickupBase pickup = col.GetComponent<PickupBase>();
            if (pickup == null) continue;
            Vector3 to = col.transform.position - transform.position; to.y = 0f;
            float dist = to.magnitude;
            float typeCode = pickup switch
            {
                HealthPickup _ => 0.00f, SpeedPickup _ => 0.33f,
                ArmorPickup  _ => 0.66f, RangePickup _ => 1.00f, _ => 0.50f
            };
            result.Add(new PickupObservation
            {
                normalizedDist = Mathf.Clamp01(dist / visionRadius),
                dirX = dist > 0f ? to.x / dist : 0f,
                dirZ = dist > 0f ? to.z / dist : 0f,
                typeCode = typeCode
            });
        }
        result.Sort((a, b) => a.normalizedDist.CompareTo(b.normalizedDist));
        if (result.Count > maxTrackedPickups) result.RemoveRange(maxTrackedPickups, result.Count - maxTrackedPickups);
        return result;
    }

    // =========================================================
    //  DOOR PERCEPTION
    // =========================================================
    private List<Vector3> GetUnlockedDoorPositions()
    {
        var result = new List<Vector3>();
        var room = RoomManager.Instance?.CurrentRoom;
        if (room == null) return result;

        List<Vector3> raw = room.GetUnlockedDoorPositions();
        raw.Sort((a, b) =>
            Vector3.Distance(transform.position, a)
            .CompareTo(Vector3.Distance(transform.position, b)));

        return raw;
    }

    // =========================================================
    //  GIZMOS
    // =========================================================
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, visionRadius);
        Transform exit = GetExitDoor();
        if (exit != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(exit.position, exitCompleteRadius);
        }
        float   range   = Application.isPlaying ? _currentAttackRange : baseAttackRange;
        Vector3 origin  = attackOrigin != null ? attackOrigin.position : transform.position;
        Vector3 forward = (Application.isPlaying ? _lastMoveDir : transform.forward).normalized;
        forward.y = 0f;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(origin, range);
        if (forward.magnitude > 0.01f)
        {
            float h = attackAngle * 0.5f;
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(origin, origin + Quaternion.Euler(0f, -h, 0f) * forward * range);
            Gizmos.DrawLine(origin, origin + Quaternion.Euler(0f,  h, 0f) * forward * range);
        }
    }
}

public struct EnemyObservation
{
    public float normalizedDist, dirX, dirZ, typeCode, healthFraction;
    public bool  inMeleeRange;
}

public struct PickupObservation
{
    public float normalizedDist, dirX, dirZ, typeCode;
}