using System.IO;
using System.Windows;
using CloudflaredTunnelHost.Core.Services;
using CloudflaredTunnelHost.ViewModels;
using CloudflaredTunnelHost.Views;

namespace CloudflaredTunnelHost;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        MainViewModel? model = null;
        var index = Array.IndexOf(e.Args, "--data-dir");
        var paths = new AppPaths(index >= 0 && index + 1 < e.Args.Length ? Path.GetFullPath(e.Args[index + 1]) : null);
        try
        {
            model = new MainViewModel(paths);
            void ApplyTheme() => Resources.MergedDictionaries[0] = new ResourceDictionary { Source = new Uri($"Themes/{model.Theme}.xaml", UriKind.Relative) };
            model.ThemeChanged += ApplyTheme; ApplyTheme();
            var window = new MainWindow(model); MainWindow = window; window.Show();
        }
        catch (Exception ex)
        {
            model?.ShutdownAsync().GetAwaiter().GetResult();
            try { Directory.CreateDirectory(paths.Root); File.WriteAllText(Path.Combine(paths.Root, "startup-error.log"), ex.ToString()); }
            catch (Exception writeError) when (writeError is IOException or UnauthorizedAccessException) { }
            MessageBox.Show("프로그램을 시작할 수 없습니다. 설정 폴더의 권한을 확인하세요.\n" + ex.Message, "시작 오류", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }
}
