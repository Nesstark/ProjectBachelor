using UnityEngine;
using UnityEngine.InputSystem;

public class InGameSettings : MonoBehaviour
{
    public static InGameSettings Instance { get; private set; }

    [Header("Input")]
    [SerializeField] private InputActionAsset inputActions;

    [Header("Panels")]
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private GameObject controlsPanel;

    public bool IsOpen { get; private set; }

    private InputAction _settingsAction;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        // Grab the action directly and keep it enabled regardless of action map switches
        _settingsAction = inputActions.FindAction("Player/Settings");
        _settingsAction.performed += OnSettingsPerformed;
        _settingsAction.Enable();
    }

    private void OnDestroy()
    {
        if (_settingsAction != null)
            _settingsAction.performed -= OnSettingsPerformed;
    }

    private void OnSettingsPerformed(InputAction.CallbackContext ctx)
    {
        if (IsOpen)
        {
            if (controlsPanel != null && controlsPanel.activeSelf)
                OnControlsBackPressed();  // ESC on controls → back to settings
            else
                CloseSettings();          // ESC on settings → close
        }
        else
        {
            OpenSettings();
        }
    }

    public void OpenSettings()
    {
        IsOpen = true;
        settingsPanel.SetActive(true);
        Time.timeScale = 0f;
    }

    // Wire to the in-game Settings panel "Back" button
    public void CloseSettings()
    {
        AudioManager.Instance.Play("Click");
        PlayerPrefs.Save();
        IsOpen = false;
        settingsPanel.SetActive(false);
        if (controlsPanel != null) controlsPanel.SetActive(false);
        Time.timeScale = 1f;
    }

    // Wire to the in-game "View Controls" button
    public void OnControlsPressed()
    {
        AudioManager.Instance.Play("Click");
        settingsPanel.SetActive(false);
        controlsPanel.SetActive(true);
    }

    // Wire to the Controls panel "Back" button
    public void OnControlsBackPressed()
    {
        AudioManager.Instance.Play("Click");
        controlsPanel.SetActive(false);
        settingsPanel.SetActive(true);
    }
}