Shader "FrogGod/Mobile Particle Additive Emission"
{
    Properties
    {
        [MainTexture] _BaseMap ("Particle Texture", 2D) = "white" {}
        [MainColor] _BaseColor ("Tint", Color) = (1, 1, 1, 1)
        [Toggle(_EMISSION)] _Emission ("Emission", Float) = 1
        _EmissionMap ("Emission Map", 2D) = "white" {}
        [HDR] _EmissionColor ("Emission Color", Color) = (2, 2, 2, 1)
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.02

        [HideInInspector] _BumpMap ("Normal Map", 2D) = "bump" {}
        [HideInInspector] _SoftParticleFadeParams ("Soft Particle Fade", Vector) = (0, 0, 0, 0)
        [HideInInspector] _CameraFadeParams ("Camera Fade", Vector) = (0, 0, 0, 0)
        [HideInInspector] _BaseColorAddSubDiff ("Color Mode", Vector) = (0, 0, 0, 0)
        [HideInInspector] _DistortionStrengthScaled ("Distortion Strength", Float) = 0
        [HideInInspector] _DistortionBlend ("Distortion Blend", Float) = 0
        [HideInInspector] _Surface ("Surface", Float) = 1
        [HideInInspector] _SrcBlend ("Source Blend", Float) = 5
        [HideInInspector] _DstBlend ("Destination Blend", Float) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
        }

        Pass
        {
            Name "ParticleAdditiveEmission"
            Tags { "LightMode" = "UniversalForward" }

            Blend [_SrcBlend] [_DstBlend]
            BlendOp Add
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma target 2.0
            #pragma never_use_dxc
            #pragma vertex vertParticleUnlit
            #pragma fragment fragMobileParticle
            #pragma shader_feature_local_fragment _EMISSION
            #pragma multi_compile_instancing
            #pragma instancing_options procedural:ParticleInstancingSetup

            #define _SURFACE_TYPE_TRANSPARENT 1

            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/Shaders/Particles/ParticlesUnlitInput.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/Shaders/Particles/ParticlesUnlitForwardPass.hlsl"

            half4 fragMobileParticle(VaryingsParticle input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half4 baseSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.texcoord);
                half4 particleColor = input.color;
                half alpha = baseSample.a * _BaseColor.a * particleColor.a;
                clip(alpha - _Cutoff);
                half3 color = baseSample.rgb * _BaseColor.rgb * particleColor.rgb;

                #if defined(_EMISSION)
                    half3 emission = SAMPLE_TEXTURE2D(
                        _EmissionMap,
                        sampler_EmissionMap,
                        input.texcoord).rgb;
                    color += emission * _EmissionColor.rgb * particleColor.rgb;
                #endif

                color = MixFog(color, input.positionWS.w);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
