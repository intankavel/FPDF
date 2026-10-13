using System.Diagnostics;
using System.IO;
using System.Windows;
using FPDF.Core;
namespace FPDF.App;
public partial class BatchResultsWindow : Window
{
    private readonly string directory;
    public BatchResultsWindow(BatchCompressionResult result)
    {
        InitializeComponent(); directory = result.Directory;
        Summary.Text = $"{result.Items.Count(i => i.Output is not null)} de {result.Items.Count} PDFs salvos. Originais preservados." + (result.Canceled ? " O lote foi cancelado." : "") + " Detalhes também estão em resumo.txt.";
        string Size(long? bytes) => bytes is long value ? $"{value/1_000_000m:0.###} MB" : "—";
        Results.ItemsSource = result.Items.Select(i => new { Name=Path.GetFileName(i.Input), Before=Size(i.OriginalBytes), After=Size(i.OutputBytes), i.Message });
    }
    private void OpenFolder(object sender, RoutedEventArgs e)
    { try { Process.Start(new ProcessStartInfo(directory) { UseShellExecute=true }); } catch (Exception error) { Summary.Text = error.Message; } }
    private void Finish(object sender, RoutedEventArgs e) => Close();
    private void ResultsResized(object sender, SizeChangedEventArgs e)
    {
        if (Results.Columns.Count != 4) return;
        double nameWidth = Math.Max(140,Results.ActualWidth*.3);
        Results.Columns[0].Width = new(nameWidth);
        Results.Columns[3].Width = new(Math.Max(180,Results.ActualWidth-nameWidth-184));
    }
}
