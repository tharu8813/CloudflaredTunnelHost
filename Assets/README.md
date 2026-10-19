# Asset provenance

All SVG icons in `Icons/` and the brand mark in `Branding/app.svg` are original assets created for this project, distributed under the repository GPL-3.0 license. No icon fonts, emoji, third-party illustrations, or remotely loaded images are used.

The 24-unit SVG paths are the source of truth. `SvgIcon` parses the embedded SVG once and renders cached WPF geometries with theme brushes at any DPI. Keep the SVG sources when changing paths. `scripts/Generate-AppIcon.ps1` renders `Branding/app.svg` to 16/32/48/256-pixel PNG frames in `app.ico` for the executable, windows, native system tray and notifications. It requires Windows PowerShell in STA mode, no image package.

QR images are generated locally at 20 pixels per module by QRCoder 1.8.0 (MIT). Its license is included in `QRCoder-LICENSE.txt`. They contain the current URL and are never requested from an external QR service.
