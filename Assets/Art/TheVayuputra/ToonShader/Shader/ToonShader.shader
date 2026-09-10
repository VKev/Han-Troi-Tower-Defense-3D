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

    // PowerVR Rogue ARMv7 devices reject the full baked-lighting variant even though they
    // report OpenGL ES 3 support. Keep a deliberately small URP forward pass first so those
    // devices use the same toon art rather than Unity's magenta error shader.
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            Name "MobileForwardPass"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma only_renderers gles3 vulkan metal
            #pragma target 2.0
            #pragma vertex MobileVert
            #pragma fragment MobileFrag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

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
            CBUFFER_END

            struct MobileAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct MobileVaryings
            {
                float2 uv : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half3 viewDirWS : TEXCOORD2;
                half fogCoord : TEXCOORD3;
                float4 positionCS : SV_POSITION;
            };

            MobileVaryings MobileVert(MobileAttributes input)
            {
                MobileVaryings output = (MobileVaryings)0;
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normal = GetVertexNormalInputs(input.normalOS, float4(1, 0, 0, 1));
                output.positionCS = position.positionCS;
                output.normalWS = normal.normalWS;
                output.viewDirWS = GetCameraPositionWS() - position.positionWS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.fogCoord = ComputeFogFactor(position.positionCS.z);
                return output;
            }

            half4 MobileFrag(MobileVaryings input) : SV_Target
            {
                half3 normal = normalize(input.normalWS);
                half3 viewDirection = normalize(input.viewDirWS);
                Light mainLight = GetMainLight();
                half lightAmount = dot(normal, mainLight.direction) * 0.5h + 0.5h;
                half shade = smoothstep(
                    _ShadeThreshold - _ShadeSoftness,
                    _ShadeThreshold + _ShadeSoftness,
                    lightAmount);
                half3 halfVector = normalize(viewDirection + mainLight.direction);
                half gloss = smoothstep(
                    (1 - _GlossThreshold * 0.05) - _GlossSoftness * 0.05,
                    (1 - _GlossThreshold * 0.05) + _GlossSoftness * 0.05,
                    dot(normal, halfVector));
                half3 baseColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb * _BaseColor.rgb;
                half3 color = baseColor * (SampleSH(normal) + mainLight.color * shade)
                    + _GlossTint.rgb * shade * gloss;
                color = lerp(color, _DamageFlashColor.rgb, saturate(_DamageFlashAmount));
                return half4(MixFog(color, input.fogCoord), 1);
            }
            ENDHLSL
        }
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            Name "ForwardPass"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            // Match URP Lit's mobile baseline so Android ARMv7 and API 30 GLES3 devices retain
            // a compiled forward variant instead of falling back to Unity's magenta error pass.
            #pragma target 2.0
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            // Select by graphics API before includes, including GLES3 on ARMv7.
            // Mobile uses lightmaps and SH without the desktop APV variants.
            #if defined(SHADER_API_MOBILE) || defined(SHADER_API_GLES3) || defined(SHADER_API_VULKAN)
                #define TOON_MOBILE_LIGHTING 1
            #endif
            #if defined(TOON_MOBILE_LIGHTING)
                #pragma multi_compile _ LIGHTMAP_ON
                #pragma multi_compile _ DIRLIGHTMAP_COMBINED
                #pragma multi_compile _ SHADOWS_SHADOWMASK
                #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
                #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
                #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH

                #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
                #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
                #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            #else
                #include_with_pragmas "Assets/Shaders/ToonBakedLighting.hlsl"
            #endif

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

            // Every material property must live in this one buffer for the SRP Batcher to
            // accept the shader. The damage flash used to sit in an instancing buffer instead,
            // which opted the shader into GPU instancing and out of the SRP Batcher - and that
            // bought nothing, because the flash is driven by a MaterialPropertyBlock, which
            // already excludes a renderer from the batcher for as long as the block is set.
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
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                float2 uv         : TEXCOORD0;
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
                #if !defined(TOON_MOBILE_LIGHTING)
                    TOON_PROBE_OCCLUSION_VARYING(6)
                #endif
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

                #if defined(TOON_MOBILE_LIGHTING)
                    OUTPUT_LIGHTMAP_UV(input.staticLightmapUV, unity_LightmapST, output.staticLightmapUV);
                    OUTPUT_SH(output.normalWS, output.vertexSH);
                #else
                    TOON_TRANSFER_BAKED(input, output, posInput.positionWS, output.normalWS);
                #endif

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

                // Baked indirect light, plus the baked occlusion of the mixed main light.
                // The mask defaults to "lit" so variants carrying no baked shadow data fall
                // back to the realtime shadow alone rather than to black.
                half4 shadowMask = half4(1, 1, 1, 1);
                half3 bakedGI = half3(0, 0, 0);
                #if defined(TOON_MOBILE_LIGHTING)
                    bakedGI = SAMPLE_GI(input.staticLightmapUV, input.vertexSH, N);
                    shadowMask = SAMPLE_SHADOWMASK(input.staticLightmapUV);
                #else
                    TOON_RESOLVE_BAKED(input, N, input.positionWS, V, bakedGI, shadowMask);
                #endif

                // URP strips all main-light shadow uniforms when shadows are disabled. Calling
                // MainLightShadow in that variant works in the Editor's desktop compiler by
                // accident, but leaves no valid GLES3 program on ARMv7 devices.
                float shadow = 1.0;
                #if defined(_MAIN_LIGHT_SHADOWS) || defined(_MAIN_LIGHT_SHADOWS_CASCADE) || defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                    float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                    shadow = MainLightShadow(
                        shadowCoord,
                        input.positionWS,
                        shadowMask,
                        _MainLightOcclusionProbes);
                #endif

                float3 diffuse = _MainLightColor.rgb * baseMap * _BaseColor.rgb * shadeFactor * shadow;
                float3 specular = _GlossTint.rgb * shadow * shadeFactor * glossFactor;
                // In Shadowmask mode the lightmap holds indirect only - the mixed light's
                // direct term stays in `diffuse` above, so this does not double count it.
                float3 ambient = bakedGI * _BaseColor.rgb * baseMap;

                float3 finalColor = diffuse + ambient + specular;
                finalColor = lerp(
                    finalColor,
                    _DamageFlashColor.rgb,
                    saturate(_DamageFlashAmount));
                finalColor = MixFog(finalColor, input.fogCoord);

                return float4(finalColor, 1.0);
            }

            ENDHLSL
        }

        // Feeds albedo to the lightmapper. Without a Meta pass the bake sees no surface
        // colour here, so bounces off these materials came back black and the indirect
        // contribution was thrown away no matter how many bounces were configured.
        Pass
        {
            Name "Meta"
            Tags { "LightMode"="Meta" }

            Cull Off

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex UniversalVertexMeta
            #pragma fragment ToonFragmentMeta
            #pragma shader_feature EDITOR_VISUALIZATION

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

            // Kept identical to the forward pass so the SRP Batcher still considers the
            // shader compatible; the batcher validates every pass, not just the lit one.
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
            CBUFFER_END

            // Declares Attributes/Varyings and UniversalVertexMeta, which transforms uv0
            // with _BaseMap_ST - so it has to come after the buffer above.
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/UniversalMetaPass.hlsl"

            half4 ToonFragmentMeta(Varyings input) : SV_Target
            {
                MetaInput metaInput = (MetaInput)0;
                metaInput.Albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb * _BaseColor.rgb;
                return UniversalFragmentMeta(input, metaInput);
            }

            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
    }
}
