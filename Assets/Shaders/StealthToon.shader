Shader "FrogGod/Stealth Toon"
{
    Properties
    {
        [MainTexture] _BaseMap ("Texture", 2D) = "white" {}
        [MainColor] _BaseColor ("Color", Color) = (0.5,0.5,0.5,1)
        [HideInInspector] _DamageFlashColor ("Damage Flash Color", Color) = (1,1,1,1)
        [HideInInspector] _DamageFlashAmount ("Damage Flash Amount", Range(0, 1)) = 0
        [HideInInspector] _StealthAlpha ("Stealth Alpha", Range(0, 1)) = 1
        _ShadeThreshold ("Shade Threshold", Range(0, 1)) = 0.5
        _ShadeSoftness ("Shade Softness", Range(0, 1)) = 0.04
        _GlossThreshold ("Gloss Threshold", Range(0, 1)) = 0.6
        _GlossSoftness ("Gloss Softness", Range(0, 1)) = 0.05
        [HDR]_GlossTint ("Gloss Tint", Color) = (1,1,1,1)
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            Name "ForwardPass"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            // URP resolves main light shadows out of the screen-space shadow texture only
            // for opaque surfaces. This shader is alpha-blended, so it has to be flagged as
            // transparent before Shadows.hlsl is included or the _MAIN_LIGHT_SHADOWS_SCREEN
            // variant samples a texture that was never written for it.
            #define _SURFACE_TYPE_TRANSPARENT 1

            // Brings in Core/Lighting/Shadows plus the whole baked-lighting keyword set.
            // A transparent renderer is never lightmapped, but it does read Adaptive Probe
            // Volumes - which the old SampleSH() call could not do, so this shader used to
            // fall back to the flat skybox ambient probe wherever it stood.
            #include_with_pragmas "Assets/Shaders/ToonBakedLighting.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float _ShadeThreshold;
                float _ShadeSoftness;
                float _GlossThreshold;
                float _GlossSoftness;
                float4 _GlossTint;
                float4 _DamageFlashColor;
                float _DamageFlashAmount;
                float _StealthAlpha;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
                float2 staticLightmapUV : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 viewDirWS : TEXCOORD2;
                half fogCoord : TEXCOORD3;
                float3 positionWS : TEXCOORD4;
                // Becomes a lightmap UV when LIGHTMAP_ON, else per-vertex SH.
                DECLARE_LIGHTMAP_OR_SH(staticLightmapUV, vertexSH, 5);
                TOON_PROBE_OCCLUSION_VARYING(6)
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normal = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.normalWS = normal.normalWS;
                output.viewDirWS = GetCameraPositionWS() - position.positionWS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.fogCoord = ComputeFogFactor(position.positionCS.z);

                TOON_TRANSFER_BAKED(input, output, position.positionWS, output.normalWS);

                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                float3 normal = normalize(input.normalWS);
                float3 viewDirection = normalize(input.viewDirWS);
                float3 lightDirection = normalize(_MainLightPosition.xyz);
                float3 halfDirection = normalize(viewDirection + lightDirection);
                float lightDot = dot(normal, lightDirection) * 0.5 + 0.5;
                float halfDot = dot(normal, halfDirection);
                float3 baseMap = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb;
                float shade = smoothstep(_ShadeThreshold - _ShadeSoftness, _ShadeThreshold + _ShadeSoftness, lightDot);
                float gloss = smoothstep(
                    (1 - _GlossThreshold * 0.05) - _GlossSoftness * 0.05,
                    (1 - _GlossThreshold * 0.05) + _GlossSoftness * 0.05,
                    halfDot);

                // Baked indirect light, plus the baked occlusion of the mixed main light.
                // The mask defaults to "lit" so variants carrying no baked shadow data fall
                // back to the realtime shadow alone rather than to black.
                half4 shadowMask = half4(1, 1, 1, 1);
                half3 bakedGI = half3(0, 0, 0);
                TOON_RESOLVE_BAKED(input, normal, input.positionWS, viewDirection, bakedGI, shadowMask);

                // Mixes the realtime shadow with the baked one and cross-fades to baked at the
                // shadow distance, so the silhouette keeps shadowing past the last cascade.
                float shadow = MainLightShadow(
                    TransformWorldToShadowCoord(input.positionWS),
                    input.positionWS,
                    shadowMask,
                    _MainLightOcclusionProbes);

                float3 diffuse = _MainLightColor.rgb * baseMap * _BaseColor.rgb * shade * shadow;
                float3 specular = _GlossTint.rgb * shadow * shade * gloss;
                float3 ambient = bakedGI * _BaseColor.rgb * baseMap;
                float3 color = lerp(diffuse + ambient + specular, _DamageFlashColor.rgb, saturate(_DamageFlashAmount));
                return float4(MixFog(color, input.fogCoord), _BaseColor.a * _StealthAlpha);
            }
            ENDHLSL
        }
    }
}
