using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FPDF.Core;

internal static class NumberingChecks
{
    public static void Run()
    {
        var root = Path.GetFullPath(Path.Combine("artifacts", "numbering-tests", Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(root);
        string input = Path.Combine(root, "formatos.pdf");
        (int Width, int Height, int Rotation, int X, int Y)[] pages = [(595,842,0,0,0), (595,842,90,0,0), (595,842,180,0,0), (595,842,270,0,0), (842,595,0,0,0), (612,792,0,0,0), (595,842,0,30,50)];
        using (var pdf = new PdfDocumentWriter(input))
        {
            pdf.Object(1, "<< /Type /Catalog /Pages 2 0 R >>");
            pdf.Object(2, $"<< /Type /Pages /Count {pages.Length} /Kids [{string.Join(" ", Enumerable.Range(0, pages.Length).Select(i => $"{3 + i * 2} 0 R"))}] >>");
            for (int i = 0; i < pages.Length; i++)
            {
                var p = pages[i]; int id = 3 + i * 2;
                pdf.Object(id, $"<< /Type /Page /Parent 2 0 R /MediaBox [{p.X} {p.Y} {p.X+p.Width} {p.Y+p.Height}] /Rotate {p.Rotation} /Resources << >> /Contents {id+1} 0 R >>");
                pdf.Stream(id + 1, "", Encoding.ASCII.GetBytes($"q 0.7 g {p.X+200} {p.Y+250} 100 100 re f Q"));
            }
            pdf.Complete(1);
        }
        var tools = new PdfTools(PdfTools.FindExecutable()!);
        foreach (var position in Enum.GetValues<PageNumberPosition>())
        {
            string output = Path.Combine(root, position + ".pdf");
            tools.AddPageNumbersAsync(input, output, position: position).GetAwaiter().GetResult();
            Execute(PdfCompression.FindExecutable()!, ["-dSAFER", "-dBATCH", "-dNOPAUSE", "-sDEVICE=png16m", "-r72", "-sOutputFile=" + Path.Combine(root, position + "-%d.png"), "-f", output]);
            for (int i = 0; i < pages.Length; i++)
            {
                using var stream = File.OpenRead(Path.Combine(root, position + $"-{i+1}.png"));
                var image = new FormatConvertedBitmap(BitmapDecoder.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad).Frames[0], PixelFormats.Bgra32, null, 0);
                int width = image.PixelWidth, height = image.PixelHeight;
                var pixels = new byte[width * height * 4]; image.CopyPixels(pixels, width * 4, 0);
                int x1 = position is PageNumberPosition.FooterLeft or PageNumberPosition.HeaderLeft ? 10 : position is PageNumberPosition.FooterRight or PageNumberPosition.HeaderRight ? width - 160 : width / 2 - 80;
                int y1 = position >= PageNumberPosition.HeaderCenter ? 10 : height - 40;
                int dark = 0;
                for (int y = y1; y < y1 + 30; y++)
                    for (int x = x1; x < x1 + 150; x++) if (pixels[(y * width + x) * 4] < 120) dark++;
                if (dark < 20) throw new Exception($"FAIL: posição {position} na página {i+1}.");
            }
            Console.WriteLine($"PASS: {position} em páginas giradas, A4, Carta, paisagem e origem deslocada.");
        }
        Console.WriteLine("PASS: pré-visualizações de numeração em " + root);
    }
    private static void Execute(string executable, string[] arguments)
    {
        var info = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (string argument in arguments) info.ArgumentList.Add(argument);
        using var process = Process.Start(info)!;
        var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
        process.WaitForExit(); Task.WaitAll(output, error);
        if (process.ExitCode != 0) throw new Exception(error.Result);
    }
}
