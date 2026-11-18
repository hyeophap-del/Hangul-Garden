# GitHub Releases 업데이트 배포

1. 공개 GitHub 저장소는 https://github.com/Hyeophap/Hangul-Garden 입니다.
2. `native/Updates.cs`의 `DefaultRepository`를 `소유자/저장소`로 지정한 후 빌드하면 사용자에게 기본 주소가 제공됩니다. 이미 받은 앱에서도 PC 창에서 주소를 저장할 수 있습니다.
3. 버전을 올릴 때 `native/Program.cs`의 어셈블리 버전·표시 버전과 `build-vr.ps1`의 EXE 파일명을 함께 변경합니다. 같은 버전 번호로는 업데이트 알림이 뜨지 않습니다.
4. 빌드, 패키징, 서명 파일 생성을 수행합니다.

```powershell
.\build-vr.ps1 -IncludeDevTools
# 현재 예시는 1.1입니다. 다음 배포에서는 새 번호로 변경하세요.
.\package-release.ps1 -Version 1.1
.\publish-update.ps1 -Repository 'Hyeophap/Hangul-Garden' -Version 1.1 -Tag v1.1 -Notes '변경 내용'
```

5. GitHub의 공개 정식 Release(v1.1 등)에 `update-publish/<버전>` 폴더의 세 파일을 첨부하고 Latest로 지정합니다.
   - `HangeulVR-<버전>-Windows-x64.zip`
   - `update.json`
   - `update.json.sig`

스크립트는 파일만 생성합니다. GitHub 업로드, 계정 로그인, 저장소 생성은 하지 않습니다. Draft 또는 prerelease는 Latest 업데이트 대상에 사용하지 마세요. ZIP은 평평한 파일 목록이어야 하고 실행 파일 이름은 manifest와 같아야 합니다. ZIP을 수정하면 반드시 서명을 다시 만드세요.

## 사용자 동작

저장소가 지정되면 앱 시작 시 및 6시간마다 새 버전을 확인합니다. '새 버전이 추가되었습니다' 안내와 설치 버튼을 표시합니다. 사용자가 설치를 누르고 확인하면 파일을 내려받고 검증 후 재시작합니다. 조용히 자동 설치하지는 않습니다. 앱이 닫혀 있는 동안에는 업데이트를 확인하지 않으며, 트레이 대기 모드는 VRChat 시작 감지만 담당합니다.

기존 1.0에는 업데이트 기능이 없으므로 이번 1.1은 한 번 직접 다운로드해야 합니다. 그 이후부터 앱 안에서 다음 버전을 받을 수 있습니다.

## 업데이트 서명 키

- 공개키: `native/update-public-key.xml` (EXE에 포함됨)
- 개인키: `.local-signing/update-key.dpapi` (현재 Windows 사용자용 DPAPI 암호화, Git 제외)
- 개인키는 배포 ZIP, GitHub 저장소, GitHub Release에 넣지 마세요.
- 이 키는 업데이트 진위 확인용이며 Windows Authenticode 코드 서명 인증서가 아닙니다.
- 기존 앱은 같은 공개키로 검증합니다. 공개키를 임의로 다시 생성하면 기존 사용자에게 업데이트를 전달할 수 없습니다.
- DPAPI 개인키는 같은 PC/Windows 사용자 프로필에 의존합니다. PC 이전·재설치 전에 별도 안전한 키 이전 절차가 필요합니다.

## 설치 및 되돌리기

다운로드 ZIP의 RSA-SHA256 manifest 서명과 SHA-256을 검증합니다. HTTPS GitHub와 지정 CDN 외 리디렉션, 압축 경로 탈출, 중복 파일, 불필요한 실행 파일, 과도한 파일 크기, 누락된 필수 파일을 거부합니다.

새 버전은 `%LOCALAPPDATA%\HangeulVR\versions` 안의 고유 폴더에 설치합니다. 기존 버전은 덮어쓰거나 삭제하지 않습니다. 크기·불투명도 설정을 복사하고 자동 실행 경로를 갱신합니다. 파일 검증 또는 설치에 실패하면 현재 앱을 계속 사용합니다. 새 앱이 시작된 뒤 발생하는 실행 오류까지 자동 복구하는 기능은 없으며, 이전 EXE를 직접 실행하면 됩니다.

현재 업데이트 검증은 동일한 OpenVR SDK DLL을 사용하는 버전을 대상으로 합니다. SDK 자체를 변경할 때는 호환되는 검증 정책도 함께 설계해야 합니다.

테스트용 `validation/update-publish`는 가상의 example/project 주소를 사용한 검증 산출물이며 실제 공개 배포용 파일이 아닙니다.
