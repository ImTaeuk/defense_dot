# TASK-014: Arena 적 렌더·피격 이펙트 복구

**작성일**: 2026-06-23 (갱신: 2026-06-29)
**상태**: 적 렌더·피격 이펙트 정리 완료 / ✅머티리얼 `*Speed` 복구(B-0) · 연출 강화·커밋·배경 대기
**우선순위**: 높음

---

## 1. 문제 정의
투사체 교체(Projectile_Water)·적 3D화(Sweeper) 이후 "피격 이펙트가 둘 다 사라졌다" 보고. 이후 추적 과정에서 **적 자체가 검은 실루엣으로만 렌더되는 더 근본적인 문제**가 드러남.

### 1.1 증상
- 투사체가 적에 명중해도 폭발/물 튀김 VFX 가 안 나옴.
- 적이 맞아도 피격 반응이 안 보임.
- **적 본체가 검은 실루엣으로만 보임** (색·피격·사망 연출 전부 묻힘).

### 1.2 근본 원인 (확정)
1. **명중 폭발 VFX 부재** — `Projectile_Water` 의 `hitVfxPrefab` 이 null. 원본 Hovl 의 자체 명중 스폰 스크립트(`HS_ProjectileMover`)를 제거하면서 대체 VFX 를 안 채움. → `Hit_Water` 복사·배선으로 해결.
2. **적 검은 실루엣 (핵심)** — `BA_ToonLit` 셰이더의 **ForwardLit(UniversalForward, 본체) 패스가 적 Sweeper 메시에서만 draw 누락**. Outline(SRPDefaultUnlit) 패스만 그려져 검은 실루엣이 됨. → **정확한 메커니즘은 미규명**, 적 색 머티리얼을 **URP/Lit 로 교체**하여 우회 해결.

> 초기 진단(2026-06-23)은 "`_HitFlash`/`_DissolveAmount` MPB 가 SRP Batcher 에 무시됨"으로 봤으나, MPB→material 전환 후에도 적은 여전히 검정이었음 → MPB 는 부분 원인이 아니라 **무관**했고, 진짜 원인은 ForwardLit 패스 누락이었음.

### 1.3 배제 검증 (적 검정 — 모두 정상/무관 확인)
- 셰이더 자체: 큐브+BA_ToonLit = 정상 / Aris 메시+BA_ToonLit = 정상 / 적 메시+URP/Lit = 정상
- 머티리얼(에셋·`new Material`), MPB, `_DissolveTex`, GPU skinning(off 포함), vert 변환(`GetVertexPositionInputs`↔명시)
- 렌더링 모드 = **ForwardPlus** 확인 후 `_CLUSTER_LIGHT_LOOP`·`multi_compile_instancing` 보강 → 효과 없음
- DepthOnly·ShadowCaster 패스 추가 → 효과 없음 (depthPriming=Disabled)
- 스케일(RG GO localScale=589.85), 메시 import(normals/optimizeMesh/Read-Write), bounds/updateWhenOffscreen
- frag 강제색 테스트: Aris=색 적용 / 적=검정 → 적 메시에서 ForwardLit 패스 자체가 그려지지 않음 확정

## 2. 구조적 변경 목록
| 파일/자산 | 변경 | 목적 |
|---|---|---|
| Sweeper_Mint/Pink/Yellow.mat | 셰이더 BA_ToonLit → **URP/Lit** (_BaseMap 유지) | 적 검은 실루엣 해결(본체 렌더 복구) |
| SweeperEnemyVisual.cs | **재작성** — emission/HitFlash/OnHit 구독 제거, 이동방향 회전 + 사망 축소(DeathRoutine scale lerp)만 유지 | 사용자 미요청 흰색 번쩍임 제거 |
| Projectile_Water.prefab | `hitVfxPrefab = Hit_Water` 배선(유지) | 명중 폭발 VFX |
| Hit_Water.prefab | Hovl "Hit 9 water" 복사본(신규) | 명중 파티클 |
| BA_ToonLit.shader | outline 월드공간 변환 / ForwardLit vert 명시변환 / DepthOnly·ShadowCaster 패스 / `_CLUSTER_LIGHT_LOOP`·instancing | (Aris 전용) URP 표준 보강 — 적 검정엔 무기여, Aris 호환성 개선 |

## 3. 진행 상황
- **A. 적 렌더·피격 이펙트 정리 (완료)**
  - A-1. ✅ 적 검은 실루엣 해결 — Sweeper 머티리얼 URP/Lit 교체 (색 정상 표시 확인)
  - A-2. ✅ 흰색 번쩍임(emission) 제거 — 사용자 요청
  - A-3. ✅ 명중 파티클 = Hit 9 water 유지 — 파티클 정상(`HS_Blend_CG` URP 호환, emit 확인), 어두운 배경에선 선명
- **B. 연출 강화 (대기)**
  - B-0. ✅ 머티리얼 `*Speed` 복구 — 카드 홀로그램 셰이더 6종이 `_Time.y`(scaled time) 사용 → `timeScale=0`(카드 모달·결과 화면) 에서 `_Time` 정지가 원인(확정). `UnscaledTimeShaderDriver`(부팅 시 자동 생성, 매 프레임 글로벌 `_UnscaledTime`=`Time.unscaledTime` 주입) + 셰이더 6종 `_Time.y`→`_UnscaledTime` 치환으로 해결. 검증: `timeScale=0` 에서 `_UnscaledTime` 16.9→38.6 증가(기존 scaled `Time.time` 은 8.1 정지). 설계: `docs/superpowers/specs/2026-06-29-unscaled-time-shader-design.md`
  - B-1. 시전 중 푸른 입자 차징 연출
  - B-2. 타격감 강화 (히트스톱·카메라 셰이크)

## 4. 검증
- 컴파일 0 에러.
- 적 색 정상 표시 — 메인 카메라(post off) 캡처로 Mint/Pink/Yellow 확인.
- Hit_Water 파티클 — 어두운 배경(scale 5) 캡처에서 물 튀김·스파크 선명 확인.
- **명중 시 Hit 9 water 가 게임 화면에서 안 보이는 것**은 버그가 아니라 **배경이 밝은 하늘색 placeholder** 라 연한 블렌드 파티클이 묻히는 것. 배경 이미지 작업 후 선명해질 예정. (사용자 판단: "일단 이대로 진행")

## 5. 잔여 과제 / 주의
- **명중 Hit_Water 크기**: 현재 `SpawnOneShot(..., scale 1)`. 적이 589배라 작을 수 있음 — 배경 작업 후 작으면 크기만 키우면 됨.
- **미커밋**: 이번 변경 전부 커밋 안 됨(사용자 미요청). SweeperEnemyVisual·머티리얼·BA_ToonLit·FBX import + (B-0) 홀로그램 셰이더 6종·UnscaledTimeShaderDriver·스펙 문서 등.
- **FBX import 변경**(RG_Sweeper FBX: isReadable/normals/optimizeMesh) — 적 검정과 무관했음, 원복 검토.
- **적 RG GO localScale=589.85** — 의도(3배)와 다름, 별도 정리 대상.
- "안 죽음"은 밸런스(baseDamage=1 vs health=10), 버그 아님.

## 6. 참고: 원인 미규명 항목
- 같은 적 Sweeper 메시에 URP/Lit 은 정상 렌더되나 BA_ToonLit 의 UniversalForward 패스만 누락되는 정확한 메커니즘은 MCP 도구로 규명 못 함. 추후 필요 시 Unity Frame Debugger 로 적 메시의 ForwardLit draw call 누락 지점 직접 확인 권장.
