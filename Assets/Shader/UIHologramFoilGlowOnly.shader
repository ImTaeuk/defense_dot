Shader "UI/Hologram Foil Glow Only"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        [HDR] _GlowColor ("Glow HDR Color", Color) = (1.2, 1.7, 3.5, 1)
        _GlowStrength ("Glow Strength", Range(0, 20)) = 6
        _GlowSize ("Glow Size", Range(0, 24)) = 5
        _GlowSoftness ("Glow Softness", Range(0.01, 2)) = 0.8
        _InnerGlow ("Inner Rim Glow", Range(0, 1)) = 1
        _OuterGlow ("Outer Halo Glow", Range(0, 1)) = 1
        _PulseSpeed ("Pulse Speed", Range(0, 5)) = 0.6
        _PulseAmount ("Pulse Amount", Range(0, 1)) = 0.18
        _AlphaCutoff ("Alpha Cutoff", Range(0, 1)) = 0.001

        [Header(UI)]
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha One
        ColorMask [_ColorMask]

        Pass
        {
            Name "GlowOnly"

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _MainTex_TexelSize;
            fixed4 _Color;
            float4 _ClipRect;

            float4 _GlowColor;
            float _GlowStrength;
            float _GlowSize;
            float _GlowSoftness;
            float _InnerGlow;
            float _OuterGlow;
            float _PulseSpeed;
            float _PulseAmount;
            float _AlphaCutoff;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                OUT.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                OUT.color = v.color * _Color;
                return OUT;
            }

            float SampleAlpha(float2 uv)
            {
                return tex2D(_MainTex, uv).a;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float alpha = SampleAlpha(IN.texcoord) * IN.color.a;
                float2 px = _MainTex_TexelSize.xy * _GlowSize;

                float a0 = SampleAlpha(IN.texcoord + float2( px.x, 0.0));
                float a1 = SampleAlpha(IN.texcoord + float2(-px.x, 0.0));
                float a2 = SampleAlpha(IN.texcoord + float2(0.0,  px.y));
                float a3 = SampleAlpha(IN.texcoord + float2(0.0, -px.y));
                float a4 = SampleAlpha(IN.texcoord + float2( px.x,  px.y));
                float a5 = SampleAlpha(IN.texcoord + float2(-px.x,  px.y));
                float a6 = SampleAlpha(IN.texcoord + float2( px.x, -px.y));
                float a7 = SampleAlpha(IN.texcoord + float2(-px.x, -px.y));

                float nearbyAverage = (a0 + a1 + a2 + a3 + a4 + a5 + a6 + a7) * 0.125;
                float nearbyMin = min(min(min(a0, a1), min(a2, a3)), min(min(a4, a5), min(a6, a7)));

                float outerHalo = saturate((nearbyAverage - alpha) / max(_GlowSoftness, 0.001)) * saturate(1.0 - alpha);
                float innerRim = alpha * saturate((alpha - nearbyMin) / max(_GlowSoftness, 0.001));
                float glowMask = saturate(outerHalo * _OuterGlow + innerRim * _InnerGlow);
                float pulse = 1.0 + sin(_Time.y * _PulseSpeed * 6.28318) * _PulseAmount;

                fixed4 OUT;
                OUT.rgb = _GlowColor.rgb * _GlowStrength * pulse * glowMask * IN.color.rgb;
                OUT.a = saturate(glowMask * _GlowColor.a * IN.color.a);

                #ifdef UNITY_UI_CLIP_RECT
                OUT.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(OUT.a - _AlphaCutoff);
                #endif

                return OUT;
            }
            ENDCG
        }
    }
}
