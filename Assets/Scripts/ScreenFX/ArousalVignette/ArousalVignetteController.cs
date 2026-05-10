using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Drives the URP Vignette intensity and custom Chromatic Aberration intensity
/// from RppgReceiver.cognitiveLoadScore (range 0–1).
/// 
/// Mapping is a single linear lerp for both effects:
///   score = 0  →  min value
///   score = 1  →  max value
///
/// All transitions are smoothed with configurable lerp speeds.
/// </summary>
public class CognitiveLoadVignetteController : MonoBehaviour
{
    // ── Vignette Intensity ───────────────────────────────────────────────────
    [Header("Vignette Intensity")]
    [Tooltip("Intensity when cognitiveLoadScore is 0 (lowest cognitive load).")]
    [Range(0f, 1f)] [SerializeField] private float minVignetteIntensity = 0.0f;

    [Tooltip("Intensity when cognitiveLoadScore is 1 (highest cognitive load).")]
    [Range(0f, 1f)] [SerializeField] private float maxVignetteIntensity = 0.55f;

    // ── Chromatic Aberration Intensity ───────────────────────────────────────
    [Header("Chromatic Aberration Intensity")]
    [Tooltip("CA intensity when cognitiveLoadScore is 0 (lowest cognitive load).")]
    [Range(0f, 1f)] [SerializeField] private float minCAIntensity = 0.0f;

    [Tooltip("CA intensity when cognitiveLoadScore is 1 (highest cognitive load).")]
    [Range(0f, 1f)] [SerializeField] private float maxCAIntensity = 0.8f;

    [Tooltip("Shrinks the CA dead-zone so the effect reaches further toward the screen centre.\n\n" +
             " 0.0 → zero offset at exact centre, grows outward linearly.\n" +
             " 0.35 → effect visible in mid-screen, good alongside vignette.\n" +
             " 0.7 → effect covers nearly the whole screen.")]
    [Range(0f, 0.7f)] [SerializeField] private float caInnerRadius = 0.35f;

    // ── Transition ───────────────────────────────────────────────────────────
    [Header("Transition")]
    [Tooltip("How quickly the vignette fades toward the target intensity. " +
             "Lower = slower/more gradual. Recommended range: 0.5 – 3.")]
    [SerializeField] private float vignetteLerpSpeed = 1.0f;

    [Tooltip("How quickly the chromatic aberration fades toward the target intensity. " +
             "Lower = slower/more gradual. Recommended range: 0.5 – 3.")]
    [SerializeField] private float caLerpSpeed = 1.0f;

    // ── Private State ────────────────────────────────────────────────────────
    private Vignette     vignette;
    private RppgReceiver bio;
    private float        targetIntensity;
    private float        currentCAIntensity;

    private static readonly int CAIntensityId   = Shader.PropertyToID("_CAIntensity");
    private static readonly int CAInnerRadiusId = Shader.PropertyToID("_CAInnerRadius");

    // ────────────────────────────────────────────────────────────────────────
    void Start()
    {
        bio = FindFirstObjectByType<RppgReceiver>();
        if (bio == null)
            Debug.LogError("CognitiveLoadVignetteController: No RppgReceiver found in scene.");

        Volume volume = GetComponent<Volume>();
        if (volume == null)
        {
            Debug.LogError("CognitiveLoadVignetteController: No Volume component found on this GameObject.");
            return;
        }

        // Clone the shared profile so we write to a runtime instance, not the asset on disk.
        volume.profile = Instantiate(volume.sharedProfile);

        if (!volume.profile.TryGet(out vignette))
        {
            Debug.LogError("CognitiveLoadVignetteController: No Vignette override found on the Global Volume profile.");
            return;
        }

        // Explicitly enable the override so URP actually applies the value.
        vignette.intensity.overrideState = true;
        vignette.intensity.value         = minVignetteIntensity;

        // Initialise CA so there is no pop on scene start.
        currentCAIntensity = minCAIntensity;
        Shader.SetGlobalFloat(CAIntensityId,   currentCAIntensity);
        Shader.SetGlobalFloat(CAInnerRadiusId, caInnerRadius);
    }

    void Update()
    {
        if (bio == null || vignette == null) return;

        // Before baseline is ready or signal is invalid, sit quietly at min intensity.
        float score = (bio.signalValid && bio.baselineReady) ? bio.cognitiveLoadScore : 0f;

        // ── Vignette ──────────────────────────────────────────────────────────
        targetIntensity = Mathf.Lerp(minVignetteIntensity, maxVignetteIntensity, score);

        vignette.intensity.value = Mathf.Lerp(
            vignette.intensity.value,
            targetIntensity,
            Time.deltaTime * vignetteLerpSpeed
        );

        // ── Chromatic Aberration ──────────────────────────────────────────────
        float targetCAIntensity = Mathf.Lerp(minCAIntensity, maxCAIntensity, score);

        currentCAIntensity = Mathf.Lerp(
            currentCAIntensity,
            targetCAIntensity,
            Time.deltaTime * caLerpSpeed
        );

        Shader.SetGlobalFloat(CAIntensityId,   currentCAIntensity);
        Shader.SetGlobalFloat(CAInnerRadiusId, caInnerRadius);
    }

    // ── Editor live-preview ──────────────────────────────────────────────────
#if UNITY_EDITOR
    void OnValidate()
    {
        if (!Application.isPlaying || vignette == null) return;
        vignette.intensity.value = targetIntensity;
        Shader.SetGlobalFloat(CAIntensityId,   currentCAIntensity);
        Shader.SetGlobalFloat(CAInnerRadiusId, caInnerRadius);
    }
#endif
}