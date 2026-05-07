using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Fullscreen chromatic aberration post-process pass for URP.
///
/// Setup:
///   1. Add this feature to your URP Renderer asset
///      (Project Settings → Graphics → URP Asset → Renderer → Add Renderer Feature).
///   2. Assign the Hidden/ChromaticAberration shader in the feature's inspector slot.
///   3. Attach CognitiveLoadVignetteController to your Global Volume GameObject —
///      it will drive _CAIntensity automatically.
/// </summary>
public class ChromaticAberrationFeature : ScriptableRendererFeature
{
    // ── Inspector ────────────────────────────────────────────────────────────
    [Tooltip("Assign the 'Hidden/ChromaticAberration' shader here.")]
    [SerializeField] private Shader shader;

    // ── Private State ────────────────────────────────────────────────────────
    private Material material;
    private ChromaticAberrationPass pass;

    // ────────────────────────────────────────────────────────────────────────
    public override void Create()
    {
        if (shader == null)
        {
            Debug.LogWarning("ChromaticAberrationFeature: No shader assigned.");
            return;
        }

        material = CoreUtils.CreateEngineMaterial(shader);
        pass     = new ChromaticAberrationPass(material);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (material == null) return;

        // Skip scene-view camera to avoid cluttering editor workflows.
        var cameraType = renderingData.cameraData.cameraType;
        if (cameraType == CameraType.Preview || cameraType == CameraType.SceneView) return;

        renderer.EnqueuePass(pass);
    }

    protected override void Dispose(bool disposing)
    {
        CoreUtils.Destroy(material);
        pass?.Dispose();
    }

    // ── Inner Render Pass ────────────────────────────────────────────────────
    sealed class ChromaticAberrationPass : ScriptableRenderPass
    {
        private readonly Material          material;
        private          RTHandle          tempRT;

        private static readonly int            CAIntensityId = Shader.PropertyToID("_CAIntensity");
        private static readonly ProfilingSampler Sampler     = new("Chromatic Aberration");

        internal ChromaticAberrationPass(Material mat)
        {
            material         = mat;
            renderPassEvent  = RenderPassEvent.AfterRenderingPostProcessing;
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            var desc              = renderingData.cameraData.cameraTargetDescriptor;
            desc.depthBufferBits  = 0;
            RenderingUtils.ReAllocateIfNeeded(ref tempRT, desc, FilterMode.Bilinear, name: "_CATempRT");
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            // Skip entirely when intensity is negligible — no GPU cost at rest.
            if (Shader.GetGlobalFloat(CAIntensityId) < 0.001f) return;

            var cmd = CommandBufferPool.Get();
            using (new ProfilingScope(cmd, Sampler))
            {
                var source = renderingData.cameraData.renderer.cameraColorTargetHandle;
                Blitter.BlitCameraTexture(cmd, source, tempRT, material, 0);
                Blitter.BlitCameraTexture(cmd, tempRT, source);
            }
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        internal void Dispose() => tempRT?.Release();
    }
}
