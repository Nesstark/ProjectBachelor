using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;


public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }


    [Header("Player Base Stats (Level 1)")]
    [SerializeField] private float basePlayerHealth = 100f;
    [SerializeField] private float basePlayerDamage = 20f;


    [Header("Player Scaling Per Level")]
    [SerializeField] private float healthPerLevel = 20f;
    [SerializeField] private float damagePerLevel = 5f;


    [Header("XP Curve")]
    [SerializeField] private float baseXpToLevel   = 100f;
    [SerializeField] private float xpScalingFactor = 1.5f;


    [Header("Enemy Type Definitions")]
    [SerializeField] private EnemyTypeData[] enemyTypes;


    [Header("Enemy Level Scaling (applied on top of base stats)")]
    [Tooltip("+X% per player level to all enemy stats")]
    [SerializeField] private float enemyScalePerLevel = 0.15f;


    [HideInInspector] public UnityEvent                    OnPlayerLevelUp       = new UnityEvent();
    [HideInInspector] public UnityEvent<float, float>      OnPlayerHealthChanged = new UnityEvent<float, float>();
    [HideInInspector] public UnityEvent<int, float, float> OnXpChanged           = new UnityEvent<int, float, float>();
    [HideInInspector] public UnityEvent                    OnPlayerDied          = new UnityEvent();


    public PlayerStats Player { get; private set; }


    private Dictionary<string, EnemyTypeData> _enemyLookup;


    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        BuildLookup();
        InitPlayer();
    }


    private void BuildLookup()
    {
        _enemyLookup = new Dictionary<string, EnemyTypeData>();
        if (enemyTypes == null) return;

        foreach (var et in enemyTypes)
        {
            if (string.IsNullOrEmpty(et.typeName)) continue;
            _enemyLookup[et.typeName.ToLower()] = et;
        }
    }


    public EnemyStats GetEnemyStats(string enemyTypeName, int dungeonLevel)
    {
        string key = enemyTypeName.ToLower();

        if (!_enemyLookup.TryGetValue(key, out EnemyTypeData data))
        {
            Debug.LogWarning($"[GameManager] Unknown enemy type '{enemyTypeName}' — using fallback stats.");
            data = FallbackType();
        }

        float scale = 1f + (dungeonLevel - 1) * enemyScalePerLevel;

        return new EnemyStats
        {
        MaxHealth     = data.baseHealth   * scale,
        CurrentHealth = data.baseHealth   * scale,
        Speed         = data.baseSpeed    * scale,
        Damage        = data.baseDamage   * scale,
        XpReward      = data.baseXpReward * Mathf.Pow(1.1f, dungeonLevel - 1)
        };
    }


    private void InitPlayer()
    {
        Player = new PlayerStats
        {
            Level           = 1,
            CurrentXp       = 0f,
            XpToNextLevel   = baseXpToLevel,
            MaxHealth       = basePlayerHealth,
            CurrentHealth   = basePlayerHealth,
            Damage          = basePlayerDamage,
            DamageReduction = 0f
        };
    }


    // ─── Reset for human game ─────────────────────────────────
    public void ResetForNewGame()
    {
        InitPlayer();

        PlayerController pc = FindFirstObjectByType<PlayerController>();
        pc?.ResetAttackRange();
        pc?.ResetMoveSpeed();

        Debug.Log("[GM] Player stats reset for new game.");
    }


    // ─── Reset for AI training episodes only ─────────────────
    public void ResetPlayer()
    {
        InitPlayer();

        PlayerController pc = FindFirstObjectByType<PlayerController>();
        pc?.ResetAttackRange();
        pc?.ResetMoveSpeed();

        OnPlayerHealthChanged.Invoke(Player.CurrentHealth, Player.MaxHealth);
        OnXpChanged.Invoke(Player.Level, Player.CurrentXp, Player.XpToNextLevel);
        Debug.Log("[GM] Player reset for new training episode.");
    }


    // ─── Pickup Methods ───────────────────────────────────────
    public void HealPlayer(float amount)
    {
        Player.CurrentHealth = Mathf.Min(Player.MaxHealth, Player.CurrentHealth + amount);
        OnPlayerHealthChanged.Invoke(Player.CurrentHealth, Player.MaxHealth);
    }

    public void AddDamageReduction(float amount)
    {
        Player.DamageReduction += amount;
        Debug.Log($"[GM] Damage reduction → {Player.DamageReduction:F1}");
    }


    // ─── Player Damage ────────────────────────────────────────
    public void ApplyDamageToPlayer(float amount)
    {
        if (Player.CurrentHealth <= 0f) return;

        float mitigated = Mathf.Max(0f, amount - Player.DamageReduction);
        Player.CurrentHealth = Mathf.Max(0f, Player.CurrentHealth - mitigated);
        OnPlayerHealthChanged.Invoke(Player.CurrentHealth, Player.MaxHealth);
        Debug.Log($"[GM] Player hit {amount:F1} → {mitigated:F1} after reduction. HP:{Player.CurrentHealth:F0}/{Player.MaxHealth:F0}");

        if (Player.CurrentHealth <= 0f)
        {
            Debug.Log("[GM] Player died.");
            OnPlayerDied.Invoke();
        }
    }


    // ─── XP & Level Up ───────────────────────────────────────
    public void AwardXp(float amount)
    {
        Player.CurrentXp += amount;

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
        Player.MaxHealth     = basePlayerHealth + healthPerLevel * (Player.Level - 1);
        Player.CurrentHealth = Mathf.Min(Player.CurrentHealth + healthPerLevel, Player.MaxHealth);
        Player.Damage        = basePlayerDamage + damagePerLevel * (Player.Level - 1);
        Player.XpToNextLevel = Mathf.Round(baseXpToLevel * Mathf.Pow(xpScalingFactor, Player.Level - 1));

        Debug.Log($"[GM] LEVEL UP → {Player.Level} HP:{Player.MaxHealth} DMG:{Player.Damage:F1}");

        OnPlayerLevelUp.Invoke();
        OnPlayerHealthChanged.Invoke(Player.CurrentHealth, Player.MaxHealth);
    }


    private EnemyTypeData FallbackType() => new EnemyTypeData
    {
        typeName     = "Fallback",
        baseHealth   = 50f,
        baseSpeed    = 2.5f,
        baseDamage   = 10f,
        baseXpReward = 25f
    };
}


// ============================================================
// Data Classes
// ============================================================
[System.Serializable]
public class PlayerStats
{
    public int   Level;
    public float CurrentXp;
    public float XpToNextLevel;
    public float MaxHealth;
    public float CurrentHealth;
    public float Damage;
    public float DamageReduction;
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


[System.Serializable]
public class EnemyTypeData
{
    [Tooltip("Must exactly match the EnemyType set on each EnemyController")]
    public string typeName;
    [Space]
    public float baseHealth   = 50f;
    public float baseSpeed    = 3f;
    public float baseDamage   = 10f;
    public float baseXpReward = 25f;
}