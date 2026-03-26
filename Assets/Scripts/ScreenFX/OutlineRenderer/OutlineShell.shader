Shader "Custom/OutlineShell"
{
    Properties
    {
        _OutlineWidth ("Outline Width", Range(0, 0.1)) = 0.015
        _OutlineColor ("Outline Color", Color) = (0, 0, 0, 1)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Geometry-1" }

        Cull Front  // Only render back faces
        ZWrite On

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _OutlineWidth;
                half4 _OutlineColor;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings   { float4 positionHCS : SV_POSITION; };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                // Push vertices out along normals to create the shell
                float3 expandedPos = IN.positionOS.xyz + IN.normalOS * _OutlineWidth;
                OUT.positionHCS = TransformObjectToHClip(expandedPos);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target { return _OutlineColor; }
            ENDHLSL
        }
    }
}