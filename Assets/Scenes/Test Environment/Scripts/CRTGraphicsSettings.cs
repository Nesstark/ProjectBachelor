using UnityEngine;
using UnityEngine.Rendering.Universal;

// Made with Claude Sonnet 4.5

public class CRTGraphicsSettings : MonoBehaviour
{
    [Header("Renderer Setup")]
    [SerializeField] private UniversalRendererData rendererData; // Your PC_Renderer asset
    
    private ScriptableRendererFeature crtFilterFeature;
    private int crtFeatureIndex = 1; // Change this to match your feature's position
    
    void Start()
    {
        // Get reference to your Full Screen Pass Renderer Feature
        if (rendererData != null && rendererData.rendererFeatures.Count > crtFeatureIndex)
        {
            crtFilterFeature = rendererData.rendererFeatures[crtFeatureIndex];
        }
    }
    
    public void EnableCRTFilter()
    {
        if (crtFilterFeature != null)
        {
            crtFilterFeature.SetActive(true);
        }
    }
    
    public void DisableCRTFilter()
    {
        if (crtFilterFeature != null)
        {
            crtFilterFeature.SetActive(false);
        }
    }
}