// Blue Archive 풍 셀셰이딩 — 셀 음영+채색 그림자+림+머리 하이라이트 / 백페이스 외곽선
Shader "DefenseDot/BA_ToonLit"
{
    Properties
    {
        _BaseMap("Base Map", 2D) = "white" {}
        _BaseColor("Base Color", Color) = (1,1,1,1)
        _ShadowColor("Shadow Tint", Color) = (0.6,0.65,0.82,1)
        _ShadowThreshold("Shadow Threshold", Range(0,1)) = 0.5
        _ShadowSmooth("Shadow Smooth", Range(0.001,0.5)) = 0.05
        _RimColor("Rim Color", Color) = (1,1,1,1)
        _RimPower("Rim Power", Range(0.5,8)) = 4
        _RimIntensity("Rim Intensity", Range(0,3)) = 0.6
        [NoScaleOffset]_SpecMap("Hair Spec", 2D) = "black" {}
        _SpecColor("Spec Color", Color) = (1,1,1,1)
        _SpecIntensity("Spec Intensity", Range(0,3)) = 0
        _OutlineColor("Outline Color", Color) = (0.1,0.1,0.12,1)
        _OutlineWidth("Outline Width", Range(0,0.05)) = 0.012
        [NoScaleOffset]_DissolveTex("Dissolve Noise", 2D) = "gray" {}
        _DissolveAmount("Dissolve Amount", Range(0,1)) = 0
        _DissolveColor("Dissolve Edge", Color) = (1,0.6,0.1,1)
        _HitFlash("Hit Flash", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }

        // ── Pass 1: 셀 라이팅 ──
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half4 _ShadowColor;
                half _ShadowThreshold;
                half _ShadowSmooth;
                half4 _RimColor;
                half _RimPower;
                half _RimIntensity;
                half4 _SpecColor;
                half _SpecIntensity;
                half4 _OutlineColor;
                half _OutlineWidth;
                half _DissolveAmount;
                half4 _DissolveColor;
                half _HitFlash;
            CBUFFER_END

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_SpecMap); SAMPLER(sampler_SpecMap);
            TEXTURE2D(_DissolveTex); SAMPLER(sampler_DissolveTex);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS   : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float2 uv         : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                float3 posWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionWS = posWS;
                OUT.positionCS = TransformWorldToHClip(posWS);
                OUT.normalWS = normalize(TransformObjectToWorldNormal(IN.normalOS));
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half dn = SAMPLE_TEXTURE2D(_DissolveTex, sampler_DissolveTex, IN.uv).r;
                clip(dn - _DissolveAmount);
                half3 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv).rgb * _BaseColor.rgb;

                float4 shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                float3 N = normalize(IN.normalWS);
                float3 L = normalize(mainLight.direction);
                float3 V = normalize(GetCameraPositionWS() - IN.positionWS);

                half NdotL = dot(N, L);
                half halfLambert = NdotL * 0.5 + 0.5;
                half lit = halfLambert * mainLight.shadowAttenuation;
                half cel = smoothstep(_ShadowThreshold - _ShadowSmooth, _ShadowThreshold + _ShadowSmooth, lit);
                half3 baseCol = lerp(albedo * _ShadowColor.rgb, albedo, cel);

                // 림라이트 (Fresnel x 빛 방향)
                half fresnel = pow(1.0 - saturate(dot(N, V)), _RimPower);
                half3 rim = fresnel * saturate(NdotL) * _RimIntensity * _RimColor.rgb;

                // 머리 하이라이트 (Hair_Spec, 하프벡터)
                float3 H = normalize(L + V);
                half NdotH = saturate(dot(N, H));
                half3 spec = SAMPLE_TEXTURE2D(_SpecMap, sampler_SpecMap, IN.uv).rgb * NdotH * _SpecColor.rgb * _SpecIntensity;

                // 앰비언트는 직접광 곱셈 뒤에 가산 — 라이트가 꺼져도 남는다
                half3 col = (baseCol + rim + spec) * mainLight.color + albedo * SampleSH(N);
                col += _DissolveColor.rgb * step(dn - _DissolveAmount, 0.08) * step(0.001, _DissolveAmount);
                col = lerp(col, (half3)1.0, _HitFlash);
                return half4(col, 1.0);
            }
            ENDHLSL
        }

        // ── Pass 2: 외곽선 (백페이스 인버티드 헐) ──
        Pass
        {
            Name "Outline"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Front

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half4 _ShadowColor;
                half _ShadowThreshold;
                half _ShadowSmooth;
                half4 _RimColor;
                half _RimPower;
                half _RimIntensity;
                half4 _SpecColor;
                half _SpecIntensity;
                half4 _OutlineColor;
                half _OutlineWidth;
                half _DissolveAmount;
                half4 _DissolveColor;
                half _HitFlash;
            CBUFFER_END

            TEXTURE2D(_DissolveTex); SAMPLER(sampler_DissolveTex);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                // 외곽선을 월드 공간에서 밀어 오브젝트 스케일과 무관하게 일정 두께 유지
                float3 posWS = TransformObjectToWorld(IN.positionOS.xyz);
                float3 nWS = normalize(TransformObjectToWorldNormal(IN.normalOS));
                OUT.positionCS = TransformWorldToHClip(posWS + nWS * _OutlineWidth);
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half dn = SAMPLE_TEXTURE2D(_DissolveTex, sampler_DissolveTex, IN.uv).r;
                clip(dn - _DissolveAmount);
                return _OutlineColor;
            }
            ENDHLSL
        }

        // ── Pass 3: DepthOnly (URP Depth Priming/prepass 대응) ──
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half4 _ShadowColor;
                half _ShadowThreshold;
                half _ShadowSmooth;
                half4 _RimColor;
                half _RimPower;
                half _RimIntensity;
                half4 _SpecColor;
                half _SpecIntensity;
                half4 _OutlineColor;
                half _OutlineWidth;
                half _DissolveAmount;
                half4 _DissolveColor;
                half _HitFlash;
            CBUFFER_END

            TEXTURE2D(_DissolveTex); SAMPLER(sampler_DissolveTex);

            struct DAttr { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct DVary { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            DVary DepthVert(DAttr IN)
            {
                DVary OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }
            half DepthFrag(DVary IN) : SV_Target
            {
                half dn = SAMPLE_TEXTURE2D(_DissolveTex, sampler_DissolveTex, IN.uv).r;
                clip(dn - _DissolveAmount);
                return 0;
            }
            ENDHLSL
        }

        // ── Pass 4: ShadowCaster ──
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half4 _ShadowColor;
                half _ShadowThreshold;
                half _ShadowSmooth;
                half4 _RimColor;
                half _RimPower;
                half _RimIntensity;
                half4 _SpecColor;
                half _SpecIntensity;
                half4 _OutlineColor;
                half _OutlineWidth;
                half _DissolveAmount;
                half4 _DissolveColor;
                half _HitFlash;
            CBUFFER_END

            TEXTURE2D(_DissolveTex); SAMPLER(sampler_DissolveTex);
            float3 _LightDirection;

            struct SAttr { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct SVary { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            SVary ShadowVert(SAttr IN)
            {
                SVary OUT;
                float3 posWS = TransformObjectToWorld(IN.positionOS.xyz);
                float3 normWS = normalize(TransformObjectToWorldNormal(IN.normalOS));
                float3 biased = ApplyShadowBias(posWS, normWS, _LightDirection);
                OUT.positionCS = TransformWorldToHClip(biased);
                OUT.uv = IN.uv;
                return OUT;
            }
            half ShadowFrag(SVary IN) : SV_Target
            {
                half dn = SAMPLE_TEXTURE2D(_DissolveTex, sampler_DissolveTex, IN.uv).r;
                clip(dn - _DissolveAmount);
                return 0;
            }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Lit"
}
