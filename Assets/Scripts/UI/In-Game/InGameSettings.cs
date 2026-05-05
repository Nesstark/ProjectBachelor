using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// Attach to Canvas in the MainGame scene.
/// Owns the in-game settings overlay: open/close, freeze, sliders, controls panel.
/// PlayerController already gates all its inputs behind InGameSettings.Instance.IsOpen.
/// </summary>
public class InGameSettings : MonoBehaviour
{
    public static InGameSettings Instance { get; private set; }

    // ── Input ─────────────────────────────────────────────────────────────
    [Header("Input")]
    [SerializeField] private InputActionAsset inputActions;

    [Header("Hint Bar")]
    [SerializeField] private HintBar hintBar;

    // ── Panels ────────────────────────────────────────────────────────────
    [Header("Panels")]
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private GameObject settingsSubPanel;
    [SerializeField] private GameObject controlsSubPanel;

    // ── Sliders ───────────────────────────────────────────────────────────
    [Header("Volume Sliders")]
    [SerializeField] private Slider masterSlider;
    [SerializeField] private Slider ambianceSlider;
    [SerializeField] private Slider sfxSlider;

    [Header("Value Labels")]
    [SerializeField] private TMP_Text masterValueLabel;
    [SerializeField] private TMP_Text ambianceValueLabel;
    [SerializeField] private TMP_Text sfxValueLabel;

    // ── Controller Navigation ─────────────────────────────────────────────
    [Header("Controller Navigation")]
    [SerializeField] private GameObject settingsFirstSelected;
    [SerializeField] private GameObject controlsFirstSelected;

    // ── State ─────────────────────────────────────────────────────────────
    public bool IsOpen { get; private set; }

    private InputAction _settingsAction;

    private const string MasterKey   = "vol_master";
    private const string AmbianceKey = "vol_ambiance";
    private const string SFXKey      = "vol_sfx";

    // ─────────────────────────────────────────────────────────────────────
    // Lifecycle
    // ─────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        _settingsAction = inputActions.FindAction("Player/Settings");
        if (_settingsAction != null)
        {
            _settingsAction.performed += OnSettingsPerformed;
            _settingsAction.Enable();
        }
        else
        {
            Debug.LogWarning("[InGameSettings] Could not find 'Player/Settings' action.");
        }

        if (masterSlider != null)
        {
            masterSlider.onValueChanged.RemoveListener(OnMasterChanged);
            masterSlider.onValueChanged.AddListener(OnMasterChanged);
        }
        if (ambianceSlider != null)
        {
            ambianceSlider.onValueChanged.RemoveListener(OnAmbianceChanged);
            ambianceSlider.onValueChanged.AddListener(OnAmbianceChanged);
        }
        if (sfxSlider != null)
        {
            sfxSlider.onValueChanged.RemoveListener(OnSFXChanged);
            sfxSlider.onValueChanged.AddListener(OnSFXChanged);
        }
    }

    private void Start()
    {
        if (settingsPanel != null)
            settingsPanel.SetActive(false);
    }

    private void OnDestroy()
    {
        if (_settingsAction != null)
            _settingsAction.performed -= OnSettingsPerformed;
    }

    // ─────────────────────────────────────────────────────────────────────
    // Input callback
    // ─────────────────────────────────────────────────────────────────────

    private void OnSettingsPerformed(InputAction.CallbackContext ctx)
    {
        if (IsOpen)
        {
            if (controlsSubPanel != null && controlsSubPanel.activeSelf)
                OnControlsBackPressed();
            else
                CloseSettings();
        }
        else
        {
            OpenSettings();
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // Open / Close
    // ─────────────────────────────────────────────────────────────────────

    public void OpenSettings()
    {
        IsOpen = true;
        Time.timeScale = 0f;

        if (InputDeviceTracker.Instance != null)
            InputDeviceTracker.Instance.LockCursorOnGamepad = false;

        if (settingsSubPanel != null) settingsSubPanel.SetActive(true);
        if (controlsSubPanel != null) controlsSubPanel.SetActive(false);
        if (settingsPanel    != null) settingsPanel.SetActive(true);

        if (masterSlider   != null) masterSlider.value   = PlayerPrefs.GetFloat(MasterKey,   1f);
        if (ambianceSlider != null) ambianceSlider.value = PlayerPrefs.GetFloat(AmbianceKey, 1f);
        if (sfxSlider      != null) sfxSlider.value      = PlayerPrefs.GetFloat(SFXKey,      1f);

        RefreshLabels();
        ApplyAll();

        // Show hint bar when settings opens.
        if (hintBar != null)
        {
            hintBar.gameObject.SetActive(true);
            hintBar.SetContext(HintBar.Context.InGameSettings);
        }

        StartCoroutine(SelectNextFrame(settingsFirstSelected));
    }

    public void CloseSettings()
    {
        AudioManager.Instance.Play("Click");
        PlayerPrefs.Save();

        IsOpen = false;

        if (InputDeviceTracker.Instance != null)
            InputDeviceTracker.Instance.LockCursorOnGamepad = true;

        if (settingsPanel != null) settingsPanel.SetActive(false);
        Time.timeScale = 1f;

        // Hide hint bar when settings closes.
        if (hintBar != null)
            hintBar.gameObject.SetActive(false);

        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);
    }

    // ─────────────────────────────────────────────────────────────────────
    // Panel navigation
    // ─────────────────────────────────────────────────────────────────────

    public void OnControlsPressed()
    {
        AudioManager.Instance.Play("Click");
        if (settingsSubPanel != null) settingsSubPanel.SetActive(false);
        if (controlsSubPanel != null) controlsSubPanel.SetActive(true);
        StartCoroutine(SelectNextFrame(controlsFirstSelected));
    }

    public void OnControlsBackPressed()
    {
        AudioManager.Instance.Play("Click");
        if (controlsSubPanel != null) controlsSubPanel.SetActive(false);
        if (settingsSubPanel != null) settingsSubPanel.SetActive(true);
        StartCoroutine(SelectNextFrame(settingsFirstSelected));
    }

    // ─────────────────────────────────────────────────────────────────────
    // Slider callbacks
    // ─────────────────────────────────────────────────────────────────────

    public void OnMasterChanged(float value)
    {
        if (masterValueLabel != null)
            masterValueLabel.text = Mathf.RoundToInt(value * 100).ToString();
        AudioManager.Instance.SetMasterVolume(value);
        PlayerPrefs.SetFloat(MasterKey, value);
    }

    public void OnAmbianceChanged(float value)
    {
        if (ambianceValueLabel != null)
            ambianceValueLabel.text = Mathf.RoundToInt(value * 100).ToString();
        AudioManager.Instance.SetAmbianceVolume(value);
        PlayerPrefs.SetFloat(AmbianceKey, value);
    }

    public void OnSFXChanged(float value)
    {
        if (sfxValueLabel != null)
            sfxValueLabel.text = Mathf.RoundToInt(value * 100).ToString();
        AudioManager.Instance.SetSFXVolume(value);
        PlayerPrefs.SetFloat(SFXKey, value);
    }

    // ─────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────

    private IEnumerator SelectNextFrame(GameObject target)
    {
        yield return null;
        if (target == null || EventSystem.current == null) yield break;
        EventSystem.current.SetSelectedGameObject(null);
        EventSystem.current.SetSelectedGameObject(target);
    }

    private void RefreshLabels()
    {
        if (masterValueLabel   != null && masterSlider   != null)
            masterValueLabel.text   = Mathf.RoundToInt(masterSlider.value   * 100).ToString();
        if (ambianceValueLabel != null && ambianceSlider != null)
            ambianceValueLabel.text = Mathf.RoundToInt(ambianceSlider.value * 100).ToString();
        if (sfxValueLabel      != null && sfxSlider      != null)
            sfxValueLabel.text      = Mathf.RoundToInt(sfxSlider.value      * 100).ToString();
    }

    private void ApplyAll()
    {
        if (masterSlider   != null) AudioManager.Instance.SetMasterVolume(masterSlider.value);
        if (ambianceSlider != null) AudioManager.Instance.SetAmbianceVolume(ambianceSlider.value);
        if (sfxSlider      != null) AudioManager.Instance.SetSFXVolume(sfxSlider.value);
    }
}