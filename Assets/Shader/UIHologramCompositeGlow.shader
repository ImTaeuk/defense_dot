Shader "UI/Hologram Composite Glow"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        [Header(Layout)]
        _FrontScale ("Front Image Scale", Range(0.1, 1)) = 0.82
        _BackScale ("Glow Silhouette Scale", Range(0.1, 1)) = 1.0

        [Header(Glow Background)]
        [HDR] _GlowColor ("Glow HDR Color", Color) = (1.5, 1.9, 3.8, 1)
        _GlowStrength ("Glow Strength", Range(0, 20)) = 4
        _GlowFill ("Glow Fill", Range(0, 1)) = 1
        _GlowEdge ("Glow Edge", Range(0, 1)) = 0.35
        _GlowEdgeSize ("Glow Edge Size", Range(0, 16)) = 4
        _GlowSoftness ("Glow Softness", Range(0.01, 2)) = 0.7

        [Header(Foil)]
        _FoilStrength ("Foil Strength", Range(0, 3)) = 0.9
        _FoilScale ("Foil Scale", Range(0.5, 24)) = 6
        _FoilSpeed ("Foil Speed", Range(-5, 5)) = 0.7
        _FoilAngle ("Foil Angle", Range(-3.1416, 3.1416)) = 0.65
        _ShineStrength ("Shine Strength", Range(0, 4)) = 0.8
        _ShineWidth ("Shine Width", Range(0.02, 0.8)) = 0.18

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
            Name "CompositeGlow"

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

            float _FrontScale;
            float _BackScale;
            float4 _GlowColor;
            float _GlowStrength;
            float _GlowFill;
            float _GlowEdge;
            float _GlowEdgeSize;
            float _GlowSoftness;
            float _FoilStrength;
            float _FoilScale;
            float _FoilSpeed;
            float _FoilAngle;
            float _ShineStrength;
            float _ShineWidth;
            float _UnscaledTime;   // 글로벌 unscaled 시간

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

            float2 ScaleUv(float2 uv, float scale)
            {
                return (uv - 0.5) / max(scale, 0.001) + 0.5;
            }

            float InUv(float2 uv)
            {
                float2 inside = step(0.0, uv) * step(uv, 1.0);
                return inside.x * inside.y;
            }

            float AlphaAt(float2 uv)
            {
                return tex2D(_MainTex, uv).a * InUv(uv);
            }

            float3 Hue(float t)
            {
                float3 p = abs(frac(t + float3(0.0, 0.6667, 0.3333)) * 6.0 - 3.0);
                return saturate(p - 1.0);
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 backUv = ScaleUv(IN.texcoord, _BackScale);
                float backAlpha = AlphaAt(backUv);
                float2 px = _MainTex_TexelSize.xy * _GlowEdgeSize;

                float nearbyMin = min(min(
                    min(AlphaAt(backUv + float2( px.x, 0.0)), AlphaAt(backUv + float2(-px.x, 0.0))),
                    min(AlphaAt(backUv + float2(0.0,  px.y)), AlphaAt(backUv + float2(0.0, -px.y)))),
                    min(
                    min(AlphaAt(backUv + float2( px.x,  px.y)), AlphaAt(backUv + float2(-px.x,  px.y))),
                    min(AlphaAt(backUv + float2( px.x, -px.y)), AlphaAt(backUv + float2(-px.x, -px.y)))));

                float backEdge = backAlpha * saturate((backAlpha - nearbyMin) / max(_GlowSoftness, 0.001));
                float glowMask = saturate(backAlpha * _GlowFill + backEdge * _GlowEdge);
                float3 backRgb = _GlowColor.rgb * _GlowStrength * glowMask;
                float backOutAlpha = saturate(glowMask * _GlowColor.a);

                float2 frontUv = ScaleUv(IN.texcoord, _FrontScale);
                fixed4 front = tex2D(_MainTex, frontUv) * IN.color;
                front.a *= InUv(frontUv);

                float2 dir = float2(cos(_FoilAngle), sin(_FoilAngle));
                float flow = dot(frontUv, dir) * _FoilScale + _UnscaledTime * _FoilSpeed;
                float shine = smoothstep(1.0 - _ShineWidth, 1.0, sin(flow * 6.28318) * 0.5 + 0.5);
                float3 foil = lerp(front.rgb, Hue(flow), saturate(_FoilStrength * (0.35 + shine)));
                front.rgb = lerp(front.rgb, foil + shine * _ShineStrength, front.a);

                fixed4 OUT;
                OUT.rgb = backRgb * (1.0 - front.a) + front.rgb * front.a;
                OUT.a = saturate(backOutAlpha * (1.0 - front.a) + front.a);

                #ifdef UNITY_UI_CLIP_RECT
                OUT.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(OUT.a - 0.001);
                #endif

                return OUT;
            }
            ENDCG
        }
    }
}
