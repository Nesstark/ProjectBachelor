using UnityEngine;

public class Billboard : MonoBehaviour
{
    public Camera mainCam;

    [Header("Tilt Angle")]
    [SerializeField, Range(0f, 1f)] private float tiltMultiplier = 0.85f;

    [Header("VFX Positioning")]
    [Tooltip("Assign VFX_Fire 1 here.")]
    [SerializeField] private Transform vfxTransform;

    [Tooltip("Local position on the Firewood sprite where the fire tip is. " +
             "Adjust Y until the particles sit on top of the sprite.")]
    [SerializeField] private Vector3 fireTipOffset = new Vector3(0f, 0.5f, 0f);

    private void Awake()
    {
        mainCam = Camera.main;
    }

    private void LateUpdate()
    {
        // Y-axis billboard
        Vector3 camForward = mainCam.transform.forward;
        camForward.y = 0f;

        if (camForward.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(camForward);

        // Camera pitch tilt
        float camPitch = mainCam.transform.eulerAngles.x;
        if (camPitch > 180f) camPitch -= 360f;
        camPitch = Mathf.Clamp(camPitch, 0f, 89f);

        transform.Rotate(camPitch * tiltMultiplier, 0f, 0f, Space.Self);

        // Move VFX to the world-space position of the fire tip after tilt
        if (vfxTransform != null)
            vfxTransform.position = transform.TransformPoint(fireTipOffset);
    }
}