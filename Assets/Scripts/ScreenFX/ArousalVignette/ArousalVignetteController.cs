using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class ArousalVignetteController : MonoBehaviour
{
    [Header("Vignette Intensity Per Arousal Level")]
    [Range(0f, 1f)] [SerializeField] private float lowArousalIntensity    = 0.1f;
    [Range(0f, 1f)] [SerializeField] private float mediumArousalIntensity = 0.3f;
    [Range(0f, 1f)] [SerializeField] private float highArousalIntensity   = 0.6f;

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

        targetIntensity = bio.arousalLabel switch
        {
            "Low"    => lowArousalIntensity,
            "Medium" => mediumArousalIntensity,
            "High"   => highArousalIntensity,
            _        => mediumArousalIntensity
        };

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