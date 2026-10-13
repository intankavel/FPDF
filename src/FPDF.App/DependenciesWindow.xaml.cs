using System.Diagnostics;
using System.Windows;
using FPDF.Core;

namespace FPDF.App;
public partial class DependenciesWindow : Window
{
    public DependenciesWindow() { InitializeComponent(); UpdateStatus(); }
    private void UpdateStatus()
    {
        string? pdftk = PdfTools.FindExecutable(), gs = PdfCompression.FindExecutable();
        bool Local(string? path) => path is not null && path.StartsWith(AppContext.BaseDirectory, StringComparison.OrdinalIgnoreCase);
        PdftkStatus.Text = pdftk is null ? "Não encontrado. Extraia novamente o pacote completo ou use o site oficial." : Local(pdftk) ? "PDFtk incluído no portátil." : "PDFtk disponível na instalação deste computador.";
        GhostscriptStatus.Text = gs is null ? "Não encontrado. Extraia novamente o pacote completo ou use o site oficial." : Local(gs) ? "Ghostscript incluído no portátil." : "Ghostscript disponível na instalação deste computador.";
        var ocr = OcrSettings.Find();
        OcrStatus.Text = ocr is null ? "Não encontrado. Consulte as instruções para preparar o OCR local." : Local(ocr.Python) && Local(ocr.Tesseract) ? "OCR incluído no portátil, com português e inglês." : "OCR disponível no ambiente de desenvolvimento deste computador.";
    }
    private void Open(string url)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch (Exception error) { Status.Text = error.Message; }
    }
    private void DownloadPdftk(object sender, RoutedEventArgs e) => Open("https://www.pdflabs.com/tools/pdftk-server/");
    private void DownloadGhostscript(object sender, RoutedEventArgs e) => Open("https://ghostscript.com/releases/gsdnld.html");
    private void OcrInstructions(object sender, RoutedEventArgs e) => Open("https://ocrmypdf.readthedocs.io/en/latest/installation.html#native-windows");
    private void Refresh(object sender, RoutedEventArgs e) => UpdateStatus();
    private void Finish(object sender, RoutedEventArgs e) => Close();
}
