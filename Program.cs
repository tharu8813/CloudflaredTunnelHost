using CloudflaredTunnelHost.Start;

namespace CloudflaredTunnelHost {
    internal static class Program {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main() {
            // To customize application configuration such as set high DPI settings or default font,
            // see https://aka.ms/applicationconfiguration.
            ApplicationConfiguration.Initialize();
            Application.SetDefaultFont(new Font(new FontFamily("맑은 고딕"), 9f));
            Application.Run(new MainForm());
        }
    }
}