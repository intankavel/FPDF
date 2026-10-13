using System.Diagnostics;
using System.Security.Cryptography;
using FPDF.Core;

internal static class OcrChecks
{
    public static async Task Run()
    {
        string? portable = Environment.GetEnvironmentVariable("FPDF_TEST_TOOL_ROOT");
        var settings = portable is null ? OcrSettings.Find() ?? throw new Exception("Prepare os componentes de OCR antes de executar estes testes.")
            : new OcrSettings(Path.Combine(portable, "tools", "ocr", "python.exe"), Path.Combine(portable, "tools", "tesseract", "tesseract.exe"));
        string root = Path.GetFullPath(Path.Combine("artifacts", "ocr", Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(root);
        await Python(settings, [Path.GetFullPath("tests/ocr/fixtures.py"), root]);
        var tools = new PdfTools(portable is null ? PdfTools.FindExecutable()! : Path.Combine(portable, "tools", "pdftk.exe"));
        var ocr = new PdfOcr(settings, tools);
        string input = Path.Combine(root, "mixed.pdf"), output = Path.Combine(root, "pesquisável.pdf");
        byte[] hash = SHA256.HashData(File.ReadAllBytes(input));
        var progress = new RecordingProgress();
        var result = await ocr.RecognizeAsync(input, output, progress: progress);
        Check(result is { Pages: 4, AlreadyText: 1, Recognized: 2, WithoutText: 1 }, "OCR distingue digitalizações, texto existente e página em branco");
        Check(progress.Messages.Any(m => m.Contains("página") && m.Contains("de 4")), "OCR apresenta progresso por página");
        Check(hash.SequenceEqual(SHA256.HashData(File.ReadAllBytes(input))), "OCR preserva o original byte a byte");
        await Python(settings, [Path.GetFullPath("tests/ocr/verify.py"), input, output, root]);
        Console.WriteLine("PASS: aparência, orientação, formato, texto pesquisável e alinhamento do OCR conferidos");
        string textInput = Path.Combine(root, "text.pdf"), copy = Path.Combine(root, "texto-copia.pdf");
        var already = await ocr.RecognizeAsync(textInput, copy);
        Check(already is { Recognized: 0, AlreadyText: 1 } && File.ReadAllBytes(textInput).SequenceEqual(File.ReadAllBytes(copy)), "PDF já pesquisável é copiado sem alterações");
        await Fails<IOException>(() => ocr.RecognizeAsync(input, output), "OCR não sobrescreve resultado existente");
        await Fails<ArgumentException>(() => ocr.RecognizeAsync(input, Path.Combine(root, "idioma.pdf"), "invalid"), "OCR rejeita idioma inválido");
        using var cancel = new CancellationTokenSource();
        string interrupted = Path.Combine(root, "cancelado.pdf");
        await Fails<OperationCanceledException>(() => ocr.RecognizeAsync(input, interrupted, progress: new CancelAtPage(cancel), cancellation: cancel.Token), "OCR pode ser cancelado durante o reconhecimento");
        Check(!File.Exists(interrupted) && !Directory.GetDirectories(root, ".fpdf-*").Any(), "Cancelamento remove tentativas e não salva PDF parcial");
        var bilingual = await ocr.RecognizeAsync(Path.Combine(root, "scan-1.pdf"), Path.Combine(root, "bilingue.pdf"), "por+eng");
        Check(bilingual.Recognized == 1, "OCR reconhece português e inglês");
        await Fails<InvalidOperationException>(() => ocr.RecognizeAsync(Path.Combine(root, "protected.pdf"), Path.Combine(root, "protegido-resultado.pdf")), "PDF protegido produz erro sem publicar resultado");
        Console.WriteLine("PASS: OCR completo. Arquivos sintéticos em " + root);
    }
    private static void Check(bool condition, string message)
    { if (!condition) throw new Exception(message); Console.WriteLine("PASS: " + message); }
    private static async Task Fails<T>(Func<Task> action, string message) where T : Exception
    { try { await action(); } catch (T) { Console.WriteLine("PASS: " + message); return; } throw new Exception(message); }
    private static async Task Python(OcrSettings settings, string[] args)
    {
        var info = new ProcessStartInfo(settings.Python) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        info.Environment["PYTHONUTF8"] = "1";
        info.ArgumentList.Add("-X"); info.ArgumentList.Add("utf8"); info.ArgumentList.Add("-B");
        foreach (string arg in args) info.ArgumentList.Add(arg);
        using var process = Process.Start(info)!;
        var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        string errors = await stderr; await stdout;
        if (process.ExitCode != 0) throw new Exception(errors);
    }
    private sealed class RecordingProgress : IProgress<string>
    { public List<string> Messages { get; } = []; public void Report(string value) => Messages.Add(value); }
    private sealed class CancelAtPage(CancellationTokenSource source) : IProgress<string>
    { public void Report(string value) { if (value.Contains("página")) source.Cancel(); } }
}
