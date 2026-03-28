using UnityEngine;
using Unity.Cinemachine;

public class CameraShakeManager : MonoBehaviour
{
    public static CameraShakeManager Instance;

    [Header("Impulse (Dash / Hit)")]
    [SerializeField] private CinemachineImpulseSource impulseSource;

    [Header("Running Shake")]
    [SerializeField] private CinemachineCamera cinemachineCamera;
    [SerializeField] private float runShakeAmplitude = 0.3f;
    [SerializeField] private float runShakeFrequency = 1.2f;
    [SerializeField] private float shakeSmoothing = 5f;

    [Header("Impulse Strengths")]
    [SerializeField] public float dashShakeForce = 0.08f;
    [SerializeField] public float hitShakeForce = 0.15f;

    private CinemachineBasicMultiChannelPerlin _perlin;
    private float _targetAmplitude;

    private void Awake()
    {
        Instance = this;

        if (cinemachineCamera != null)
            _perlin = cinemachineCamera.GetComponent<CinemachineBasicMultiChannelPerlin>();
    }

    private void Update()
    {
        // Smoothly lerp the running noise amplitude toward target
        if (_perlin != null)
            _perlin.AmplitudeGain = Mathf.Lerp(_perlin.AmplitudeGain, _targetAmplitude, Time.deltaTime * shakeSmoothing);
    }

    // Call this every frame from PlayerController with current speed
    public void SetRunningShake(float speed, float moveSpeedThreshold)
    {
        _targetAmplitude = speed > moveSpeedThreshold * 0.3f ? runShakeAmplitude : 0f;
        if (_perlin != null) _perlin.FrequencyGain = runShakeFrequency;
    }

    // Call this for one-shot shakes (dash, hit)
    public void ShakeImpulse(float force)
    {
        impulseSource?.GenerateImpulse(force);
    }
}