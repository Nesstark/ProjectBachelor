using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Drives the URP Vignette intensity from the RppgReceiver cognitiveLoadScore (range 0–1).
///
/// The mapping is V-shaped:
///   score ≤ lowSatPoint    →  lowArousalIntensity    (strong – understimulated)
///   score == neutralScore   →  neutralIntensity        (barely visible – calm)
///   score ≥ highSatPoint   →  highArousalIntensity   (strong – overstimulated)
///
/// Between those anchor points the intensity is linearly interpolated.
/// All transitions are smoothed with a configurable lerp speed.
/// </summary>
public class ArousalVignetteController : MonoBehaviour
{
    // ── Intensity Sliders ────────────────────────────────────────────────────
    [Header("Vignette Intensity")]
    [Tooltip("Intensity at the calm/neutral score (~0.275). Keep this very low.")]
    [Range(0f, 1f)] [SerializeField] private float neutralIntensity = 0.04f;

    [Tooltip("Maximum intensity when cognitive load is very LOW (≤ low saturation point). " +
             "This is the understimulated / disengaged state.")]
    [Range(0f, 1f)] [SerializeField] private float lowArousalIntensity = 0.50f;

    [Tooltip("Maximum intensity when cognitive load is very HIGH (≥ high saturation point). " +
             "This is the overstimulated / stressed state.")]
    [Range(0f, 1f)] [SerializeField] private float highArousalIntensity = 0.55f;

    // ── Score Thresholds (cognitiveLoadScore is always 0–1) ──────────────────
    [Header("Score Thresholds  (score range: 0 – 1)")]
    [Tooltip("Score at which the vignette is at its minimum (the calm midpoint).")]
    [SerializeField] private float neutralScore = 0.275f;

    [Tooltip("Scores at or below this value are treated as fully low-arousal (understimulated).")]
    [SerializeField] private float lowSatPoint = 0.15f;

    [Tooltip("Scores at or above this value are treated as fully high-arousal (overstimulated). " +
             "cognitiveLoadScore maxes out at 1.0, so keep this below 1.")]
    [SerializeField] private float highSatPoint = 0.75f;

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
            Debug.LogError("ArousalVignetteController: No RppgReceiver found in scene.");

        Volume volume = GetComponent<Volume>();
        if (volume == null || !volume.profile.TryGet(out vignette))
            Debug.LogError("ArousalVignetteController: No Vignette override found on the Global Volume profile.");
    }

    void Update()
    {
        if (bio == null || vignette == null) return;

        // Before baseline is ready or signal is invalid, sit quietly at neutral.
        targetIntensity = (bio.signalValid && bio.baselineReady)
            ? ScoreToIntensity(bio.cognitiveLoadScore)
            : neutralIntensity;

        // Gradual lerp — never snaps instantly.
        vignette.intensity.value = Mathf.Lerp(
            vignette.intensity.value,
            targetIntensity,
            Time.deltaTime * lerpSpeed
        );
    }

    /// <summary>
    /// Maps a 0–1 cognitive-load score to a vignette intensity using a V-curve.
    ///
    ///   score ≤ lowSatPoint          →  lowArousalIntensity   (left plateau)
    ///   lowSatPoint → neutralScore   →  lerp up to neutralIntensity
    ///   neutralScore → highSatPoint  →  lerp up to highArousalIntensity
    ///   score ≥ highSatPoint         →  highArousalIntensity  (right plateau)
    ///
    /// </summary>
    private float ScoreToIntensity(float score)
    {
        // Left plateau — fully understimulated
        if (score <= lowSatPoint)
            return lowArousalIntensity;

        // Left arm — rising from low extreme toward calm centre
        if (score < neutralScore)
        {
            float t = Mathf.InverseLerp(lowSatPoint, neutralScore, score);
            return Mathf.Lerp(lowArousalIntensity, neutralIntensity, t);
        }

        // Right plateau — fully overstimulated
        if (score >= highSatPoint)
            return highArousalIntensity;

        // Right arm — rising from calm centre toward high extreme
        float t2 = Mathf.InverseLerp(neutralScore, highSatPoint, score);
        return Mathf.Lerp(neutralIntensity, highArousalIntensity, t2);
    }

// ── Editor live-preview ──────────────────────────────────────────────────
#if UNITY_EDITOR
    void OnValidate()
    {
        // Guard against inverted or nonsensical thresholds.
        lowSatPoint  = Mathf.Clamp(lowSatPoint,  0f, neutralScore - 0.01f);
        highSatPoint = Mathf.Clamp(highSatPoint, neutralScore + 0.01f, 1f);

        if (!Application.isPlaying || vignette == null) return;
        vignette.intensity.value = targetIntensity;
    }
#endif
}