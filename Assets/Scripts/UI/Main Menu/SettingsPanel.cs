using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SettingsPanel : MonoBehaviour
{
    [Header("Volume Sliders")]
    [SerializeField] private Slider masterSlider;
    [SerializeField] private Slider musicSlider;
    [SerializeField] private Slider sfxSlider;

    [Header("Value Labels")]
    [SerializeField] private TMP_Text masterValueLabel;
    [SerializeField] private TMP_Text musicValueLabel;
    [SerializeField] private TMP_Text sfxValueLabel;

    [Header("CRT Toggle")]
    [SerializeField] private Toggle crtToggle;

    [Header("Panels")]
    [SerializeField] private GameObject menuRoot;       // Main Menu screen
    [SerializeField] private GameObject settingsPanel;  // This panel
    [SerializeField] private GameObject controlsPanel;  // Controls screen

    // Keys for PlayerPrefs
    private const string MasterKey = "vol_master";
    private const string MusicKey  = "vol_music";
    private const string SFXKey    = "vol_sfx";
    private const string CRTKey    = "crt_enabled";

    private void OnEnable()
    {
        // Load saved values each time the panel opens
        masterSlider.value = PlayerPrefs.GetFloat(MasterKey, 0.8f);
        musicSlider.value  = PlayerPrefs.GetFloat(MusicKey,  0.65f);
        sfxSlider.value    = PlayerPrefs.GetFloat(SFXKey,    0.9f);
        crtToggle.isOn     = PlayerPrefs.GetInt(CRTKey, 1) == 1;

        RefreshLabels();
        ApplyAll();
    }

    // ── Slider callbacks (wire to OnValueChanged in Inspector) ───────────────

    public void OnMasterChanged(float value)
    {
        masterValueLabel.text = Mathf.RoundToInt(value * 100).ToString();
        AudioManager.Instance.SetMasterVolume(value);
        PlayerPrefs.SetFloat(MasterKey, value);
    }

    public void OnMusicChanged(float value)
    {
        musicValueLabel.text = Mathf.RoundToInt(value * 100).ToString();
        AudioManager.Instance.SetMusicVolume(value);
        PlayerPrefs.SetFloat(MusicKey, value);
    }

    public void OnSFXChanged(float value)
    {
        sfxValueLabel.text = Mathf.RoundToInt(value * 100).ToString();
        AudioManager.Instance.SetSFXVolume(value);
        PlayerPrefs.SetFloat(SFXKey, value);
    }

    // ── CRT toggle callback (wire to OnValueChanged in Inspector) ────────────

    public void OnCRTToggled(bool enabled)
    {
        // Hook this up to your CRT post-process volume:
        // e.g. crtVolume.weight = enabled ? 1f : 0f;
        PlayerPrefs.SetInt(CRTKey, enabled ? 1 : 0);
        Debug.Log($"CRT filter: {(enabled ? "on" : "off")}");
    }

    // ── Navigation ────────────────────────────────────────────────────────────

    // "Controls" button on the Settings panel → go to Controls
    public void OnControlsPressed()
    {
        AudioManager.Instance.Play("Click");
        settingsPanel.SetActive(false);
        controlsPanel.SetActive(true);
    }

    // "Back" button on the Controls panel → return to Settings
    public void OnControlsBackPressed()
    {
        AudioManager.Instance.Play("Click");
        controlsPanel.SetActive(false);
        settingsPanel.SetActive(true);
    }

    // "Back" button on the Settings panel → return to Main Menu
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
        masterValueLabel.text = Mathf.RoundToInt(masterSlider.value * 100).ToString();
        musicValueLabel.text  = Mathf.RoundToInt(musicSlider.value  * 100).ToString();
        sfxValueLabel.text    = Mathf.RoundToInt(sfxSlider.value    * 100).ToString();
    }

    private void ApplyAll()
    {
        AudioManager.Instance.SetMasterVolume(masterSlider.value);
        AudioManager.Instance.SetMusicVolume(musicSlider.value);
        AudioManager.Instance.SetSFXVolume(sfxSlider.value);
    }
}