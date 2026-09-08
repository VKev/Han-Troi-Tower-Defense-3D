Shader "TowerDefense3D/UI/TutorialSoftMask"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Overlay Color", Color) = (0,0,0,0.72)
        _FocusCenter ("Focus Center", Vector) = (0.5,0.5,0,0)
        _FocusSize ("Focus Size", Vector) = (0.2,0.2,0,0)
        _FocusCenter2 ("Focus Center 2", Vector) = (0.5,0.5,0,0)
        _FocusSize2 ("Focus Size 2", Vector) = (0.2,0.2,0,0)
        _FocusCount ("Focus Count", Float) = 1
        _Softness ("Softness", Range(0.02,0.5)) = 0.2
        _GlowColor ("Glow Color", Color) = (1,0.68,0.24,0.95)
        _GlowWidth ("Glow Width", Range(0.005,0.12)) = 0.012
        _GlowEnabled ("Glow Enabled", Float) = 1
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "CanUseSpriteAtlas"="True" }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float2 texcoord : TEXCOORD0;
                float4 color : COLOR;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            float4 _FocusCenter;
            float4 _FocusSize;
            float4 _FocusCenter2;
            float4 _FocusSize2;
            float _FocusCount;
            float _Softness;
            float4 _Color;
            float4 _GlowColor;
            float _GlowWidth;
            float _GlowEnabled;

            v2f vert(appdata_t input)
            {
                v2f output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.uv = input.texcoord;
                output.color = input.color * _Color;
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                float2 halfSize = max(_FocusSize.xy * 0.5, float2(0.001, 0.001));
                float2 halfSize2 = max(_FocusSize2.xy * 0.5, float2(0.001, 0.001));
                float distance1 = length((input.uv - _FocusCenter.xy) / halfSize);
                float distance2 = length((input.uv - _FocusCenter2.xy) / halfSize2);
                float dim1 = smoothstep(1.0 - _Softness, 1.0 + _Softness, distance1);
                float dim2 = smoothstep(1.0 - _Softness, 1.0 + _Softness, distance2);
                float dim = lerp(dim1, min(dim1, dim2), step(1.5, _FocusCount));
                float ring1 = 1.0 - smoothstep(_GlowWidth, _GlowWidth * 2.0, abs(distance1 - 1.0));
                float ring2 = 1.0 - smoothstep(_GlowWidth, _GlowWidth * 2.0, abs(distance2 - 1.0));
                float glow = lerp(ring1, max(ring1, ring2), step(1.5, _FocusCount)) * _GlowEnabled;
                fixed4 color = fixed4(0, 0, 0, input.color.a * dim);
                color.rgb = lerp(color.rgb, _GlowColor.rgb, glow);
                color.a = max(color.a, glow * _GlowColor.a);
                return color;
            }
            ENDCG
        }
    }
}
