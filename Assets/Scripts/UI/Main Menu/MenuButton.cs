using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections;

/// <summary>
/// Attach to BtnPlay, BtnSettings, BtnQuit.
/// Handles hover / controller-select visuals and responds to device switches.
/// </summary>
public class MenuButton : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler,
    ISelectHandler, IDeselectHandler
{
    [SerializeField] private TMP_Text label;
    [SerializeField] private RectTransform underline;

    // Optional: assign a hint icon that shows the correct button prompt.
    [Header("Input Hint (optional)")]
    [SerializeField] private GameObject mouseHint;      // e.g. "Click" icon
    [SerializeField] private GameObject gamepadHint;    // e.g. "A" button icon

    private static readonly Color normalColor = new Color(0.165f, 0.122f, 0.055f);
    private static readonly Color hoverColor  = new Color(0.545f, 0.102f, 0.102f);

    private Coroutine _underlineRoutine;
    private const float FullWidth = 140f;
    private const float AnimSpeed = 6f;

    private bool _isPointerOver;
    private bool _isSelected;

    // ── Lifecycle ────────────────────────────────────────────────────────

    private void Awake() => SetUnderlineWidth(0f);

    private void OnEnable()
    {
        if (InputDeviceTracker.Instance != null)
            InputDeviceTracker.Instance.OnDeviceChanged += OnDeviceChanged;

        RefreshHints();
    }

    private void OnDisable()
    {
        if (InputDeviceTracker.Instance != null)
            InputDeviceTracker.Instance.OnDeviceChanged -= OnDeviceChanged;

        _isPointerOver = false;
        _isSelected    = false;
        StopAllCoroutines();
        SetUnderlineWidth(0f);
        if (label != null) label.color = normalColor;
    }

    // ── Pointer events (mouse) ───────────────────────────────────────────

    public void OnPointerEnter(PointerEventData _)
    {
        _isPointerOver = true;

        // Mouse took over — clear any controller selection on OTHER buttons.
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);

        RefreshVisuals();
    }

    public void OnPointerExit(PointerEventData _)
    {
        _isPointerOver = false;
        RefreshVisuals();
    }

    public void OnPointerClick(PointerEventData _) =>
        AudioManager.Instance.Play("Click");

    // ── Controller events ────────────────────────────────────────────────

    public void OnSelect  (BaseEventData _) { _isSelected = true;  RefreshVisuals(); }
    public void OnDeselect(BaseEventData _) { _isSelected = false; RefreshVisuals(); }

    // ── Device switch ────────────────────────────────────────────────────

    private void OnDeviceChanged(InputDeviceTracker.Device device)
    {
        // When switching to mouse, drop controller highlight.
        if (device == InputDeviceTracker.Device.KeyboardMouse && _isSelected)
        {
            _isSelected = false;
            RefreshVisuals();
        }
        RefreshHints();
    }

    // ── Visuals ──────────────────────────────────────────────────────────

    private void RefreshVisuals()
    {
        bool on = _isPointerOver || _isSelected;
        if (label != null) label.color = on ? hoverColor : normalColor;
        AnimateUnderline(on ? FullWidth : 0f);
    }

    private void RefreshHints()
    {
        if (InputDeviceTracker.Instance == null) return;
        bool usingGamepad = InputDeviceTracker.Instance.CurrentDevice
                            == InputDeviceTracker.Device.Gamepad;
        if (mouseHint   != null) mouseHint.SetActive(!usingGamepad);
        if (gamepadHint != null) gamepadHint.SetActive(usingGamepad);
    }

    private void AnimateUnderline(float target)
    {
        if (_underlineRoutine != null) StopCoroutine(_underlineRoutine);
        _underlineRoutine = StartCoroutine(TweenUnderline(target));
    }

    private IEnumerator TweenUnderline(float target)
    {
        float cur = underline.sizeDelta.x;
        while (Mathf.Abs(cur - target) > 0.5f)
        {
            cur = Mathf.Lerp(cur, target, Time.unscaledDeltaTime * AnimSpeed * 10f);
            SetUnderlineWidth(cur);
            yield return null;
        }
        SetUnderlineWidth(target);
    }

    private void SetUnderlineWidth(float w) =>
        underline.sizeDelta = new Vector2(w, underline.sizeDelta.y);
}