Shader "Universal Render Pipeline/Particles/Unlit_NoiseBlend"
{
    Properties
    {
        [Header(Surface Inputs)]
        [MainTexture] _MainTex("MainTex", 2D) = "white" {}
        _Mask("Mask", 2D) = "white" {}
        _Noise("Noise", 2D) = "white" {}
        _Speed("Speed MainTex U/V + Noise Z/W", Vector) = (0, 0, 0, 0)

        _Emission("Emission", Float) = 4.0
        
        [Toggle(_USE_FRESNEL)] _UseFresnel("Use Fresnel?", Float) = 1
        [Toggle(_USE_SMOOTH_CORNERS)] _UseSmoothCorners("Use smooth corners?", Float) = 1
        _Fresnel("Fresnel", Float) = 2.0
        _FresnelEmission("Fresnel Emission", Float) = 3.0
        
        [Toggle(_SEPARATE_FRESNEL)] _SeparateFresnel("SeparateFresnel", Float) = 0
        _SeparateEmission("Separate Emission", Float) = 2.0
        
        [HDR] _FresnelColor("Fresnel Color", Color) = (1,1,1,1)
        _FrontFacesColor("Front Faces Color", Color) = (1, 0.9, 0.9, 1)
        _BackFacesColor("Back Faces Color", Color) = (1,1,1,1)
        [HDR] _BackFresnelColor("Back Fresnel Color", Color) = (1,1,1,1)
        
        [Toggle(_USE_BACK_FRESNEL)] _UseBackFresnel("Use Back Fresnel?", Float) = 0
        _BackFresnel("Back Fresnel", Float) = -2.0
        _BackFresnelEmission("Back Fresnel Emission", Float) = 1.0
        
        [Toggle(_USE_SCREENSPACE)] _UseScreenSpace("Use ScreenSpace?", Float) = 0
        _Opacity("Opacity", Range(0, 1)) = 1.0

        [Header(Blending and Culling)]
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 5.0 
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 10.0 
        [Enum(Off, 0, On, 1)] _ZWrite ("Z Write", Float) = 0.0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull Mode", Float) = 0.0 
        
        [Toggle(_PREMULTIPLY_ALPHA)] _PremultiplyAlpha("Premultiply Alpha (For Additive Blend)", Float) = 0
    }
    SubShader
    {
        Tags 
        { 
            "RenderType" = "Transparent" 
            "RenderPipeline" = "UniversalPipeline" 
            "Queue" = "Transparent" 
            "IgnoreProjector" = "True"
        }

        Blend [_SrcBlend] [_DstBlend]
        ZWrite [_ZWrite]
        Cull [_Cull]
        ZTest LEqual

        Pass
        {
            Name "ForwardUnlit"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            #pragma multi_compile_fog

            // Features
            #pragma shader_feature_local _USE_FRESNEL
            #pragma shader_feature_local _USE_SMOOTH_CORNERS
            #pragma shader_feature_local _SEPARATE_FRESNEL
            #pragma shader_feature_local _USE_BACK_FRESNEL
            #pragma shader_feature_local _USE_SCREENSPACE
            #pragma shader_feature_local _PREMULTIPLY_ALPHA

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uvMain     : TEXCOORD0;
                float2 uvMask     : TEXCOORD1;
                float2 uvNoise    : TEXCOORD2;
                float4 color      : COLOR;
                half fogFactor    : TEXCOORD5;
                #if _USE_SCREENSPACE
                    float4 screenPos : TEXCOORD6;
                #endif
            };

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex); float4 _MainTex_ST;
            TEXTURE2D(_Mask);    SAMPLER(sampler_Mask);    float4 _Mask_ST;
            TEXTURE2D(_Noise);   SAMPLER(sampler_Noise);   float4 _Noise_ST;

            CBUFFER_START(UnityPerMaterial)
                float4 _Speed;
                half _Emission;
                
                half _Fresnel;
                half _FresnelEmission;
                half _SeparateEmission;
                
                half4 _FresnelColor;
                half4 _FrontFacesColor;
                half4 _BackFacesColor;
                half4 _BackFresnelColor;
                
                half _BackFresnel;
                half _BackFresnelEmission;
                
                half _Opacity;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = vertexInput.positionCS;
                
                output.uvMain  = TRANSFORM_TEX(input.uv, _MainTex);
                output.uvMask  = TRANSFORM_TEX(input.uv, _Mask);
                output.uvNoise = TRANSFORM_TEX(input.uv, _Noise);
                
                output.color = input.color;
                output.fogFactor = ComputeFogFactor(vertexInput.positionCS.z);
                
                #if _USE_SCREENSPACE
                    output.screenPos = ComputeScreenPos(vertexInput.positionCS);
                #endif

                return output;
            }

            half4 frag(Varyings input, FRONT_FACE_TYPE isFrontFace : FRONT_FACE_SEMANTIC) : SV_Target
            {
                float2 uvMain  = input.uvMain;
                float2 uvMask  = input.uvMask;
                float2 uvNoise = input.uvNoise;

                #if _USE_SCREENSPACE
                    float2 screenUV = input.screenPos.xy / input.screenPos.w;
                    uvMain  = screenUV * _MainTex_ST.xy + _MainTex_ST.zw;
                    uvMask  = screenUV * _Mask_ST.xy + _Mask_ST.zw;
                    uvNoise = screenUV * _Noise_ST.xy + _Noise_ST.zw;
                #endif

                // Panning speed
                float timeY = _Time.y;
                uvMain  += timeY * _Speed.xy;
                uvNoise += timeY * _Speed.zw;

                half4 mainTex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uvMain);
                half4 maskTex = SAMPLE_TEXTURE2D(_Mask,    sampler_Mask,    uvMask);
                half4 noiseTex= SAMPLE_TEXTURE2D(_Noise,   sampler_Noise,   uvNoise);

                // FIX: Support Black & White textures (without Alpha channel)
                // Multiply Alpha by Red channel so black parts become transparent.
                maskTex.a *= maskTex.r;
                noiseTex.a *= noiseTex.r;

                // Base Color Calculation 
                half4 finalColor = mainTex * maskTex * noiseTex * input.color;
                
                // Front / Back Face Color
                half4 faceColor = IS_FRONT_VFACE(isFrontFace, true, false) ? _FrontFacesColor : _BackFacesColor;
                finalColor *= faceColor;
                
                // Opacity & Emission
                finalColor.a *= _Opacity;
                half3 rgb = finalColor.rgb * _Emission;

                #if _USE_FRESNEL
                    float dotNV = 1.0;
                    #if _USE_SMOOTH_CORNERS
                        float2 centeredUV = input.uvMain * 2.0 - 1.0;
                        dotNV = sqrt(1.0 - saturate(dot(centeredUV, centeredUV)));
                    #endif

                    if (IS_FRONT_VFACE(isFrontFace, true, false))
                    {
                        half f = pow(1.0 - dotNV, _Fresnel);
                        #if _SEPARATE_FRESNEL
                            // Separate adds to both RGB and Alpha so Fresnel acts as its own solid outline
                            half fresnelVal = f * _SeparateEmission;
                            rgb += _FresnelColor.rgb * (fresnelVal * input.color.a);
                            finalColor.a = saturate(finalColor.a + f * input.color.a);
                        #else
                            half fresnelVal = f * _FresnelEmission;
                            rgb += _FresnelColor.rgb * (fresnelVal * finalColor.a);
                        #endif
                    }
                    else
                    {
                        #if _USE_BACK_FRESNEL
                            half bf = pow(abs(1.0 - dotNV), _BackFresnel);
                            half fresnelVal = bf * _BackFresnelEmission;
                            rgb += _BackFresnelColor.rgb * (fresnelVal * finalColor.a);
                        #endif
                    }
                #endif
                
                #if _PREMULTIPLY_ALPHA
                    rgb *= finalColor.a;
                #endif

                // Apply Fog
                rgb = MixFog(rgb, input.fogFactor);

                return half4(rgb, finalColor.a);
            }
            ENDHLSL
        }
    }
}
