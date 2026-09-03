Shader "FrogGod/Road Shadow Overlay"
{
    Properties
    {
        [MainTexture] _BaseMap ("Road Texture", 2D) = "white" {}
        [MainColor] _BaseColor ("Tint", Color) = (1, 1, 1, 1)
        _AlphaCutoff ("Minimum Alpha", Range(0, 1)) = 0.02
        _ShadowStrength ("Shadow Strength", Range(0, 1)) = 0.65
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
            Name "RoadShadowOverlay"
            Tags { "LightMode" = "UniversalForward" }

            ZWrite On
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog

            // Brings in Core/Lighting/Shadows plus the whole baked-lighting keyword set.
            // The road is the largest static surface in a level, so it is also the one
            // that loses the most by ignoring the bake.
            #include_with_pragmas "Assets/Shaders/ToonBakedLighting.hlsl"

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Random.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                float2 staticLightmapUV : TEXCOORD1;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half fogFactor : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float3 normalWS : TEXCOORD3;
                // Becomes a lightmap UV when LIGHTMAP_ON, else per-vertex SH.
                DECLARE_LIGHTMAP_OR_SH(staticLightmapUV, vertexSH, 4);
                TOON_PROBE_OCCLUSION_VARYING(5)
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half _AlphaCutoff;
                half _ShadowStrength;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
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

            half4 Frag(Varyings input) : SV_Target
            {
                half4 road = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                half dither = InterleavedGradientNoise(input.positionCS.xy, 0);
                clip(road.a - max(_AlphaCutoff, dither));

                float3 normalWS = normalize(input.normalWS);
                half3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);

                // Baked indirect light, plus the baked occlusion of the mixed main light.
                // The mask defaults to "lit" so variants carrying no baked shadow data fall
                // back to the realtime shadow alone rather than to black.
                half4 shadowMask = half4(1, 1, 1, 1);
                half3 bakedGI = half3(0, 0, 0);
                TOON_RESOLVE_BAKED(input, normalWS, input.positionWS, viewDirWS, bakedGI, shadowMask);

                // Was MainLightRealtimeShadow, which meant the road went unshadowed past the
                // shadow distance even though the shadowmask had the answer baked in.
                half mainShadow = MainLightShadow(
                    TransformWorldToShadowCoord(input.positionWS),
                    input.positionWS,
                    shadowMask,
                    _MainLightOcclusionProbes);
                half shadow = lerp(1.0h, mainShadow, _ShadowStrength);

                // The direct term keeps the shader's original "texture times shadow" look, so
                // sunlit road reads as before. The baked bounce is added on top, which is what
                // stops shadowed road from being a flat multiply of the albedo.
                // The direct term keeps the shader's original "texture times shadow" look, so
                // sunlit road reads as before. The baked bounce is added on top, which is what
                // stops shadowed road from being a flat multiply of the albedo.
                road.rgb = road.rgb * shadow + road.rgb * bakedGI;
                road.rgb = MixFog(road.rgb, input.fogFactor);
                road.a = 1.0h;
                return road;
            }
            ENDHLSL
        }

        // Feeds albedo to the lightmapper. The road covers most of the ground plane, so
        // without this pass the bake had nothing to bounce the sun off and the indirect
        // light in every level came out far darker and greyer than it should be.
        Pass
        {
            Name "Meta"
            Tags { "LightMode" = "Meta" }

            Cull Off

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex UniversalVertexMeta
            #pragma fragment RoadFragmentMeta
            #pragma shader_feature EDITOR_VISUALIZATION

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half _AlphaCutoff;
                half _ShadowStrength;
            CBUFFER_END

            // Declares Attributes/Varyings and UniversalVertexMeta, which transforms uv0
            // with _BaseMap_ST - so it has to come after the buffer above.
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/UniversalMetaPass.hlsl"

            half4 RoadFragmentMeta(Varyings input) : SV_Target
            {
                half4 road = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                // The forward pass dithers the cutoff in screen space, which has no meaning
                // during a bake - the lightmapper rasterizes into lightmap space.
                clip(road.a - _AlphaCutoff);

                MetaInput metaInput = (MetaInput)0;
                metaInput.Albedo = road.rgb;
                return UniversalFragmentMeta(input, metaInput);
            }

            ENDHLSL
        }
    }

    FallBack Off
}
