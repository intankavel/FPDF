using System.Globalization;
using System.Text.RegularExpressions;

namespace FPDF.Core;

public record PdfPart(string Name, int FirstPage, int LastPage, long Bytes);
public record SplitResult(string Directory, IReadOnlyList<PdfPart> Parts);
public record OptimizationResult(long OriginalBytes, long OutputBytes, long? ProcessedBytes = null)
{
    public decimal ReductionPercent => OriginalBytes == 0 ? 0 : (OriginalBytes - OutputBytes) * 100m / OriginalBytes;
}

public sealed partial class PdfTools(string executable)
{
    public static string? FindExecutable()
    {
        string[] candidates = [
            Path.Combine(AppContext.BaseDirectory, "tools", "pdftk.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "PDFtk", "bin", "pdftk.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "PDFtk", "bin", "pdftk.exe")];
        return candidates.FirstOrDefault(File.Exists);
    }

    public async Task<int> GetPageCountAsync(string input, CancellationToken cancellation = default)
    {
        CheckInput(input);
        var data = await RunAsync([input, "dump_data_utf8"], cancellation);
        var match = Regex.Match(data, @"(?m)^NumberOfPages:\s*(\d+)\s*$");
        if (!match.Success || !int.TryParse(match.Groups[1].Value, out int pages) || pages < 1)
            throw new InvalidOperationException("Não foi possível identificar as páginas do PDF. Verifique se o arquivo está protegido por senha ou danificado.");
        return pages;
    }

    public async Task MergeAsync(IReadOnlyList<string> inputs, string destination, IProgress<string>? progress = null, CancellationToken cancellation = default)
    {
        if (inputs.Count < 2) throw new ArgumentException("Adicione pelo menos dois PDFs para juntar.");
        foreach (var input in inputs) CheckInput(input);
        destination = Path.GetFullPath(destination);
        if (File.Exists(destination)) throw new IOException("Já existe um arquivo com esse nome. Escolha outro nome para preservar o original.");
        var work = CreateWorkDirectory(Path.GetDirectoryName(destination)!);
        try
        {
            progress?.Report("Juntando os documentos na ordem da lista…");
            var output = Path.Combine(work, "unido.pdf");
            await RunAsync([.. inputs, "cat", "output", output], cancellation);
            var expected = 0;
            foreach (var input in inputs) expected += await GetPageCountAsync(input, cancellation);
            if (await GetPageCountAsync(output, cancellation) != expected)
                throw new InvalidOperationException("A verificação do número de páginas falhou. Nenhum resultado foi salvo.");
            cancellation.ThrowIfCancellationRequested();
            File.Move(output, destination, overwrite: false);
        }
        finally { Cleanup(work); }
    }

    public async Task<SplitResult> SplitBySizeAsync(string input, string outputParent, long maximumBytes, IProgress<string>? progress = null, CancellationToken cancellation = default)
    {
        CheckInput(input);
        if (maximumBytes < 1) throw new ArgumentOutOfRangeException(nameof(maximumBytes));
        var total = await GetPageCountAsync(input, cancellation);
        var work = CreateWorkDirectory(outputParent);
        var parts = new List<PdfPart>();
        var stem = Path.GetFileNameWithoutExtension(input);
        // Limit directory and output name lengths independently of the input name.
        if (stem.Length > 70) stem = stem[..70];
        try
        {
            int first = 1;
            while (first <= total)
            {
                cancellation.ThrowIfCancellationRequested();
                progress?.Report($"Preparando parte {parts.Count + 1}: páginas {first} a {total}…");
                var candidate = Path.Combine(work, "candidate.pdf");
                var best = Path.Combine(work, "best.pdf");
                async Task<long> Extract(int last)
                {
                    if (File.Exists(candidate)) File.Delete(candidate);
                    await RunAsync([input, "cat", $"{first}-{last}", "output", candidate], cancellation);
                    if (!File.Exists(candidate)) throw new IOException("O PDFtk não gerou o arquivo esperado.");
                    return new FileInfo(candidate).Length;
                }
                // Test the single page explicitly: size is measured after serialization.
                var bytes = await Extract(first);
                if (bytes > maximumBytes)
                    throw new InvalidOperationException($"A página {first}, sozinha, ocupa {bytes / 1_000_000m:0.###} MB e ultrapassa o limite de {maximumBytes / 1_000_000m:0.###} MB. É necessário comprimi-la ou aumentar o limite. Nenhuma parte foi salva.");
                File.Move(candidate, best, overwrite: true);
                int lastValid = first;
                int low = first + 1, high = total;
                // PDF sizes are not guaranteed to be monotonic. This search finds a valid
                // grouping, without promising the mathematically minimum number of parts.
                // Every chosen result is measured and verified before publication.
                while (low <= high)
                {
                    var middle = low + (high - low) / 2;
                    bytes = await Extract(middle);
                    if (bytes <= maximumBytes)
                    {
                        lastValid = middle;
                        File.Move(candidate, best, overwrite: true);
                        low = middle + 1;
                    }
                    else high = middle - 1;
                }
                var name = $"{stem}_parte-{parts.Count + 1:000}_paginas-{first}-{lastValid}.pdf";
                var partPath = Path.Combine(work, name);
                File.Move(best, partPath);
                bytes = new FileInfo(partPath).Length;
                if (bytes > maximumBytes || await GetPageCountAsync(partPath, cancellation) != lastValid - first + 1)
                    throw new InvalidOperationException("A verificação de uma parte falhou. Nenhuma parte foi salva.");
                parts.Add(new(name, first, lastValid, bytes));
                first = lastValid + 1;
            }
            var finalDirectory = Path.Combine(outputParent, $"{stem}_dividido_{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid().ToString("N")[..6]}");
            if (File.Exists(Path.Combine(work, "candidate.pdf"))) File.Delete(Path.Combine(work, "candidate.pdf"));
            var summary = new List<string> { "FPDF — divisão por tamanho máximo", $"Origem: {Path.GetFileName(input)}", $"Limite: {maximumBytes.ToString(CultureInfo.InvariantCulture)} bytes (1 MB = 1.000.000 bytes)", $"Páginas: {total}", "" };
            summary.AddRange(parts.Select(p => $"{p.Name} | páginas {p.FirstPage}–{p.LastPage} | {p.Bytes} bytes"));
            await File.WriteAllLinesAsync(Path.Combine(work, "resumo.txt"), summary, cancellation);
            cancellation.ThrowIfCancellationRequested();
            Directory.Move(work, finalDirectory);
            return new(finalDirectory, parts);
        }
        finally { Cleanup(work); }
    }

    public async Task ExtractAsync(string input, string destination, string selection, IProgress<string>? progress = null, CancellationToken cancellation = default)
    {
        var ranges = PageSelection.Parse(selection, await GetPageCountAsync(input, cancellation));
        var count = checked(ranges.Sum(r => r.Count));
        progress?.Report("Extraindo as páginas na ordem informada…");
        await WriteSingleAsync(destination, count, output => RunAsync([input, "cat", .. ranges.Select(r => r.Argument), "output", output], cancellation), cancellation);
    }

    public async Task RotateAsync(string input, string destination, string selection, int degrees, IProgress<string>? progress = null, CancellationToken cancellation = default)
    {
        string direction = degrees switch { 90 => "right", -90 => "left", 180 => "down", _ => throw new ArgumentException("Escolha 90°, -90° ou 180°.") };
        var count = await GetPageCountAsync(input, cancellation);
        var ranges = PageSelection.Union(PageSelection.Parse(selection, count, emptyMeansAll: true));
        progress?.Report("Girando as páginas selecionadas…");
        await WriteSingleAsync(destination, count, output => RunAsync([input, "rotate", .. ranges.Select(r => r.Argument + direction), "output", output], cancellation), cancellation);
    }

    public async Task RotateMixedAsync(string input, string destination, string expression, IProgress<string>? progress = null, CancellationToken cancellation = default)
    {
        var count = await GetPageCountAsync(input, cancellation);
        var rules = RotationSelection.Parse(expression, count);
        progress?.Report("Aplicando os giros de cada página…");
        await WriteSingleAsync(destination, count, output => RunAsync([input, "rotate", .. rules.Select(r => r.Pages.Argument + (r.Degrees == 90 ? "right" : r.Degrees == -90 ? "left" : "down")), "output", output], cancellation), cancellation);
    }

    public async Task<SplitResult> SplitByRangesAsync(string input, string outputParent, string selection, IProgress<string>? progress = null, CancellationToken cancellation = default)
    {
        var ranges = PageSelection.Parse(selection, await GetPageCountAsync(input, cancellation));
        var work = CreateWorkDirectory(outputParent);
        var stem = Path.GetFileNameWithoutExtension(input);
        if (stem.Length > 70) stem = stem[..70];
        var parts = new List<PdfPart>();
        try
        {
            foreach (var range in ranges)
            {
                cancellation.ThrowIfCancellationRequested();
                progress?.Report($"Criando parte {parts.Count + 1} de {ranges.Count}…");
                var name = $"{stem}_parte-{parts.Count + 1:000}_paginas-{range.First}-{range.Last}.pdf";
                var path = Path.Combine(work, name);
                await RunAsync([input, "cat", range.Argument, "output", path], cancellation);
                if (await GetPageCountAsync(path, cancellation) != range.Count) throw new InvalidOperationException("A verificação das páginas falhou. Nenhuma parte foi salva.");
                parts.Add(new(name, range.First, range.Last, new FileInfo(path).Length));
            }
            await File.WriteAllLinesAsync(Path.Combine(work, "resumo.txt"), parts.Select(p => $"{p.Name} | páginas {p.FirstPage}–{p.LastPage} | {p.Bytes} bytes"), cancellation);
            var final = Path.Combine(outputParent, $"{stem}_intervalos_{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid().ToString("N")[..6]}");
            cancellation.ThrowIfCancellationRequested();
            Directory.Move(work, final);
            return new(final, parts);
        }
        finally { Cleanup(work); }
    }

    public async Task<OptimizationResult> OptimizeAsync(string input, string destination, IProgress<string>? progress = null, CancellationToken cancellation = default)
    {
        var count = await GetPageCountAsync(input, cancellation);
        var original = new FileInfo(input).Length;
        progress?.Report("Otimizando o PDF sem reduzir a resolução das imagens…");
        await WriteSingleAsync(destination, count, async output =>
        {
            await RunAsync([input, "output", output, "compress"], cancellation);
            if (new FileInfo(output).Length >= original) File.Copy(input, output, overwrite: true);
        }, cancellation);
        return new(original, new FileInfo(destination).Length);
    }

    public async Task AddWatermarkAsync(string input, string destination, string text, int opacityPercent, bool diagonal, IProgress<string>? progress = null, CancellationToken cancellation = default)
    {
        int count = await GetPageCountAsync(input, cancellation);
        progress?.Report("Aplicando a marca-d'água em todas as páginas…");
        await WriteSingleAsync(destination, count, async output =>
        {
            var stamp = Path.Combine(Path.GetDirectoryName(output)!, "marca.pdf");
            PdfWatermark.Create(stamp, text, opacityPercent, diagonal);
            await RunAsync([input, "stamp", stamp, "output", output], cancellation);
        }, cancellation);
    }

    public async Task AddPageNumbersAsync(string input, string destination, int firstPage = 1, int initialNumber = 1, PageNumberFormat format = PageNumberFormat.PageNumberOfTotal, PageNumberPosition position = PageNumberPosition.FooterCenter, IProgress<string>? progress = null, CancellationToken cancellation = default)
    {
        if (initialNumber is < 1 or > 1_000_000 || !Enum.IsDefined(format) || !Enum.IsDefined(position)) throw new ArgumentException("Verifique o número inicial, o formato e a posição da numeração.");
        int count = await GetPageCountAsync(input, cancellation);
        if (firstPage < 1 || firstPage > count) throw new ArgumentException($"A primeira página a numerar deve estar entre 1 e {count}.");
        if ((long)initialNumber + count - firstPage > 1_000_000) throw new ArgumentException("O número final não pode ultrapassar 1.000.000.");
        var pages = PdfNumbering.ReadPages(await RunAsync([input, "dump_data_utf8"], cancellation), count);
        progress?.Report("Numerando as páginas, mantendo seus formatos e orientações…");
        await WriteSingleAsync(destination, count, async output =>
        {
            var stamp = Path.Combine(Path.GetDirectoryName(output)!, "numeros.pdf");
            PdfNumbering.Create(stamp, pages, firstPage, initialNumber, format, position, cancellation);
            await RunAsync([input, "multistamp", stamp, "output", output], cancellation);
        }, cancellation);
    }

    public async Task WriteSingleAsync(string destination, int expectedPages, Func<string, Task> generate, CancellationToken cancellation)
    {
        destination = Path.GetFullPath(destination);
        if (File.Exists(destination)) throw new IOException("Já existe um arquivo com esse nome. Escolha outro nome para preservar o original.");
        var work = CreateWorkDirectory(Path.GetDirectoryName(destination)!);
        try
        {
            var output = Path.Combine(work, "resultado.pdf");
            await generate(output);
            if (await GetPageCountAsync(output, cancellation) != expectedPages) throw new InvalidOperationException("A verificação do número de páginas falhou. Nenhum resultado foi salvo.");
            cancellation.ThrowIfCancellationRequested();
            File.Move(output, destination, overwrite: false);
        }
        finally { Cleanup(work); }
    }

    private Task<string> RunAsync(IEnumerable<string> arguments, CancellationToken cancellation)
    {
        var args = arguments.ToArray();
        return ExternalProcess.RunAsync(executable, args.Contains("output") ? [.. args, "dont_ask"] : args, "PDFtk", cancellation);
    }

    private static void CheckInput(string input)
    {
        if (!File.Exists(input)) throw new FileNotFoundException("Arquivo não encontrado: " + input);
        if (!string.Equals(Path.GetExtension(input), ".pdf", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Selecione somente arquivos PDF.");
    }
    private static string CreateWorkDirectory(string parent)
    {
        if (!Directory.Exists(parent)) throw new DirectoryNotFoundException("A pasta de destino não existe.");
        var path = Path.Combine(Path.GetFullPath(parent), ".fpdf-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
    private static void Cleanup(string work)
    {
        try { if (Directory.Exists(work)) Directory.Delete(work, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
