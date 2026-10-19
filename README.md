<img width="100" height="100" alt="image" src="icon.png" />

# Cloudflared Tunnel Host

[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![Windows](https://img.shields.io/badge/Platform-Windows-0078D6?logo=windows)](https://www.microsoft.com/windows)
[![License](https://img.shields.io/badge/License-GPL3.0-green.svg)](LICENSE)
[![Latest Release](https://img.shields.io/github/v/release/tharu8813/CloudflaredTunnelHost?label=Download)](https://github.com/tharu8813/CloudflaredTunnelHost/releases/latest)

**Cloudflared Tunnel Host**는 Cloudflare Tunnel을 간편하게 관리할 수 있는 WPF 기반 Windows 애플리케이션입니다.<br />
로컬 서버를 인터넷에 노출시킬 수 있으며, GUI를 통해 쉽게 터널을 시작하고 관리할 수 있습니다.

## 스크린샷

아래는 기존 Windows Forms 버전의 화면입니다. 현재 WPF 버전과 UI가 다릅니다.

<img width="632" height="390" alt="image" src="https://github.com/user-attachments/assets/6d1a212f-31ed-4bb6-8ec5-191c859cee27" />
<br />
<img width="632" height="390" alt="image" src="https://github.com/user-attachments/assets/7e0cc8b7-1082-4b10-8aa5-cda77d63a9ed" />

## 사용 방법

1. 로컬 웹 서버의 HTTP/HTTPS 유형과 포트를 선택합니다.
2. **터널 시작**을 누릅니다. cloudflared가 없으면 자동 설치합니다. 로컬 서버는 주소 발급 후 실행해도 됩니다.
3. 공개 주소를 복사하거나 브라우저·QR 코드로 접속합니다. 메인 하단에서 활동 로그를 확인하고, **별도 창으로 보기** 또는 `Ctrl+Shift+L`로 로그 창을 열 수 있습니다.
4. **중지 / 취소**는 터널과 진행 중인 작업을 멈추고, **프로그램 종료**는 앱을 완전히 종료합니다.

Quick Tunnel은 개발·테스트용 임시 연결입니다. 공개 모드에서는 주소를 아는 누구나 접근할 수 있으며, 재연결하면 주소가 바뀔 수 있습니다.

## 빌드 방법

### 개발 환경

- Visual Studio 2026 (.NET 데스크톱 개발 워크로드)
- .NET 8.0 SDK

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
