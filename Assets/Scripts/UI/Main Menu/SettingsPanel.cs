using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SettingsPanel : MonoBehaviour
{
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

    [Header("Panels")]
    [SerializeField] private GameObject menuRoot;
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private GameObject controlsPanel;

    // Keys for PlayerPrefs
    private const string MasterKey   = "vol_master";
    private const string AmbianceKey = "vol_ambiance";
    private const string SFXKey      = "vol_sfx";
    private const string CRTKey      = "crt_enabled";

    private void Awake()
    {
        // Register listeners in code so we never depend on Inspector wiring.
        // RemoveListener first avoids duplicates if the panel is toggled repeatedly.
        masterSlider.onValueChanged.RemoveListener(OnMasterChanged);
        masterSlider.onValueChanged.AddListener(OnMasterChanged);

        ambianceSlider.onValueChanged.RemoveListener(OnAmbianceChanged);
        ambianceSlider.onValueChanged.AddListener(OnAmbianceChanged);

        sfxSlider.onValueChanged.RemoveListener(OnSFXChanged);
        sfxSlider.onValueChanged.AddListener(OnSFXChanged);

        crtToggle.onValueChanged.RemoveListener(OnCRTToggled);
        crtToggle.onValueChanged.AddListener(OnCRTToggled);
    }

    private void OnEnable()
    {
        // Load saved values. Setting .value fires the onValueChanged listeners
        // registered above, which update labels, push to AudioManager, and save
        // to PlayerPrefs — all in one consistent path.
        //
        // IMPORTANT: if the stored value happens to equal the slider's current
        // value Unity will NOT fire the callback (no change = no event), so we
        // call ApplyAll() unconditionally afterwards as a safety net.

        masterSlider.value   = PlayerPrefs.GetFloat(MasterKey,   1f);
        ambianceSlider.value = PlayerPrefs.GetFloat(AmbianceKey, 1f);
        sfxSlider.value      = PlayerPrefs.GetFloat(SFXKey,      1f);
        crtToggle.isOn       = PlayerPrefs.GetInt(CRTKey, 1) == 1;

        // Safety net: always push every value to AudioManager on open,
        // even when the slider value didn't change and the callback didn't fire.
        RefreshLabels();
        ApplyAll();
    }

    private void OnDisable()
    {
        // Persist whenever the panel is hidden, not only on Back press.
        PlayerPrefs.Save();
    }

    // ── Slider callbacks ─────────────────────────────────────────────────────

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

    // ── CRT toggle callback ──────────────────────────────────────────────────

    public void OnCRTToggled(bool enabled)
    {
        // Uncomment and point at your post-processing volume:
        // crtVolume.weight = enabled ? 1f : 0f;
        PlayerPrefs.SetInt(CRTKey, enabled ? 1 : 0);
        Debug.Log($"CRT filter: {(enabled ? "on" : "off")}");
    }

    // ── Navigation ────────────────────────────────────────────────────────────

    public void OnControlsPressed()
    {
        AudioManager.Instance.Play("Click");
        settingsPanel.SetActive(false);
        controlsPanel.SetActive(true);
    }

    public void OnControlsBackPressed()
    {
        AudioManager.Instance.Play("Click");
        controlsPanel.SetActive(false);
        settingsPanel.SetActive(true);
    }

    public void OnBackPressed()
    {
        AudioManager.Instance.Play("Click");
        PlayerPrefs.Save();
        settingsPanel.SetActive(false);
        menuRoot.SetActive(true);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

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