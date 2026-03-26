Shader "Custom/SpriteOutline"
{
    Properties
    {
        _MainTex       ("Sprite",         2D)           = "white" {}
        _OutlineColor  ("Outline Color",  Color)        = (0,0,0,1)
        _OutlineWidth  ("Outline Width",  Range(0,5))   = 1.5
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent"
               "IgnoreProjector"="True" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _MainTex_TexelSize;
                half4  _OutlineColor;
                float  _OutlineWidth;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Varyings   { float4 positionHCS : SV_POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv    = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.color = IN.color;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv) * IN.color;

                // Sample 8 neighbours to detect sprite edge
                float2 offset = _MainTex_TexelSize.xy * _OutlineWidth;
                float  neighbourAlpha = 0;
                neighbourAlpha += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2( offset.x,  0)).a;
                neighbourAlpha += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2(-offset.x,  0)).a;
                neighbourAlpha += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2( 0,  offset.y)).a;
                neighbourAlpha += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2( 0, -offset.y)).a;
                neighbourAlpha += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2( offset.x,  offset.y)).a;
                neighbourAlpha += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2(-offset.x,  offset.y)).a;
                neighbourAlpha += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2( offset.x, -offset.y)).a;
                neighbourAlpha += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2(-offset.x, -offset.y)).a;

                // If this pixel is transparent but neighbours are not → it's an outline pixel
                if (col.a < 0.1 && neighbourAlpha > 0.1)
                    return _OutlineColor;

                return col;
            }
            ENDHLSL
        }
    }
}