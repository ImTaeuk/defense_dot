# TASK-014: Aris 코어 전투 연출 복구 (명중 VFX·피격 플래시)

**작성일**: 2026-06-23
**상태**: 진행 중 (복구 수정 완료·커밋 8b7932d8·06a39d48·611219b9 / Play 시각 검증 대기)
**우선순위**: 높음

---

## 1. 문제 정의
투사체 교체(Projectile_Water)·적 3D화(Sweeper) 이후 "피격 이펙트가 둘 다 사라졌다" 보고.

### 1.1 증상
- 투사체가 적에 명중해도 폭발/물 튀김 VFX 가 안 나옴
- 적이 맞아도 흰색 피격 플래시가 안 보임 (사망 dissolve 도 동일 증상)

### 1.2 근본 원인 (2건, 독립)
1. **명중 폭발 VFX 부재** — `Projectile_Water` 의 `hitVfxPrefab` 이 null. 원본 Hovl 의 자체 명중 스폰 스크립트(`HS_ProjectileMover`)를 제거하면서 대체 VFX 를 안 채움.
2. **적 플래시·dissolve 미표시** — `SweeperEnemyVisual` 이 `_HitFlash`·`_DissolveAmount` 를 `MaterialPropertyBlock` 으로 설정. 두 프로퍼티가 `BA_ToonLit` 의 `CBUFFER_START(UnityPerMaterial)` 안에 있어 SRP Batcher 가 MPB per-renderer override 를 무시함.

### 1.3 추적 경로
- 명중 VFX: `ProjectileEffect.Update` 의 `if (hitVfxPrefab != null)` 분기가 null 로 건너뜀.
- 적 플래시: 구독(owner)·발화(OnHit)·`hitTimer` 모두 정상. 강제 `_HitFlash=1`(MPB) 에도 흰색 미적용 → MPB 미반영 확정. (`lerp(col,1,1)=흰색` 이어야 하나 검정)

## 2. 구조적 문제 목록
| 파일:위치 | 문제 | 수정 |
|---|---|---|
| ProjectileEffect.cs:66 | `hitVfxPrefab` null | `Hit_Water` 프리팹 복사·배선 |
| SweeperEnemyVisual.cs `SetRendererFloat` | MPB 가 UnityPerMaterial 에 미반영 | `runtimeMat`(material 인스턴스) 직접 `SetFloat` |
| SweeperEnemyVisual.cs `ApplyRandomColor` | 색을 sharedMaterial 로 설정 | 인스턴스에 `CopyPropertiesFromMaterial` |
| SweeperEnemyVisual.cs | 인스턴스 수명 미정리 | `OnDestroy` 에서 `Destroy(runtimeMat)` |

## 3. TODO
- **A. 복구 (완료)**
  - A-1. ✅ `Hit 9 water` → `Hit_Water.prefab` 복사 후 `hitVfxPrefab` 배선
  - A-2. ✅ `SweeperEnemyVisual` MPB → material 인스턴스 전환 (+OnDestroy 정리)
- **B. 연출 강화 (대기)**
  - B-1. 시전 중 푸른 입자 차징 연출
  - B-2. 타격감 강화 (히트스톱·카메라 셰이크)

## 4. 검증
- 컴파일 0 에러, `runtimeMat`/`OnDestroy` 반영 확인.
- Play 시각 검증(명중 폭발·적 플래시·dissolve) — **사용자 확인 대기**.

## 5. 예상 영향도
- `ProjectileEffect`: 배선만(코드 무변).
- `SweeperEnemyVisual`: 렌더링 경로 변경(적당 머티리얼 인스턴스 1개 — 적 수 적어 부담 낮음).
- `Hit_Water.prefab`: 신규 복사본(원본 미수정).

## 7. 참고: 설계 패턴
- per-instance 셰이더 효과는 SRP Batcher 호환 셰이더에서 MPB 가 무시될 수 있으므로 **material 인스턴스 직접 제어**가 안전. (장기적으로 GPU instancing per-instance 프로퍼티 검토 가능 — TASK-013 연계)
