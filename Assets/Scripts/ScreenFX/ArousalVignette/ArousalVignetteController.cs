using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class ArousalVignetteController : MonoBehaviour
{
    [Header("Vignette Range")]
    [Range(0f, 1f)] [SerializeField] private float minIntensity = 0.05f;
    [Range(0f, 1f)] [SerializeField] private float maxIntensity = 0.65f;

    [Header("Transition")]
    [SerializeField] private float lerpSpeed = 2f;

    private Vignette vignette;
    private RppgReceiver bio;
    private float targetIntensity;

    void Start()
    {
        bio = FindFirstObjectByType<RppgReceiver>();

        Volume volume = GetComponent<Volume>();

        if (!volume.profile.TryGet(out vignette))
            Debug.LogError("ArousalVignetteController: No Vignette override found on the Global Volume profile.");
    }

    void Update()
    {
        if (bio == null || vignette == null) return;

        // Map arousalScore (0–1) directly to the configured intensity range.
        // Falls back to minIntensity when the signal is invalid or baseline isn't ready.
        targetIntensity = bio.signalValid
            ? Mathf.Lerp(minIntensity, maxIntensity, bio.arousalScore)
            : minIntensity;

        vignette.intensity.value = Mathf.Lerp(
            vignette.intensity.value,
            targetIntensity,
            Time.deltaTime * lerpSpeed
        );
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (!Application.isPlaying || vignette == null) return;
        vignette.intensity.value = targetIntensity;
    }
#endif
}