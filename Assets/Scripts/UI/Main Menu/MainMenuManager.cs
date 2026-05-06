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

    // ── Save State Buttons ────────────────────────────────────────────────
    [Header("Save State Buttons")]
    [SerializeField] private Button continueButton;   // ← Button, not GameObject
    [SerializeField] private float  disabledAlpha = 0.35f;

    // ── Sliders & Labels ──────────────────────────────────────────────────
    [Header("Volume Sliders")]
    [SerializeField] private Slider masterSlider;
    [SerializeField] private Slider ambianceSlider;
    [SerializeField] private Slider sfxSlider;

    [Header("Value Labels")]
    [SerializeField] private TMP_Text masterValueLabel;
    [SerializeField] private TMP_Text ambianceValueLabel;
    [SerializeField] private TMP_Text sfxValueLabel;

    // ── Controller Navigation ─────────────────────────────────────────────
    [Header("First Selected")]
    [SerializeField] private GameObject menuFirstSelected;
    [SerializeField] private GameObject settingsFirstSelected;
    [SerializeField] private GameObject controlsFirstSelected;

    // ── Hint Bar ──────────────────────────────────────────────────────────
    [Header("Hint Bar")]
    [SerializeField] private HintBar hintBar;

    // ── PlayerPrefs keys ──────────────────────────────────────────────────
    private const string MasterKey   = "vol_master";
    private const string AmbianceKey = "vol_ambiance";
    private const string SFXKey      = "vol_sfx";

    // ─────────────────────────────────────────────────────────────────────
    // Lifecycle
    // ─────────────────────────────────────────────────────────────────────

    private void Awake()
    {
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

    private IEnumerator Start()
    {
        if (InputDeviceTracker.Instance != null)
            InputDeviceTracker.Instance.LockCursorOnGamepad = false;

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

        if (hintBar != null)
            hintBar.SetContext(HintBar.Context.Menu);

        // Refresh continue button state after everything is ready
        RefreshContinueButton();

        bool hasSave = RunSaveManager.Instance != null && RunSaveManager.Instance.HasActiveSave;
        SetSelected(hasSave && continueButton != null ? continueButton.gameObject : menuFirstSelected);
    }

    // ─────────────────────────────────────────────────────────────────────
    // Device switching
    // ─────────────────────────────────────────────────────────────────────

    private void OnDeviceChanged(InputDeviceTracker.Device device)
    {
        if (device == InputDeviceTracker.Device.Gamepad)
        {
            GameObject target;

            if (settingsPanel != null && settingsPanel.activeSelf)
            {
                target = (controlsSubPanel != null && controlsSubPanel.activeSelf)
                    ? controlsFirstSelected
                    : settingsFirstSelected;
            }
            else
            {
                target = menuFirstSelected;
            }

            StartCoroutine(SelectNextFrame(target));
        }
        else
        {
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(null);
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // Main menu buttons
    // ─────────────────────────────────────────────────────────────────────

    public void OnContinuePressed()
    {
        AudioManager.Instance.Play("Click");
        SceneManager.LoadScene(gameSceneName);
    }

    public void OnNewGamePressed()
    {
        AudioManager.Instance.Play("Click");
        RunSaveManager.Instance?.ClearSave();
        GameManager.Instance?.ResetForNewGame();
        PickupTracker.Instance?.ClearAll();
        SceneManager.LoadScene(gameSceneName);
    }

    public void OnSettingsPressed()
    {
        AudioManager.Instance.Play("Click");
        if (menuRoot != null) menuRoot.SetActive(false);

        if (hintBar != null)
            hintBar.SetContext(HintBar.Context.Settings);

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

        if (hintBar != null)
            hintBar.SetContext(HintBar.Context.Menu);

        StartCoroutine(SelectNextFrame(menuFirstSelected));
    }

    public void OnControlsPressed()
    {
        AudioManager.Instance.Play("Click");
        if (settingsSubPanel != null) settingsSubPanel.SetActive(false);
        if (controlsSubPanel != null) controlsSubPanel.SetActive(true);

        if (hintBar != null)
            hintBar.SetContext(HintBar.Context.Settings);

        StartCoroutine(SelectNextFrame(controlsFirstSelected));
    }

    public void OnControlsBackPressed()
    {
        AudioManager.Instance.Play("Click");
        if (controlsSubPanel != null) controlsSubPanel.SetActive(false);
        if (settingsSubPanel != null) settingsSubPanel.SetActive(true);

        if (hintBar != null)
            hintBar.SetContext(HintBar.Context.Settings);

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

    private void RefreshContinueButton()
    {
        if (continueButton == null) return;

        bool hasSave = RunSaveManager.Instance != null && RunSaveManager.Instance.HasActiveSave;

        // Enable or disable click interaction on the Button itself
        continueButton.interactable = hasSave;

        // Disable the MenuButton hover/select behaviour when no save exists
        MenuButton menuBtn = continueButton.GetComponent<MenuButton>();
        if (menuBtn != null) menuBtn.enabled = hasSave;

        // Add a CanvasGroup if not already present
        CanvasGroup cg = continueButton.GetComponent<CanvasGroup>();
        if (cg == null) cg = continueButton.gameObject.AddComponent<CanvasGroup>();

        cg.alpha          = hasSave ? 1f : disabledAlpha;
        cg.blocksRaycasts = hasSave;  // prevents mouse hover entirely when greyed out
        cg.interactable   = hasSave;  // prevents gamepad/keyboard navigation targeting it
    }

    private void OpenSettingsView()
    {
        if (settingsSubPanel != null) settingsSubPanel.SetActive(true);
        if (controlsSubPanel != null) controlsSubPanel.SetActive(false);
        if (settingsPanel    != null) settingsPanel.SetActive(true);

        if (masterSlider   != null) masterSlider.value   = PlayerPrefs.GetFloat(MasterKey,   1f);
        if (ambianceSlider != null) ambianceSlider.value = PlayerPrefs.GetFloat(AmbianceKey, 1f);
        if (sfxSlider      != null) sfxSlider.value      = PlayerPrefs.GetFloat(SFXKey,      1f);

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