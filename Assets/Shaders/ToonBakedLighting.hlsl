#ifndef FROGGOD_TOON_BAKED_LIGHTING_INCLUDED
#define FROGGOD_TOON_BAKED_LIGHTING_INCLUDED

// Shared baked-lighting plumbing for the hand-written toon/road/grass shaders.
//
// Every macro URP gives us for baked data (SAMPLE_GI, SAMPLE_SHADOWMASK,
// OUTPUT_SH4) silently degrades to "no baked data" when one of the keywords
// below is missing - no compile error, no warning, just a scene that ignores
// its own lightmaps. Keeping the keyword set and the sampling code in one file
// is what stops the four shaders from drifting back into that state one at a
// time.
//
// Include with #include_with_pragmas so the pragmas reach the compiling shader.

// -------------------------------------
// Baked GI keywords
#pragma multi_compile _ LIGHTMAP_ON
#pragma multi_compile _ DIRLIGHTMAP_COMBINED
#pragma multi_compile _ PROBE_VOLUMES_L1 PROBE_VOLUMES_L2
#pragma multi_compile _ EVALUATE_SH_MIXED EVALUATE_SH_VERTEX
#pragma multi_compile_fragment _ LIGHTMAP_BICUBIC_SAMPLING

// -------------------------------------
// Mixed lighting / shadowmask keywords
#pragma multi_compile _ SHADOWS_SHADOWMASK
#pragma multi_compile _ LIGHTMAP_SHADOW_MIXING

// -------------------------------------
// Realtime main light shadows. The three cascade/screen variants are mutually
// exclusive in URP; declaring them as independent on/off pragmas (as these
// shaders used to) both misses _MAIN_LIGHT_SHADOWS_SCREEN and compiles
// combinations that can never be set.
#pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
#pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

// USE_APV_PROBE_OCCLUSION is derived from the keywords above by
// core/Runtime/Lighting/ProbeVolume/ProbeVolume.hlsl, so it is only known after
// the includes - which is why the Varyings macros live below them.
//
// Contract for the macros below. A shader using them must have:
//   Attributes : float2 staticLightmapUV : TEXCOORD1
//   Varyings   : float4 positionCS : SV_POSITION
//                DECLARE_LIGHTMAP_OR_SH(staticLightmapUV, vertexSH, <n>)
//                TOON_PROBE_OCCLUSION_VARYING(<n+1>)
// The member names are not configurable: the macros expand to them directly, so
// a shader that renames positionCS or vertexSH fails to compile in exactly the
// keyword variants that need them - which is rarely the variant you are looking
// at in the inspector.

// Interpolator for APV's per-probe occlusion, present only in the variants that
// sample shadow occlusion out of probe volumes instead of a shadowmask texture.
#ifdef USE_APV_PROBE_OCCLUSION
    #define TOON_PROBE_OCCLUSION_VARYING(index) float4 probeOcclusion : TEXCOORD##index;
#else
    #define TOON_PROBE_OCCLUSION_VARYING(index)
#endif

// Vertex stage: pack the lightmap UV, or evaluate probe/APV SH per vertex.
// Exactly one of the two does real work in any given variant.
#define TOON_TRANSFER_BAKED(input, output, positionWS, normalWS)                            \
    OUTPUT_LIGHTMAP_UV(input.staticLightmapUV, unity_LightmapST, output.staticLightmapUV);  \
    OUTPUT_SH4(positionWS, normalWS, GetWorldSpaceNormalizeViewDir(positionWS), output.vertexSH, output.probeOcclusion)

// Fragment stage: resolve baked diffuse GI and the baked shadow mask.
//
// The two branches are not interchangeable. Probe-lit (non-lightmapped)
// geometry reads occlusion back out of APV through SAMPLE_GI's last argument,
// whereas lightmapped geometry reads it from the shadowmask texture. Arguments
// naming members that a variant does not declare are discarded by the
// preprocessor before compilation, which is why both branches can reference
// staticLightmapUV and vertexSH.
#if !defined(LIGHTMAP_ON) && (defined(PROBE_VOLUMES_L1) || defined(PROBE_VOLUMES_L2))
    #define TOON_RESOLVE_BAKED(input, normalWS, positionWS, viewDirWS, bakedGI, shadowMask) \
        bakedGI = SAMPLE_GI(input.vertexSH,                                                 \
                            GetAbsolutePositionWS(positionWS),                              \
                            normalWS,                                                       \
                            viewDirWS,                                                      \
                            input.positionCS.xy,                                            \
                            input.probeOcclusion,                                           \
                            shadowMask)
#else
    #define TOON_RESOLVE_BAKED(input, normalWS, positionWS, viewDirWS, bakedGI, shadowMask) \
        bakedGI = SAMPLE_GI(input.staticLightmapUV, input.vertexSH, normalWS);              \
        shadowMask = SAMPLE_SHADOWMASK(input.staticLightmapUV)
#endif

#endif // FROGGOD_TOON_BAKED_LIGHTING_INCLUDED
