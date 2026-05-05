using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// Singleton that tracks whether the player is using a gamepad or keyboard/mouse.
/// Fires OnDeviceChanged whenever the active device switches.
/// Put this on a persistent manager GameObject (DontDestroyOnLoad).
/// </summary>
public class InputDeviceTracker : MonoBehaviour
{
    public static InputDeviceTracker Instance { get; private set; }

    public enum Device { KeyboardMouse, Gamepad }

    public Device CurrentDevice { get; private set; } = Device.KeyboardMouse;

    /// <summary>Fired whenever the active input device changes.</summary>
    public event System.Action<Device> OnDeviceChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()  => InputSystem.onActionChange += HandleActionChange;
    private void OnDisable() => InputSystem.onActionChange -= HandleActionChange;

    private void Update()
    {
        // Mouse movement is not an "action", so we poll it here.
        if (Mouse.current != null && Mouse.current.delta.ReadValue().sqrMagnitude > 1f)
            SwitchTo(Device.KeyboardMouse);
    }

    private void HandleActionChange(object obj, InputActionChange change)
    {
        if (change != InputActionChange.ActionPerformed) return;

        var device = (obj as InputAction)?.activeControl?.device;
        if (device == null) return;

        if (device is Gamepad || device is Joystick)
            SwitchTo(Device.Gamepad);
        else if (device is Keyboard || device is Mouse)
            SwitchTo(Device.KeyboardMouse);
    }

    private void SwitchTo(Device next)
    {
        if (next == CurrentDevice) return;
        CurrentDevice = next;
        OnDeviceChanged?.Invoke(CurrentDevice);

        // Show/hide hardware cursor automatically.
        Cursor.visible   = (next == Device.KeyboardMouse);
        Cursor.lockState = (next == Device.Gamepad)
            ? CursorLockMode.Locked
            : CursorLockMode.None;
    }
}