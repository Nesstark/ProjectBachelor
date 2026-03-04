using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;

// ============================================================
//  GameManager.cs  — Central Stat & Game-State Manager
//  Attach to a single "GameManager" GameObject in your scene.
//  All other scripts reference: GameManager.Instance
// ============================================================

public class GameManager : MonoBehaviour
{
    // ─── Singleton ───────────────────────────────────────────
    public static GameManager Instance { get; private set; }

    // ─── Inspector Config ────────────────────────────────────
    [Header("Player Base Stats (Level 1)")]
    [SerializeField] private float basePlayerHealth = 100f;
    [SerializeField] private float basePlayerSpeed = 5f;
    [SerializeField] private float basePlayerDamage = 20f;

    [Header("Player Scaling Per Level")]
    [SerializeField] private float healthPerLevel = 20f;   // +20 HP each level
    [SerializeField] private float speedPerLevel = 0.3f;  // +0.3 speed each level
    [SerializeField] private float damagePerLevel = 5f;    // +5 damage each level

    [Header("XP Curve")]
    [SerializeField] private float baseXpToLevel = 100f;
    [SerializeField] private float xpScalingFactor = 1.5f;  // multiplied each level

    [Header("Enemy Base Stats (Level 1)")]
    [SerializeField] private float baseEnemyHealth = 50f;
    [SerializeField] private float baseEnemySpeed = 2.5f;
    [SerializeField] private float baseEnemyDamage = 10f;
    [SerializeField] private float baseEnemyXpReward = 30f;

    [Header("Enemy Scaling Per Player Level")]
    [SerializeField] private float enemyHealthScale = 10f;
    [SerializeField] private float enemyDamageScale = 2f;
    [SerializeField] private float enemySpeedScale = 0.2f;

    [Header("Attack")]
    [SerializeField] private float attackRange = 3f;    // units (world space)

    // ─── Events (subscribe in other scripts) ─────────────────
    [HideInInspector] public UnityEvent OnPlayerLevelUp = new UnityEvent();
    [HideInInspector] public UnityEvent<float, float> OnPlayerHealthChanged = new UnityEvent<float, float>(); // current, max
    [HideInInspector] public UnityEvent<int, float, float> OnXpChanged = new UnityEvent<int, float, float>(); // level, xp, needed
    [HideInInspector] public UnityEvent OnPlayerDied = new UnityEvent();

    // ─── Runtime Player Stats (live values) ──────────────────
    public PlayerStats Player { get; private set; }

    // ─── Getters ─────────────────────────────────────────────
    public float AttackRange => attackRange;

    // ─────────────────────────────────────────────────────────
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        InitPlayer();
    }

    // ─── Initialise ──────────────────────────────────────────
    private void InitPlayer()
    {
        Player = new PlayerStats
        {
            Level = 1,
            CurrentXp = 0f,
            XpToNextLevel = baseXpToLevel,
            MaxHealth = basePlayerHealth,
            CurrentHealth = basePlayerHealth,
            Speed = basePlayerSpeed,
            Damage = basePlayerDamage
        };
    }

    // ─── Enemy Stat Factory ───────────────────────────────────
    /// <summary>Creates a fresh EnemyStats block scaled to the current player level.</summary>
    public EnemyStats CreateEnemyStats()
    {
        int lvl = Player.Level;
        return new EnemyStats
        {
            MaxHealth = baseEnemyHealth + enemyHealthScale * (lvl - 1),
            CurrentHealth = baseEnemyHealth + enemyHealthScale * (lvl - 1),
            Speed = baseEnemySpeed + enemySpeedScale * (lvl - 1),
            Damage = baseEnemyDamage + enemyDamageScale * (lvl - 1),
            XpReward = baseEnemyXpReward * Mathf.Pow(1.1f, lvl - 1)
        };
    }

    // ─── Player Takes Damage ──────────────────────────────────
    public void ApplyDamageToPlayer(float amount)
    {
        if (Player.CurrentHealth <= 0f) return;

        Player.CurrentHealth = Mathf.Max(0f, Player.CurrentHealth - amount);
        OnPlayerHealthChanged.Invoke(Player.CurrentHealth, Player.MaxHealth);
        Debug.Log($"[GameManager] Player hit for {amount:F1}. HP: {Player.CurrentHealth:F1}/{Player.MaxHealth:F1}");

        if (Player.CurrentHealth <= 0f)
        {
            Debug.Log("[GameManager] Player died!");
            OnPlayerDied.Invoke();
        }
    }

    // ─── Player Heals ────────────────────────────────────────
    public void HealPlayer(float amount)
    {
        Player.CurrentHealth = Mathf.Min(Player.MaxHealth, Player.CurrentHealth + amount);
        OnPlayerHealthChanged.Invoke(Player.CurrentHealth, Player.MaxHealth);
    }

    // ─── XP & Level Up ───────────────────────────────────────
    public void AwardXp(float amount)
    {
        Player.CurrentXp += amount;
        Debug.Log($"[GameManager] +{amount:F0} XP  ({Player.CurrentXp:F0}/{Player.XpToNextLevel:F0})");

        while (Player.CurrentXp >= Player.XpToNextLevel)
        {
            Player.CurrentXp -= Player.XpToNextLevel;
            LevelUp();
        }

        OnXpChanged.Invoke(Player.Level, Player.CurrentXp, Player.XpToNextLevel);
    }

    private void LevelUp()
    {
        Player.Level++;
        Player.MaxHealth = basePlayerHealth + healthPerLevel * (Player.Level - 1);
        Player.CurrentHealth = Mathf.Min(Player.CurrentHealth + healthPerLevel, Player.MaxHealth);
        Player.Speed = basePlayerSpeed + speedPerLevel * (Player.Level - 1);
        Player.Damage = basePlayerDamage + damagePerLevel * (Player.Level - 1);
        Player.XpToNextLevel = Mathf.Round(baseXpToLevel * Mathf.Pow(xpScalingFactor, Player.Level - 1));

        Debug.Log($"[GameManager] *** LEVEL UP! Now Level {Player.Level} ***  " +
                  $"HP:{Player.MaxHealth} SPD:{Player.Speed:F1} DMG:{Player.Damage:F1}  XpNeeded:{Player.XpToNextLevel:F0}");

        OnPlayerLevelUp.Invoke();
        OnPlayerHealthChanged.Invoke(Player.CurrentHealth, Player.MaxHealth);
    }

    // ─── Debug GUI ───────────────────────────────────────────
#if UNITY_EDITOR
    private void OnGUI()
    {
        if (Player == null) return;
        GUILayout.BeginArea(new Rect(10, 10, 250, 160));
        GUILayout.Label($"Level : {Player.Level}");
        GUILayout.Label($"HP    : {Player.CurrentHealth:F0} / {Player.MaxHealth:F0}");
        GUILayout.Label($"Speed : {Player.Speed:F2}");
        GUILayout.Label($"Damage: {Player.Damage:F1}");
        GUILayout.Label($"XP    : {Player.CurrentXp:F0} / {Player.XpToNextLevel:F0}");
        GUILayout.EndArea();
    }
#endif
}

// ============================================================
//  Data classes  — pure containers, no MonoBehaviour
// ============================================================

[System.Serializable]
public class PlayerStats
{
    public int Level;
    public float CurrentXp;
    public float XpToNextLevel;
    public float MaxHealth;
    public float CurrentHealth;
    public float Speed;
    public float Damage;
}

[System.Serializable]
public class EnemyStats
{
    public float MaxHealth;
    public float CurrentHealth;
    public float Speed;
    public float Damage;
    public float XpReward;
}