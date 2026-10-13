using System.Text.Json;

namespace FPDF.Core;

public record OcrSettings(string Python, string Tesseract)
{
    public static OcrSettings? Find()
    {
        var python = Path.Combine(AppContext.BaseDirectory, "tools", "ocr", "python.exe");
        var tesseract = Path.Combine(AppContext.BaseDirectory, "tools", "tesseract", "tesseract.exe");
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (!File.Exists(Path.Combine(directory.FullName, "FPDF.sln"))) continue;
            if (!File.Exists(python)) python = Path.Combine(directory.FullName, ".build", "ocr-runtime", "Scripts", "python.exe");
            if (!File.Exists(tesseract)) tesseract = Path.Combine(directory.FullName, ".build", "ocr-tesseract", "tesseract.exe");
            break;
        }
        if (!File.Exists(python))
            python = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share", "uv", "tools", "ocrmypdf", "Scripts", "python.exe");
        if (!File.Exists(tesseract)) tesseract = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Tesseract-OCR", "tesseract.exe");
        return File.Exists(python) && File.Exists(tesseract) ? new(python, tesseract) : null;
    }
}

public record OcrResult(int Pages, int AlreadyText, int Recognized, int WithoutText);

public sealed class PdfOcr(OcrSettings settings, PdfTools tools)
{
    public async Task<OcrResult> RecognizeAsync(string input, string destination, string language = "por", IProgress<string>? progress = null, CancellationToken cancellation = default)
    {
        if (language is not ("por" or "por+eng" or "eng")) throw new ArgumentException("Selecione um idioma disponível para OCR.");
        string helper = Path.Combine(AppContext.BaseDirectory, "Ocr", "recognize.py");
        if (!File.Exists(helper) || !File.Exists(settings.Python) || !File.Exists(settings.Tesseract))
            throw new InvalidOperationException("Os componentes de OCR não foram encontrados. Use Verificar componentes para preparar este computador.");
        string data = Path.Combine(Path.GetDirectoryName(settings.Tesseract)!, "tessdata");
        if (language.Split('+').Any(name => !File.Exists(Path.Combine(data, name + ".traineddata"))))
            throw new InvalidOperationException("O modelo do idioma selecionado não foi encontrado. Verifique os componentes de OCR.");
        int pages = await tools.GetPageCountAsync(input, cancellation);
        OcrResult? result = null;
        await tools.WriteSingleAsync(destination, pages, async output =>
        {
            string temporary = Path.Combine(Path.GetDirectoryName(output)!, "ocr-temporario");
            Directory.CreateDirectory(temporary);
            var environment = new Dictionary<string, string>
            {
                ["PATH"] = Path.GetDirectoryName(settings.Tesseract)! + Path.PathSeparator + Environment.GetEnvironmentVariable("PATH"),
                ["TESSDATA_PREFIX"] = data, ["PYTHONUTF8"] = "1", ["PYTHONUNBUFFERED"] = "1",
                ["TEMP"] = temporary, ["TMP"] = temporary, ["TMPDIR"] = temporary
            };
            string json = await ExternalProcess.RunAsync(settings.Python, ["-X", "utf8", "-B", helper, Path.GetFullPath(input), output, language], "OCR", cancellation, progress, environment);
            result = JsonSerializer.Deserialize<OcrResult>(json.Trim()) ?? throw new InvalidOperationException("Não foi possível conferir o resultado do OCR.");
            if (result.Pages != pages || result.AlreadyText < 0 || result.Recognized < 0 || result.WithoutText < 0 || result.AlreadyText + result.Recognized + result.WithoutText != pages)
                throw new InvalidOperationException("O resumo do OCR está incompleto. Nenhum resultado foi salvo.");
        }, cancellation);
        return result!;
    }
}
