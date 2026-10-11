using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FPDF.Core;

namespace FPDF.App;

public enum ImagePageLayout { AutoA4, PortraitA4, LandscapeA4 }

public static class ImagesToPdf
{
    public static async Task ConvertAsync(IReadOnlyList<string> inputs, string destination, ImagePageLayout layout, PdfTools tools, IProgress<string>? progress = null, CancellationToken cancellation = default)
    {
        if (inputs.Count == 0) throw new ArgumentException("Adicione pelo menos uma imagem.");
        var work = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(destination))!, ".fpdf-imagens-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(work);
        try
        {
            var generated = Path.Combine(work, "imagens.pdf");
            var completion = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            var thread = new Thread(() =>
            {
                try { completion.SetResult(WriteImages(inputs, generated, layout, progress, cancellation)); }
                catch (OperationCanceledException) { completion.SetCanceled(cancellation); }
                catch (Exception error) { completion.SetException(error); }
            }) { IsBackground = true, Name = "FPDF imagens" };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            int pages = await completion.Task;
            await tools.WriteSingleAsync(destination, pages, output =>
            {
                cancellation.ThrowIfCancellationRequested();
                File.Copy(generated, output, overwrite: false);
                return Task.CompletedTask;
            }, cancellation);
        }
        finally
        {
            try { if (Directory.Exists(work)) Directory.Delete(work, recursive: true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    private static int WriteImages(IReadOnlyList<string> inputs, string destination, ImagePageLayout layout, IProgress<string>? progress, CancellationToken cancellation)
    {
        using var pdf = new PdfDocumentWriter(destination);
        pdf.Object(1, "<< /Type /Catalog /Pages 2 0 R >>");
        var pageIds = new List<int>();
        foreach (var input in inputs)
        {
            cancellation.ThrowIfCancellationRequested();
            progress?.Report("Convertendo imagem: " + Path.GetFileName(input));
            using var source = File.OpenRead(input);
            var decoder = BitmapDecoder.Create(source, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnDemand);
            foreach (var frame in decoder.Frames)
            {
                cancellation.ThrowIfCancellationRequested();
                int w = frame.PixelWidth, h = frame.PixelHeight;
                if ((long)w * h > 60_000_000) throw new ArgumentException("A imagem excede 60 milhões de pixels. Reduza suas dimensões antes de converter.");
                int orientation = GetOrientation(frame.Metadata as BitmapMetadata);
                int width = orientation >= 5 ? h : w, height = orientation >= 5 ? w : h;
                var visual = new DrawingVisual();
                using (var drawing = visual.RenderOpen())
                {
                    drawing.DrawRectangle(Brushes.White, null, new Rect(0, 0, width, height));
                    Matrix matrix = orientation switch
                    {
                        2 => new(-1, 0, 0, 1, w, 0), 3 => new(-1, 0, 0, -1, w, h),
                        4 => new(1, 0, 0, -1, 0, h), 5 => new(0, 1, 1, 0, 0, 0),
                        6 => new(0, 1, -1, 0, h, 0), 7 => new(0, -1, -1, 0, h, w),
                        8 => new(0, -1, 1, 0, 0, w), _ => Matrix.Identity
                    };
                    drawing.PushTransform(new MatrixTransform(matrix));
                    drawing.DrawImage(frame, new Rect(0, 0, w, h));
                    drawing.Pop();
                }
                var rendered = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
                rendered.Render(visual);
                var rgb = new FormatConvertedBitmap(rendered, PixelFormats.Bgr24, null, 0);
                var encoder = new JpegBitmapEncoder { QualityLevel = 95 };
                encoder.Frames.Add(BitmapFrame.Create(rgb));
                using var jpeg = new MemoryStream();
                encoder.Save(jpeg);
                bool landscape = layout == ImagePageLayout.LandscapeA4 || (layout == ImagePageLayout.AutoA4 && width > height);
                double pageWidth = landscape ? 842 : 595, pageHeight = landscape ? 595 : 842;
                const double margin = 24;
                double scale = Math.Min((pageWidth - margin * 2) / width, (pageHeight - margin * 2) / height);
                double drawWidth = width * scale, drawHeight = height * scale;
                string N(double value) => value.ToString("0.####", CultureInfo.InvariantCulture);
                int id = 3 + pageIds.Count * 3;
                pageIds.Add(id);
                pdf.Object(id, $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {N(pageWidth)} {N(pageHeight)}] /Resources << /XObject << /Im0 {id + 1} 0 R >> >> /Contents {id + 2} 0 R >>");
                pdf.Stream(id + 1, $"/Type /XObject /Subtype /Image /Width {width} /Height {height} /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /DCTDecode", jpeg.GetBuffer().AsSpan(0, checked((int)jpeg.Length)));
                string content = $"q {N(drawWidth)} 0 0 {N(drawHeight)} {N((pageWidth - drawWidth) / 2)} {N((pageHeight - drawHeight) / 2)} cm /Im0 Do Q";
                pdf.Stream(id + 2, "", Encoding.ASCII.GetBytes(content));
            }
        }
        cancellation.ThrowIfCancellationRequested();
        pdf.Object(2, $"<< /Type /Pages /Count {pageIds.Count} /Kids [{string.Join(" ", pageIds.Select(id => $"{id} 0 R"))}] >>");
        pdf.Complete(1);
        return pageIds.Count;
    }
    private static int GetOrientation(BitmapMetadata? metadata)
    {
        if (metadata is null) return 1;
        foreach (var query in new[] { "/app1/ifd/{ushort=274}", "/ifd/{ushort=274}" })
        {
            try { if (metadata.GetQuery(query) is ushort value && value is >= 1 and <= 8) return value; }
            catch (NotSupportedException) { }
            catch (ArgumentException) { }
        }
        return 1;
    }
}
