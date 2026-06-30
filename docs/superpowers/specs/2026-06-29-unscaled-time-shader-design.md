# 일시정지 중 홀로그램 셰이더 애니메이션 복구 (Unscaled Time 주입)

**작성일**: 2026-06-29
**상태**: 설계 승인 완료
**관련 TASK**: TASK-014 B-0

---

## 1. 목표

카드 선택 모달·결과 화면 등 `Time.timeScale = 0` 상태에서 홀로그램 셰이더의 `*Speed` 프로퍼티(`_RainbowSpeed`·`_ShineSpeed`·`_FoilSpeed`·`_PulseSpeed`)가 멈추는 문제를 해소한다. 일시정지 중에도 홀로그램이 흐르도록 한다.

## 2. 근본 원인 (확정)

- 홀로그램 셰이더 6종이 애니메이션을 Unity 빌트인 `_Time.y` 기반으로 계산한다.
- `_Time`은 scaled time(`Time.time`) 기반이라 `Time.timeScale = 0`이면 증가를 멈춘다. Unity 공식 문서상 unscaled time을 제공하는 빌트인 셰이더 변수는 없다.
- 카드 모달은 `CardSelectionPresenter.cs:58`에서 `Time.timeScale = 0f`를 걸어 **항상 정지 상태에서만 표시**되므로, 홀로그램 흐름이 완전히 멈춰 `*Speed`가 전부 무효처럼 보인다.
- 방증: `CardSelectionView.cs:98`은 `Time.unscaledDeltaTime`을 써서 C# 등장 연출은 정지 중에도 동작한다 → C#과 셰이더의 시간 기준 불일치.

## 3. 설계

### 3.1 신규 컴포넌트 `UnscaledTimeShaderDriver`

- 경로: `Assets/Scripts/Systems/Rendering/UnscaledTimeShaderDriver.cs`
- `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]` static 진입점이 부팅 시 숨김 `DontDestroyOnLoad` 객체를 1개 생성하고 컴포넌트를 부착한다. **인스펙터 배선·프리팹 수정 불필요** — 모든 씬/모드에서 자동 동작.
- `Update()`에서 매 프레임 `Shader.SetGlobalFloat(propertyId, Time.unscaledTime)`로 주입한다. `propertyId`는 `Shader.PropertyToID("_UnscaledTime")`를 정적 캐싱.

### 3.2 셰이더 6종 — `_Time.y` → `_UnscaledTime`

각 셰이더 uniform 선언부에 `float _UnscaledTime;`를 추가하고(글로벌이라 `Properties` 블록엔 불필요), 시간 참조만 치환한다.

| 셰이더 | 라인 | 변경 |
|---|---|---|
| HologramFoilTinted | 157 | `float t = _Time.y;` → `float t = _UnscaledTime;` |
| HologramImage | 161 | `float t = _Time.y;` → `float t = _UnscaledTime;` |
| UIHologramAlphaGlowOnly | 142 | `_Time.y * _PulseSpeed` → `_UnscaledTime * _PulseSpeed` |
| UIHologramFoilGlowOnly | 139 | `_Time.y * _PulseSpeed` → `_UnscaledTime * _PulseSpeed` |
| UIHologramCompositeGlow | 174 | `_Time.y * _FoilSpeed` → `_UnscaledTime * _FoilSpeed` |
| UIHologramFoilBloom | 156 | `_Time.y * _FoilSpeed` → `_UnscaledTime * _FoilSpeed` |

## 4. 동작·영향

- **게임 중(timeScale=1)**: `unscaledTime ≈ time`이라 기존과 사실상 동일하게 흐른다 (회귀 없음).
- **일시정지(timeScale=0 — 카드 모달, 결과 화면)**: 홀로그램이 계속 흐른다 → `*Speed` 정상 작동.
- **에디터 비플레이 머티리얼 프리뷰**: 글로벌 주입이 안 돌아 프리뷰만 정지한다 (게임 플레이 무관, 허용 트레이드오프).

## 5. 검증

- PlayMode + Unity MCP: `timeScale=0` 설정 후 `_UnscaledTime` 글로벌이 증가하는지 + 카드 포일이 실제로 흐르는지 캡처로 확인.
- 컴파일 0 에러.

## 6. YAGNI — 의도적 제외

- 머티리얼별 `material.SetFloat` (등급 머티리얼이 여러 개라 글로벌 주입이 효율적).
- 일시정지 전용 on/off 토글 (항상 주입해도 회귀 없음).
- 에디터 비플레이 프리뷰 대응(`ExecuteAlways`).

## 7. 영향 파일

| 파일 | 변경 |
|---|---|
| `Assets/Scripts/Systems/Rendering/UnscaledTimeShaderDriver.cs` | 신규 — 글로벌 unscaled time 주입 드라이버 |
| `Assets/Shader/HologramFoilTinted.shader` | `_UnscaledTime` uniform + `t` 치환 |
| `Assets/Shader/HologramImage.shader` | 동일 |
| `Assets/Shader/UIHologramAlphaGlowOnly.shader` | `_UnscaledTime` uniform + `_PulseSpeed` 항 치환 |
| `Assets/Shader/UIHologramFoilGlowOnly.shader` | 동일 |
| `Assets/Shader/UIHologramCompositeGlow.shader` | `_UnscaledTime` uniform + `_FoilSpeed` 항 치환 |
| `Assets/Shader/UIHologramFoilBloom.shader` | 동일 |
