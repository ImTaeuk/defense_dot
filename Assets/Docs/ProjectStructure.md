# Defense Dot Project Structure

## 📂 Project Hierarchy Summary

| Folder | Description |
| :--- | :--- |
| **Assets/Data/Maps** | **MapData** (.asset) 파일들이 보관되는 곳입니다. 맵 에디터로 구워진(Bake) 데이터가 여기에 저장됩니다. |
| **Assets/Docs** | 프로젝트 설계 문서, 폴더 구조 가이드 및 시각화 파일들이 보관됩니다. |
| **Assets/ExternalResources** | 외부 에셋(Asset Store, Package Manager 등)을 프로젝트에 임포트한 후 정리하여 보관하는 전용 폴더입니다. |
| **Assets/Prefabs/Game** | 타워, 적, 투사체 등 실제 게임 오브젝트 프리팹들이 위치합니다. |
| **Assets/Prefabs/UI** | HUD, 메인 메뉴, 설정 창 등 UI 관련 프리팹들이 보관됩니다. |
| **Assets/Textures/UI** | 게임 내 UI에서 사용되는 스프라이트, 아이콘, 배경 이미지들이 보관되는 곳입니다. 원본 프로젝트에서 가져온 어빌리티 및 캐릭터 아이콘이 포함되어 있습니다. |
| **Assets/Resources/Data** | 밸런싱 데이터나 런타임에 동적으로 로드해야 할 파일들이 위치합니다. |
| **Assets/Scripts/Core** | 인터페이스(Interfaces), 이벤트 버스(GameEvents), 오브젝트 풀(ObjectPool) 등 핵심 엔진 로직입니다. |
| **Assets/Scripts/Data** | ScriptableObject 클래스 정의(TowerData, EnemyData, MapData)가 모여 있습니다. |
| **Assets/Scripts/Editor** | **Map Editor** 윈도우와 관련된 스크립트 및 UI 자원(UXML, USS)이 들어있는 에디터 전용 폴더입니다. |
| **Assets/Scripts/Systems** | 그리드 시스템, 타워 공격 로직, 적 AI 등 실제 게임 플레이 시스템 로직입니다. |
| **Assets/Scripts/UI** | **MVP (Model-View-Presenter)** 패턴에 따라 분리된 UI 로직 폴더입니다. |

---

## 🌲 Folder Hierarchy

```text
📁 Assets/
  📁 Data/
    📁 Maps/
  📁 Docs/
  📁 Prefabs/
    📁 Game/
    📁 UI/
  📁 Resources/
    📁 Data/
  📁 Scenes/
  📁 Scripts/
    📁 Core/
    📁 Data/
    📁 Systems/
      📁 Enemy/
      📁 Grid/
      📁 Projectile/
      📁 Tower/
    📁 UI/
      📁 Models/
      📁 Presenters/
      📁 Views/
    📁 Editor/
  📁 Settings/
```
