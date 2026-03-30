Shader "Custom/SpriteVerticalFade"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        [Header(Fade Settings)]
        _FadeStart ("Fade Start (UV Y)", Range(0, 1)) = 0.0
        _FadeEnd   ("Fade End   (UV Y)", Range(0, 1)) = 0.5
        _FadeColor ("Fade Color", Color) = (0,0,0,1)
    }

    SubShader
    {
        Tags
        {
            "Queue"           = "Transparent"
            "RenderType"      = "Transparent"
            "RenderPipeline"  = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4  _Color;
                float  _FadeStart;
                float  _FadeEnd;
                half4  _FadeColor;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                half4  color      : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                half4  color       : COLOR;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv          = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.color       = IN.color;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv) * _Color * IN.color;

                // t = 0 at bottom (FadeStart), t = 1 at FadeEnd → full color
                float t = saturate((IN.uv.y - _FadeStart) / max(_FadeEnd - _FadeStart, 0.001));

                half3 finalRGB = lerp(_FadeColor.rgb, texColor.rgb, t);
                return half4(finalRGB, texColor.a);
            }
            ENDHLSL
        }
    }
}