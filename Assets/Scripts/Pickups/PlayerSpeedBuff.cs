using UnityEngine;
using System.Collections;

// ============================================================
//  PlayerSpeedBuff.cs — Added at runtime by SpeedPickup
//  Boosts PlayerController.moveSpeed temporarily.
// ============================================================
public class PlayerSpeedBuff : MonoBehaviour
{
    private PlayerController _controller;
    private Coroutine        _activeRoutine;

    // Uses reflection to access the private moveSpeed field
    // because PlayerController doesn't expose it publicly
    private System.Reflection.FieldInfo _speedField;

    private void Awake()
    {
        _controller = GetComponent<PlayerController>();
        _speedField = typeof(PlayerController)
            .GetField("moveSpeed", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
    }

    public void Activate(float bonus, float duration)
    {
        // Cancel any existing buff before applying a new one
        if (_activeRoutine != null)
            StopCoroutine(_activeRoutine);

        _activeRoutine = StartCoroutine(BuffRoutine(bonus, duration));
    }

    private IEnumerator BuffRoutine(float bonus, float duration)
    {
        if (_controller == null || _speedField == null) yield break;

        float original = (float)_speedField.GetValue(_controller);
        _speedField.SetValue(_controller, original + bonus);

        Debug.Log($"[SpeedBuff] Speed now {original + bonus} for {duration}s");

        // Optional: show trail or colour tint here
        SpriteRenderer sr = GetComponentInChildren<SpriteRenderer>();
        Color originalColor = sr != null ? sr.color : Color.white;
        if (sr != null) sr.color = new Color(0.5f, 1f, 0.5f);

        yield return new WaitForSeconds(duration);

        _speedField.SetValue(_controller, original);
        if (sr != null) sr.color = originalColor;

        Debug.Log("[SpeedBuff] Speed buff expired");
        _activeRoutine = null;
    }
}