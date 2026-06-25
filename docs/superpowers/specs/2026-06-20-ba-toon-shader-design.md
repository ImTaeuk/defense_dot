# Blue Archive 풍 Toon Lit 셰이더 설계

**작성일**: 2026-06-20
**상태**: 설계 승인 완료
**대상**: `Assets/ExternalResources/BlueArchive/Aris` 모델 (BA 전투용 SD/데포르메 3D 모델)
**범위**: 실용형 "BA 느낌" — 셀 음영 + 외곽선 + 림라이트 + 머리 하이라이트

---

## 1. 목표 / 성공 기준

- Aris 모델에 **Blue Archive 풍 셀셰이딩** 룩을 입힌다. albedo 텍스처는 그대로 두고 **명암 처리(셰이딩)만** BA 풍으로 바꾼다.
- **이 모델에만 격리** 적용한다. 외부 원본 FBX·텍스처·타 모델 머티리얼은 건드리지 않는다.
- 성공 기준: 프리뷰(블룸 off)에서 ① 2~3톤 셀 음영(채색 그림자), ② 캐릭터 외곽선, ③ 빛 쪽 실루엣 림라이트, ④ 머리카락 하이라이트가 보이고, "평범한 URP 디폴트 + 툰" 수준의 깔끔함을 유지한다.

## 2. 배경 조사 요약 (judge 웹 조사)

- BA 전투 화면은 **데포르메(SD) 3D 모델**을 쓴다. 우리 Aris 모델이 정확히 그 대상이다.
- BA 공식 셰이더 1차 자료는 비공개. 따라서 검증된 **애니 NPR(셀셰이딩) 표준 기법**으로 재현한다: 바디 `NdotL→ramp`, 얼굴 SDF 그림자, 백페이스 헐 외곽선, `Fresnel×NdotL` 림, 머리 angel-ring 하이라이트.
- 견고한 구현은 전부 **HLSL**(ShaderGraph 아님). URP 17 기준으로 직접 작성한다.
- 모델 동봉 mask 텍스처가 BA 셰이더용 채널 데이터로 확인됨: **Face_Mask**(코 그림자 SDF 곡선), **Hair_Mask**(머리 음영/스펙 영역), **Hair_Spec**(컬러 하이라이트), **Body_Mask**(부위 분할). 단 정확한 채널 의미는 웹 미문서화 → 실측 영역.
- **결정**: 실용형 1차는 `Hair_Spec` 중심으로 활용하고, `Face_Mask` SDF·Body/Hair_Mask 정밀 채널은 **정밀형(후속)** 으로 분리. 얼굴은 부드러운 ramp 음영으로 처리.

## 3. 아키텍처 / 파일 구조

| 구분 | 경로 | 책임 |
|---|---|---|
| 신규 셰이더 | `Assets/ExternalResources/BlueArchive/Aris/Shaders/BA_ToonLit.shader` | URP HLSL 툰 라이팅 + 외곽선 2패스 |
| 기존 머티리얼 | `Assets/ExternalResources/BlueArchive/Aris/Materials/*.mat` (8개) | 셰이더를 `BA_ToonLit`으로 교체, `_BaseMap` 유지 |
| FBX 리맵 | `Aris_Original.fbx` (ModelImporter remap) | 이미 위 8개 `.mat`을 가리킴 → 자동 적용 |

- 격리: `BA_ToonLit`은 이 8개 머티리얼만 사용. 렌더러 전역 설정 변경 없음 → 타 모델 무영향.

## 4. 셰이더 패스 설계

### Pass 1 — ForwardLit (`Tags{ "LightMode"="UniversalForward" }`)

| 요소 | 계산 |
|---|---|
| 셀 음영 | `halfLambert = NdotL*0.5+0.5` → `smoothstep(_ShadowThreshold-_ShadowSmooth, _ShadowThreshold+_ShadowSmooth, halfLambert)` 로 단계화. 메인 라이트 그림자(receive) 곱 |
| 채색 그림자 | `lit = albedo`, `shadow = albedo * _ShadowColor`(기본 푸른/보라 틴트), 셀 경계로 `lerp(shadow, lit, t)` |
| 림라이트 | `fresnel = pow(1-saturate(NdotV), _RimPower)`, `rim = fresnel * saturate(NdotL) * _RimIntensity`, `+ _RimColor` 가산 |
| 머리 하이라이트 | `_SpecMap`(=Hair_Spec) 컬러를 하프벡터(`NdotH`) 기반으로 `* _SpecColor * _SpecIntensity` 가산. 머리 머티리얼만 `_SpecMap` 보유 |
| 최종 | `(lerp(shadow,lit,cel) + rim + hairSpec) * lightColor` |

- 라이트: 씬 메인 디렉셔널(`GetMainLight`) 방향/색. 추가 라이트는 1차 범위에서 선택(과하면 생략).

### Pass 2 — Outline (백페이스 헐)

- `Cull Front`, 정점을 노멀 방향으로 `_OutlineWidth` 확장(클립공간/뷰공간 보정), `_OutlineColor` 단색 출력.
- 균일 두께(파라미터). 하드엣지 분리 발생 시 스무딩 노멀 보정 검토(모델 노멀 상태 확인 후 결정).

## 5. 머티리얼 프로퍼티

```
_BaseMap (2D)            // albedo (8개 이미 매칭)
_BaseColor (Color)
_ShadowColor (Color)     // 채색 그림자 틴트, 기본 (0.6,0.65,0.8)
_ShadowThreshold (0..1)  // 셀 경계 위치
_ShadowSmooth (0..0.5)   // 경계 부드러움
_RimColor (Color) _RimPower (float) _RimIntensity (float)
_SpecMap (2D) _SpecColor (Color) _SpecIntensity (float)  // 머리
_OutlineColor (Color) _OutlineWidth (float)
```

## 6. 텍스처·라이팅 매핑

- `_BaseMap` = 각 부위 albedo (Body/Face/EyeMouth/Hair/Halo/Weapon — 매칭 완료)
- `_SpecMap` = `Aris_Original_Hair_Spec` (Hair 머티리얼만)
- 라이팅 = ArenaScene 메인 디렉셔널 라이트(현 intensity 1.3). 탑다운 각도에서 음영 약하면 `_ShadowThreshold` 조정
- mask(Face/Body/Hair_Mask)는 1차 미사용(불확실 채널) → 정밀형 후속

## 7. 검증

1. 셰이더 컴파일 에러 확인 (`read_console`)
2. 프리뷰 카메라(블룸 off) 스샷으로 셀/외곽선/림/머리 하이라이트 확인
3. 부위별 머티리얼 파라미터 튜닝 (얼굴 그림자 약하게, 머리 spec 강조 등)

## 8. 범위 외 (후속 / 정밀형)

- `Face_Mask` 기반 SDF 얼굴 그림자(코 그림자 라이트 스윕)
- Body/Hair_Mask 정밀 채널 실측·활용
- 추가 라이트 다중 처리, 정점 컬러 외곽선 두께, matcap 머리 하이라이트
