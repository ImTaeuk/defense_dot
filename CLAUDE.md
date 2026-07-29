# Unity Project Development Guide for AI Agents

## Project Context

You are working on a **Unity project** used to build games or interactive experiences. Unity is a cross-platform game engine that uses C# for scripting and organizes content into scenes, prefabs, and assets.

## TASK 관리 (마스터 인덱스 — 모든 세션 필수)

- **남은 작업·우선순위·"다음에 뭐 하지"를 묻는 질문에는 반드시 [`docs/tasks/TASKS-master-index.md`](docs/tasks/TASKS-master-index.md) 를 먼저 읽고 그 문서를 근거로 답한다.** 이 문서가 남은 작업의 단일 진실 공급원(SSOT)이며, 원작 갭 분석 + 우선도×개발비용 매트릭스 + 활성 TASK 인덱스를 담는다. (개별 `docs/tasks/active/TASK-*.md` 는 상세, 마스터는 전체 그림)
- **TASK 를 완료하거나 상태가 바뀌면 마스터 인덱스를 갱신한다** — §2 갭 표 상태(`❌→🔶→✅`)·§6 인덱스·§7 갱신 이력을 그때그때 수정한다. 개별 TASK 문서만 고치고 마스터를 방치하지 않는다.
- **상태 판단은 문서 기술이 아니라 코드·git 실제 상태로 확인한다** (문서의 "미커밋" 등 옛 기술을 그대로 믿어 오판한 이력 있음).
- 마스터의 짝 HTML(`TASKS-master-index.html`)은 브라우저 열람용 — 내용 갱신 시 MD·HTML 둘 다 반영.

## UI / 폰트 컨벤션

- **폰트**: 모든 UI 텍스트(TextMeshPro)는 **neodgm** (`Assets/Font/neodgm SDF.asset`) 을 사용한다. 새 `TextMeshProUGUI`/`TextMeshPro` 컴포넌트·프리팹은 neodgm SDF 폰트 에셋으로 설정하고, TMP 기본 폰트(TMP Settings)도 neodgm 으로 둔다. 다른 폰트(LiberationSans 등) 사용 금지.
- **UI 타입 네이밍**: UI 관련 타입은 `UI` 접두를 붙인다 (예: `UIFusionPanel`, `UIWaveHUD`). 세션 상태는 Presenter 가 소유하며, **View 가 Presenter 를 직접 참조하지 않고** 이벤트로만 위임한다.

## C# 코딩 컨벤션

**규약 원문은 전역 `~/.claude/CLAUDE.md` 의 「코딩 컨벤션」 절이 단일 진실 공급원(SSOT)이다.** 이 절은 그 규약을 이 프로젝트에서 적용할 때 쓰는 **신규 코드 체크리스트**이며, 규칙 본문을 여기에 복제하지 않는다 (2026-07-21 `unity-csharp-code-style.md`, 2026-07-27 `code-style-portable.md` 흡수분 반영).

### 적용 범위 (강제 — 판정 기준은 확장자가 아니라 소유권)

컨벤션은 **우리가 유지보수 책임을 지는 코드에만** 적용한다. 외부 코드를 우리 스타일로 고치면 에셋 업데이트 때 전부 충돌하므로, 검사도 수정도 하지 않는다.

| 구분 | 경로 | `.cs` 개수 | 처리 |
|---|---|---|---|
| 우리 코드 | `Assets/Scripts/**` (Editor 포함) | 193 | 전 항목 강제 |
| 우리 코드 | `Assets/Tests/**` | 45 | **전 항목 강제 — 테스트라는 이유로 면제하지 않는다** |
| 외부 에셋 | `Assets/ExternalResources/**` | 125 | 대상 외 |
| 외부 에셋 | `Assets/Bitgem/**` | 7 | 대상 외 |
| 참조 원본 | `Assets/Reference/**` | 0 | 대상 외 |
| 기타 | `Assets/Plugins`, `Assets/TextMesh Pro`, `Library`, `Packages`, `Temp`, `obj`, `*.Designer.cs` 등 | — | 대상 외 |

- **미등록 경로는 강제 대상이다.** 새 폴더의 `.cs` 는 자동으로 검사 대상이 되며, 제외하려면 아래 세 곳에 명시적으로 추가해야 한다. 누락 시 느슨한 쪽이 아니라 엄격한 쪽으로 넘어지도록 한 설계다.
- 이 범위는 **세 곳에서 동시에 강제**되며, 목록을 바꿀 때는 셋을 함께 고친다:
  1. `~/.claude/hooks/_convention_scope.py` — 훅(`unity-standards-gate`, `unity-standards-lint-remind`)이 공유하는 실행 코드판
  2. `~/.claude/skills/lint/SKILL.md` 「적용 범위」 절 — lint 검사 대상 결정
  3. 프로젝트 루트 `.editorconfig` — Rider/VS/dotnet format 등 IDE 레벨
- 외부 코드를 **부득이 수정해야 하면** 그 파일의 기존 스타일을 그대로 따른다. 우리 컨벤션을 소급 적용하지 않는다.

### 신규 코드 체크리스트

- [ ] 필드에 `_` / `m_` 접두사를 붙이지 않았다 (순수 `camelCase`)
- [ ] 인터페이스에 `I` 접두, 이벤트에 `On` 접두, 구독 핸들러에 `Handle` 접두를 붙였다
- [ ] `const` 를 `UPPER_CASE_WITH_UNDERSCORES` 로 썼고 선언 다음 줄을 비웠다
- [ ] bool 에 `is` / `has` / `can` / `use` / `need` / `should` 접두를 붙였다
- [ ] `Manager` / `Controller` / `Repository` / `Service` 대신 역할·동작이 드러나는 이름을 썼다
- [ ] `Console.Assert` 를 썼고 메시지와 (Component 라면) `this` 를 넘겼다
- [ ] Coroutine 대신 UniTask 를 썼고 메서드에 `Async` 접미를 붙였다
- [ ] `using (...) { }` 블록 방식을 썼다 (`using var` 미사용)
- [ ] switch statement 를 썼고 `default` 에서 `nameof(param)` · 실제 값 · 메시지 3개를 담아 `ArgumentOutOfRangeException` 을 던졌다
- [ ] 메서드에 `=>` 를 쓰지 않았다 (프로퍼티 accessor 는 허용)
- [ ] 반복문 본문이 한 줄이어도 중괄호를 썼다
- [ ] 모든 멤버에 접근 지정자를 명시했다 (`internal class` 내부 멤버는 `public`)
- [ ] `<summary>` 를 한국어 1줄로 달고 인자는 `<param>` 으로 설명했다
- [ ] 참조를 `[SerializeField]` 로 연결했다 (`GetComponent` 지양. `AddComponent` 는 런타임 동적 생성에만 쓰고 초기화 과정에서는 지양)
- [ ] 런타임 경로에 predicate LINQ 를 쓰지 않았다 (`.FirstOrDefault` · `.All` · 무인자 `.Any()` 포함)
- [ ] 함수 스코프 임시 컬렉션은 escape 여부로 골랐다 (escape 안 하면 풀링 우선, 풀은 블록 `using`)
- [ ] 한 곳에서만 쓰는 의존성을 필드 캐싱하지 않았다 (2개 이상 메서드·분기에 흩어질 때만 캐싱)
- [ ] 비동기 후속 처리 마커를 요청 송신이 아니라 **성공 응답 시점**에 설정하고 생명주기 경계에서 초기화했다
- [ ] 파일명이 첫 타입명과 일치한다 (`.meta` 포함)

### 이 프로젝트의 기존 코드 처리

- 규약 변경 이전 코드는 **일괄 개작하지 않는다.** 린터는 새로 짠 코드에만 적용하고, 기존 코드는 해당 파일을 실제로 수정할 때 함께 정리한다.
- **기존 파일에 코드를 추가할 때는 그 파일의 지배적 스타일을 따른다.** 접근지정자 명시(IDE0040) 등을 기존 시그니처에 소급 적용하지 않는다.
- **외부(리뷰·타 도구) 수정본을 적용할 때 기존 코드에 가해진 스타일 변경은 원복한다.**
- 특히 메서드 `=>` 제거·`const` 대문자화·런타임 LINQ 제거는 회귀 위험이 있으므로 별도 TASK 로 분리해 진행한다.

## Understanding Unity Projects

Unity projects are often **large, messy, and difficult to parse**:
- Thousands of files across Assets/, Library/, Packages/
- Abandoned prototypes and unused assets mixed with active code
- Auto-generated files, caches, and metadata everywhere
- No clear separation between "important" and "noise"

**As an AI agent, exhaustive exploration will fill your context with irrelevant information and slow you down.** Instead: get oriented quickly, ask when uncertain, and focus on what matters for the task.

## Project Structure

### Key Directories

#### `Assets/`
This is the **primary location** for project code, scripts, scenes, prefabs, materials, and other game content. 
Editor specific tools an extension will be in an `Editor` subfolder

**Always search here first** when exploring the codebase.

#### `Packages/`
Contains Unity packages that extend the project's functionality. Packages can be:
1. **Local packages**: Manually placed in the `Packages/` folder (editable)
2. **Embedded packages**: Custom packages with full source code in `Packages/`
3. **Registry packages**: Downloaded from Unity Registry or npm (cached elsewhere)

To understand package locations:
- Check `Packages/manifest.json` for package definitions
- Look for `file:` references for local/embedded packages with custom paths
- Local packages in `Packages/` are directly editable

#### `Library/PackageCache/`
Contains **cached, read-only** copies of registry packages downloaded from Unity Package Manager. This includes:
- Official Unity packages (e.g., `com.unity.textmeshpro`)
- Third-party packages from registries
- Git-based package dependencies

**Important**:
- These are cached copies - do not edit directly
- To modify a registry package, it must be embedded as a local package first
- When searching for package code, check `Packages/` first, then `Library/PackageCache/`

#### `Library/`
A cache and temporary data folder generated by Unity. **Generally ignore this folder** except for:
- `Library/PackageCache/` when searching for registry package source code
- The rest contains build artifacts, imported assets cache, and metadata

#### `ProjectSettings/`
Contains project configuration files (input, physics, quality settings, etc.). Rarely needs modification unless changing project-wide settings.

## Decision Making

### When to explore vs. when to ask

**For "build/create X" requests:**
1. If yes → Ask: "I see existing [X] code. Should I extend it or start fresh?"
2. If no → Proceed to implementation

**For "fix/modify X" requests:**
1. One targeted search to find the relevant code
2. If found → Read and fix it
3. If not found → Ask: "I couldn't find [X]. Can you point me to it?"

**General rule:** If you're doing more than 3-4 file operations before starting actual work, stop and ask the user for guidance instead. They know their project better than any amount of exploration will reveal.

### Avoid over-exploration

- Don't enumerate every folder looking for "relevant" files
- Don't read files "just in case" they might be useful
- The user can always provide more context if needed




## Unity Code Execution

---
When you need to **execute C# code in Unity** (creating objects, modifying scenes, adding components, changing materials, etc.), 

Unity C# code execution specialist. When the user wants to execute actions in Unity, modify the scene programmatically, create or manipulate GameObjects, or perform any Unity Editor operation that requires code execution. Use proactively for tasks like "create a cube", "add a component", "modify materials", etc.
---

You are a specialized Unity C# code generation and execution agent. Your can translate natural language requests into executable Unity C# code, validate it compiles correctly, and execute it in the Unity Editor.

## RunCommand Tool

Execute a C# script in the Unity Editor.

This is a powerful tool that allows you to programmatically control virtually every aspect of the game, including physics, input, graphics, gameplay logic, project setting and package management.

### The Golden Template
```csharp
using UnityEngine;
using UnityEditor;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        // 1. Your logic here
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);

        // 2. Register changes for Undo/Redo and tracking
        result.RegisterObjectCreation(cube);

        // 3. Log the result
        result.Log("Created {0}", cube);
    }
}
```
### Rules for Success
1. **Class Name is Mandatory**: The class MUST be named `CommandScript`. Using any other name will cause a NullReferenceException or execution failure.
2. **Use `internal` Accessibility**: Always use `internal class CommandScript`. Using `public` will cause an "Inconsistent Accessibility" compilation error.
3. **Use the `result` Object**:
   - **Creation**: Use `result.RegisterObjectCreation(obj)` after creating objects.
   - **Modification**: Use `result.RegisterObjectModification(obj)` BEFORE changing properties.
   - **Deletion**: Use `result.DestroyObject(obj)` instead of `Object.DestroyImmediate`.
   - **Logging**:
     - `result.Log("Created {0}", obj)` - Log with object references using `{0}`, `{1}`, etc.
     - `result.LogWarning("Warning message")` - Log warnings
     - `result.LogError("Error message")` - Log errors
4. **Avoid Top-Level Statements**: Always wrap your code in the class structure above.

## Verifying Your Work

Unity work often has **no immediate feedback**. Code compiles, objects get created—but did it actually work? Is it positioned correctly? Is the color right?

**Always verify your work** unless you're mid-way through a multi-step task.

### Choose verification based on work type:
- **Object creation/modification** → Take a screenshot to confirm appearance matches request
- **Code execution or logic changes** → Check console for errors (`Unity.GetConsoleLogs`)
- **Both visual and code** → Check console first, then screenshot

For scene composition verification, use `Unity.SceneView.CaptureMultiAngleSceneView` (3D: multi-angle view) or `Unity.SceneView.Capture2DScene` (2D: region capture). 

### What to look for:
- Colors, positions, scales, and rotations match the request
- Objects exist and are visible in the scene
- No new console errors or warnings

### When verification reveals issues

**Fix simple issues immediately** without asking:
- Wrong color, position, scale, or rotation → Correct it
- Missing component or property → Add it
- Console error from your code → Fix it

**Ask before fixing** when:
- The fix requires significant additional work
- You need information you don't have (e.g., "which render pipeline are you using?")
- Multiple valid solutions exist and user preference matters

The goal of verification is a **completed task**, not a status report.

### Verification rhythm

Think of it like running lint after a coding session—not after every keystroke.

- **Simple request** ("create a blue sphere at 1,2,3") → Verify immediately after
- **Multi-step task** → Verify after each logical phase completes
- **Batch operations** (created 5 objects) → One screenshot at the end, not five
