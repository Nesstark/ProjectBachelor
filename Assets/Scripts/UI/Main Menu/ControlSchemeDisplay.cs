using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Swaps a single Image between a gamepad layout sprite
/// and a keyboard/mouse layout sprite when the input device changes.
/// Attach to the UI Image that shows your control diagram.
/// </summary>
public class ControlSchemeDisplay : MonoBehaviour
{
    [SerializeField] private Image  displayImage;
    [SerializeField] private Sprite keyboardMouseSprite;
    [SerializeField] private Sprite gamepadSprite;

    private void OnEnable()
    {
        if (InputDeviceTracker.Instance != null)
            InputDeviceTracker.Instance.OnDeviceChanged += Refresh;

        Refresh(InputDeviceTracker.Instance != null
            ? InputDeviceTracker.Instance.CurrentDevice
            : InputDeviceTracker.Device.KeyboardMouse);
    }

    private void OnDisable()
    {
        if (InputDeviceTracker.Instance != null)
            InputDeviceTracker.Instance.OnDeviceChanged -= Refresh;
    }

    private void Refresh(InputDeviceTracker.Device device)
    {
        if (displayImage == null) return;
        displayImage.sprite = device == InputDeviceTracker.Device.Gamepad
            ? gamepadSprite
            : keyboardMouseSprite;
    }
}