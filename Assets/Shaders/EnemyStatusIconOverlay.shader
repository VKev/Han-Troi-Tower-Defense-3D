// The element mark above an enemy's head is a readout, not scenery: the player has to be able
// to tell what an enemy is charged with while the projectiles that charged it are still in the
// air. As an ordinary transparent it lost that race - every projectile, trail and hit burst
// sits in the same queue, so whichever happened to be nearer the camera drew last and painted
// over the icon. This draws after all of that and ignores depth entirely, so the mark stays
// legible through projectiles, towers and scenery alike.
Shader "FrogGod/Enemy Status Icon Overlay"
{
    Properties
    {
        [MainTexture] _BaseMap ("Icon Atlas", 2D) = "white" {}
        [MainColor] _BaseColor ("Tint", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            // Past every projectile, trail and impact effect, which all sit at Transparent.
            "Queue" = "Transparent+500"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
        }

        Pass
        {
            Name "EnemyStatusIconOverlay"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            // The whole point: never let map geometry or a passing projectile occlude the mark.
            ZTest Always
            // The billboard is turned to face the camera, but the enemy root keeps spinning
            // underneath it, so a back face must still draw while the turn is in progress.
            Cull Off

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                // No fog: the mark reads as an overlay, and fading it with distance would
                // undo the legibility this shader exists for.
                return SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
