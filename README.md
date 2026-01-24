<img width="100" height="100" alt="image" src="icon.png" />

# Cloudflared Tunnel Host

[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![Windows](https://img.shields.io/badge/Platform-Windows-0078D6?logo=windows)](https://www.microsoft.com/windows)
[![License](https://img.shields.io/badge/License-GPL3.0-green.svg)](LICENSE)

**Cloudflared Tunnel Host**는 Cloudflare Tunnel을 간편하게 관리할 수 있는 Windows Forms 애플리케이션입니다.  
로컬 서버를 인터넷에 노출시킬 수 있으며, GUI를 통해 쉽게 터널을 시작하고 관리할 수 있습니다.

## 스크린샷
<img width="632" height="390" alt="image" src="https://github.com/user-attachments/assets/6d1a212f-31ed-4bb6-8ec5-191c859cee27" />
</br>
<img width="632" height="390" alt="image" src="https://github.com/user-attachments/assets/7e0cc8b7-1082-4b10-8aa5-cda77d63a9ed" />

## 빌드 방법

### 개발 환경
- Visual Studio 2026
- .NET 8.0 SDK

### 빌드 단계
```bash
# 저장소 클론
git clone https://github.com/yourusername/CloudflaredTunnelHost.git
```

## 프로젝트 구조

```
CloudflaredTunnelHost/
├── Start/
│   ├── MainForm.cs
│   ├── MainForm.Designer.cs
│   └── Download.cs
│   └── QRViewr.cs
├── Tools/
│   └── Tol.cs
└── Program.cs
```

## 버그 리포트

버그를 발견하셨나요? [Issues](https://github.com/tharu8813/CloudflaredTunnelHost/issues)에 등록해주세요.

## 라이선스

이 프로젝트는 GPL-3.0 라이선스를 따릅니다. 자세한 내용은 [LICENSE](LICENSE) 파일을 참조하세요.
