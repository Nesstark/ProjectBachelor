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
    public bool LockCursorOnGamepad { get; set; } = false;

    public enum Device { KeyboardMouse, Gamepad }

    public Device CurrentDevice { get; private set; } = Device.Gamepad;

    /// <summary>Fired whenever the active input device changes.</summary>
    public event System.Action<Device> OnDeviceChanged;

    private float _deviceSwitchCooldown = 0f;
    private const float CooldownDuration = 0.2f;
    private const float MouseDeltaThreshold = 25f; // sqrMagnitude — raise this to reduce jitter sensitivity


    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Start in gamepad mode — cursor hidden and free by default.
        Cursor.visible   = false;
        Cursor.lockState = CursorLockMode.None;
    }

    private void OnEnable()  => InputSystem.onActionChange += HandleActionChange;
    private void OnDisable() => InputSystem.onActionChange -= HandleActionChange;

    private void Update()
    {
        if (_deviceSwitchCooldown > 0f)
        {
            _deviceSwitchCooldown -= Time.unscaledDeltaTime;
            return;
        }

        // Real physical mouse movement
        if (Mouse.current != null &&
            Mouse.current.delta.ReadValue().sqrMagnitude > MouseDeltaThreshold)
        {
            SwitchTo(Device.KeyboardMouse);
            return;
        }

        // Real physical key press
        if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame)
            SwitchTo(Device.KeyboardMouse);
    }

    // Replace HandleActionChange entirely
    private void HandleActionChange(object obj, InputActionChange change)
    {
        if (change != InputActionChange.ActionPerformed) return;

        var action = obj as InputAction;
        if (action == null) return;

        // Ignore UI map — EventSystem fires these internally.
        if (action.actionMap?.name == "UI") return;

        var device = action.activeControl?.device;
        if (device == null) return;

        // Only switch TO gamepad here — PlayerInput scheme switching causes
        // false KB/Mouse positives so we handle that direction in Update only.
        if (device is Gamepad || device is Joystick)
            SwitchTo(Device.Gamepad);
    }

    private void SwitchTo(Device next)
    {
        if (next == CurrentDevice) return;

        CurrentDevice = next;
        _deviceSwitchCooldown = CooldownDuration; // prevent immediate ping-pong back
        OnDeviceChanged?.Invoke(CurrentDevice);

        if (LockCursorOnGamepad)
        {
            Cursor.visible   = (next == Device.KeyboardMouse);
            Cursor.lockState = (next == Device.Gamepad)
                ? CursorLockMode.Locked
                : CursorLockMode.None;
        }
        else
        {
            Cursor.visible   = true;
            Cursor.lockState = CursorLockMode.None;
        }
    }
}