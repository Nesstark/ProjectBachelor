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

    private void OnEnable()
    {
        masterSlider.value   = PlayerPrefs.GetFloat(MasterKey,   1f);
        ambianceSlider.value = PlayerPrefs.GetFloat(AmbianceKey, 1f);
        sfxSlider.value      = PlayerPrefs.GetFloat(SFXKey,      1f);
        crtToggle.isOn       = PlayerPrefs.GetInt(CRTKey, 1) == 1;

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

    public void OnAmbianceChanged(float value)
    {
        ambianceValueLabel.text = Mathf.RoundToInt(value * 100).ToString();
        AudioManager.Instance.SetAmbianceVolume(value);
        PlayerPrefs.SetFloat(AmbianceKey, value);
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
        // e.g. crtVolume.weight = enabled ? 1f : 0f;
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
        masterValueLabel.text   = Mathf.RoundToInt(masterSlider.value   * 100).ToString();
        ambianceValueLabel.text = Mathf.RoundToInt(ambianceSlider.value * 100).ToString();
        sfxValueLabel.text      = Mathf.RoundToInt(sfxSlider.value      * 100).ToString();
    }

    private void ApplyAll()
    {
        AudioManager.Instance.SetMasterVolume(masterSlider.value);
        AudioManager.Instance.SetAmbianceVolume(ambianceSlider.value);
        AudioManager.Instance.SetSFXVolume(sfxSlider.value);
    }
}