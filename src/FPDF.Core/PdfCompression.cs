namespace FPDF.Core;

public enum CompressionQuality { High, Balanced, Small }
public record TargetCompressionResult(long OriginalBytes, long OutputBytes, long MaximumBytes, CompressionQuality? Quality)
{
    public bool TargetReached => OutputBytes <= MaximumBytes;
    public string Message => !TargetReached ? "Limite não atingido; menor resultado disponível"
        : Quality is null ? "Já estava dentro do limite; cópia do original"
        : "Dentro do limite — " + QualityLabel(Quality.Value);
    public static string QualityLabel(CompressionQuality quality) => quality switch
    { CompressionQuality.High => "maior qualidade", CompressionQuality.Balanced => "equilíbrio", _ => "menor tamanho" };
}

public sealed class PdfCompression(string executable, PdfTools tools)
{
    internal Task<PdfInspection> InspectAsync(string input, CancellationToken cancellation) => tools.InspectAsync(input, cancellation);
    public async Task<TargetCompressionResult> CompressToSizeAsync(string input, string destination, long maximumBytes, IProgress<string>? progress = null, CancellationToken cancellation = default)
    {
        if (maximumBytes < 1) throw new ArgumentOutOfRangeException(nameof(maximumBytes));
        input = Path.GetFullPath(input);
        int pages = await tools.GetPageCountAsync(input, cancellation);
        long original = new FileInfo(input).Length, smallest = original;
        CompressionQuality? chosen = null;
        await tools.WriteSingleAsync(destination, pages, async output =>
        {
            string best = input;
            if (original > maximumBytes)
                foreach (var quality in new[] { CompressionQuality.High, CompressionQuality.Balanced, CompressionQuality.Small })
                {
                    cancellation.ThrowIfCancellationRequested();
                    progress?.Report("Tentando " + TargetCompressionResult.QualityLabel(quality) + "…");
                    string attempt = Path.Combine(Path.GetDirectoryName(output)!, $"tentativa-{quality}.pdf");
                    var result = await CompressAsync(input, attempt, quality, cancellation: cancellation);
                    if (result.OutputBytes < smallest)
                    { best = attempt; smallest = result.OutputBytes; chosen = quality; }
                    if (smallest <= maximumBytes) break;
                }
            cancellation.ThrowIfCancellationRequested();
            File.Copy(best, output, overwrite: true);
        }, cancellation);
        return new(original, new FileInfo(destination).Length, maximumBytes, chosen);
    }
    public async Task<SplitResult> CompressAndSplitAsync(string input, string outputParent, long maximumBytes, CompressionQuality quality, IProgress<string>? progress = null, CancellationToken cancellation = default)
    {
        if (maximumBytes < 1) throw new ArgumentOutOfRangeException(nameof(maximumBytes));
        if (!Directory.Exists(outputParent)) throw new DirectoryNotFoundException("A pasta de destino não existe.");
        var work = Path.Combine(Path.GetFullPath(outputParent), ".fpdf-compressao-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(work);
        try
        {
            var compressed = Path.Combine(work, Path.GetFileName(input));
            var sizes = await CompressAsync(input, compressed, quality, progress, cancellation);
            var result = await tools.SplitBySizeAsync(compressed, work, maximumBytes, progress, cancellation);
            await File.AppendAllTextAsync(Path.Combine(result.Directory, "resumo.txt"), $"\nCompressão: {sizes.OriginalBytes} → {sizes.OutputBytes} bytes.\n", cancellation);
            var destination = Path.Combine(outputParent, Path.GetFileName(result.Directory).Replace("_dividido_", "_comprimido_dividido_"));
            cancellation.ThrowIfCancellationRequested();
            Directory.Move(result.Directory, destination);
            return result with { Directory = destination };
        }
        finally
        {
            try { if (Directory.Exists(work)) Directory.Delete(work, recursive: true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
    public static string? FindExecutable()
    {
        var local = Path.Combine(AppContext.BaseDirectory, "tools", "ghostscript", "bin", "gswin64c.exe");
        if (File.Exists(local)) return local;
        foreach (var folder in new[] { Environment.SpecialFolder.ProgramFiles, Environment.SpecialFolder.ProgramFilesX86 })
        {
            var root = Path.Combine(Environment.GetFolderPath(folder), "gs");
            if (!Directory.Exists(root)) continue;
            var candidates = Directory.GetDirectories(root).Select(path => new { Path = path, Version = Version.TryParse(Path.GetFileName(path).TrimStart('g', 's'), out var version) ? version : new Version(0, 0) }).OrderByDescending(item => item.Version);
            foreach (var item in candidates)
                foreach (var name in new[] { "gswin64c.exe", "gswin32c.exe" })
                {
                    var candidate = Path.Combine(item.Path, "bin", name);
                    if (File.Exists(candidate)) return candidate;
                }
        }
        return null;
    }

    public async Task<OptimizationResult> CompressAsync(string input, string destination, CompressionQuality quality, IProgress<string>? progress = null, CancellationToken cancellation = default)
    {
        var version = (await ExternalProcess.RunAsync(executable, ["--version"], "Ghostscript", cancellation)).Trim();
        if (!Version.TryParse(version, out var parsed) || parsed.Major < 10)
            throw new InvalidOperationException("Instale uma versão atual do Ghostscript 10 ou superior para usar a compressão.");
        int dpi = quality switch { CompressionQuality.High => 200, CompressionQuality.Balanced => 150, CompressionQuality.Small => 96, _ => throw new ArgumentOutOfRangeException(nameof(quality)) };
        var pages = await tools.GetPageCountAsync(input, cancellation);
        var original = new FileInfo(input).Length;
        long processed = original;
        string qFactor = quality switch { CompressionQuality.High => "0.25", CompressionQuality.Balanced => "0.65", _ => "0.95" };
        int sampling = quality == CompressionQuality.High ? 1 : 2;
        string jpegDictionary = $"<< /QFactor {qFactor} /HSamples [{sampling} 1 1 {sampling}] /VSamples [{sampling} 1 1 {sampling}] >>";
        progress?.Report($"Comprimindo imagens a {dpi} dpi. Aguarde…");
        await tools.WriteSingleAsync(destination, pages, async output =>
        {
            await ExternalProcess.RunAsync(executable, [
                "-dSAFER", "-dBATCH", "-dNOPAUSE", "-sDEVICE=pdfwrite", "-dCompatibilityLevel=1.6", "-dAutoRotatePages=/None",
                "-dDetectDuplicateImages=true", "-dCompressFonts=true", "-dDownsampleColorImages=true", "-dDownsampleGrayImages=true", "-dDownsampleMonoImages=true",
                "-dPassThroughJPEGImages=false", "-dPassThroughJPXImages=false", "-dEncodeColorImages=true", "-dEncodeGrayImages=true",
                "-dAutoFilterColorImages=false", "-dAutoFilterGrayImages=false", "-dColorImageFilter=/DCTEncode", "-dGrayImageFilter=/DCTEncode",
                "-dColorImageDownsampleType=/Bicubic", "-dGrayImageDownsampleType=/Bicubic",
                $"-dColorImageResolution={dpi}", $"-dGrayImageResolution={dpi}", $"-dMonoImageResolution={(quality == CompressionQuality.High ? 300 : quality == CompressionQuality.Balanced ? 200 : 150)}",
                "-dColorImageDownsampleThreshold=1.0", "-dGrayImageDownsampleThreshold=1.0", "-dMonoImageDownsampleThreshold=1.0",
                "-sOutputFile=" + output, "-c", $"<< /ColorImageDict {jpegDictionary} /GrayImageDict {jpegDictionary} >> setdistillerparams", "-f", Path.GetFullPath(input)
            ], "Ghostscript", cancellation);
            processed = new FileInfo(output).Length;
            if (await tools.GetPageCountAsync(output, cancellation) != pages) throw new InvalidOperationException("A compressão alterou a quantidade de páginas. Nenhum resultado foi salvo.");
            if (processed >= original) File.Copy(input, output, overwrite: true);
        }, cancellation);
        return new(original, new FileInfo(destination).Length, processed);
    }
}
