using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// Attach to MainCanvas in the MainMenu scene.
/// Owns the entire main menu: fade-in, navigation, settings, sliders.
/// </summary>
public class MainMenuManager : MonoBehaviour
{
    // ── Scene ─────────────────────────────────────────────────────────────
    [Header("Scene")]
    [SerializeField] private string gameSceneName = "MainGame";

    // ── UI Roots ──────────────────────────────────────────────────────────
    [Header("UI Roots")]
    [SerializeField] private GameObject menuRoot;
    [SerializeField] private CanvasGroup menuGroup;
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private GameObject settingsSubPanel;
    [SerializeField] private GameObject controlsSubPanel;

    // ── Sliders & Labels ──────────────────────────────────────────────────
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
    [Header("First Selected")]
    [SerializeField] private GameObject menuFirstSelected;
    [SerializeField] private GameObject settingsFirstSelected;
    [SerializeField] private GameObject controlsFirstSelected;

    // ── PlayerPrefs keys ──────────────────────────────────────────────────
    private const string MasterKey   = "vol_master";
    private const string AmbianceKey = "vol_ambiance";
    private const string SFXKey      = "vol_sfx";
    private const string CRTKey      = "crt_enabled";

    // ─────────────────────────────────────────────────────────────────────
    // Lifecycle
    // ─────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        // Only hook listeners if the slider/toggle is actually assigned.
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
        if (crtToggle != null)
        {
            crtToggle.onValueChanged.RemoveListener(OnCRTToggled);
            crtToggle.onValueChanged.AddListener(OnCRTToggled);
        }
    }

    private IEnumerator Start()
    {
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (menuRoot      != null) menuRoot.SetActive(true);

        if (menuGroup != null)
        {
            menuGroup.alpha = 0f;
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime * 1.5f;
                menuGroup.alpha = Mathf.Clamp01(t);
                yield return null;
            }
            menuGroup.alpha = 1f;
        }

        SetSelected(menuFirstSelected);
    }

    // ─────────────────────────────────────────────────────────────────────
    // Main menu buttons
    // ─────────────────────────────────────────────────────────────────────

    public void OnPlayPressed()
    {
        AudioManager.Instance.Play("Click");
        SceneManager.LoadScene(gameSceneName);
    }

    public void OnSettingsPressed()
    {
        AudioManager.Instance.Play("Click");
        if (menuRoot != null) menuRoot.SetActive(false);
        OpenSettingsView();
    }

    public void OnQuitPressed()
    {
        AudioManager.Instance.Play("Click");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ─────────────────────────────────────────────────────────────────────
    // Settings navigation
    // ─────────────────────────────────────────────────────────────────────

    public void OnSettingsBackPressed()
    {
        AudioManager.Instance.Play("Click");
        PlayerPrefs.Save();
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (menuRoot      != null) menuRoot.SetActive(true);
        StartCoroutine(SelectNextFrame(menuFirstSelected));
    }

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

    private void OnEnable()
    {
        if (InputDeviceTracker.Instance != null)
            InputDeviceTracker.Instance.OnDeviceChanged += OnDeviceChanged;
    }

    private void OnDisable()
    {
        if (InputDeviceTracker.Instance != null)
            InputDeviceTracker.Instance.OnDeviceChanged -= OnDeviceChanged;
    }

    private void OnDeviceChanged(InputDeviceTracker.Device device)
    {
        // When the player grabs a controller, auto-focus the correct first button.
        if (device == InputDeviceTracker.Device.Gamepad)
        {
            // Pick the right first-selected depending on which panel is showing.
            GameObject target = (settingsPanel != null && settingsPanel.activeSelf)
                ? (controlsSubPanel != null && controlsSubPanel.activeSelf
                    ? controlsFirstSelected
                    : settingsFirstSelected)
                : menuFirstSelected;

            StartCoroutine(SelectNextFrame(target));
        }
        else
        {
            // Mouse took over — drop the EventSystem selection so no button
            // stays highlighted while the cursor is moving.
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(null);
        }
    }
    
    private void OpenSettingsView()
    {
        if (settingsSubPanel != null) settingsSubPanel.SetActive(true);
        if (controlsSubPanel != null) controlsSubPanel.SetActive(false);
        if (settingsPanel    != null) settingsPanel.SetActive(true);

        if (masterSlider   != null) masterSlider.value   = PlayerPrefs.GetFloat(MasterKey,   1f);
        if (ambianceSlider != null) ambianceSlider.value = PlayerPrefs.GetFloat(AmbianceKey, 1f);
        if (sfxSlider      != null) sfxSlider.value      = PlayerPrefs.GetFloat(SFXKey,      1f);
        if (crtToggle      != null) crtToggle.isOn       = PlayerPrefs.GetInt(CRTKey, 1) == 1;

        RefreshLabels();
        ApplyAll();

        StartCoroutine(SelectNextFrame(settingsFirstSelected));
    }

    private static void SetSelected(GameObject target)
    {
        if (target == null || EventSystem.current == null) return;
        EventSystem.current.SetSelectedGameObject(null);
        EventSystem.current.SetSelectedGameObject(target);
    }

    private IEnumerator SelectNextFrame(GameObject target)
    {
        yield return null;
        SetSelected(target);
    }

    private void RefreshLabels()
    {
        if (masterValueLabel   != null && masterSlider   != null) masterValueLabel.text   = Mathf.RoundToInt(masterSlider.value   * 100).ToString();
        if (ambianceValueLabel != null && ambianceSlider != null) ambianceValueLabel.text = Mathf.RoundToInt(ambianceSlider.value * 100).ToString();
        if (sfxValueLabel      != null && sfxSlider      != null) sfxValueLabel.text      = Mathf.RoundToInt(sfxSlider.value      * 100).ToString();
    }

    private void ApplyAll()
    {
        if (masterSlider   != null) AudioManager.Instance.SetMasterVolume(masterSlider.value);
        if (ambianceSlider != null) AudioManager.Instance.SetAmbianceVolume(ambianceSlider.value);
        if (sfxSlider      != null) AudioManager.Instance.SetSFXVolume(sfxSlider.value);
    }
}