using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace FPDF.Core;

public enum PageNumberFormat { Number, PageNumber, PageNumberOfTotal }
public enum PageNumberPosition { FooterCenter, FooterRight, FooterLeft, HeaderCenter, HeaderRight, HeaderLeft }

internal record NumberingPage(double Width, double Height);

internal static class PdfNumbering
{
    internal static IReadOnlyList<NumberingPage> ReadPages(string data, int count)
    {
        var pages = new List<NumberingPage>();
        foreach (string block in data.Split("PageMediaBegin").Skip(1))
        {
            var number = Regex.Match(block, @"(?m)^PageMediaNumber:\s*(\d+)\s*$");
            var rotation = Regex.Match(block, @"(?m)^PageMediaRotation:\s*(-?\d+)\s*$");
            var rect = Regex.Match(block, @"(?m)^PageMediaRect:\s*([^\r\n]+)");
            if (!number.Success || int.Parse(number.Groups[1].Value) != pages.Count + 1 || !rotation.Success || !rect.Success)
                throw new InvalidOperationException("Não foi possível identificar o formato das páginas para numerá-las.");
            var values = rect.Groups[1].Value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
                .Select(v => double.Parse(v, CultureInfo.InvariantCulture)).ToArray();
            if (values.Length != 4 || values.Any(v => !double.IsFinite(v))) throw new InvalidOperationException("Dimensões de página inválidas.");
            double width = values[2] - values[0], height = values[3] - values[1];
            int angle = (int.Parse(rotation.Groups[1].Value) % 360 + 360) % 360;
            if (angle is not (0 or 90 or 180 or 270)) throw new InvalidOperationException("A rotação de uma página não é suportada para numeração.");
            if (angle is 90 or 270) (width, height) = (height, width);
            if (width < 80 || height < 80) throw new InvalidOperationException("Uma página é pequena demais para receber a numeração com margem.");
            pages.Add(new(width, height));
        }
        if (pages.Count != count) throw new InvalidOperationException("A leitura dos formatos das páginas ficou incompleta.");
        return pages;
    }

    internal static void Create(string path, IReadOnlyList<NumberingPage> pages, int firstPage, int initialNumber, PageNumberFormat format, PageNumberPosition position, CancellationToken cancellation)
    {
        int finalNumber = checked(initialNumber + pages.Count - firstPage);
        string N(double value) => value.ToString("0.####", CultureInfo.InvariantCulture);
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var encoding = Encoding.GetEncoding(1252);
        using var pdf = new PdfDocumentWriter(path);
        pdf.Object(1, "<< /Type /Catalog /Pages 2 0 R >>");
        pdf.Object(2, $"<< /Type /Pages /Count {pages.Count} /Kids [{string.Join(" ", Enumerable.Range(0, pages.Count).Select(i => $"{4 + i * 2} 0 R"))}] >>");
        pdf.Object(3, "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>");
        for (int i = 0; i < pages.Count; i++)
        {
            cancellation.ThrowIfCancellationRequested();
            var page = pages[i];
            int id = 4 + i * 2;
            string content = "";
            if (i + 1 >= firstPage)
            {
                int number = initialNumber + i + 1 - firstPage;
                string text = format switch
                {
                    PageNumberFormat.Number => number.ToString(CultureInfo.InvariantCulture),
                    PageNumberFormat.PageNumber => $"Página {number}",
                    _ => $"Página {number} de {finalNumber}"
                };
                // Helvetica character widths for the fixed numbering vocabulary, in em units.
                double units = text.Sum(c => c switch { 'P' => .667, 'i' => .222, ' ' => .278, _ => .556 });
                double size = Math.Min(11, (page.Width - 36) / units);
                if (size < 8) throw new InvalidOperationException("A numeração não cabe em uma página. Escolha o formato apenas com número.");
                double width = units * size;
                double x = position switch
                {
                    PageNumberPosition.FooterRight or PageNumberPosition.HeaderRight => page.Width - 18 - width,
                    PageNumberPosition.FooterLeft or PageNumberPosition.HeaderLeft => 18,
                    _ => (page.Width - width) / 2
                };
                double y = position >= PageNumberPosition.HeaderCenter ? page.Height - 30 : 22;
                content = $"q 0.2 g BT /F1 {N(size)} Tf 1 0 0 1 {N(x)} {N(y)} Tm <{Convert.ToHexString(encoding.GetBytes(text))}> Tj ET Q";
            }
            pdf.Object(id, $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {N(page.Width)} {N(page.Height)}] /Resources << /Font << /F1 3 0 R >> >> /Contents {id + 1} 0 R >>");
            pdf.Stream(id + 1, "", Encoding.ASCII.GetBytes(content));
        }
        pdf.Complete(1);
    }
}
