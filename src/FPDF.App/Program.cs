using System.IO;
using System.Windows;

namespace FPDF.App;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            var app = new App();
            app.InitializeComponent();
            return app.Run();
        }
        catch (Exception error)
        {
            string report = args.Length == 2 && args[0] == "--verify-package" ? args[1] : Path.Combine(Path.GetTempPath(), "FPDF-inicializacao.txt");
            try { File.WriteAllText(report, error.ToString()); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            if (!(args.Length == 2 && args[0] == "--verify-package")) MessageBox.Show("Não foi possível iniciar o FPDF. Detalhes em " + report, "FPDF — erro de inicialização", MessageBoxButton.OK, MessageBoxImage.Warning);
            return 1;
        }
    }
}
