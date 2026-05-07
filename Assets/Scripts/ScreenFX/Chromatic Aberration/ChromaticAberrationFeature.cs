using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

public class ChromaticAberrationFeature : ScriptableRendererFeature
{
    [Tooltip("Assign the 'Hidden/ChromaticAberration' shader here.")]
    [SerializeField] private Shader shader;

    private Material              material;
    private ChromaticAberrationPass pass;

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
        var ct = renderingData.cameraData.cameraType;
        if (ct == CameraType.Preview || ct == CameraType.SceneView) return;
        renderer.EnqueuePass(pass);
    }

    protected override void Dispose(bool disposing)
    {
        CoreUtils.Destroy(material);
    }

    // ── Inner Pass ────────────────────────────────────────────────────────────
    sealed class ChromaticAberrationPass : ScriptableRenderPass
    {
        private readonly Material material;
        private static readonly int CAIntensityId = Shader.PropertyToID("_CAIntensity");

        // Render graph pass data structs
        class CopyData   { public TextureHandle source; }
        class EffectData { public TextureHandle source; public Material material; }

        internal ChromaticAberrationPass(Material mat)
        {
            material        = mat;
            renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (Shader.GetGlobalFloat(CAIntensityId) < 0.001f) return;

            var resourceData = frameData.Get<UniversalResourceData>();

            // Skip when URP renders directly to the backbuffer.
            if (resourceData.isActiveTargetBackBuffer) return;

            TextureHandle activeColor = resourceData.activeColorTexture;

            // Create a temp texture matching the active colour buffer.
            var tempDesc         = renderGraph.GetTextureDesc(activeColor);
            tempDesc.name        = "_CATempCopy";
            tempDesc.clearBuffer = false;
            TextureHandle tempTexture = renderGraph.CreateTexture(tempDesc);

            // ── Pass 1: copy activeColor → tempTexture (plain blit, no material) ──
            using (var builder = renderGraph.AddRasterRenderPass<CopyData>("CA_Copy", out var passData))
            {
                passData.source = activeColor;
                builder.UseTexture(passData.source);
                builder.SetRenderAttachment(tempTexture, 0);
                builder.SetRenderFunc((CopyData data, RasterGraphContext ctx) =>
                {
                    Blitter.BlitTexture(ctx.cmd, data.source, new Vector4(1, 1, 0, 0), 0, false);
                });
            }

            // ── Pass 2: blit tempTexture → activeColor through the CA shader ─────
            using (var builder = renderGraph.AddRasterRenderPass<EffectData>("CA_Effect", out var passData))
            {
                passData.source   = tempTexture;
                passData.material = material;
                builder.UseTexture(passData.source);
                builder.SetRenderAttachment(activeColor, 0);
                builder.SetRenderFunc((EffectData data, RasterGraphContext ctx) =>
                {
                    Blitter.BlitTexture(ctx.cmd, data.source, new Vector4(1, 1, 0, 0), data.material, 0);
                });
            }
        }
    }
}