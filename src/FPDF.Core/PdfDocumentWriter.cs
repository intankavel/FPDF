using System.Globalization;
using System.Text;

namespace FPDF.Core;

// Writes PDF objects directly to disk, so an image collection need not remain in memory.
public sealed class PdfDocumentWriter : IDisposable
{
    private readonly FileStream stream;
    private readonly Dictionary<int, long> offsets = [];
    public PdfDocumentWriter(string path)
    {
        stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write);
        Write("%PDF-1.4\n");
    }
    public void Object(int id, string body)
    {
        Begin(id); Write(body); Write("\nendobj\n");
    }
    public void Stream(int id, string properties, ReadOnlySpan<byte> bytes)
    {
        Begin(id); Write($"<< {properties} /Length {bytes.Length} >>\nstream\n");
        stream.Write(bytes); Write("\nendstream\nendobj\n");
    }
    public void Complete(int catalogId)
    {
        int count = offsets.Keys.Max();
        if (offsets.Count != count) throw new InvalidOperationException("A estrutura do PDF ficou incompleta.");
        long start = stream.Position;
        Write($"xref\n0 {count + 1}\n0000000000 65535 f \n");
        for (int i = 1; i <= count; i++) Write(offsets[i].ToString("0000000000", CultureInfo.InvariantCulture) + " 00000 n \n");
        Write($"trailer\n<< /Size {count + 1} /Root {catalogId} 0 R >>\nstartxref\n{start.ToString(CultureInfo.InvariantCulture)}\n%%EOF\n");
    }
    private void Begin(int id)
    {
        if (id < 1 || !offsets.TryAdd(id, stream.Position)) throw new InvalidOperationException("Objeto PDF duplicado.");
        Write($"{id} 0 obj\n");
    }
    private void Write(string text) => stream.Write(Encoding.ASCII.GetBytes(text));
    public void Dispose() => stream.Dispose();
}

public static class PdfWatermark
{
    public static void Create(string path, string text, int opacityPercent, bool diagonal)
    {
        text = text.Trim();
        if (text.Length is < 1 or > 80 || text.Any(char.IsControl)) throw new ArgumentException("Informe uma marca de 1 a 80 caracteres, sem quebras de linha.");
        if (opacityPercent is < 5 or > 60) throw new ArgumentException("Escolha uma opacidade entre 5% e 60%.");
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var encoding = Encoding.GetEncoding(1252, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
        byte[] bytes;
        try { bytes = encoding.GetBytes(text); }
        catch (EncoderFallbackException) { throw new ArgumentException("Use letras, números e símbolos latinos na marca-d'água. Emojis e alguns alfabetos ainda não são suportados."); }
        string hex = Convert.ToHexString(bytes);
        double size = Math.Clamp((diagonal ? 640 : 490) / (text.Length * 0.65), 10, 72);
        double width = text.Sum(c => c == ' ' ? 0.28 : char.IsUpper(c) ? 0.67 : 0.53) * size;
        string N(double number) => number.ToString("0.####", CultureInfo.InvariantCulture);
        string matrix = diagonal ? "0.7071 0.7071 -0.7071 0.7071" : "1 0 0 1";
        string content = $"q /GS1 gs 0.45 g {matrix} 297.5 421 cm BT /F1 {N(size)} Tf 1 0 0 1 {N(-width / 2)} 0 Tm <{hex}> Tj ET Q";
        using var pdf = new PdfDocumentWriter(path);
        pdf.Object(1, "<< /Type /Catalog /Pages 2 0 R >>");
        pdf.Object(2, "<< /Type /Pages /Count 1 /Kids [3 0 R] >>");
        pdf.Object(3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 5 0 R >> /ExtGState << /GS1 6 0 R >> >> /Contents 4 0 R >>");
        pdf.Stream(4, "", Encoding.ASCII.GetBytes(content));
        pdf.Object(5, "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>");
        pdf.Object(6, $"<< /Type /ExtGState /ca {N(opacityPercent / 100d)} /CA {N(opacityPercent / 100d)} >>");
        pdf.Complete(1);
    }
}
