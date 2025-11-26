Shader "Custom/WireframeBarycentricURP"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0,0,0,0)
        _WireColor ("Wire Color", Color) = (0,1,1,1)
        _Thickness ("Line Thickness", Range(0.1, 3.0)) = 0.8
        _Bias ("Edge Bias", Range(0.0, 1.0)) = 0.0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Back

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv0        : TEXCOORD0;
                float2 uv2_xy     : TEXCOORD1; // bary x,y
                float2 uv3_xz     : TEXCOORD2; // bary z, unused
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 bary        : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _WireColor;
                float _Thickness;
                float _Bias;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs posInputs = GetVertexPositionInputs(IN.positionOS);
                VertexNormalInputs normInputs = GetVertexNormalInputs(IN.normalOS);

                OUT.positionHCS = posInputs.positionCS;
                OUT.normalWS    = normInputs.normalWS;

                // Reconstruct barycentrics (x,y from UV2, z from UV3.x)
                OUT.bary = float3(IN.uv2_xy.x, IN.uv2_xy.y, IN.uv3_xz.x);

                return OUT;
            }

            // Simple edge function: smaller min(bary) => closer to edge
            float edgeFactor(float3 bary, float thickness, float bias)
            {
                float m = min(bary.x, min(bary.y, bary.z));
                // smoothstep to control thickness; bias pushes edge inward/outward
                float e = smoothstep(thickness + bias, bias, m);
                return saturate(e);
            }

            float4 frag(Varyings IN) : SV_Target
            {
                float e = edgeFactor(IN.bary, _Thickness * 0.02, _Bias); // scale thickness for typical mesh sizes
                float4 baseCol = _BaseColor;
                float4 wireCol = _WireColor;

                // Blend wire over base
                float4 col = lerp(baseCol, wireCol, e);

                // Optional: fade base for clearer wires
                col.a = max(col.a, wireCol.a);

                return col;
            }
            ENDHLSL
        }
    }
}