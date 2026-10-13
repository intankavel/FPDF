namespace FPDF.Core;

public record BatchCompressionItem(string Input, string? Output, long? OriginalBytes, long? OutputBytes, string Message);
public record BatchCompressionResult(string Directory, IReadOnlyList<BatchCompressionItem> Items, bool Canceled);

public static class PdfBatchCompression
{
    public static async Task<BatchCompressionResult> RunAsync(PdfCompression compressor, IReadOnlyList<string> inputs, string parent, CompressionQuality quality, IProgress<string>? progress = null, CancellationToken cancellation = default, long? maximumBytes = null)
    {
        if (inputs.Count == 0 || !Directory.Exists(parent)) throw new ArgumentException("Selecione PDFs e uma pasta de destino existente.");
        if (maximumBytes is < 1) throw new ArgumentOutOfRangeException(nameof(maximumBytes));
        cancellation.ThrowIfCancellationRequested();
        string directory = Path.Combine(parent, "FPDF_comprimidos_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + "_" + Guid.NewGuid().ToString("N")[..6]);
        Directory.CreateDirectory(directory);
        var items = new List<BatchCompressionItem>();
        bool canceled = false;
        for (int i = 0; i < inputs.Count; i++)
        {
            if (cancellation.IsCancellationRequested) { canceled = true; break; }
            string input = inputs[i], stem = Path.GetFileNameWithoutExtension(input);
            if (stem.Length > 60) stem = stem[..60];
            string output = Path.Combine(directory, $"{i+1:000}_{stem}_comprimido.pdf");
            progress?.Report($"Comprimindo {i+1} de {inputs.Count}: {Path.GetFileName(input)}…");
            try
            {
                var inspection = await compressor.InspectAsync(input, cancellation);
                long originalBytes, outputBytes;
                string message;
                if (maximumBytes is long limit)
                {
                    var result = await compressor.CompressToSizeAsync(input, output, limit, cancellation: cancellation);
                    originalBytes = result.OriginalBytes; outputBytes = result.OutputBytes; message = result.Message;
                }
                else
                {
                    var result = await compressor.CompressAsync(input, output, quality, cancellation: cancellation);
                    originalBytes = result.OriginalBytes; outputBytes = result.OutputBytes;
                    message = result.OutputBytes < result.OriginalBytes ? "Comprimido" : "Sem redução; cópia do original";
                }
                if (inspection.Signatures == SignatureDetection.SignatureFields) message += "; há campos de assinatura — confira no original";
                if (inspection.Signatures == SignatureDetection.InspectionFailed) message += "; verificação de assinatura inconclusiva";
                items.Add(new(input, output, originalBytes, outputBytes, message));
            }
            catch (OperationCanceledException) { canceled = true; break; }
            catch (Exception error) when (error is IOException or InvalidOperationException or ArgumentException)
            { items.Add(new(input, null, File.Exists(input) ? new FileInfo(input).Length : null, null, "Falha: " + error.Message)); }
        }
        foreach (string input in inputs.Skip(items.Count)) items.Add(new(input, null, null, null, "Não processado; cancelamento"));
        // Already completed PDFs remain available when the batch is canceled.
        var lines = new List<string> { "FPDF — compressão em lote", "Originais preservados. PDFs concluídos são mantidos mesmo após cancelar.", "" };
        if (maximumBytes is long maximum) lines.Add($"Limite por PDF: {maximum} bytes.");
        foreach (var item in items)
            lines.Add($"{Path.GetFileName(item.Input)} | {item.OriginalBytes?.ToString() ?? "—"} → {item.OutputBytes?.ToString() ?? "—"} bytes | {item.Message.Replace('\r',' ').Replace('\n',' ')}");
        await File.WriteAllLinesAsync(Path.Combine(directory, "resumo.txt"), lines, CancellationToken.None);
        return new(directory, items, canceled);
    }
}
