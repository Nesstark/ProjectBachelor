using UnityEngine;
using UnityEngine.Rendering.Universal;

// Made with Claude Sonnet 4.6

public class CRTGraphicsSettings : MonoBehaviour
{
    [Header("Renderer Setup")]
    [SerializeField] private UniversalRendererData rendererData;

    private ScriptableRendererFeature crtFilterFeature;
    private int crtFeatureIndex = 1;
    private bool initialActiveState; // Gem starttilstanden

    void Start()
    {
        if (rendererData != null && rendererData.rendererFeatures.Count > crtFeatureIndex)
        {
            crtFilterFeature = rendererData.rendererFeatures[crtFeatureIndex];
            initialActiveState = crtFilterFeature.isActive; // Husk hvad den var
        }
    }

    // Kaldes automatisk når objektet destroyes (inkl. når Play Mode stopper)
    void OnDestroy()
    {
        if (crtFilterFeature != null)
            crtFilterFeature.SetActive(initialActiveState); // Gendan til original tilstand
    }

    public void EnableCRTFilter()
    {
        if (crtFilterFeature != null)
            crtFilterFeature.SetActive(true);
    }

    public void DisableCRTFilter()
    {
        if (crtFilterFeature != null)
            crtFilterFeature.SetActive(false);
    }
}
