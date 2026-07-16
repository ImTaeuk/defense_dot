# TASK-018: 레비나 폭풍 계보 능력 세부 구현

**작성일**: 2026-07-14
**상태**: 진행 중 (잎 풀 편입·실플레이 등반 검증 완료 · 스탯/VFX 미완)
**우선순위**: 중간 (계보 다단 실플레이 시)

---

## 1. 배경

원작 dot-defense 레비나(번개술사) 시그니처 라인을 레퍼런스해 **Aris 타워 계보를 높이 3 다층 트리로 구축**하고, 다단 합성 등반이 실제 작동함을 검증 완료했다(코드 변경 0, 데이터만).

**검증됨**: 1층→2층→3층 각 단계에서 합성 카드 제시·재료 소진·결과 획득, 정점(StormSovereign) 획득, 기존 분기(Shot+Orbital→AreaWave) 공존. FusionSystem 평면 리스트 구조가 다단+분기를 그대로 지원.

## 2. 현재 상태 (구조만 · 세부 미완)

**신규 능력 7개** (`Assets/Data/Abilities/Ability_*.asset`) — 전부 **기존 에셋 복제본**이라 세부가 임시:
| id | 타입 | 복제원본 | 계보 위치 |
|---|---|---|---|
| stormBrand | Projectile | Ability_Shot | 잎 |
| stunBomb | AreaWave | Ability_AreaWave | 잎 |
| tornado | Orbital | Ability_Orbital | 잎 |
| railgun | Projectile | Ability_Shot | 잎 |
| levinLash | AreaWave | Ability_AreaWave | 중간① (t2) |
| tempestVeil | AreaWave | Ability_AreaWave | 중간② (t3) |
| stormSovereign | AreaWave | Ability_AreaWave | 정점 (t4) |

**계보**: `Assets/Data/Fusion/Aris_FusionLineage.asset` — 레시피 4개(기존 1 + 체인 3).

## 3. 후속 TODO (세부)

- **A. 스탯/밸런스**: 각 능력의 damage·pierce·cooldown·baseCost를 티어별로 차등(정점이 가장 강). 현재는 원본 복제값 그대로.
- **B. 이펙트/VFX**: 현재 프로젝타일 2종은 Shot, 에어리어 4종은 AreaWave와 **완전히 동일한 외형**. 최소한 티어·능력별로 구분되는 VFX/색.
- **C. 능력 획득 경로(실플레이)** ✅ 완료: 잎 4종(stormBrand·stunBomb·tornado·railgun)을 `AbilityPool.asset`에 편입 → 신규 카드로 뽑힘 확인. 실제 플레이(카드 UI)로 1층→2층→3층 정점(StormSovereign)까지 등반 완주 검증. (계보 결과는 `IsResult`가 풀에서 배제)
- **D. 표시**: displayName 한글 확정, description·icon 지정(현재 복제본 아이콘 공유).
- **E. (선택) 타입 매핑 재검토**: tornado=Orbital 등 매핑이 능력 성격에 맞는지.

## 4. 참고

- 원작 레퍼런스: `Assets/Reference/dot-defense-main/index.html` COMBO_RECIPES(레비나 라인: stormBrand+stunBomb→levinLash → +tornado→tempestVeil(t3) → +railgun→stormSovereign(t4))
- 구조: `Assets/Scripts/Systems/Cards/FusionSystem.cs` (평면 List<FusionRecipe>, 결과→재료 체이닝으로 다단)
- 관련: 계보 비주얼 에디터 TASK-017, 계보 설계 의도(캐릭터 고유 능력 획득 유도)
