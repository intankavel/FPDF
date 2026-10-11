using System.Text.RegularExpressions;

namespace FPDF.Core;

public enum SignatureDetection { NotDetected, SignatureFields, InspectionFailed }
public record PdfInspection(int Pages, SignatureDetection Signatures, int SignatureFields)
{
    public string SignatureMessage => Signatures switch
    {
        SignatureDetection.SignatureFields => $"Encontrado(s) {SignatureFields} campo(s) de assinatura digital. Alterar o PDF pode invalidar assinaturas. Esta verificação não valida certificados nem confirma se os campos estão assinados.",
        SignatureDetection.InspectionFailed => "Não foi possível verificar os campos de assinatura. Alterações podem invalidar assinaturas digitais.",
        _ => "Nenhum campo de assinatura identificado nesta verificação. Isso não comprova a ausência de assinaturas e não valida certificados."
    };
}

public sealed partial class PdfTools
{
    public async Task<PdfInspection> InspectAsync(string input, CancellationToken cancellation = default)
    {
        int pages = await GetPageCountAsync(input, cancellation);
        try
        {
            var fields = await RunAsync([input, "dump_data_fields_utf8"], cancellation);
            int signatures = Regex.Matches(fields, @"(?m)^FieldType:\s*Signature\s*$").Count;
            return new(pages, signatures > 0 ? SignatureDetection.SignatureFields : SignatureDetection.NotDetected, signatures);
        }
        catch (OperationCanceledException) { throw; }
        catch (InvalidOperationException) { return new(pages, SignatureDetection.InspectionFailed, 0); }
    }

    public async Task ReorderPagesAsync(string input, string destination, IReadOnlyList<int> pages, IProgress<string>? progress = null, CancellationToken cancellation = default)
    {
        int count = await GetPageCountAsync(input, cancellation);
        if (pages.Count == 0 || pages.Any(p => p < 1 || p > count) || pages.Distinct().Count() != pages.Count)
            throw new ArgumentException("Mantenha pelo menos uma página, sem números repetidos ou inexistentes.");
        // Compact consecutive runs so large documents don't exceed the Windows argument limit.
        var ranges = new List<string>();
        int first = pages[0], last = first;
        foreach (int page in pages.Skip(1))
        {
            if (page == last + 1) { last = page; continue; }
            ranges.Add(first == last ? first.ToString() : $"{first}-{last}"); first = last = page;
        }
        ranges.Add(first == last ? first.ToString() : $"{first}-{last}");
        progress?.Report("Salvando as páginas na ordem escolhida…");
        await WriteSingleAsync(destination, pages.Count, output => RunAsync([input, "cat", .. ranges, "output", output], cancellation), cancellation);
    }
}

public static class PdfPreview
{
    public static async Task RenderPageAsync(string ghostscript, string input, int page, string png, bool large = false, CancellationToken cancellation = default)
    {
        if (page < 1) throw new ArgumentOutOfRangeException(nameof(page));
        // A fixed canvas caps memory even for unusually large PDF pages; PDF content is fit proportionally.
        string temporary = png + "." + Guid.NewGuid().ToString("N") + ".png";
        try
        {
            await ExternalProcess.RunAsync(ghostscript, ["-dSAFER", "-dBATCH", "-dNOPAUSE", "-sDEVICE=png16m", "-dUseCropBox", "-dFIXEDMEDIA", "-dPDFFitPage", large ? "-g1000x1300" : "-g240x312", "-r72", "-dTextAlphaBits=4", "-dGraphicsAlphaBits=4", $"-dFirstPage={page}", $"-dLastPage={page}", "-sOutputFile=" + temporary, "-f", input], "Ghostscript", cancellation);
            cancellation.ThrowIfCancellationRequested();
            File.Move(temporary, png, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
