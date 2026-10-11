using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using FPDF.Core;

try { await RunTests(); }
catch (Exception error) { Console.Error.WriteLine(error); Environment.ExitCode = 1; }

static async Task RunTests()
{
var portableRoot = Environment.GetEnvironmentVariable("FPDF_TEST_TOOL_ROOT");
var engine = portableRoot is null ? PdfTools.FindExecutable() ?? throw new Exception("PDFtk não encontrado.") : Path.Combine(portableRoot, "tools", "pdftk.exe");
var tools = new PdfTools(engine);
var root = Path.GetFullPath(Path.Combine("artifacts", "smoke", Guid.NewGuid().ToString("N")));
Directory.CreateDirectory(root);
var first = Path.Combine(root, "documento ação.pdf");
var second = Path.Combine(root, "documento B.pdf");
WritePdf(first, ["A1", "A2", "A3", "A4", "A5", "A6"]);
WritePdf(second, ["B1", "B2"]);
var firstHash = SHA256.HashData(File.ReadAllBytes(first));
var secondHash = SHA256.HashData(File.ReadAllBytes(second));
Assert(await tools.GetPageCountAsync(first) == 6, "PDF com nome português e espaço é reconhecido");
var merged = Path.Combine(root, "unido.pdf");
await tools.MergeAsync([second, first], merged);
Assert(await tools.GetPageCountAsync(merged) == 8, "Junção preserva todas as páginas");
string[] markers = ["B1", "B2", "A1", "A2", "A3", "A4", "A5", "A6"];
// Extract each resulting page to verify content order, not just the total.
for (int index = 0; index < markers.Length; index++)
{
    var start = new System.Diagnostics.ProcessStartInfo(engine) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true };
    var page = Path.Combine(root, $"page-{index + 1}.pdf");
    foreach (var arg in new[] { merged, "cat", (index + 1).ToString(), "output", page, "uncompress" }) start.ArgumentList.Add(arg);
    using var process = System.Diagnostics.Process.Start(start)!;
    var error = process.StandardError.ReadToEndAsync();
    await process.WaitForExitAsync();
    if (process.ExitCode != 0) throw new Exception(await error);
    Assert(Encoding.ASCII.GetString(File.ReadAllBytes(page)).Contains($"(PAGE-{markers[index]})"), $"Conteúdo e ordem da página {index + 1}");
}
await ExpectFailure<IOException>(() => tools.MergeAsync([first, second], merged), "Arquivo existente não é sobrescrito");
var limit = new FileInfo(Path.Combine(root, "page-1.pdf")).Length * 2 + 500;
var result = await tools.SplitBySizeAsync(merged, root, limit);
Assert(result.Parts.Count > 1 && result.Parts.Sum(p => p.LastPage - p.FirstPage + 1) == 8, "Divisão gera várias partes sem perder páginas");
int next = 1;
foreach (var part in result.Parts)
{
    Assert(part.FirstPage == next, "Partes têm sequência contínua");
    Assert(new FileInfo(Path.Combine(result.Directory, part.Name)).Length <= limit, "Tamanho real de cada parte respeita o limite");
    next = part.LastPage + 1;
}
var generous = await tools.SplitBySizeAsync(first, root, 50_000_000);
Assert(generous.Parts.Count == 1 && generous.Parts[0].LastPage == 6, "PDF abaixo de 50 MB gera uma parte completa");
var exact = await tools.SplitBySizeAsync(first, root, generous.Parts[0].Bytes);
Assert(exact.Parts.Count == 1 && exact.Parts[0].Bytes == generous.Parts[0].Bytes, "Limite exato em bytes aceita arquivo com tamanho igual ao máximo");
var before = Directory.GetDirectories(root).Length;
await ExpectFailure<InvalidOperationException>(() => tools.SplitBySizeAsync(first, root, 10), "Página que excede o limite é rejeitada");
Assert(Directory.GetDirectories(root).Length == before, "Falha não publica partes nem deixa pastas temporárias");
var corrupt = Path.Combine(root, "invalido.pdf");
await File.WriteAllTextAsync(corrupt, "This is not a PDF");
await ExpectFailure<InvalidOperationException>(() => tools.GetPageCountAsync(corrupt), "PDF inválido apresenta erro");
using var cancellation = new CancellationTokenSource();
cancellation.Cancel();
var canceled = Path.Combine(root, "cancelado.pdf");
await ExpectFailure<OperationCanceledException>(() => tools.MergeAsync([first, second], canceled, cancellation: cancellation.Token), "Cancelamento interrompe a operação");
Assert(!File.Exists(canceled) && !Directory.GetDirectories(root, ".fpdf-*").Any(), "Cancelamento não salva resultados parciais");
using var midway = new CancellationTokenSource();
await ExpectFailure<OperationCanceledException>(() => tools.SplitBySizeAsync(first, root, limit, new CancelProgress(midway), midway.Token), "Cancelamento após iniciar divisão interrompe a operação");
Assert(!Directory.GetDirectories(root, ".fpdf-*").Any(), "Cancelamento de divisão remove arquivos temporários");
var extracted = Path.Combine(root, "extraido.pdf");
await tools.ExtractAsync(first, extracted, "5, 1-2, fim");
Assert(await tools.GetPageCountAsync(extracted) == 4, "Extração aceita páginas e intervalos com fim");
foreach (var item in new[] { (Page: 1, Marker: "A5"), (Page: 2, Marker: "A1"), (Page: 3, Marker: "A2"), (Page: 4, Marker: "A6") })
{
    var path = Path.Combine(root, $"extracted-check-{item.Page}.pdf");
    await Run(engine, [extracted, "cat", item.Page.ToString(), "output", path, "uncompress"]);
    Assert(Encoding.ASCII.GetString(File.ReadAllBytes(path)).Contains($"(PAGE-{item.Marker})"), "Extração respeita a ordem informada");
}
await ExpectFailure<ArgumentException>(() => tools.ExtractAsync(first, Path.Combine(root, "fora.pdf"), "7"), "Página inexistente é rejeitada");
foreach (string invalid in new[] { "0", "3-1", "1,,2", "1,", "1north", "999999999999999999" })
    await ExpectFailure<ArgumentException>(() => tools.ExtractAsync(first, Path.Combine(root, "invalid-range.pdf"), invalid), "Intervalo inválido rejeitado: " + invalid);
var ranged = await tools.SplitByRangesAsync(first, root, "1-2, 4, 5-fim");
Assert(ranged.Parts.Count == 3 && ranged.Parts.Select(p => p.LastPage - p.FirstPage + 1).SequenceEqual([2, 1, 2]), "Divisão por intervalos cria as partes escolhidas");
var rotated = Path.Combine(root, "girado.pdf");
await tools.RotateAsync(first, rotated, "2-3, 3", 90);
var rotationData = await Run(engine, [rotated, "dump_data"]);
Assert(Rotations(rotationData).SequenceEqual([0, 90, 90, 0, 0, 0]), "Rotação altera apenas páginas selecionadas e elimina sobreposições");
var twice = Path.Combine(root, "girado-duas-vezes.pdf");
await tools.RotateAsync(rotated, twice, "2", 90);
Assert(Rotations(await Run(engine, [twice, "dump_data"])).SequenceEqual([0, 180, 90, 0, 0, 0]), "Rotação é relativa à orientação existente");
var left = Path.Combine(root, "girado-esquerda.pdf");
await tools.RotateAsync(first, left, "", -90);
Assert(Rotations(await Run(engine, [left, "dump_data"])).All(r => r == 270), "Campo vazio gira todas as páginas para a esquerda");
var upsideDown = Path.Combine(root, "invertido.pdf");
await tools.RotateAsync(first, upsideDown, "", 180);
Assert(Rotations(await Run(engine, [upsideDown, "dump_data"])).All(r => r == 180), "Rotação de 180 graus");
var mixed = Path.Combine(root, "giros-diferentes.pdf");
await tools.RotateMixedAsync(first, mixed, "1:E90, 2:180, 3-4:D90");
Assert(Rotations(await Run(engine, [mixed, "dump_data"])).SequenceEqual([270, 180, 90, 90, 0, 0]), "Giros diferentes por página e intervalo preservam páginas não citadas");
var semicolons = Path.Combine(root, "giros-ponto-virgula.pdf");
await tools.RotateMixedAsync(first, semicolons, "1;E;90, 2;180, 3;D;90");
Assert(Rotations(await Run(engine, [semicolons, "dump_data"])).SequenceEqual([270, 180, 90, 0, 0, 0]), "Formato solicitado com ponto e vírgula é aceito");
await ExpectFailure<ArgumentException>(() => tools.RotateMixedAsync(first, Path.Combine(root, "sobreposto.pdf"), "1-3:D90, 3:E90"), "Giros sobrepostos são rejeitados sem ambiguidade");
await ExpectFailure<ArgumentException>(() => tools.RotateMixedAsync(first, Path.Combine(root, "angulo-invalido.pdf"), "1:45"), "Giro inválido é rejeitado");
var optimized = Path.Combine(root, "otimizado.pdf");
var optimizedResult = await tools.OptimizeAsync(first, optimized);
Assert(optimizedResult.OutputBytes < optimizedResult.OriginalBytes && await tools.GetPageCountAsync(optimized) == 6, "Otimização sem perda reduz streams descomprimidos");
var ghostscript = portableRoot is null ? PdfCompression.FindExecutable() ?? throw new Exception("Ghostscript necessário para validar compressão.") : Path.Combine(portableRoot, "tools", "ghostscript", "bin", "gswin64c.exe");
var imagePdf = Path.Combine(root, "digitalizacao-sintetica.pdf");
WriteImagePdf(imagePdf);
var imageHash = SHA256.HashData(File.ReadAllBytes(imagePdf));
var compression = new PdfCompression(ghostscript, tools);
foreach (var quality in Enum.GetValues<CompressionQuality>())
{
    var output = Path.Combine(root, $"comprimido-{quality}.pdf");
    var sizes = await compression.CompressAsync(imagePdf, output, quality);
    Assert(sizes.OutputBytes < sizes.OriginalBytes && await tools.GetPageCountAsync(output) == 1, $"Compressão {quality} reduz PDF com imagem mantendo as páginas");
    var textPath = Path.Combine(root, $"texto-{quality}.txt");
    await Run(ghostscript, ["-dSAFER", "-dBATCH", "-dNOPAUSE", "-sDEVICE=txtwrite", "-sOutputFile=" + textPath, "-f", output]);
    Assert(File.ReadAllText(textPath).Contains("SCAN-TEXT"), "Compressão preserva texto selecionável");
}
Assert(imageHash.SequenceEqual(SHA256.HashData(File.ReadAllBytes(imagePdf))), "Compressão preserva o original");
var lowDpi = Path.Combine(root, "jpeg-baixa-resolucao.pdf");
await Run(ghostscript, ["-dSAFER", "-dBATCH", "-dNOPAUSE", "-sDEVICE=pdfwrite", "-dDownsampleColorImages=true", "-dColorImageResolution=72", "-dColorImageDownsampleThreshold=1.0", "-dAutoFilterColorImages=false", "-dColorImageFilter=/DCTEncode", "-sOutputFile=" + lowDpi, "-c", "<< /ColorImageDict << /QFactor 0.05 >> >> setdistillerparams", "-f", imagePdf]);
Assert(Encoding.ASCII.GetString(File.ReadAllBytes(lowDpi)).Contains("/DCTDecode"), "Fixture contém imagem JPEG abaixo do limite de resolução");
var lowDpiOutput = Path.Combine(root, "jpeg-recomprimido.pdf");
var lowDpiResult = await compression.CompressAsync(lowDpi, lowDpiOutput, CompressionQuality.Small);
Assert(lowDpiResult.OutputBytes < lowDpiResult.OriginalBytes, "Imagem JPEG de baixa resolução também pode ser recomprimida");
var tiny = Path.Combine(root, "pequeno.pdf");
WritePdf(tiny, ["TINY"], padding: false);
var noGain = Path.Combine(root, "sem-ganho.pdf");
var noGainResult = await compression.CompressAsync(tiny, noGain, CompressionQuality.Balanced);
Assert(noGainResult.OutputBytes <= noGainResult.OriginalBytes, "Compressão nunca salva arquivo maior que o original");
if (noGainResult.OutputBytes == noGainResult.OriginalBytes)
{
    Assert(File.ReadAllBytes(tiny).SequenceEqual(File.ReadAllBytes(noGain)), "Sem redução, o resultado é cópia exata do original");
    Assert(noGainResult.ProcessedBytes >= noGainResult.OriginalBytes, "Resultado registra o tamanho da tentativa para explicar ausência de redução");
}
var marked = Path.Combine(root, "marca-dagua.pdf");
await tools.AddWatermarkAsync(first, marked, "CÓPIA - MINUTA", 25, false);
Assert(await tools.GetPageCountAsync(marked) == 6, "Marca-d'água preserva todas as páginas");
var markedText = Path.Combine(root, "marca-texto.txt");
await Run(ghostscript, ["-dSAFER", "-dBATCH", "-dNOPAUSE", "-sDEVICE=txtwrite", "-sOutputFile=" + markedText, "-f", marked]);
var watermarkContents = File.ReadAllText(markedText);
Assert(Regex.Matches(watermarkContents, "CÓPIA - MINUTA").Count == 6 && watermarkContents.Contains("PAGE-A6"), "Marca com acentos aparece em todas as páginas sem remover o texto original");
var markedRotated = Path.Combine(root, "marca-em-paginas-giradas.pdf");
await tools.AddWatermarkAsync(mixed, markedRotated, "MINUTA", 15, true);
Assert(Rotations(await Run(engine, [markedRotated, "dump_data"])).SequenceEqual([270, 180, 90, 90, 0, 0]), "Marca-d'água preserva a rotação existente");
await ExpectFailure<ArgumentException>(() => tools.AddWatermarkAsync(first, Path.Combine(root, "marca-invalida.pdf"), "😀", 25, true), "Símbolo não suportado na marca apresenta erro claro");
var numbered = Path.Combine(root, "numerado.pdf");
await tools.AddPageNumbersAsync(first, numbered, firstPage: 2, initialNumber: 10);
var numberedText = Path.Combine(root, "numerado.txt");
await Run(ghostscript, ["-dSAFER", "-dBATCH", "-dNOPAUSE", "-sDEVICE=txtwrite", "-sOutputFile=" + numberedText, "-f", numbered]);
string numberingContents = File.ReadAllText(numberedText);
Assert(await tools.GetPageCountAsync(numbered) == 6 && Regex.Matches(numberingContents, "Página").Count == 5, "Numeração preserva páginas e permite deixar a capa sem número");
Assert(Enumerable.Range(10, 5).All(n => numberingContents.Contains($"Página {n} de 14")) && Enumerable.Range(1, 6).All(n => numberingContents.Contains($"PAGE-A{n}")), "Numeração começa no valor escolhido, informa o número final e preserva o conteúdo");
foreach (var format in new[] { PageNumberFormat.Number, PageNumberFormat.PageNumber })
{
    var output = Path.Combine(root, $"numerado-{format}.pdf");
    await tools.AddPageNumbersAsync(first, output, initialNumber: 20, format: format);
    var text = Path.Combine(root, $"numerado-{format}.txt");
    await Run(ghostscript, ["-dSAFER", "-dBATCH", "-dNOPAUSE", "-sDEVICE=txtwrite", "-sOutputFile=" + text, "-f", output]);
    Assert(File.ReadAllText(text).Contains(format == PageNumberFormat.Number ? "25" : "Página 25"), "Formato de numeração: " + format);
}
var numberedRotated = Path.Combine(root, "numerado-girado.pdf");
await tools.AddPageNumbersAsync(mixed, numberedRotated);
Assert(Rotations(await Run(engine, [numberedRotated, "dump_data"])).SequenceEqual([270, 180, 90, 90, 0, 0]), "Numeração mantém as orientações originais");
await ExpectFailure<ArgumentException>(() => tools.AddPageNumbersAsync(first, Path.Combine(root, "numero-invalido.pdf"), firstPage: 7), "Primeira página inexistente é rejeitada");
await ExpectFailure<ArgumentException>(() => tools.AddPageNumbersAsync(first, Path.Combine(root, "numero-zero.pdf"), initialNumber: 0), "Número inicial zero é rejeitado");
await ExpectFailure<ArgumentException>(() => tools.AddPageNumbersAsync(first, Path.Combine(root, "numero-excedido.pdf"), initialNumber: 1_000_000), "Número final excedido é rejeitado");
await ExpectFailure<IOException>(() => tools.AddPageNumbersAsync(first, numbered), "Numeração não sobrescreve resultados existentes");
await ExpectFailure<OperationCanceledException>(() => tools.AddPageNumbersAsync(first, Path.Combine(root, "numero-cancelado.pdf"), cancellation: cancellation.Token), "Numeração aceita cancelamento sem publicar resultado");
Assert(!Directory.GetDirectories(root, ".fpdf-*").Any(), "Numeração não deixa temporários");
var organized = Path.Combine(root, "organizado.pdf");
await tools.ReorderPagesAsync(first, organized, [6, 1, 4]);
Assert(await tools.GetPageCountAsync(organized) == 3, "Organização visual pode excluir páginas sem perder as mantidas");
foreach (var item in new[] { (Page: 1, Marker: "A6"), (Page: 2, Marker: "A1"), (Page: 3, Marker: "A4") })
{
    var path = Path.Combine(root, $"organized-check-{item.Page}.pdf");
    await Run(engine, [organized, "cat", item.Page.ToString(), "output", path, "uncompress"]);
    Assert(Encoding.ASCII.GetString(File.ReadAllBytes(path)).Contains($"(PAGE-{item.Marker})"), "Conteúdo corresponde à nova ordem visual");
}
await ExpectFailure<ArgumentException>(() => tools.ReorderPagesAsync(first, Path.Combine(root, "sem-paginas.pdf"), []), "Exclusão de todas as páginas é rejeitada");
await ExpectFailure<ArgumentException>(() => tools.ReorderPagesAsync(first, Path.Combine(root, "pagina-repetida.pdf"), [1, 1]), "Organização rejeita duplicação acidental");
await ExpectFailure<IOException>(() => tools.ReorderPagesAsync(first, organized, [1]), "Organização preserva resultados existentes");
await ExpectFailure<OperationCanceledException>(() => tools.ReorderPagesAsync(first, Path.Combine(root, "organizado-cancelado.pdf"), [1], cancellation: cancellation.Token), "Organização permite cancelamento");
Assert((await tools.InspectAsync(first)).Signatures == SignatureDetection.NotDetected, "Inspeção não acusa campos inexistentes");
foreach (bool filled in new[] { false, true })
{
    var signatureFixture = Path.Combine(root, filled ? "campo-preenchido.pdf" : "campo-vazio.pdf");
    using (var pdf = new PdfDocumentWriter(signatureFixture))
    {
        pdf.Object(1, "<< /Type /Catalog /Pages 2 0 R /AcroForm << /Fields [5 0 R] /SigFlags 3 >> >>");
        pdf.Object(2, "<< /Type /Pages /Count 1 /Kids [3 0 R] >>");
        pdf.Object(3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << >> /Annots [5 0 R] /Contents 4 0 R >>");
        pdf.Stream(4, "", []);
        pdf.Object(5, "<< /Type /Annot /Subtype /Widget /FT /Sig /T (AssinaturaTeste) /Rect [40 40 200 80] /P 3 0 R " + (filled ? "/V 6 0 R " : "") + ">>");
        // Structural fixture only: not a cryptographically valid signature.
        pdf.Object(6, "<< /Type /Sig /Filter /Adobe.PPKLite /SubFilter /adbe.pkcs7.detached /ByteRange [0 10 20 30] /Contents <0000> >>");
        pdf.Complete(1);
    }
    var inspected = await tools.InspectAsync(signatureFixture);
    Assert(inspected.Signatures == SignatureDetection.SignatureFields && inspected.SignatureFields == 1, "Inspeção identifica campo de assinatura " + (filled ? "preenchido" : "vazio") + " sem declarar validade criptográfica");
}
var thumbnail = Path.Combine(root, "miniatura.png");
await PdfPreview.RenderPageAsync(ghostscript, mixed, 1, thumbnail);
Assert(File.Exists(thumbnail) && new FileInfo(thumbnail).Length > 100, "Pré-visualização de página girada gera uma imagem local");
await ExpectFailure<OperationCanceledException>(() => PdfPreview.RenderPageAsync(ghostscript, first, 1, Path.Combine(root, "preview-cancelado.png"), cancellation: cancellation.Token), "Pré-visualização permite cancelamento");
var combined = await compression.CompressAndSplitAsync(merged, root, 50_000, CompressionQuality.Balanced);
Assert(combined.Parts.Sum(p => p.LastPage - p.FirstPage + 1) == 8 && combined.Parts.All(p => new FileInfo(Path.Combine(combined.Directory, p.Name)).Length <= 50_000), "Compressão seguida de divisão preserva páginas e respeita o limite");
Assert(File.ReadAllText(Path.Combine(combined.Directory, "resumo.txt")).Contains("Compressão:"), "Operação combinada registra os tamanhos no resumo");
var beforeCombinedFailure = Directory.GetDirectories(root).Length;
await ExpectFailure<InvalidOperationException>(() => compression.CompressAndSplitAsync(merged, root, 10, CompressionQuality.Small), "Operação combinada rejeita página que excede o limite mesmo após compressão");
Assert(Directory.GetDirectories(root).Length == beforeCombinedFailure && !Directory.GetDirectories(root, ".fpdf-*").Any(), "Falha da operação combinada não publica resultados parciais");
Assert(firstHash.SequenceEqual(SHA256.HashData(File.ReadAllBytes(first))) && secondHash.SequenceEqual(SHA256.HashData(File.ReadAllBytes(second))), "Originais permanecem idênticos");
Console.WriteLine($"PASS: todos os testes. Arquivos de teste: {root}");
}

static void Assert(bool condition, string name)
{
    if (!condition) throw new Exception("FAIL: " + name);
    Console.WriteLine("PASS: " + name);
}
static async Task ExpectFailure<T>(Func<Task> action, string name) where T : Exception
{
    try { await action(); }
    catch (T) { Console.WriteLine("PASS: " + name); return; }
    throw new Exception("FAIL: " + name);
}
static int[] Rotations(string data) => Regex.Matches(data, @"PageMediaRotation:\s*(-?\d+)").Select(m => (int.Parse(m.Groups[1].Value) % 360 + 360) % 360).ToArray();
static async Task<string> Run(string executable, string[] arguments)
{
    var info = new System.Diagnostics.ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
    foreach (var arg in arguments) info.ArgumentList.Add(arg);
    using var process = System.Diagnostics.Process.Start(info)!;
    var output = process.StandardOutput.ReadToEndAsync();
    var error = process.StandardError.ReadToEndAsync();
    await process.WaitForExitAsync();
    if (process.ExitCode != 0) throw new Exception(await error);
    return await output;
}
static void WritePdf(string path, string[] markers, bool padding = true)
{
    // Minimal, valid, text-based PDF fixture with deterministic uncompressed streams.
    var objects = new List<string> { "<< /Type /Catalog /Pages 2 0 R >>", "" };
    var kids = new List<int>();
    var random = new Random(123);
    foreach (var marker in markers)
    {
        var pageId = objects.Count + 1;
        kids.Add(pageId);
        objects.Add($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 << /Type /Font /Subtype /Type1 /BaseFont /Helvetica >> >> >> /Contents {pageId + 1} 0 R >>");
        var content = new StringBuilder($"BT /F1 12 Tf 72 760 Td (PAGE-{marker}) Tj ET\n");
        for (int line = 0; line < (padding ? 180 : 0); line++)
        {
            content.Append('%');
            for (int c = 0; c < 80; c++) content.Append((char)('A' + random.Next(26)));
            content.Append('\n');
        }
        var stream = content.ToString();
        objects.Add($"<< /Length {Encoding.ASCII.GetByteCount(stream)} >>\nstream\n{stream}endstream");
    }
    objects[1] = $"<< /Type /Pages /Count {markers.Length} /Kids [{string.Join(" ", kids.Select(id => $"{id} 0 R"))}] >>";
    var pdf = new StringBuilder("%PDF-1.4\n");
    var offsets = new List<int>();
    for (int i = 0; i < objects.Count; i++)
    {
        offsets.Add(Encoding.ASCII.GetByteCount(pdf.ToString()));
        pdf.Append($"{i + 1} 0 obj\n{objects[i]}\nendobj\n");
    }
    int xref = Encoding.ASCII.GetByteCount(pdf.ToString());
    pdf.Append($"xref\n0 {objects.Count + 1}\n0000000000 65535 f \n");
    foreach (int offset in offsets) pdf.Append($"{offset:0000000000} 00000 n \n");
    pdf.Append($"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
    File.WriteAllBytes(path, Encoding.ASCII.GetBytes(pdf.ToString()));
}

static void WriteImagePdf(string path)
{
    const int width = 1200, height = 1600;
    var pixels = new byte[width * height * 3];
    var random = new Random(42);
    for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            bool ink = x > 80 && x < 1000 && y > 160 && y < 1300 && y % 70 < 12;
            byte value = (byte)(ink ? random.Next(15, 50) : random.Next(220, 256));
            int index = (y * width + x) * 3;
            pixels[index] = pixels[index + 1] = pixels[index + 2] = value;
        }
    using var imageData = new MemoryStream();
    using (var deflate = new System.IO.Compression.ZLibStream(imageData, System.IO.Compression.CompressionLevel.Optimal, leaveOpen: true)) deflate.Write(pixels);
    var compressed = imageData.ToArray();
    using var pdf = new MemoryStream();
    void Write(string text) => pdf.Write(Encoding.ASCII.GetBytes(text));
    var offsets = new List<long>();
    void Begin(int id) { offsets.Add(pdf.Position); Write($"{id} 0 obj\n"); }
    Write("%PDF-1.4\n");
    Begin(1); Write("<< /Type /Catalog /Pages 2 0 R >>\nendobj\n");
    Begin(2); Write("<< /Type /Pages /Count 1 /Kids [3 0 R] >>\nendobj\n");
    Begin(3); Write("<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /XObject << /Im0 5 0 R >> /Font << /F1 << /Type /Font /Subtype /Type1 /BaseFont /Helvetica >> >> >> /Contents 4 0 R >>\nendobj\n");
    var content = "BT /F1 12 Tf 72 760 Td (SCAN-TEXT) Tj ET\nq 240 0 0 320 72 350 cm /Im0 Do Q\n";
    Begin(4); Write($"<< /Length {content.Length} >>\nstream\n{content}endstream\nendobj\n");
    Begin(5); Write($"<< /Type /XObject /Subtype /Image /Width {width} /Height {height} /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /FlateDecode /Length {compressed.Length} >>\nstream\n");
    pdf.Write(compressed); Write("\nendstream\nendobj\n");
    var xref = pdf.Position;
    Write("xref\n0 6\n0000000000 65535 f \n");
    foreach (var offset in offsets) Write($"{offset:0000000000} 00000 n \n");
    Write($"trailer\n<< /Size 6 /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
    File.WriteAllBytes(path, pdf.ToArray());
}

sealed class CancelProgress(CancellationTokenSource cancellation) : IProgress<string>
{
    public void Report(string value) => cancellation.Cancel();
}
