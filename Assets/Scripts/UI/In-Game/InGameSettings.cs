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
    [Tooltip("Assign your InputSystem_Actions asset.")]
    [SerializeField] private InputActionAsset inputActions;

    // ── Panels ────────────────────────────────────────────────────────────
    [Header("Panels")]
    [Tooltip("The outer SettingsPanel wrapper GameObject.")]
    [SerializeField] private GameObject settingsPanel;
    [Tooltip("ContentGroup inside SettingsPanel — sliders/toggles view.")]
    [SerializeField] private GameObject settingsSubPanel;
    [Tooltip("ControlsPanel inside SettingsPanel — keybinds view.")]
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

    [Header("CRT Toggle")]
    [SerializeField] private Toggle crtToggle;

    // ── Controller Navigation ─────────────────────────────────────────────
    [Header("Controller Navigation")]
    [Tooltip("First element focused when settings opens. E.g. RowMaster.")]
    [SerializeField] private GameObject settingsFirstSelected;
    [Tooltip("First element focused when controls panel opens. E.g. BtnBack in ControlsPanel.")]
    [SerializeField] private GameObject controlsFirstSelected;

    // ── State ─────────────────────────────────────────────────────────────
    public bool IsOpen { get; private set; }

    private InputAction _settingsAction;

    private const string MasterKey   = "vol_master";
    private const string AmbianceKey = "vol_ambiance";
    private const string SFXKey      = "vol_sfx";
    private const string CRTKey      = "crt_enabled";

    // ─────────────────────────────────────────────────────────────────────
    // Lifecycle
    // ─────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        // Keep the Settings toggle action alive regardless of action-map switches.
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

        masterSlider.onValueChanged.RemoveListener(OnMasterChanged);
        masterSlider.onValueChanged.AddListener(OnMasterChanged);
        ambianceSlider.onValueChanged.RemoveListener(OnAmbianceChanged);
        ambianceSlider.onValueChanged.AddListener(OnAmbianceChanged);
        sfxSlider.onValueChanged.RemoveListener(OnSFXChanged);
        sfxSlider.onValueChanged.AddListener(OnSFXChanged);
        crtToggle.onValueChanged.RemoveListener(OnCRTToggled);
        crtToggle.onValueChanged.AddListener(OnCRTToggled);
    }

    private void Start()
    {
        // Ensure panel is hidden at game start.
        settingsPanel.SetActive(false);
    }

    private void OnDestroy()
    {
        if (_settingsAction != null)
            _settingsAction.performed -= OnSettingsPerformed;
    }

    // ─────────────────────────────────────────────────────────────────────
    // Input callback — Settings button / ESC
    // ─────────────────────────────────────────────────────────────────────

    private void OnSettingsPerformed(InputAction.CallbackContext ctx)
    {
        if (IsOpen)
        {
            // If controls sub-panel is showing, go back to settings view first.
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

        settingsSubPanel.SetActive(true);
        controlsSubPanel.SetActive(false);
        settingsPanel.SetActive(true);

        masterSlider.value   = PlayerPrefs.GetFloat(MasterKey,   1f);
        ambianceSlider.value = PlayerPrefs.GetFloat(AmbianceKey, 1f);
        sfxSlider.value      = PlayerPrefs.GetFloat(SFXKey,      1f);
        crtToggle.isOn       = PlayerPrefs.GetInt(CRTKey, 1) == 1;

        RefreshLabels();
        ApplyAll();

        StartCoroutine(SelectNextFrame(settingsFirstSelected));
    }

    // Wire to BtnBack inside the settings (ContentGroup) view.
    public void CloseSettings()
    {
        AudioManager.Instance.Play("Click");
        PlayerPrefs.Save();

        IsOpen = false;
        settingsPanel.SetActive(false);
        Time.timeScale = 1f;

        // Clear selection so no ghost highlight lingers on the game UI.
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);
    }

    // ─────────────────────────────────────────────────────────────────────
    // Panel navigation  (wire OnClick to these on Canvas)
    // ─────────────────────────────────────────────────────────────────────

    // Wire to the SectionControls / "View Controls" button.
    public void OnControlsPressed()
    {
        AudioManager.Instance.Play("Click");
        settingsSubPanel.SetActive(false);
        controlsSubPanel.SetActive(true);
        StartCoroutine(SelectNextFrame(controlsFirstSelected));
    }

    // Wire to BtnBack inside ControlsPanel.
    public void OnControlsBackPressed()
    {
        AudioManager.Instance.Play("Click");
        controlsSubPanel.SetActive(false);
        settingsSubPanel.SetActive(true);
        StartCoroutine(SelectNextFrame(settingsFirstSelected));
    }

    // ─────────────────────────────────────────────────────────────────────
    // Slider / Toggle callbacks
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

    public void OnCRTToggled(bool enabled)
    {
        // crtVolume.weight = enabled ? 1f : 0f;
        PlayerPrefs.SetInt(CRTKey, enabled ? 1 : 0);
        Debug.Log($"CRT filter: {(enabled ? "on" : "off")}");
    }

    // ─────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Waits one frame before selecting so the target is fully active.
    /// Uses unscaled time because Time.timeScale is 0 while settings is open.
    /// </summary>
    private IEnumerator SelectNextFrame(GameObject target)
    {
        yield return null; // WaitForEndOfFrame also works; yield null is fine with timeScale 0
        if (target == null || EventSystem.current == null) yield break;
        EventSystem.current.SetSelectedGameObject(null);
        EventSystem.current.SetSelectedGameObject(target);
    }

    private void RefreshLabels()
    {
        if (masterValueLabel   != null) masterValueLabel.text   = Mathf.RoundToInt(masterSlider.value   * 100).ToString();
        if (ambianceValueLabel != null) ambianceValueLabel.text = Mathf.RoundToInt(ambianceSlider.value * 100).ToString();
        if (sfxValueLabel      != null) sfxValueLabel.text      = Mathf.RoundToInt(sfxSlider.value      * 100).ToString();
    }

    private void ApplyAll()
    {
        AudioManager.Instance.SetMasterVolume(masterSlider.value);
        AudioManager.Instance.SetAmbianceVolume(ambianceSlider.value);
        AudioManager.Instance.SetSFXVolume(sfxSlider.value);
    }
}