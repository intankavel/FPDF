using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FPDF.App;
using FPDF.Core;

internal static class ImageChecks
{
    public static void Run()
    {
        var root = Path.GetFullPath(Path.Combine("artifacts", "image-tests", Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(root);
        string portrait = Path.Combine(root, "transparente.png"), landscape = Path.Combine(root, "paisagem.bmp"), rotated = Path.Combine(root, "foto-exif.jpg"), multi = Path.Combine(root, "duas-paginas.tif");
        var p = Bitmap(160, 240, transparent: true);
        var l = Bitmap(240, 160);
        Save(new PngBitmapEncoder(), portrait, BitmapFrame.Create(p));
        Save(new BmpBitmapEncoder(), landscape, BitmapFrame.Create(l));
        var metadata = new BitmapMetadata("jpg");
        metadata.SetQuery("/app1/ifd/{ushort=274}", (ushort)6);
        Save(new JpegBitmapEncoder { QualityLevel = 95 }, rotated, BitmapFrame.Create(Bitmap(160, 240), null, metadata, null));
        Save(new TiffBitmapEncoder(), multi, BitmapFrame.Create(p), BitmapFrame.Create(l));
        string[] inputs = [portrait, landscape, rotated, multi];
        var hashes = inputs.Select(path => SHA256.HashData(File.ReadAllBytes(path))).ToArray();
        var tools = new PdfTools(PdfTools.FindExecutable()!);
        var output = Path.Combine(root, "imagens.pdf");
        ImagesToPdf.ConvertAsync(inputs, output, ImagePageLayout.AutoA4, tools).GetAwaiter().GetResult();
        Assert(tools.GetPageCountAsync(output).GetAwaiter().GetResult() == 5, "Imagens e TIFF multipágina geram cinco páginas");
        var dump = Execute(PdfTools.FindExecutable()!, [output, "dump_data"]);
        var dimensions = Regex.Matches(dump, @"PageMediaDimensions:\s*([\d.]+)\s+([\d.]+)").Select(m => m.Groups[1].Value + "x" + m.Groups[2].Value).ToArray();
        Assert(dimensions.SequenceEqual(["595x842", "842x595", "842x595", "595x842", "842x595"]), "A4 automático respeita orientação, incluindo EXIF de câmera");
        var gs = PdfCompression.FindExecutable()!;
        Execute(gs, ["-dSAFER", "-dBATCH", "-dNOPAUSE", "-sDEVICE=png16m", "-r72", "-sOutputFile=" + Path.Combine(root, "pagina-%d.png"), "-f", output]);
        var exifPage = Load(Path.Combine(root, "pagina-3.png"));
        var left = Pixel(exifPage, exifPage.PixelWidth / 2 - 100, exifPage.PixelHeight / 2);
        var right = Pixel(exifPage, exifPage.PixelWidth / 2 + 100, exifPage.PixelHeight / 2);
        Assert(left.B > 200 && left.R < 60 && right.R > 200 && right.B < 60, "Rotação EXIF mantém o conteúdo no lado correto");
        var alphaPage = Load(Path.Combine(root, "pagina-1.png"));
        var white = Pixel(alphaPage, alphaPage.PixelWidth / 2, alphaPage.PixelHeight / 2 + 180);
        Assert(white.R > 240 && white.G > 240 && white.B > 240, "Transparência é convertida para fundo branco");
        var fixedPortrait = Path.Combine(root, "retrato.pdf");
        ImagesToPdf.ConvertAsync([landscape], fixedPortrait, ImagePageLayout.PortraitA4, tools).GetAwaiter().GetResult();
        Assert(Execute(PdfTools.FindExecutable()!, [fixedPortrait, "dump_data"]).Contains("PageMediaDimensions: 595 842"), "Formato A4 retrato pode ser escolhido explicitamente");
        var corrupt = Path.Combine(root, "corrompido.png");
        File.WriteAllText(corrupt, "Not an image");
        var failedOutput = Path.Combine(root, "falha.pdf");
        bool failed = false;
        try { ImagesToPdf.ConvertAsync([portrait, corrupt], failedOutput, ImagePageLayout.AutoA4, tools).GetAwaiter().GetResult(); }
        catch (Exception) { failed = true; }
        Assert(failed && !File.Exists(failedOutput) && !Directory.GetDirectories(root, ".fpdf-*").Any(), "Imagem inválida não publica PDF parcial");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        try { ImagesToPdf.ConvertAsync(inputs, Path.Combine(root, "cancelado.pdf"), ImagePageLayout.AutoA4, tools, cancellation: cancellation.Token).GetAwaiter().GetResult(); }
        catch (OperationCanceledException) { }
        Assert(!File.Exists(Path.Combine(root, "cancelado.pdf")) && !Directory.GetDirectories(root, ".fpdf-*").Any(), "Cancelamento da conversão remove temporários");
        Assert(inputs.Select((path, i) => hashes[i].SequenceEqual(SHA256.HashData(File.ReadAllBytes(path)))).All(x => x), "Imagens originais são preservadas");
        Console.WriteLine("PASS: arquivos de teste de imagens em " + root);
    }
    private static BitmapSource Bitmap(int width, int height, bool transparent = false)
    {
        var visual = new DrawingVisual();
        using (var drawing = visual.RenderOpen())
        {
            if (!transparent) drawing.DrawRectangle(Brushes.Blue, null, new Rect(0, 0, width, height));
            drawing.DrawRectangle(Brushes.Red, null, new Rect(0, 0, width, height / 2));
        }
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        return bitmap;
    }
    private static void Save(BitmapEncoder encoder, string path, params BitmapFrame[] frames)
    {
        foreach (var frame in frames) encoder.Frames.Add(frame);
        using var stream = File.Create(path);
        encoder.Save(stream);
    }
    private static BitmapSource Load(string path)
    {
        using var input = File.OpenRead(path);
        return BitmapDecoder.Create(input, BitmapCreateOptions.None, BitmapCacheOption.OnLoad).Frames[0];
    }
    private static Color Pixel(BitmapSource bitmap, int x, int y)
    {
        var converted = new FormatConvertedBitmap(bitmap, PixelFormats.Bgra32, null, 0);
        var bytes = new byte[4];
        converted.CopyPixels(new Int32Rect(x, y, 1, 1), bytes, 4, 0);
        return Color.FromRgb(bytes[2], bytes[1], bytes[0]);
    }
    private static string Execute(string executable, string[] arguments)
    {
        var info = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in arguments) info.ArgumentList.Add(arg);
        using var process = Process.Start(info)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        if (process.ExitCode != 0) throw new Exception(error.GetAwaiter().GetResult());
        return output.GetAwaiter().GetResult();
    }
    private static void Assert(bool value, string description)
    {
        if (!value) throw new Exception("FAIL: " + description);
        Console.WriteLine("PASS: " + description);
    }
}
