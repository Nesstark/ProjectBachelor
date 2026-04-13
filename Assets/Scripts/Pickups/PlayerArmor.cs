using UnityEngine;
using System.Collections;

// ============================================================
//  PlayerArmor.cs — Added at runtime by ArmorPickup
//  Intercepts the next damage call and blocks it.
//  Shows a visual indicator while armor is active.
// ============================================================
public class PlayerArmor : MonoBehaviour
{
    [SerializeField] private float flashDuration = 0.3f;

    private bool _isActive = false;
    private GameManager _gm;

    // Optional: assign a visual indicator in Inspector if you
    // want a shield glow sprite shown while armor is active
    [SerializeField] private GameObject armorVFX;

    private void Start()
    {
        _gm = GameManager.Instance;
        if (_gm != null)
            _gm.OnPlayerHealthChanged.AddListener(OnHealthChanged);
    }

    private void OnDestroy()
    {
        if (_gm != null)
            _gm.OnPlayerHealthChanged.RemoveListener(OnHealthChanged);
    }

    public void Activate()
    {
        _isActive = true;
        if (armorVFX != null) armorVFX.SetActive(true);
        Debug.Log("[PlayerArmor] Shield active");
    }

    // This listens AFTER damage is already applied —
    // instead we patch via GameManager's public method approach below
    private void OnHealthChanged(float current, float max) { }

    // Called by GameManager before damage is applied
    // Returns true if damage should be blocked
    public bool TryBlockDamage()
    {
        if (!_isActive) return false;

        _isActive = false;
        if (armorVFX != null) armorVFX.SetActive(false);

        Debug.Log("[PlayerArmor] Hit blocked! Shield consumed.");
        AudioManager.Instance?.Play("ArmorBlock");

        StartCoroutine(FlashShield());
        return true;
    }

    private IEnumerator FlashShield()
    {
        SpriteRenderer sr = GetComponentInChildren<SpriteRenderer>();
        if (sr == null) yield break;

        Color original = sr.color;
        sr.color = Color.blue;
        yield return new WaitForSeconds(flashDuration);
        sr.color = original;
    }

    public bool IsActive => _isActive;
}