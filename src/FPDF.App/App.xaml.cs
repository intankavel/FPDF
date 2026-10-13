namespace FPDF.App;
public partial class App : System.Windows.Application
{
    protected override void OnStartup(System.Windows.StartupEventArgs e)
    {
        if (e.Args.Length == 2 && e.Args[0] == "--verify-package")
        {
            ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown;
            int exit = 1;
            try
            {
                var window = new MainWindow();
                var view = (System.Windows.FrameworkElement)window.Content;
                view.Measure(new System.Windows.Size(900, 900));
                view.Arrange(new System.Windows.Rect(0, 0, 900, 900));
                view.UpdateLayout();
                string runtime = typeof(object).Assembly.Location;
                bool localRuntime = System.IO.Path.GetDirectoryName(runtime) == System.IO.Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory);
                bool source = System.IO.File.Exists(System.IO.Path.Combine(AppContext.BaseDirectory, "codigo-fonte", "FPDF.sln"));
                bool license = System.IO.File.Exists(System.IO.Path.Combine(AppContext.BaseDirectory, "LICENSE"));
                bool LocalTool(string? path) => path is not null && path.StartsWith(AppContext.BaseDirectory, StringComparison.OrdinalIgnoreCase);
                bool localPdftk = LocalTool(FPDF.Core.PdfTools.FindExecutable());
                bool localGhostscript = LocalTool(FPDF.Core.PdfCompression.FindExecutable());
                var ocr = FPDF.Core.OcrSettings.Find();
                bool localOcr = ocr is not null && LocalTool(ocr.Python) && LocalTool(ocr.Tesseract);
                bool icon = window.Icon is not null;
                System.IO.File.WriteAllText(e.Args[1], System.Text.Json.JsonSerializer.Serialize(new { Runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription, LocalRuntime = localRuntime, SourceIncluded = source, LicenseIncluded = license, InterfaceInitialized = true, LocalPdftk = localPdftk, LocalGhostscript = localGhostscript, LocalOcr = localOcr, ApplicationIcon = icon }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
                exit = localRuntime && source && license && localPdftk && localGhostscript && localOcr && icon ? 0 : 1;
                window.Close();
            }
            catch (Exception error) { System.IO.File.WriteAllText(e.Args[1], error.ToString()); }
            Shutdown(exit);
            return;
        }
        base.OnStartup(e);
        var splash = new StartupSplash();
        splash.Show();
        try
        {
            var window = new MainWindow();
            MainWindow = window;
            window.ContentRendered += (_, _) => { if (splash.IsVisible) splash.Close(); };
            window.Closed += (_, _) => { if (splash.IsVisible) splash.Close(); };
            window.Show();
        }
        catch { splash.Close(); throw; }
    }
}
