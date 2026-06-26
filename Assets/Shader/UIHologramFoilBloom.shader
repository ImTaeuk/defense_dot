Shader "UI/Hologram Foil Bloom"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        [Header(Foil)]
        _FoilStrength ("Foil Strength", Range(0, 3)) = 1.15
        _FoilScale ("Foil Scale", Range(0.5, 24)) = 6
        _FoilSpeed ("Foil Speed", Range(-5, 5)) = 0.7
        _FoilAngle ("Foil Angle", Range(-3.1416, 3.1416)) = 0.65
        _FoilEdgeOnly ("Foil Edge Only", Range(0, 1)) = 0.85
        _FoilEdgeSize ("Foil Edge Size", Range(0, 12)) = 2.0
        _ShineStrength ("Shine Strength", Range(0, 4)) = 1.2
        _ShineWidth ("Shine Width", Range(0.02, 0.8)) = 0.18

        [Header(Edge Glow)]
        [HDR] _EdgeColor ("Edge HDR Color", Color) = (1.4, 1.8, 2.6, 1)
        _EdgeStrength ("Edge Strength", Range(0, 12)) = 3.0
        _EdgeSize ("Edge Size", Range(0, 12)) = 2.5
        _EdgeSoftness ("Edge Softness", Range(0.01, 1)) = 0.4

        [Header(Bloom Output)]
        _EmissionBoost ("HDR Bloom Boost", Range(0, 12)) = 3.0
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
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"

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

            float _FoilStrength;
            float _FoilScale;
            float _FoilSpeed;
            float _FoilAngle;
            float _FoilEdgeOnly;
            float _FoilEdgeSize;
            float _ShineStrength;
            float _ShineWidth;
            float4 _EdgeColor;
            float _EdgeStrength;
            float _EdgeSize;
            float _EdgeSoftness;
            float _EmissionBoost;
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

            float3 Hue(float t)
            {
                float3 p = abs(frac(t + float3(0.0, 0.6667, 0.3333)) * 6.0 - 3.0);
                return saturate(p - 1.0);
            }

            float SampleAlpha(float2 uv)
            {
                return tex2D(_MainTex, uv).a;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                fixed4 sprite = tex2D(_MainTex, IN.texcoord) * IN.color;
                float alpha = sprite.a;

                float2 foilPx = _MainTex_TexelSize.xy * _FoilEdgeSize;
                float nearAlphaMin = min(min(
                    min(SampleAlpha(IN.texcoord + float2( foilPx.x, 0.0)), SampleAlpha(IN.texcoord + float2(-foilPx.x, 0.0))),
                    min(SampleAlpha(IN.texcoord + float2(0.0,  foilPx.y)), SampleAlpha(IN.texcoord + float2(0.0, -foilPx.y)))),
                    min(
                    min(SampleAlpha(IN.texcoord + float2( foilPx.x,  foilPx.y)), SampleAlpha(IN.texcoord + float2(-foilPx.x,  foilPx.y))),
                    min(SampleAlpha(IN.texcoord + float2( foilPx.x, -foilPx.y)), SampleAlpha(IN.texcoord + float2(-foilPx.x, -foilPx.y)))));
                float innerEdge = alpha * saturate((alpha - nearAlphaMin) / max(_EdgeSoftness, 0.001));
                float foilMask = alpha * lerp(1.0, innerEdge, _FoilEdgeOnly);

                float2 dir = float2(cos(_FoilAngle), sin(_FoilAngle));
                float flow = dot(IN.texcoord, dir) * _FoilScale + _Time.y * _FoilSpeed;
                float3 rainbow = Hue(flow);
                float shine = smoothstep(1.0 - _ShineWidth, 1.0, sin(flow * 6.28318) * 0.5 + 0.5);
                float3 foil = lerp(sprite.rgb, rainbow, saturate(_FoilStrength * (0.35 + shine)));
                sprite.rgb = lerp(sprite.rgb, foil + shine * _ShineStrength, foilMask);

                float2 px = _MainTex_TexelSize.xy * _EdgeSize;
                float outside =
                    SampleAlpha(IN.texcoord + float2( px.x, 0.0)) +
                    SampleAlpha(IN.texcoord + float2(-px.x, 0.0)) +
                    SampleAlpha(IN.texcoord + float2(0.0,  px.y)) +
                    SampleAlpha(IN.texcoord + float2(0.0, -px.y)) +
                    SampleAlpha(IN.texcoord + float2( px.x,  px.y)) +
                    SampleAlpha(IN.texcoord + float2(-px.x,  px.y)) +
                    SampleAlpha(IN.texcoord + float2( px.x, -px.y)) +
                    SampleAlpha(IN.texcoord + float2(-px.x, -px.y));
                outside *= 0.125;

                float edge = saturate((outside - alpha) / max(_EdgeSoftness, 0.001));
                float edgeMask = edge * saturate(1.0 - alpha);
                sprite.rgb += _EdgeColor.rgb * edgeMask * _EdgeStrength * _EmissionBoost;
                sprite.a = saturate(alpha + edgeMask * _EdgeColor.a);

                #ifdef UNITY_UI_CLIP_RECT
                sprite.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(sprite.a - _AlphaCutoff);
                #endif

                return sprite;
            }
            ENDCG
        }
    }
}
