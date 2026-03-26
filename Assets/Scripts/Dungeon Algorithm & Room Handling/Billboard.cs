using UnityEngine;

public class Billboard : MonoBehaviour
{
    public Camera mainCam;

    [Header("Tilt Angle")]
    [Tooltip("Multiplier on camera pitch for the sprite lean.")]
    [SerializeField, Range(0f, 1f)] private float tiltMultiplier = 0.85f;

    private void Awake()
    {
        mainCam = Camera.main;
    }

    private void LateUpdate()
    {
        // Y-axis only billboard — sprite faces camera horizontally, stays upright
        Vector3 camForward = mainCam.transform.forward;
        camForward.y = 0f;

        if (camForward.sqrMagnitude > 0.001f)
        {
            transform.rotation = Quaternion.LookRotation(camForward);
        }

        float camPitch = mainCam.transform.eulerAngles.x;
        if (camPitch > 180f) camPitch -= 360f;
        camPitch = Mathf.Clamp(camPitch, 0f, 89f);

        transform.Rotate(camPitch * tiltMultiplier, 0f, 0f, Space.Self);
    }
}