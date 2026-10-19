namespace CloudflaredTunnelHost.Core.Services;

public sealed class AppPaths(string? root = null)
{
    // Preserve the WinForms product-name directory so existing installations migrate in place.
    public string Root { get; } = root ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CloudflaredTunnelHost");
    public string Settings => Path.Combine(Root, "settings.json");
    public string LegacySettings => Path.Combine(Root, "settings.txt");
    public string Executable => Path.Combine(Root, "tools", "cloudflared.exe");
    public string Logs => Path.Combine(Root, "logs");
}
