# Motion guidelines

Cloudflared Tunnel Host keeps transitions restrained and consistent. Motion must explain change, not delay interaction.

| Interaction | Target |
| --- | --- |
| Main page | 260 ms, 18 DIP horizontal travel |
| Settings category | 205 ms, 12 DIP travel |
| One-time reveal | 190 ms fade, 260 ms travel (10 DIP) |
| Stagger | 28 ms increments, maximum four |
| Button press | 90 ms down, 150 ms release, 0.988 scale |
| Button hover | 140 ms tint, no text scaling |

C# motion tokens are in `Infrastructure/MotionProfile.cs`.

Rules:
- Do not combine page/tab transitions with independent section-level entrances.
- A visible bound label update must not restart its entrance animation.
- Disposing a hidden element resets clocks; showing it again may reveal it.
- Leave text in place on hover. Button press has a subtle compression.
- Respect Windows `SystemParameters.ClientAreaAnimation` and keyboard focus.
- Keep bindings, user inputs, live log virtualization, scroll behavior and functional commands intact.

Verify using:
```powershell
dotnet build CloudflaredTunnelHost.sln -c Release
dotnet run --project Tests/Motion/CloudflaredTunnelHost.MotionTests.csproj -c Release
dotnet run --project Tests/Wpf/CloudflaredTunnelHost.WpfTests.csproj -c Release -- --allow-no-tray
```
The motion test produces GIF previews in `.artifacts/`, now uploaded by CI. Look for flashes, late entrances and abrupt interruption, not merely test success.

The existing presenter still captures the outgoing visual as a temporary bitmap to support both
index-only navigation and tab content replacement without moving live WPF controls across visual
parents. A fully live old/new-view arrangement requires a separate navigation-host migration.
