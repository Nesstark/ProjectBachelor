Shader "Custom/SpriteStencilOnly"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _AlphaClip ("Alpha Clip", Range(0,1)) = 0.1
    }
    SubShader
    {
        Tags
        {
            "Queue" = "Geometry-1"
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
        }

        ColorMask 0    // Write nothing to the color buffer
        ZWrite Off
        Cull Off

        Stencil
        {
            Ref 1
            Comp Always
            Pass Replace   // Stamp 1 where the sprite silhouette is
        }

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float _AlphaClip;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Varyings  { float4 positionHCS : SV_POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.color = IN.color;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv) * IN.color;
                clip(col.a - _AlphaClip); // Don't stamp stencil on transparent sprite edges
                return 0;
            }
            ENDHLSL
        }
    }
}