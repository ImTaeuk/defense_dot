Shader "UI/Hologram/Foil Diagonal Shine"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _FoilStrength ("Foil Strength", Range(0, 1)) = 0.28
        _RainbowScale ("Rainbow Scale", Range(1, 20)) = 8
        _RainbowSpeed ("Rainbow Speed", Range(0, 3)) = 0.35
        _FoilBandScale ("Foil Band Scale", Range(0.3, 12)) = 3.0

        _ShineStrength ("Shine Strength", Range(0, 2)) = 0.75
        _ShineWidth ("Shine Width", Range(0.01, 0.5)) = 0.16
        _ShineSoftness ("Shine Softness", Range(0.001, 0.3)) = 0.08
        _ShineSpeed ("Shine Speed", Range(0, 3)) = 0.45
        _ShineFalloff ("Shine Center Falloff", Range(0.5, 8)) = 2.8
        _ShineEaseAmount ("Shine Ease Amount", Range(0, 1)) = 0.45

        _ViewMetallicStrength ("View Metallic Strength", Range(0, 1)) = 0.35
        _ViewDarkenStrength ("View Darken Strength", Range(0, 1)) = 0.28
        _ViewContrast ("View Contrast", Range(0.5, 8)) = 2.2
        _EdgeDullness ("Edge Dullness", Range(0, 1)) = 0.22
        _DepthMetallicStrength ("Near Side Metallic Strength", Range(0, 1)) = 0.3
        _DepthDarkenStrength ("Far Side Darken Strength", Range(0, 1)) = 0.25
        _DepthResponse ("Near/Far Response", Range(0, 8)) = 2.5

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

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

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
                float3 worldNormal : TEXCOORD2;
                float3 effectWorldPos : TEXCOORD3;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _MainTex_ST;
            float4 _ClipRect;

            float _FoilStrength;
            float _RainbowScale;
            float _RainbowSpeed;
            float _FoilBandScale;

            float _ShineStrength;
            float _ShineWidth;
            float _ShineSoftness;
            float _ShineSpeed;
            float _ShineFalloff;
            float _ShineEaseAmount;

            float _ViewMetallicStrength;
            float _ViewDarkenStrength;
            float _ViewContrast;
            float _EdgeDullness;
            float _DepthMetallicStrength;
            float _DepthDarkenStrength;
            float _DepthResponse;

            float3 HologramRainbow(float phase)
            {
                float3 color;
                color.r = 0.5 + 0.5 * sin(phase + 0.0);
                color.g = 0.5 + 0.5 * sin(phase + 2.094);
                color.b = 0.5 + 0.5 * sin(phase + 4.188);
                return color;
            }

            float EaseInOutSine(float value)
            {
                return 0.5 - 0.5 * cos(saturate(value) * 3.14159265);
            }

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                OUT.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                OUT.color = v.color * _Color;

                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                OUT.worldNormal = UnityObjectToWorldNormal(float3(0.0, 0.0, -1.0));
                OUT.effectWorldPos = worldPos;

                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 uv = IN.texcoord;
                fixed4 baseColor = (tex2D(_MainTex, uv) + _TextureSampleAdd) * IN.color;
                float t = _Time.y;

                float3 normalDir = normalize(IN.worldNormal);
                float3 viewNormal = normalize(mul((float3x3)UNITY_MATRIX_V, normalDir));
                float viewFacing = saturate(abs(viewNormal.z));
                float rotationWeight = pow(1.0 - viewFacing, _ViewContrast);
                float3 centerWorldPos = mul(unity_ObjectToWorld, float4(0.0, 0.0, 0.0, 1.0)).xyz;
                float centerViewDepth = mul(UNITY_MATRIX_V, float4(centerWorldPos, 1.0)).z;
                float pixelViewDepth = mul(UNITY_MATRIX_V, float4(IN.effectWorldPos, 1.0)).z;
                float signedDepthOffset = pixelViewDepth - centerViewDepth;
                float nearSide = saturate(0.5 + signedDepthOffset * _DepthResponse);
                float nearSideWeight = smoothstep(0.5, 1.0, nearSide) * rotationWeight;
                float farSideWeight = (1.0 - smoothstep(0.0, 0.5, nearSide)) * rotationWeight;

                float rainbowPhase =
                    (uv.x + uv.y * 0.65) * _RainbowScale +
                    sin((uv.x - uv.y) * 6.28318) * 0.35 +
                    t * _RainbowSpeed;

                float3 foilColor = HologramRainbow(rainbowPhase);
                float foilPattern =
                    0.55 +
                    0.45 * sin((uv.x * _FoilBandScale + uv.y * _FoilBandScale * 1.66 + t * 0.18) * 6.28318);

                float viewFoilBoost =
                    1.0 +
                    nearSideWeight * _ViewMetallicStrength +
                    nearSideWeight * _DepthMetallicStrength;
                float edgeDull = 1.0 - rotationWeight * _EdgeDullness;
                float3 foilScreen = 1.0 - (1.0 - baseColor.rgb) * (1.0 - foilColor * foilPattern * viewFoilBoost);
                float3 color = lerp(baseColor.rgb, foilScreen, _FoilStrength * edgeDull);

                float diagonal = saturate((uv.x - uv.y + 1.0) * 0.5);
                float linearTravel = frac(t * _ShineSpeed);
                float easedTravel = EaseInOutSine(linearTravel);
                float shineCenter = lerp(linearTravel, easedTravel, _ShineEaseAmount);
                float shineDistance = abs(diagonal - shineCenter);

                float shineBase =
                    1.0 - smoothstep(_ShineWidth, _ShineWidth + _ShineSoftness, shineDistance);
                float shineCore = pow(saturate(shineBase), _ShineFalloff);

                float3 metallicShine = lerp(float3(1.0, 1.0, 1.0), foilColor, 0.35);
                float shineViewBoost =
                    1.0 +
                    nearSideWeight * _DepthMetallicStrength;
                color += metallicShine * shineCore * _ShineStrength * shineViewBoost * baseColor.a;

                float viewDarken = 1.0 - rotationWeight * _ViewDarkenStrength;
                float depthDarken = 1.0 - farSideWeight * _DepthDarkenStrength;
                color *= viewDarken * depthDarken;

                baseColor.rgb = saturate(color);

                #ifdef UNITY_UI_CLIP_RECT
                baseColor.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(baseColor.a - 0.001);
                #endif

                return baseColor;
            }
            ENDCG
        }
    }
}
