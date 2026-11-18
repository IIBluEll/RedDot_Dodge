Shader "RedDot Dodge/Player Trail Neon"
{
    Properties
    {
        _Intensity ("Neon Intensity", Range(0, 8)) = 3.2
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        Blend SrcAlpha One
        Cull Off
        ZWrite Off

        Pass
        {
            Tags { "LightMode" = "SRPDefaultUnlit" }
            HLSLPROGRAM
            #pragma vertex TrailVertex
            #pragma fragment TrailFragment
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half _Intensity;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings TrailVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            half4 TrailFragment(Varyings input) : SV_Target
            {
                half distance = abs(input.uv.y * 2.0h - 1.0h);
                half core = exp2(-distance * distance * 22.0h);
                half halo = exp2(-distance * distance * 4.0h) * (1.0h - smoothstep(0.75h, 1.0h, distance));
                half3 color = input.color.rgb * (core * 0.85h + halo * 0.35h) * _Intensity;
                return half4(color, input.color.a * halo);
            }
            ENDHLSL
        }
    }
}
