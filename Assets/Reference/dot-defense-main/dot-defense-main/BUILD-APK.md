# Dot Defense — APK 만들기 가이드

## 준비물

| 항목 | 필요? | 비고 |
|---|---|---|
| Windows PC | ✓ | 사용자 환경 |
| Chrome / Edge 브라우저 | ✓ | 아이콘 생성 + PWABuilder 접속 |
| GitHub 계정 | ✓ | 무료 호스팅 (또는 Netlify 등 대체) |
| Android SDK / JDK | ✗ | PWABuilder가 클라우드에서 빌드 |
| Apple Developer 계정 | ✗ | iOS 아님 |

총 비용: **0원**. 소요 시간: **10~20분**.

---

## 1단계 — 아이콘 생성

1. `icon-generator.html`을 더블클릭해서 브라우저에서 열기
2. `↓ icon-192.png` / `↓ icon-512.png` / `↓ icon-512-maskable.png` 버튼 모두 클릭
3. `dot-defense\icons\` 폴더를 새로 만들고 다운로드한 3개 PNG 파일을 그 안에 이동

폴더 구조 확인:
```
dot-defense/
  index.html
  manifest.json
  sw.js
  icon-generator.html
  icons/
    icon-192.png
    icon-512.png
    icon-512-maskable.png
```

---

## 2단계 — GitHub Pages에 무료 호스팅

PWABuilder는 **공개 URL**이 필요합니다. GitHub Pages가 가장 쉬워요.

### A) GitHub 저장소 만들기
1. <https://github.com> 로그인
2. 우측 상단 **+** → **New repository**
3. Name: `dot-defense` (아무거나)
4. **Public** 선택
5. **Create repository**

### B) 파일 업로드
1. 만들어진 저장소 페이지에서 **uploading an existing file** 링크 클릭
2. `dot-defense` 폴더 안의 **모든 파일과 icons 폴더**를 드래그하여 업로드
   - index.html
   - manifest.json
   - sw.js
   - icon-generator.html
   - icons/icon-192.png
   - icons/icon-512.png
   - icons/icon-512-maskable.png
3. 아래 **Commit changes** 클릭

### C) Pages 활성화
1. 저장소 → **Settings** 탭
2. 좌측 메뉴 **Pages**
3. Source: **Deploy from a branch**
4. Branch: **main** / **(root)** → **Save**
5. 1~2분 후 페이지 새로고침 → 상단에 URL이 표시됨
   예: `https://USERNAME.github.io/dot-defense/`

### D) 동작 확인
- 그 URL을 브라우저에서 열어서 게임이 잘 로드되는지 확인
- 휴대폰에서도 같은 URL로 접속해서 한 번 플레이해보면 좋음

---

## 3단계 — PWABuilder로 APK 생성

1. <https://www.pwabuilder.com> 접속
2. 가운데 입력란에 GitHub Pages URL 붙여넣기 (`https://USERNAME.github.io/dot-defense/`)
3. **Start** 클릭 → 분석이 진행되고 점수가 나옴
4. 점수가 낮아도 OK (필수만 통과하면 됨). 만약 빨간 경고가 있으면 표시된 항목 수정 후 다시 분석
5. 우측 상단 **Package For Stores** 클릭
6. **Android** 카드에서 **Generate Package**
7. 다음 옵션 화면:
   - Package ID: `com.yourname.dotdefense` (원하는 대로)
   - App name: `Dot Defense`
   - 나머지 기본값 OK
   - 하단 **Signing key**: **New** 선택 (서명키 자동 생성)
   - 사용자 정보 입력 (이름/조직 등 임의)
8. **Download** 클릭 → ZIP 파일 다운로드

### ZIP 내용물
- `app-release-signed.apk` ← **이 파일이 APK!**
- `app-release-bundle.aab` (Play Store 배포용)
- `signing.keystore` + `signing-key-info.txt` ← **잘 보관**, 분실 시 업데이트 못함

---

## 4단계 — 휴대폰에 설치

### 직접 설치 (개인용)
1. APK 파일을 휴대폰으로 전송 (메일, 카톡, USB, Google Drive 등)
2. 휴대폰에서 APK 파일 열기
3. 첫 설치 시 "출처를 알 수 없는 앱 허용" 권한 요청 → 허용
4. 설치 → 앱 아이콘이 휴대폰에 생김
5. 일반 앱처럼 실행

### Play Store에 올리기 (선택)
- Google Play Console 가입 ($25 1회 결제)
- ZIP 안의 `.aab` 파일 업로드
- 심사 며칠

---

## 트러블슈팅

**Q. PWABuilder가 manifest를 못 찾는다고 함**
→ GitHub Pages URL 끝에 `/` 있는지 확인. `https://user.github.io/dot-defense/` 처럼.

**Q. 아이콘이 깨져서 표시됨**
→ `icons/` 폴더 경로 정확한지, PNG 파일이 올라갔는지 확인.

**Q. 휴대폰 설치 시 "안전하지 않음" 경고**
→ APK 서명이 자체 서명이라 그렇습니다. **허용** 누르고 진행. Play Store에 올리지 않는 한 정상.

**Q. 게임이 화면을 다 차지 안 함 / 작게 나옴**
→ `index.html`의 fitScale이 자동 처리하지만, 만약 안 된다면 휴대폰 다시 시작 후 재실행.

**Q. 업데이트는 어떻게?**
→ 코드 수정 → GitHub에 push → PWABuilder 다시 돌려서 새 APK 생성 → 휴대폰 재설치. 또는 게임 안의 서비스워커가 자동 최신 버전을 가져옴 (앱 재실행 시).

---

## 대안 경로 (참고)

- **Bubblewrap (CLI)**: Node + JDK 직접 설치해서 로컬 빌드. 더 정밀하지만 환경 구축 필요.
- **Capacitor**: Ionic 팀의 도구. 풍부한 네이티브 기능 접근 가능. Android Studio 필수.
- **Cordova**: 구식 옵션. 비추천.

이 가이드(PWABuilder)는 **가장 빠르고 무료**인 길입니다.
