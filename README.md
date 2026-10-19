# Cloudflared Tunnel Host 2.0

로컬 웹 서버를 임시 공개 주소로 연결하는 **.NET 8 WPF Windows 앱**입니다. WinForms 화면과 의존성을 제거하고 MVVM, 취소 가능한 비동기 서비스, 명시적인 터널 상태 머신으로 재구축했습니다.

## 사용 방법

1. 연결할 로컬 웹 서버의 포트를 확인합니다. 서버는 터널 주소를 발급한 후 실행해도 됩니다.
2. 대시보드에서 HTTP/HTTPS와 포트를 선택합니다. 예: `http://localhost:3000`.
3. **터널 시작**을 누릅니다. 로컬 서버가 없어도 터널 주소를 먼저 발급하고 **로컬 서버 대기**로 표시합니다. 이후 서버를 실행하면 같은 터널 주소로 연결됩니다. cloudflared가 없으면 공식 배포 파일을 자동 설치합니다. 다운로드와 연결은 **중지 / 취소**로 취소할 수 있습니다.
4. 공개 주소가 발급되면 복사·브라우저 열기·QR을 사용할 수 있습니다. **주소 발급과 연결 성공은 서로 다릅니다.** cloudflared의 `/ready`와 공개 주소의 HTTP 응답을 모두 확인해야 Connected로 표시합니다. 401·403·404 등 앱 응답도 연결 확인으로 인정하며, 502·503·Cloudflare 52x/530은 성공으로 처리하지 않습니다.
5. **터널 중지**는 터널만 종료합니다. **프로그램 종료**는 다운로드/재시도를 취소하고 프로세스 및 트레이를 정리한 뒤 앱을 종료합니다.

공개 URL을 자동으로 클립보드에 복사하지 않습니다. `Ctrl+L`은 URL 복사, `Ctrl+S`는 설정 저장입니다. Tab 키로 입력과 버튼을 탐색할 수 있습니다. 로그 검색 입력란과 레벨 선택은 툴팁 및 접근성 이름을 제공합니다.

## 화면과 기능

- 상태·연결 시간·대상·공개 URL 중심의 대시보드, 항상 보이는 시작/중지 제어 영역
- 다크/라이트 테마, SVG 아이콘, 가벼운 Opacity/Translate/Scale 전환, Windows 애니메이션 감소 설정 고려
- 기본 HTTP/HTTPS 연결과 로컬 서버 진단. 진단 실패는 경고로 안내하며 터널 생성을 차단하지 않습니다. 인증서 검증은 기본으로 켜져 있습니다.
- 고급 설정: cloudflared 로그 레벨, auto/quic/http2, HTTPS 검증 생략, 로그 자동 저장, 자동 재연결과 최대 횟수, 앱 시작 시 터널 실행, 시작/최소화/닫기 시 트레이 동작, 알림, 업데이트 관리
- 접이식 로그: 타임스탬프·레벨·검색·필터·자동 스크롤·복사·저장·초기화. 화면은 최근 2,000개, 대기 큐는 5,000개로 제한하고 250ms마다 최대 200개를 한 번에 반영합니다. 초과 로그는 오래된 순서로 버립니다.
- 파일 로그는 `logs/cloudflared.log`에 기록하고 5MB 초과 시 `.1` 한 개를 보관합니다. 파일 저장 실패는 화면에 알립니다.
- QR은 QRCoder로 로컬 생성하며 고해상도 PNG 저장과 이미지 복사를 지원합니다. 터널 종료/주소 변경 시 QR 창을 닫습니다.
- WinForms 없는 네이티브 시스템 트레이: 상태, 시작, 중지, 복사, 브라우저, QR, 로그 폴더, 화면 복원, 완전 종료. 트레이 등록이 실패하면 창을 숨기지 않습니다.
- Windows Job Object와 프로세스 트리 종료로 앱 종료 및 비정상 종료 시 자식 프로세스 잔류를 방지합니다.

## 보안과 Quick Tunnel 제한

Quick Tunnel은 개발 및 테스트용 임시 연결입니다. 공개 모드에서는 URL을 아는 누구나 로컬 서비스에 접근할 수 있습니다. 재연결하면 주소가 바뀔 수 있고, 가동 시간 보장이나 고정 도메인이 제공되지 않습니다. 운영 환경은 정식 Tunnel과 적절한 접근 정책을 사용하세요. [Cloudflare Quick Tunnels 공식 문서](https://developers.cloudflare.com/tunnel/get-started/quick-tunnels/)

고급 설정에 정확한 이메일 주소를 쉼표로 입력하면 CLI의 `--allowed-mail`을 사용합니다. 실행 전에 **설치된 CLI의 실제 `tunnel --help`**에서 지원 여부를 검사하며, 지원하지 않으면 실행을 차단합니다. 보호 기능을 끄고 공개 터널로 대신 실행하지 않습니다. 이메일 PIN 인증은 대화형 브라우저가 필요합니다. 와일드카드 도메인은 이 앱에서 제공하지 않습니다. 2026-10-19에 공식 cloudflared 2026.10.1의 CLI 지원을 확인했습니다. 실제 이메일 송수신/PIN 로그인은 별도 검증이 필요합니다.

HTTPS 인증서 검증 생략은 자체 서명 개발 서버에만 신중하게 사용하세요. 서버 신원을 확인하지 않으므로 연결 가로채기 위험이 커집니다. 로컬 진단에도 동일한 인증서 정책을 적용합니다.

로그에서는 공개 주소(호스트명 포함), 이메일 및 일반적인 token/secret/authorization 값을 가립니다. 임의의 로그 형식에 대한 완전한 비식별화를 보장하지 않으므로 공유 전에 내용을 직접 확인하세요. QR 이미지와 복사한 URL에는 실제 주소가 들어 있습니다. 저장한 QR 파일은 자동 삭제되지 않습니다.

## Cloudflared 설치와 업데이트

GitHub의 `cloudflare/cloudflared` 공식 최신 릴리스 메타데이터와 Windows amd64 자산만 사용합니다. API digest 또는 릴리스 본문의 SHA256이 없으면 설치를 중단합니다. 임시 파일로 스트리밍하고 크기·SHA256·PE/x64 헤더·실제 `--version`을 확인한 뒤 원자적으로 교체합니다. 취소·검증 실패·네트워크 오류 시 기존 파일을 보존하고 임시 파일을 정리합니다. 기존 버전은 `.bak`으로 보관합니다. 실행 중에는 업데이트할 수 없습니다.

앱이 설치한 버전의 자동 업데이트는 `--no-autoupdate`로 비활성화하여 앱의 검증 및 교체 절차로 관리합니다. 기본 사용자 `.cloudflared` 설정과 충돌하지 않도록 전용 Quick Tunnel 설정 파일을 사용하고 `TUNNEL_*` 환경 변수를 자식 프로세스에서 제거합니다. WARN/ERROR 로그 수준에서도 URL을 얻을 수 있도록 `/quicktunnel` 엔드포인트를 함께 사용합니다.

## 설정 위치와 이전

기본 데이터 폴더는 `%APPDATA%\CloudflaredTunnelHost`입니다.

```text
settings.json           구조화된 설정 / SchemaVersion 1
settings.json.bak       마지막 교체 전 설정
settings.txt            기존 버전 설정, 이전 후에도 보존
tools/cloudflared.exe   검증된 설치 파일
logs/cloudflared.log    선택적 순환 로그
quick-tunnel.yml        앱이 관리하는 전용 실행 설정
```

기존 `settings.txt`의 포트·서비스·로그 레벨·프로토콜·인증서 검증·파일 로그 옵션을 자동 이전합니다. 손상되거나 지원하지 않는 JSON은 알림을 표시하고 기본값으로 시작하며, 저장할 때 원본을 `.bak`으로 보관합니다. 설정은 시작/설정 저장/앱 종료 시 저장합니다. 자동 터널 실행은 기본으로 꺼져 있습니다. 이 앱은 Windows 로그인 자동 실행을 등록하지 않습니다.

개발/테스트 시 `--data-dir <절대 경로>`로 별도 데이터 폴더를 사용할 수 있습니다.

## 프로젝트 구조

```text
App.xaml / App.xaml.cs           WPF 진입점과 테마
Views/                           대시보드, 설정, QR 창
ViewModels/                      바인딩, 명령, UI 로그 배치
Core/Models/                     설정, 상태, 로그 모델과 전이 규칙
Core/Services/                   터널, 프로세스/Job, 다운로드, 진단, 설정, 로깅
Services/                        WPF 클립보드/QR, 브라우저, Windows 트레이
Infrastructure/                  명령, 변경 알림, 로그 컬렉션, 가벼운 모션
Controls/                        SVG 렌더러, 가상화 로그 리스트
Themes/                          색상, 타이포그래피, 스타일, 템플릿, 모션 값
Assets/Icons/                    원본 SVG 18개
Assets/Branding/                 원본 SVG와 다중 해상도 ICO
Tests/                           패키지 없는 핵심 회귀 검사 및 선택적 실제 연결 검사
Tests/Wpf/                       WPF 바인딩/렌더링/QR/로그/트레이 종료 검사
.github/workflows/windows.yml    Windows 빌드·테스트·배포 아티팩트
```

Core는 UI 참조가 없는 `net8.0` 라이브러리입니다. 앱은 `net8.0-windows`, `UseWPF=true`입니다. 추가 런타임 패키지는 **QRCoder 1.8.0 (MIT)** 하나입니다. SVG는 프로젝트 자체 에셋이며 라이선스/생성 방법은 [Assets/README.md](Assets/README.md)에 있습니다. AI 이미지나 원격 이미지 서비스는 사용하지 않습니다.

## 빌드와 실행

Windows 10/11 x64, .NET 8 SDK 이상이 필요합니다. 소스 실행/런타임 미포함 배포에는 .NET 8 Desktop Runtime이 필요합니다. Visual Studio에서는 .NET 데스크톱 개발 워크로드로 솔루션을 엽니다.

```powershell
git clone https://github.com/tharu8813/CloudflaredTunnelHost.git
cd CloudflaredTunnelHost
dotnet restore CloudflaredTunnelHost.sln
dotnet build CloudflaredTunnelHost.sln -c Release
dotnet run --project CloudflaredTunnelHost.csproj -c Release
```

SDK가 최신 버전이어도 대상은 .NET 8입니다. `dotnet --info`가 서비스 권한 오류를 내는 제한 환경에서는 빌드 명령으로 SDK 동작 여부를 별도로 확인할 수 있습니다.

## 자동 테스트

저장소 루트에서 실행합니다. 테스트 러너는 성공 시 0, 실패 시 1을 반환합니다.

```powershell
dotnet run --project Tests/CloudflaredTunnelHost.Tests.csproj -c Release
dotnet run --project Tests/Wpf/CloudflaredTunnelHost.WpfTests.csproj -c Release
```

핵심 테스트는 설정 검증/이전/보존, URL 파싱, 인자와 안전한 기본값, 전이 규칙, 로그 제한/마스킹/회전, HTTP/HTTPS 진단, 다운로드 취소/해시 실패 복구, 중복 시작, 연결 성공 조건, 재시도 제한, 이전 세션 출력 격리, 실제 프로세스 트리 종료를 검사합니다. WPF 테스트는 실제 창과 트레이를 만들고 로컬 QR·바인딩·로그 검색·테마·최소 크기를 검증하며 `.artifacts/`에 PNG를 생성합니다. 125/150/200% PNG 렌더링은 물리 모니터 DPI 전환 검증을 대신하지 않습니다.

Windows Shell에 접근할 수 없는 CI에서는 `-- --allow-no-tray`를 WPF 테스트 명령 뒤에 추가할 수 있습니다. 이때 접근할 수 없는 트레이 검증만 명시적으로 SKIP 처리하며, 로컬 기본 모드에서는 트레이 생성 실패를 테스트 실패로 처리합니다. GitHub Actions는 외부 Quick Tunnel 서비스에 의존하는 실제 연결 검사를 기본 실행하지 않습니다.

실제 공식 다운로드와 테스트 전용 로컬 웹 페이지의 공개 연결을 검사하려면:

```powershell
dotnet run --project Tests/CloudflaredTunnelHost.Tests.csproj -c Release -- --live
```

이 검사는 `.test-data/live`에 설치하고 테스트 전용 페이지만 잠시 공개합니다. 종료/실패 시 터널과 서버를 정리합니다. 실제 네트워크·DNS·Cloudflare 상태에 따라 실패할 수 있습니다. 서버를 나중에 실행해도 같은 URL을 유지하는 흐름만 검증하려면 `--live` 대신 `--live-origin-late`를 사용합니다. 이 모드는 외부 공개 페이지의 응답 검증을 명시적으로 생략합니다.

## 배포

런타임 포함 Windows x64 단일 실행 파일:

```powershell
dotnet publish CloudflaredTunnelHost.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o .artifacts/publish
```

`.artifacts/publish` 전체를 ZIP으로 배포하세요. GPL 및 QRCoder 라이선스도 함께 배포합니다. cloudflared는 번들하지 않고 첫 터널 실행 시 설치합니다. CI에서도 동일한 명령으로 배포 파일을 생성해 Actions 아티팩트로 보관합니다.

설치된 .NET 8 Desktop Runtime을 사용하는 작은 폴더 배포:

```powershell
dotnet publish CloudflaredTunnelHost.csproj -c Release -r win-x64 --self-contained false -o .artifacts/publish
```

## 검증 결과와 남은 작업

실제 수행 결과와 미검증 범위는 [VALIDATION.md](VALIDATION.md)를 확인하세요. 특히 테스트 환경에서 trycloudflare DNS 조회가 실패한 결과를 공개 접속 성공으로 간주하지 않습니다. 다중 모니터 이동, 실제 DPI 변경, Windows 종료/로그오프, 접근성 읽기 프로그램, 이메일 PIN 인증은 실제 사용자 환경에서 추가 확인이 필요합니다.

다음 개선 우선순위는 다른 네트워크에서 공개 접속 검증, 실제 고DPI/다중 모니터 QA, 배포 서명과 설치 프로그램입니다. 이 저장소의 라이선스는 [GPL-3.0](LICENSE)입니다.
