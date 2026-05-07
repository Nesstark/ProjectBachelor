Shader "Hidden/ChromaticAberration"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off Blend Off

        Pass
        {
            Name "ChromaticAberration"

            HLSLPROGRAM
            #pragma vertex   Vert
            #pragma fragment Frag

            // Provides: Varyings, Vert(), _BlitTexture (TEXTURE2D_X), SAMPLE_TEXTURE2D_X
            // Core.hlsl must come first — it defines TEXTURE2D_X and all platform API macros
            // that Blit.hlsl depends on.
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            // Driven by CognitiveLoadVignetteController via Shader.SetGlobalFloat.
            float _CAIntensity;

            // Controls where the "zero offset" ring sits.
            //   0.0  → effect is zero at exact screen centre, grows linearly to edges (default).
            //   0.3  → zero-ring shrinks inward; mid-screen starts showing aberration.
            //  -0.5  → zero-ring is pushed outside the screen; every pixel gets some offset.
            // Recommended: start around 0.3–0.5 to keep CA visible alongside the vignette.
            float _CAInnerRadius;

            // Maximum UV offset at intensity = 1 and dist = 1. Tune to taste.
            #define CA_MAX_OFFSET 0.015

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv         = input.texcoord;
                float2 centeredUV = uv - 0.5;
                float  dist       = length(centeredUV);     // 0 at centre, ~0.707 at corner

                // Remap so the "no-effect" ring moves inward.
                // _CAInnerRadius = 0 → identical to old linear formula.
                // As it grows, mid-screen picks up aberration too.
                float remappedDist = saturate(dist + _CAInnerRadius);

                // Preserve radial direction; guard divide-by-zero at exact centre.
                float2 dir = (centeredUV / max(dist, 0.0001)) * remappedDist
                             * (_CAIntensity * CA_MAX_OFFSET);

                half r = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + dir).r;
                half g = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv      ).g;
                half b = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv - dir).b;
                half a = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv      ).a;

                return half4(r, g, b, a);
            }
            ENDHLSL
        }
    }
}
