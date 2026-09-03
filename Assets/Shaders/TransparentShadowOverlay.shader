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
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            // Brings in Core/Lighting/Shadows plus the whole baked-lighting keyword set.
            // The grass renderers are static, lightmapped and have Receive Shadows on, so
            // the bake already writes shadowmask and lightmap data for them - this shader
            // simply never read any of it.
            #include_with_pragmas "Assets/Shaders/ToonBakedLighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                float2 staticLightmapUV : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                // Named positionCS because the TOON_* macros address it by name.
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half fogFactor : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float3 normalWS : TEXCOORD3;
                // Becomes a lightmap UV when LIGHTMAP_ON, else per-vertex SH.
                DECLARE_LIGHTMAP_OR_SH(staticLightmapUV, vertexSH, 4);
                TOON_PROBE_OCCLUSION_VARYING(5)
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
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                // The bake needs a world normal: the directional lightmap and the probe
                // volumes are both evaluated against it, not against a flat up vector.
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);

                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = normalInputs.normalWS;
                output.fogFactor = ComputeFogFactor(positionInputs.positionCS.z);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);

                TOON_TRANSFER_BAKED(input, output, positionInputs.positionWS, output.normalWS);

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

                float3 normalWS = normalize(input.normalWS);
                half3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);

                // Baked indirect light, plus the baked occlusion of the mixed main light.
                // The mask defaults to "lit" so variants carrying no baked shadow data fall
                // back to the realtime shadow alone rather than to black.
                half4 shadowMask = half4(1, 1, 1, 1);
                half3 bakedGI = half3(0, 0, 0);
                TOON_RESOLVE_BAKED(input, normalWS, input.positionWS, viewDirWS, bakedGI, shadowMask);

                // Was GetMainLight(shadowCoord), which is realtime only. The scene's props do
                // not cast realtime shadows at all - every shadow they throw lives in the
                // shadowmask - so realtime-only shading left the grass permanently sunlit.
                // TransformWorldToShadowCoord already handles the screen-space shadow variant
                // internally, so the old manual _MAIN_LIGHT_SHADOWS_SCREEN branch is gone.
                half mainShadow = MainLightShadow(
                    TransformWorldToShadowCoord(input.positionWS),
                    input.positionWS,
                    shadowMask,
                    _MainLightOcclusionProbes);
                half shadow = lerp(1.0h, mainShadow, _ShadowStrength);

                // The direct term keeps the shader's original "texture times shadow" look, so
                // sunlit grass reads as before. The baked bounce is added on top, which is what
                // stops shadowed grass from being a flat multiply of the albedo.
                color = color * shadow + color * bakedGI;
                color = MixFog(color, input.fogFactor);
                return half4(color, 1.0h);
            }
            ENDHLSL
        }

        // Feeds albedo to the lightmapper. Without a Meta pass the bake sees no surface
        // colour here, so bounces off the grass came back black and its indirect
        // contribution was thrown away no matter how many bounces were configured.
        Pass
        {
            Name "Meta"
            Tags { "LightMode" = "Meta" }

            Cull Off

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex UniversalVertexMeta
            #pragma fragment GrassFragmentMeta
            #pragma shader_feature EDITOR_VISUALIZATION

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half _ShadowStrength;
                half _AlphaCutoff;
                half _TextureBlur;
            CBUFFER_END

            // Declares Attributes/Varyings and UniversalVertexMeta, which transforms uv0
            // with _BaseMap_ST - so it has to come after the buffer above.
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/UniversalMetaPass.hlsl"

            half4 GrassFragmentMeta(Varyings input) : SV_Target
            {
                half4 source = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                // The blur in the forward pass is a look-softening trick, not a property of
                // the surface, so the bake reads the texture straight.
                clip(source.a * _BaseColor.a - _AlphaCutoff);

                MetaInput metaInput = (MetaInput)0;
                metaInput.Albedo = source.rgb * _BaseColor.rgb;
                return UniversalFragmentMeta(input, metaInput);
            }

            ENDHLSL
        }
    }

    FallBack Off
}
