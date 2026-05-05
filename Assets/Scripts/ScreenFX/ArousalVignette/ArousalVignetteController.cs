using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Drives the URP Vignette intensity from RppgReceiver.cognitiveLoadScore (range 0–1).
/// 
/// Mapping is a single linear lerp:
///   score = 0  →  minVignetteIntensity
///   score = 1  →  maxVignetteIntensity
///
/// All transitions are smoothed with a configurable lerp speed.
/// </summary>
public class CognitiveLoadVignetteController : MonoBehaviour
{
    // ── Intensity ────────────────────────────────────────────────────────────
    [Header("Vignette Intensity")]
    [Tooltip("Intensity when cognitiveLoadScore is 0 (lowest cognitive load).")]
    [Range(0f, 1f)] [SerializeField] private float minVignetteIntensity = 0.0f;

    [Tooltip("Intensity when cognitiveLoadScore is 1 (highest cognitive load).")]
    [Range(0f, 1f)] [SerializeField] private float maxVignetteIntensity = 0.55f;

    // ── Transition ───────────────────────────────────────────────────────────
    [Header("Transition")]
    [Tooltip("How quickly the vignette fades toward the target intensity. " +
             "Lower = slower/more gradual. Recommended range: 0.5 – 3.")]
    [SerializeField] private float lerpSpeed = 1.0f;

    // ── Private State ────────────────────────────────────────────────────────
    private Vignette vignette;
    private RppgReceiver bio;
    private float targetIntensity;

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
        vignette.intensity.value = minVignetteIntensity;
    }

    void Update()
    {
        if (bio == null || vignette == null) return;

        // Before baseline is ready or signal is invalid, sit quietly at min intensity.
        targetIntensity = (bio.signalValid && bio.baselineReady)
            ? Mathf.Lerp(minVignetteIntensity, maxVignetteIntensity, bio.cognitiveLoadScore)
            : minVignetteIntensity;

        // Gradual lerp — never snaps instantly.
        vignette.intensity.value = Mathf.Lerp(
            vignette.intensity.value,
            targetIntensity,
            Time.deltaTime * lerpSpeed
        );
    }

// ── Editor live-preview ──────────────────────────────────────────────────
#if UNITY_EDITOR
    void OnValidate()
    {
        if (!Application.isPlaying || vignette == null) return;
        vignette.intensity.value = targetIntensity;
    }
#endif
}