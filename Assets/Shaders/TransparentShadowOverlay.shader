Shader "FrogGod/Grass Shadow Cutout"
{
    Properties
    {
        [MainTexture] _BaseMap ("Base Texture", 2D) = "white" {}
        [MainColor] _BaseColor ("Tint", Color) = (1, 1, 1, 1)
        _ShadowStrength ("Shadow Strength", Range(0, 1)) = 0.65
        _AlphaCutoff ("Alpha Cutoff", Range(0, 1)) = 0.01
        _TextureBlur ("Texture Blur", Range(0, 10)) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "TransparentCutout"
            "Queue" = "AlphaTest"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "GrassShadowCutout"
            Tags { "LightMode" = "UniversalForward" }

            ZWrite On
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half fogFactor : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float4 screenPos : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            float4 _BaseMap_TexelSize;

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half _ShadowStrength;
                half _AlphaCutoff;
                half _TextureBlur;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionHCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.fogFactor = ComputeFogFactor(positionInputs.positionCS.z);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.screenPos = ComputeScreenPos(positionInputs.positionCS);
                return output;
            }

            half3 SampleBlurredTexture(float2 uv, half4 center)
            {
                if (_TextureBlur <= 0.0h)
                {
                    return center.rgb;
                }

                float2 offset = _BaseMap_TexelSize.xy * _TextureBlur;
                half4 north = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv + float2(0, offset.y));
                half4 south = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv - float2(0, offset.y));
                half4 east = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv + float2(offset.x, 0));
                half4 west = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv - float2(offset.x, 0));

                half weight = center.a * 4.0h + north.a + south.a + east.a + west.a;
                half3 weightedColor = center.rgb * center.a * 4.0h
                    + north.rgb * north.a
                    + south.rgb * south.a
                    + east.rgb * east.a
                    + west.rgb * west.a;
                return weightedColor / max(weight, 0.0001h);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                half4 source = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                half alpha = source.a * _BaseColor.a;
                clip(alpha - _AlphaCutoff);
                half3 color = SampleBlurredTexture(input.uv, source) * _BaseColor.rgb;
#if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                half shadow = SampleScreenSpaceShadowmap(input.screenPos);
#else
                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                half shadow = mainLight.shadowAttenuation;
#endif
                shadow = lerp(1.0h, shadow, _ShadowStrength);
                color = MixFog(color * shadow, input.fogFactor);
                return half4(color, 1.0h);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
