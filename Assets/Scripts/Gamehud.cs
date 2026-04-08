using UnityEngine;
using UnityEngine.UI;
using TMPro;

// ============================================================
//  GameHUD.cs  —  Dark Souls style HUD
//
//  CANVAS LAYOUT (anchor: top-left):
//  ─────────────────────────────────────────────────────────
//  Canvas (Screen Space Overlay)
//   └─ HUD_Root  ← attach this script here
//       ├─ LevelText         TMP  — "LEVEL 3"
//       ├─ HealthBarBG       Image (dark bg)
//       │   └─ HealthFill    Image (red, Fill Method: Horizontal)
//       ├─ HealthValueText   TMP  — "75 / 100"
//       ├─ XpBarBG           Image (dark bg)
//       │   └─ XpFill        Image (gold, Fill Method: Horizontal)
//       ├─ XpValueText       TMP  — "340 / 500"
//       ├─ StaminaBarBG      Image (dark bg)
//       │   └─ StaminaFill   Image (green, Fill Method: Horizontal)
//       └─ StaminaReadyText  TMP  — "ready" / "recharging"
//
//  Anchor the HUD_Root panel to top-left in the Rect Transform.
//  Set pivot to (0, 1) and position to e.g. (20, -20).
// ============================================================

public class GameHUD : MonoBehaviour
{
    [Header("Level")]
    [SerializeField] private TMP_Text levelText;

    [Header("Health")]
    [SerializeField] private Image    healthFill;
    [SerializeField] private TMP_Text healthValueText;

    [Header("XP")]
    [SerializeField] private Image    xpFill;
    [SerializeField] private TMP_Text xpValueText;

    [Header("Stamina / Dash")]
    [SerializeField] private Image    staminaFill;
    [SerializeField] private TMP_Text staminaReadyText;

    [Header("Colors")]
    [SerializeField] private Color healthColor    = new Color(0.55f, 0.10f, 0.10f);
    [SerializeField] private Color healthLowColor = new Color(0.85f, 0.08f, 0.08f);
    [SerializeField] private Color xpColor        = new Color(0.72f, 0.58f, 0.18f);
    [SerializeField] private Color staminaColor   = new Color(0.23f, 0.43f, 0.16f);
    [SerializeField] private Color staminaLowColor= new Color(0.15f, 0.28f, 0.10f);

    private GameManager      _gm;
    private PlayerController _player;

    private void Start()
    {
        _gm     = GameManager.Instance;
        _player = FindObjectOfType<PlayerController>();

        if (_gm == null) { Debug.LogError("[GameHUD] GameManager not found!"); return; }

        if (healthFill  != null) healthFill .color = healthColor;
        if (xpFill      != null) xpFill     .color = xpColor;
        if (staminaFill != null) staminaFill.color = staminaColor;

        _gm.OnPlayerHealthChanged.AddListener(OnHealthChanged);
        _gm.OnXpChanged          .AddListener(OnXpChanged);

        RefreshAll();
    }

    private void OnDestroy()
    {
        if (_gm == null) return;
        _gm.OnPlayerHealthChanged.RemoveListener(OnHealthChanged);
        _gm.OnXpChanged          .RemoveListener(OnXpChanged);
    }

    private void Update()
    {
        // Stamina reads from PlayerController.DashReadyFraction each frame
        if (_player == null)
        {
            _player = FindObjectOfType<PlayerController>();
            return;
        }

        float fraction = _player.DashReadyFraction;

        if (staminaFill != null)
        {
            staminaFill.fillAmount = fraction;
            staminaFill.color = fraction < 0.2f ? staminaLowColor : staminaColor;
        }

        if (staminaReadyText != null)
            staminaReadyText.text = fraction >= 1f ? "ready" : "recharging";
    }

    // ─────────────────────────────────────────────────────────
    private void OnHealthChanged(float current, float max)
    {
        float fraction = max > 0f ? current / max : 0f;

        if (healthFill != null)
        {
            healthFill.fillAmount = fraction;
            healthFill.color      = fraction < 0.25f ? healthLowColor : healthColor;
        }

        if (healthValueText != null)
            healthValueText.text = $"{Mathf.RoundToInt(current)} / {Mathf.RoundToInt(max)}";
    }

    private void OnXpChanged(int level, float currentXp, float xpToNext)
    {
        float fraction = xpToNext > 0f ? currentXp / xpToNext : 0f;

        if (xpFill != null)
            xpFill.fillAmount = fraction;

        if (xpValueText != null)
            xpValueText.text = $"{Mathf.RoundToInt(currentXp)} / {Mathf.RoundToInt(xpToNext)}";

        if (levelText != null)
            levelText.text = $"LEVEL  {level}";
    }

    private void RefreshAll()
    {
        if (_gm?.Player == null) return;
        OnHealthChanged(_gm.Player.CurrentHealth, _gm.Player.MaxHealth);
        OnXpChanged(_gm.Player.Level, _gm.Player.CurrentXp, _gm.Player.XpToNextLevel);
    }
}