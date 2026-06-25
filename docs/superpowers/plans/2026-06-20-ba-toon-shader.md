# BA_ToonLit Shader Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Aris 모델에 Blue Archive 풍 셀셰이딩(셀 음영+채색 그림자+림라이트+머리 하이라이트+외곽선)을 입히는 단일 HLSL URP 셰이더를 작성하고 격리 적용한다.

**Architecture:** 한 개의 `BA_ToonLit.shader`에 두 패스를 둔다 — `UniversalForward`(셀 라이팅) + `SRPDefaultUnlit`(백페이스 헐 외곽선). Aris의 8개 머티리얼이 이 셰이더로 교체되고, FBX 리맵이 그 머티리얼을 가리키므로 이 모델에만 적용된다.

**Tech Stack:** Unity 6000.4, URP 17 (HLSL), Unity MCP(`create_script`/`execute_code`/`read_console`/`manage_camera`).

## Global Constraints

- Unity 6000.4 / URP 17 기준 HLSL 셰이더 (ShaderGraph 아님)
- 외부 원본 FBX·텍스처·타 모델 머티리얼·씬 무수정 (셰이더 신규 + Aris 8개 .mat만 수정)
- 격리: 렌더러 전역 설정(RenderObjects 등) 변경 금지
- 셰이더는 단위 테스트 불가 → 컴파일 검증(`read_console`) + 시각 검증(프리뷰 블룸 off) 게이트
- 커밋은 사용자 명시 요청 시에만 (자동 커밋 금지)

---

### Task 1: BA_ToonLit.shader 작성 + 컴파일 검증

**Files:**
- Create: `Assets/ExternalResources/BlueArchive/Aris/Shaders/BA_ToonLit.shader`

**Interfaces:**
- Produces: `Shader "DefenseDot/BA_ToonLit"` — 프로퍼티 `_BaseMap,_BaseColor,_ShadowColor,_ShadowThreshold,_ShadowSmooth,_RimColor,_RimPower,_RimIntensity,_SpecMap,_SpecColor,_SpecIntensity,_OutlineColor,_OutlineWidth`

- [ ] **Step 1: 셰이더 파일 작성**

Properties 블록 + 2 패스 구조:

```hlsl
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
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST; half4 _BaseColor; half4 _ShadowColor;
                half _ShadowThreshold; half _ShadowSmooth;
                half4 _RimColor; half _RimPower; half _RimIntensity;
                half4 _SpecColor; half _SpecIntensity;
                half4 _OutlineColor; half _OutlineWidth;
            CBUFFER_END
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_SpecMap); SAMPLER(sampler_SpecMap);

            struct A { float4 posOS:POSITION; float3 nOS:NORMAL; float2 uv:TEXCOORD0; };
            struct V { float4 posCS:SV_POSITION; float3 nWS:TEXCOORD0; float3 posWS:TEXCOORD1; float2 uv:TEXCOORD2; };

            V vert(A i){
                V o; VertexPositionInputs p = GetVertexPositionInputs(i.posOS.xyz);
                o.posCS=p.positionCS; o.posWS=p.positionWS;
                o.nWS=normalize(GetVertexNormalInputs(i.nOS).normalWS);
                o.uv=TRANSFORM_TEX(i.uv,_BaseMap); return o;
            }
            half4 frag(V i):SV_Target{
                half3 albedo = SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv).rgb*_BaseColor.rgb;
                float4 sc = TransformWorldToShadowCoord(i.posWS);
                Light mainL = GetMainLight(sc);
                float3 N=normalize(i.nWS), L=normalize(mainL.direction);
                float3 V=normalize(GetCameraPositionWS()-i.posWS);
                half NdotL=dot(N,L);
                half hl=NdotL*0.5+0.5;
                half shadowAtten=mainL.shadowAttenuation;
                half cel=smoothstep(_ShadowThreshold-_ShadowSmooth,_ShadowThreshold+_ShadowSmooth,hl*shadowAtten);
                half3 baseCol=lerp(albedo*_ShadowColor.rgb,albedo,cel);
                half fres=pow(1-saturate(dot(N,V)),_RimPower);
                half3 rim=fres*saturate(NdotL)*_RimIntensity*_RimColor.rgb;
                float3 H=normalize(L+V); half NdotH=saturate(dot(N,H));
                half3 spec=SAMPLE_TEXTURE2D(_SpecMap,sampler_SpecMap,i.uv).rgb*NdotH*_SpecColor.rgb*_SpecIntensity;
                half3 col=(baseCol+rim+spec)*mainL.color;
                return half4(col,1);
            }
            ENDHLSL
        }

        // ── Pass 2: 외곽선 (백페이스 헐) ──
        Pass
        {
            Name "Outline"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Front
            HLSLPROGRAM
            #pragma vertex vo; #pragma fragment fo
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST; half4 _BaseColor; half4 _ShadowColor;
                half _ShadowThreshold; half _ShadowSmooth;
                half4 _RimColor; half _RimPower; half _RimIntensity;
                half4 _SpecColor; half _SpecIntensity;
                half4 _OutlineColor; half _OutlineWidth;
            CBUFFER_END
            struct A{float4 posOS:POSITION; float3 nOS:NORMAL;};
            struct V{float4 posCS:SV_POSITION;};
            V vo(A i){
                V o; float3 posOS=i.posOS.xyz+normalize(i.nOS)*_OutlineWidth;
                o.posCS=TransformObjectToHClip(posOS); return o;
            }
            half4 fo(V i):SV_Target{ return _OutlineColor; }
            ENDHLSL
        }
    }
}
```

- [ ] **Step 2: 파일 임포트 + 컴파일 검증**

`refresh_unity(scope=all, mode=force)` → `read_console(types=[Error,Warning])`
Expected: BA_ToonLit 관련 셰이더 컴파일 에러 0건. (있으면 include 경로·CBUFFER·struct 수정 후 재검증)

---

### Task 2: 8개 머티리얼 셰이더 교체 + 텍스처/기본값 배선

**Files:**
- Modify: `Assets/ExternalResources/BlueArchive/Aris/Materials/*.mat` (8개)

**Interfaces:**
- Consumes: `Shader "DefenseDot/BA_ToonLit"` (Task 1), 기존 `_BaseMap`(URP/Lit에서 매칭됨), `Aris_Original_Hair_Spec` 텍스처

- [ ] **Step 1: execute_code 로 셰이더 교체 + 배선**

각 머티리얼: 현재 `_BaseMap` 텍스처 보존 → 셰이더를 `DefenseDot/BA_ToonLit`로 교체 → `_BaseMap` 재설정 → 이름에 "Hair" 포함 시 `_SpecMap=Aris_Original_Hair_Spec`, `_SpecIntensity=1.2` 설정 → "Face"/"EyeMouth" 포함 시 `_ShadowSmooth=0.12`(얼굴 그림자 부드럽게) → `EditorUtility.SetDirty` → `SaveAssets`.

```csharp
var sh = Shader.Find("DefenseDot/BA_ToonLit");
var hairSpec = AssetDatabase.LoadAssetAtPath<Texture>(".../Aris_Original_Hair_Spec.png");
foreach (mat in Materials/*.mat) {
    var albedo = mat.GetTexture("_BaseMap");
    mat.shader = sh;
    if (albedo) mat.SetTexture("_BaseMap", albedo);
    if (name.Contains("Hair")) { mat.SetTexture("_SpecMap", hairSpec); mat.SetFloat("_SpecIntensity",1.2f); }
    if (name.Contains("Face")||name.Contains("EyeMouth")) mat.SetFloat("_ShadowSmooth",0.12f);
    EditorUtility.SetDirty(mat);
}
AssetDatabase.SaveAssets();
```

- [ ] **Step 2: 배선 검증**

execute_code: 8개 머티리얼의 `shader.name=="DefenseDot/BA_ToonLit"`, `_BaseMap!=null` (8/8), Hair 머티리얼 `_SpecMap!=null` 확인.
Expected: shader=8, baseMap=8, hairSpec≥1.

---

### Task 3: 프리뷰 시각 검증 + 파라미터 튜닝

**Files:** (없음 — 검증/튜닝)

- [ ] **Step 1: 프리뷰 재생성 + 블룸 off 카메라 + 캡처**

execute_code로 `ArisPreview` 인스턴스화(pos 0,30,0) + 메시 bounds로 앞면 프레이밍 + `ArisPreviewCam`에 `UniversalAdditionalCameraData.renderPostProcessing=false` → `manage_camera(screenshot, ArisPreviewCam)`.
Expected: 셀 음영(2~3톤)·외곽선·림·머리 하이라이트가 보임.

- [ ] **Step 2: 파라미터 튜닝**

스샷 판단으로 머티리얼 파라미터 조정 — 음영 약하면 `_ShadowThreshold`↑, 외곽선 두꺼우면 `_OutlineWidth`↓, 림 과하면 `_RimIntensity`↓, 머리 하이라이트 약하면 `_SpecIntensity`↑. 재캡처로 확인.

- [ ] **Step 3: 미리보기 정리**

`ArisPreview`·`ArisPreviewCam` DestroyImmediate (씬 저장 안 함).

---

## 완료 기준

- BA_ToonLit.shader 컴파일 에러 0
- Aris 8개 머티리얼 = BA_ToonLit, albedo 유지, Hair에 Spec 연결
- 프리뷰에서 BA 풍 셀셰이딩 4요소(셀/외곽선/림/머리) 가시 확인
- 타 모델·씬·외부 원본 무영향
