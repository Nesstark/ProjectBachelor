using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Bottom-corner hint bar. Swaps icons and labels automatically
/// when the input device or focused UI element changes.
///
/// Setup: Create a horizontal layout group in the bottom corner.
/// Add 2–4 HintSlot children (Icon + Label pairs).
/// Assign them to the slots list in the Inspector.
/// </summary>
public class HintBar : MonoBehaviour
{
    // ── Context ───────────────────────────────────────────────────────────
    public enum Context { Menu, Settings, SliderFocused, InGameSettings }

    // ── Hint Slot ─────────────────────────────────────────────────────────
    [System.Serializable]
    public class HintSlot
    {
        public GameObject root;
        public Image      icon;
        public TMP_Text   label;
    }

    // ── Hint Definition ───────────────────────────────────────────────────
    [System.Serializable]
    public class HintDefinition
    {
        public string actionLabel;
        public Sprite gamepadIcon;
        public Sprite keyboardMouseIcon;
    }

    // ── Inspector ─────────────────────────────────────────────────────────
    [Header("Slots (left to right in the bar)")]
    [SerializeField] private List<HintSlot> slots;

    [Tooltip("Hints shown in the in-game settings overlay.")]
    [SerializeField] private List<HintDefinition> inGameSettingsHints;

    [Header("Hints per context")]
    [SerializeField] private List<HintDefinition> menuHints;
    [SerializeField] private List<HintDefinition> settingsHints;
    [SerializeField] private List<HintDefinition> sliderHints;

    // ── State ─────────────────────────────────────────────────────────────
    private Context _currentContext  = Context.Menu;
    private Context _previousContext = Context.Menu; // so slider can restore correctly
    private InputDeviceTracker.Device _currentDevice;
    private GameObject _lastSelected;

    // ─────────────────────────────────────────────────────────────────────
    // Lifecycle
    // ─────────────────────────────────────────────────────────────────────

    private void OnEnable()
    {
        // Only subscribe if the tracker is already alive.
        // If it isn't yet, Start() handles it after a frame.
        if (InputDeviceTracker.Instance != null)
        {
            InputDeviceTracker.Instance.OnDeviceChanged -= OnDeviceChanged; // prevent double-subscribe
            InputDeviceTracker.Instance.OnDeviceChanged += OnDeviceChanged;
            _currentDevice = InputDeviceTracker.Instance.CurrentDevice;
        }
        Refresh();
    }

    private void OnDisable()
    {
        if (InputDeviceTracker.Instance != null)
            InputDeviceTracker.Instance.OnDeviceChanged -= OnDeviceChanged;
    }

    private IEnumerator Start()
    {
        yield return null; // wait for InputDeviceTracker.Awake to run

        if (InputDeviceTracker.Instance != null)
        {
            // Re-subscribe in case OnEnable missed it.
            InputDeviceTracker.Instance.OnDeviceChanged -= OnDeviceChanged;
            InputDeviceTracker.Instance.OnDeviceChanged += OnDeviceChanged;
            _currentDevice = InputDeviceTracker.Instance.CurrentDevice;
        }
        else
        {
            Debug.LogWarning("[HintBar] InputDeviceTracker not found in scene.");
        }

        Refresh();
    }

    private void Update()
    {
        if (EventSystem.current == null) return;

        var selected = EventSystem.current.currentSelectedGameObject;
        if (selected == _lastSelected) return;
        _lastSelected = selected;

        // Check the selected object directly — Slider is the Selectable, not a child.
        bool sliderFocused = selected != null
                        && selected.GetComponent<UnityEngine.UI.Slider>() != null;

        if (sliderFocused && _currentContext != Context.SliderFocused)
        {
            _previousContext = _currentContext;
            _currentContext  = Context.SliderFocused;
            Refresh();
        }
        else if (!sliderFocused && _currentContext == Context.SliderFocused)
        {
            _currentContext = _previousContext;
            Refresh();
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // Public API
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Call this from MainMenuManager / InGameSettings when navigating
    /// between panels. Do NOT call it with SliderFocused — that's automatic.
    /// </summary>
    public void SetContext(Context context)
    {
        if (context == Context.SliderFocused) return;
        _currentContext  = context;
        _previousContext = context;
        _lastSelected    = null;
        Refresh();
    }

    // ─────────────────────────────────────────────────────────────────────
    // Private
    // ─────────────────────────────────────────────────────────────────────

    private void OnDeviceChanged(InputDeviceTracker.Device device)
    {
        _currentDevice = device;
        Refresh();
    }

    private void Refresh()
    {
        List<HintDefinition> hints = _currentContext switch
        {
            Context.SliderFocused  => sliderHints,
            Context.Settings       => settingsHints,
            Context.InGameSettings => inGameSettingsHints,
            _                      => menuHints
        };

        for (int i = 0; i < slots.Count; i++)
        {
            var slot = slots[i];
            if (slot?.root == null) continue;

            if (i >= hints.Count)
            {
                slot.root.SetActive(false);
                continue;
            }

            slot.root.SetActive(true);
            var hint = hints[i];

            if (slot.label != null)
                slot.label.text = hint.actionLabel;

            if (slot.icon != null)
                slot.icon.sprite = _currentDevice == InputDeviceTracker.Device.Gamepad
                    ? hint.gamepadIcon
                    : hint.keyboardMouseIcon;
        }
    }
}