Shader "TheVayuputra/ToonShader"
{
    Properties
    {
        [MainTexture] _BaseMap ("Texture", 2D) = "white" {}
        [MainColor] _BaseColor ("Color", Color) = (0.5,0.5,0.5,1)
        [HideInInspector] _DamageFlashColor ("Damage Flash Color", Color) = (1,1,1,1)
        [HideInInspector] _DamageFlashAmount ("Damage Flash Amount", Range(0, 1)) = 0

        _ShadeThreshold ("Shade Threshold", Range(0, 1)) = 0.5
        _ShadeSoftness ("Shade Softness", Range(0, 1)) = 0.04

        _GlossThreshold ("Gloss Threshold", Range(0, 1)) = 0.6
        _GlossSoftness ("Gloss Softness", Range(0, 1)) = 0.05
        [HDR]_GlossTint ("Gloss Tint", Color) = (1,1,1,1)

    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            Name "ForwardPass"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float _ShadeThreshold;
                float _ShadeSoftness;
                float _GlossThreshold;
                float _GlossSoftness;
                float4 _GlossTint;
            CBUFFER_END

            UNITY_INSTANCING_BUFFER_START(EnemyInstanceProperties)
                UNITY_DEFINE_INSTANCED_PROP(float4, _DamageFlashColor)
                UNITY_DEFINE_INSTANCED_PROP(float, _DamageFlashAmount)
            UNITY_INSTANCING_BUFFER_END(EnemyInstanceProperties)

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 viewDirWS : TEXCOORD2;
                half fogCoord : TEXCOORD3;
                float3 positionWS : TEXCOORD4;
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;

                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                VertexPositionInputs posInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);

                output.positionCS = posInput.positionCS;
                output.positionWS = posInput.positionWS;
                output.normalWS = normInput.normalWS;
                output.viewDirWS = GetCameraPositionWS() - posInput.positionWS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);

                output.fogCoord = ComputeFogFactor(posInput.positionCS.z);

                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                float3 N = normalize(input.normalWS);
                float3 V = normalize(input.viewDirWS);
                float3 L = normalize(_MainLightPosition.xyz);
                float3 H = normalize(V + L);

                float NL = dot(N, L) * 0.5 + 0.5;
                float NH = dot(N, H);

                float3 baseMap = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb;

                float shadeFactor = smoothstep(_ShadeThreshold - _ShadeSoftness,
                                               _ShadeThreshold + _ShadeSoftness, NL);

                float glossFactor = smoothstep(
                    (1 - _GlossThreshold * 0.05) - _GlossSoftness * 0.05,
                    (1 - _GlossThreshold * 0.05) + _GlossSoftness * 0.05,
                    NH
                );

                // Computed per-fragment (not interpolated from the vertex stage) so cascade
                // selection is correct per pixel; a per-vertex shadow coordinate is only valid
                // when a single cascade covers the whole triangle, and produces visibly warped,
                // blocky shadows once the object spans or crosses a cascade boundary.
                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                float shadow = MainLightRealtimeShadow(shadowCoord);

                float3 diffuse = _MainLightColor.rgb * baseMap * _BaseColor.rgb * shadeFactor * shadow;
                float3 specular = _GlossTint.rgb * shadow * shadeFactor * glossFactor;
                float3 ambient = SampleSH(N) * _BaseColor.rgb * baseMap;

                float3 finalColor = diffuse + ambient + specular;
                float3 damageFlashColor = UNITY_ACCESS_INSTANCED_PROP(
                    EnemyInstanceProperties,
                    _DamageFlashColor).rgb;
                float damageFlashAmount = UNITY_ACCESS_INSTANCED_PROP(
                    EnemyInstanceProperties,
                    _DamageFlashAmount);
                finalColor = lerp(finalColor, damageFlashColor, saturate(damageFlashAmount));
                finalColor = MixFog(finalColor, input.fogCoord);

                return float4(finalColor, 1.0);
            }

            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
    }
}
