<img width="100" height="100" alt="Cloudflared Tunnel Host 프로그램 아이콘" src="icon.png" />

# Cloudflared Tunnel Host

[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![Windows](https://img.shields.io/badge/Platform-Windows-0078D6?logo=windows)](https://www.microsoft.com/windows)
[![License](https://img.shields.io/badge/License-GPL3.0-green.svg)](LICENSE)
[![Latest Release](https://img.shields.io/github/v/release/tharu8813/CloudflaredTunnelHost?label=Download)](https://github.com/tharu8813/CloudflaredTunnelHost/releases/latest)

**Cloudflared Tunnel Host**는 Cloudflare Quick Tunnel을 간편하게 관리할 수 있는 .NET 8 WPF 기반 Windows 애플리케이션입니다.<br />
로컬 서버를 인터넷에 노출시킬 수 있으며, GUI를 통해 쉽게 터널을 시작하고 관리할 수 있습니다.

## 주요 기능

- HTTP/HTTPS 로컬 서버 연결과 임시 공개 주소 발급
- 공개 주소 복사·브라우저 열기·로컬 QR 코드 생성
- 다크/라이트 테마와 메인 화면의 실시간 활동 로그
- 실행 상태를 표시하는 커스텀 상단 표시줄과 부드러운 휠 스크롤
- 메인 창에 통합된 활동 로그 검색·필터·복사·저장, 자동 재연결과 시스템 트레이 지원
- Windows 11의 기본 둥근 창 모서리와 최소화·최대화·복원 효과
- 앱과 같은 테마의 트레이 메뉴, 실행 중 앱 옵션 변경, 종료 전 확인
- 카테고리별 고급 설정 탭과 터널 상태 변화에 따른 Windows 알림

## 스크린샷

서버 입력과 주소 공유를 두 단계로 정리한 시작 화면입니다. 주요 버튼과 최근 로그는 스크롤과 관계없이 하단에 표시됩니다.

**다크 테마**

<img width="840" alt="WPF 다크 테마 대시보드: 로컬 포트 입력, 연결 상태, 활동 로그와 시작 버튼" src="docs/screenshots/dashboard-dark.png" />

**라이트 테마**

<img width="840" alt="WPF 라이트 테마 대시보드: 로컬 포트 입력, 연결 상태, 활동 로그와 시작 버튼" src="docs/screenshots/dashboard-light.png" />

## 사용 방법

1. 로컬 웹 서버의 HTTP/HTTPS 유형과 포트를 선택합니다.
2. **터널 시작**을 누릅니다. cloudflared가 없으면 자동 설치합니다. 로컬 서버가 없으면 **로컬 서버 대기**로 표시되며, 서버를 나중에 실행해도 같은 주소를 사용합니다.
3. 공개 주소가 발급되면 공유 영역이 표시됩니다. **주소 복사**, **브라우저 열기**, **QR 코드**로 공유하세요. 메인 하단의 최근 로그와 사이드바의 **활동 로그**에서 상태를 확인할 수 있습니다.
4. 진행 상태에 따라 **작업 취소**, **연결 취소**, **터널 중지**가 표시됩니다. **프로그램 종료**를 누르면 확인창이 열리고, 종료를 선택하면 터널과 앱을 함께 종료합니다. **계속 사용**이나 `Esc`는 종료를 취소합니다.

입력 오류가 있으면 **입력값 수정**으로 해당 항목에 바로 이동할 수 있습니다. 연결 설정은 터널 시작 시 자동 저장되며, 고급 설정의 **설정 저장**으로 미리 보관할 수 있습니다.

고급 설정은 **연결**, **접근 제한**, **로그**, **앱·트레이**, **업데이트** 탭으로 나뉩니다. 탭 제목과 저장 버튼은 고정하고 해당 카테고리의 내용만 스크롤합니다. 이메일·재시도 입력 오류는 해당 탭으로 바로 이동합니다.

앱 시작·트레이·알림·로그 파일 저장 옵션은 터널 준비/연결 중에도 변경할 수 있습니다. 트레이·알림·로그 저장은 즉시, 앱 시작 옵션은 다음 실행부터 적용됩니다. 연결 대상·프로토콜·접근 제한·재연결 정책은 터널 중지 후 변경합니다. **창 닫기 버튼을 누르면 트레이로 숨기기**가 켜져 있으면 닫기 버튼은 기존처럼 숨기며, 실제 종료는 메인 화면이나 트레이 메뉴의 **프로그램 종료**에서 확인 후 진행합니다.

터널 시작, 연결 완료, 로컬 서버 대기, 재연결, 복구, 오류와 종료를 Windows 배너 알림으로 알려드립니다. 같은 상태의 반복 갱신은 알림을 만들지 않고, 알림을 누르면 메인 창을 엽니다. **고급 설정 → 앱·트레이**에서 알림을 끌 수 있습니다. 배너 표시는 Windows 알림 설정을 따르며, Windows 11에서는 알림 센터에 기록이 남지 않을 수 있습니다. [Windows 알림 동작](https://learn.microsoft.com/en-us/windows/win32/api/shellapi/nf-shellapi-shell_notifyiconw)

[설정 탭 미리보기](docs/screenshots/settings-tabs-dark.png) · [통합 활동 로그 미리보기](docs/screenshots/activity-logs-dark.png) · [트레이 메뉴 미리보기](docs/screenshots/tray-menu-dark.png) · [종료 확인창 미리보기](docs/screenshots/exit-confirmation-dark.png)

주소 발급과 연결 완료는 구분해서 표시합니다. `Ctrl+L`은 주소 복사, `Ctrl+S`는 설정 저장, `Ctrl+Shift+L`은 메인 창의 활동 로그 화면으로 이동합니다. 활동 로그에서 `Esc`를 누르면 대시보드로 돌아오고, QR 창에서는 창을 닫습니다. 검색어와 필터는 화면을 바꿔도 유지됩니다.

창 모서리와 창 전환 효과는 Windows가 처리하며 시스템의 애니메이션 설정을 따릅니다. 최대화·화면 분할 상태에서는 Windows 기본 동작대로 모서리가 펴집니다. **최소화하면 트레이로 숨기기**를 끄면 작업 표시줄로 최소화합니다.

Quick Tunnel은 개발·테스트용 임시 연결입니다. 공개 모드에서는 주소를 아는 누구나 접근할 수 있으며, 재연결하면 주소가 바뀔 수 있습니다.

## 빌드 방법

### 개발 환경

- Windows 10/11 x64
- .NET 8.0 SDK 이상
- Visual Studio를 사용하는 경우 .NET 데스크톱 개발 워크로드

### 빌드 단계

```powershell
git clone https://github.com/tharu8813/CloudflaredTunnelHost.git
cd CloudflaredTunnelHost
dotnet build CloudflaredTunnelHost.sln -c Release
dotnet run --project CloudflaredTunnelHost.csproj -c Release
```

설정 위치, 보안 옵션, 테스트와 배포 방법은 [개발 및 상세 동작](docs/DEVELOPMENT.md), 실제 검증 결과는 [VALIDATION.md](VALIDATION.md)를 확인하세요.

## 버그 리포트

버그를 발견하셨나요? [Issues](https://github.com/tharu8813/CloudflaredTunnelHost/issues)에 등록해주세요.

## 라이선스

이 프로젝트는 GPL-3.0 라이선스를 따릅니다. 자세한 내용은 [LICENSE](LICENSE) 파일을 참조하세요.
