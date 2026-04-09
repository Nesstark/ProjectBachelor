using UnityEngine;
using UnityEngine.UI;
using TMPro;

// ============================================================
//  EnemyHealthBar.cs
//  World-space health bar that floats above each enemy.
//
//  PREFAB SETUP:
//  ─────────────────────────────────────────────────────────
//  1. Create a new GameObject → "EnemyHealthBarPrefab"
//  2. Add a Canvas component:
//       Render Mode     → World Space
//       Scale           → 0.01, 0.01, 0.01  (shrinks it to world scale)
//  3. Inside the Canvas add:
//       ├─ NameText     (TMP — enemy type name)
//       ├─ BarBG        (Image — dark background, e.g. #111111)
//       │   └─ BarFill  (Image — red fill, Filled Horizontal)
//  4. Set Canvas width/height to ~200×30 in the Rect Transform
//  5. Attach this script to the prefab root
//  6. Assign BarFill, NameText in the Inspector
//  7. Drag this prefab into each enemy prefab and position it
//     above the sprite (Y offset ~1.5 units)
//
//  WIRING INTO BaseEnemy:
//  ─────────────────────────────────────────────────────────
//  In BaseEnemy.cs → Start(), after Stats are assigned, call:
//      GetComponentInChildren<EnemyHealthBar>()?.Init(EnemyTypeName, Stats.MaxHealth);
//
//  In BaseEnemy.cs → TakeDamage(), after reducing health, call:
//      GetComponentInChildren<EnemyHealthBar>()?.SetHealth(Stats.CurrentHealth, Stats.MaxHealth);
// ============================================================

public class EnemyHealthBar : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Image    barFill;
    [SerializeField] private TMP_Text nameText;

    [Header("Settings")]
    [SerializeField] private float    offsetY      = 1.5f;   // how far above the enemy root
    [SerializeField] private bool     hideWhenFull = true;   // hide bar until first hit
    [SerializeField] private float    hideDelay    = 3f;     // seconds after last hit to hide

    [Header("Colors")]
    [SerializeField] private Color fullColor  = new Color(0.55f, 0.10f, 0.10f);
    [SerializeField] private Color lowColor   = new Color(0.85f, 0.08f, 0.08f);

    private Transform  _enemyRoot;
    private Camera     _cam;
    private float      _hideTimer;
    private bool       _initialised;
    private float      _maxHealth;

    // ─────────────────────────────────────────────────────────
    private void Awake()
    {
        _cam = Camera.main;
        if (barFill != null) barFill.color = fullColor;
    }

    private void LateUpdate()
    {
        if (_enemyRoot == null) return;

        // Always face the camera
        if (_cam != null)
            transform.rotation = Quaternion.LookRotation(
                transform.position - _cam.transform.position);

        // Keep floating above the enemy
        transform.position = _enemyRoot.position + Vector3.up * offsetY;

        // Auto-hide after delay
        if (hideWhenFull && _hideTimer > 0f)
        {
            _hideTimer -= Time.deltaTime;
            if (_hideTimer <= 0f)
                SetVisible(false);
        }
    }

    // ─────────────────────────────────────────────────────────
    /// <summary>Call once after the enemy Stats are ready.</summary>
    public void Init(string enemyTypeName, float maxHealth)
    {
        _enemyRoot  = transform.parent;
        _maxHealth  = maxHealth;
        _initialised= true;

        if (nameText != null)
            nameText.text = enemyTypeName.ToUpper();

        if (barFill != null) barFill.fillAmount = 1f;

        // Start hidden if configured
        SetVisible(!hideWhenFull);
    }

    /// <summary>Call every time the enemy takes damage.</summary>
    public void SetHealth(float current, float max)
    {
        if (!_initialised) return;

        float fraction = max > 0f ? Mathf.Clamp01(current / max) : 0f;

        if (barFill != null)
        {
            barFill.fillAmount = fraction;
            barFill.color = fraction < 0.3f ? lowColor : fullColor;
        }

        // Show bar and reset hide timer on damage
        SetVisible(true);
        _hideTimer = hideDelay;
    }

    private void SetVisible(bool visible)
    {
        gameObject.SetActive(visible);
    }
}