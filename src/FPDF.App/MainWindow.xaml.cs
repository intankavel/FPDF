using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using FPDF.Core;
using Microsoft.Win32;

namespace FPDF.App;

public record PdfItem(string Path)
{
    public string Name => System.IO.Path.GetFileName(Path);
    public string Size => $"{new FileInfo(Path).Length / 1_000_000m:0.##} MB";
}

public partial class MainWindow : Window
{
    private readonly ObservableCollection<PdfItem> files = [];
    private string? executable = PdfTools.FindExecutable();
    private string? ghostscript = PdfCompression.FindExecutable();
    private CancellationTokenSource? active;
    private string? outputFolder;
    private static readonly string[] ImageExtensions = [".jpg", ".jpeg", ".png", ".bmp", ".tif", ".tiff"];

    public MainWindow()
    {
        InitializeComponent();
        // Let WPF size the initial height from the content, while keeping it inside the work area.
        var area = SystemParameters.WorkArea;
        MaxHeight = area.Height;
        MaxWidth = area.Width;
        MinHeight = Math.Min(MinHeight, MaxHeight);
        MinWidth = Math.Min(MinWidth, MaxWidth);
        Width = Math.Min(Width, MaxWidth);
        FileList.ItemsSource = files;
        RefreshOperation();
    }
    private void OperationChanged(object sender, SelectionChangedEventArgs e)
    {
        RefreshOperation();
    }
    private void RotationChanged(object sender, SelectionChangedEventArgs e) => RefreshOperation();
    private void RefreshOperation()
    {
        if (LimitPanel is null || SelectionPanel is null || QualityPanel is null || RotationPanel is null || Rotation is null || Hint is null || RunButton is null || EngineStatus is null || UseCase is null || Illustration is null || ImageLayoutPanel is null || WatermarkTextPanel is null || ExtraOptions is null || FilesLabel is null || NumberOptionsPanel is null) return;
        int operation = Operation.SelectedIndex;
        bool mixed = operation == 4 && Rotation.SelectedIndex == 3;
        UseCase.Text = operation switch
        {
            0 => "Use se você quer reunir vários documentos em um único PDF.",
            1 => "Use se você precisa respeitar o limite de tamanho do Lepisma ou de um edital.",
            2 => "Use se você quer selecionar páginas e reunir todas em UM novo PDF.",
            3 => "Use se você quer criar VÁRIOS PDFs, um para cada intervalo de páginas.",
            4 => mixed ? "Use giros por página para corrigir orientações diferentes: E90 = esquerda, D90 = direita, 180 = inverter." : "Use se você quer corrigir a orientação de uma ou mais páginas.",
            5 => "Use se você quer tentar reduzir o tamanho mantendo a resolução das imagens.",
            6 => "Use se você aceita reduzir detalhes das imagens para tentar obter um arquivo menor.",
            7 => "Use se você quer comprimir e preparar as partes para o Lepisma em uma única operação.",
            8 => "Use se você quer reunir fotos ou digitalizações em um PDF, na ordem da lista.",
            9 => "Use se você quer identificar todas as páginas com um texto como MINUTA ou CÓPIA.",
            10 => "Use se você quer acrescentar uma sequência de números às páginas do documento.",
            _ => "Use se você quer conferir o PDF, excluir páginas ou mudar sua ordem visualmente."
        };
        Illustration.Visibility = Visibility.Visible;
        Illustration.Operation = operation;
        LimitPanel.Visibility = operation is 1 or 7 ? Visibility.Visible : Visibility.Collapsed;
        SelectionPanel.Visibility = operation is 2 or 3 or 4 ? Visibility.Visible : Visibility.Collapsed;
        RotationPanel.Visibility = operation == 4 ? Visibility.Visible : Visibility.Collapsed;
        QualityPanel.Visibility = operation == 6 ? Visibility.Visible : Visibility.Collapsed;
        ImageLayoutPanel.Visibility = operation == 8 ? Visibility.Visible : Visibility.Collapsed;
        WatermarkTextPanel.Visibility = operation == 9 ? Visibility.Visible : Visibility.Collapsed;
        ExtraOptions.Visibility = operation is 7 or 9 or 10 ? Visibility.Visible : Visibility.Collapsed;
        NumberFormatPanel.Visibility = NumberOptionsPanel.Visibility = operation == 10 ? Visibility.Visible : Visibility.Collapsed;
        CombinedQualityPanel.Visibility = operation == 7 ? Visibility.Visible : Visibility.Collapsed;
        WatermarkOptionsPanel.Visibility = operation == 9 ? Visibility.Visible : Visibility.Collapsed;
        FilesLabel.Text = operation == 8 ? "Imagens — arraste os arquivos para esta janela" : "Arquivos — arraste PDFs para esta janela";
        AddButton.Content = operation == 8 ? "Adicionar imagens" : "Adicionar PDFs";
        SelectionLabel.Text = mixed ? "Giros (ex.: 1:E90, 2:180)" : operation == 4 ? "Páginas (vazio = todas)" : "Páginas (ex.: 1-3, 5, 8-fim)";
        RunButton.Content = operation switch { 0 => "Juntar PDFs…", 1 or 3 => "Dividir PDF…", 2 => "Extrair páginas…", 4 => "Girar páginas…", 5 => "Otimizar PDF…", 6 => "Comprimir PDF…", 7 => "Comprimir e dividir…", 8 => "Criar PDF…", 10 => "Numerar páginas…", 11 => "Abrir páginas…", _ => "Aplicar marca…" };
        Hint.Text = operation switch
        {
            0 => "Os PDFs serão unidos na ordem da lista. Adicione pelo menos dois arquivos.",
            1 => "Selecione um único PDF. Cada parte terá até o limite escolhido, mantendo a ordem das páginas. 1 MB = 1.000.000 bytes. Esta operação não comprime as imagens.",
            2 => "Selecione um único PDF. As páginas informadas serão salvas em um novo arquivo, na ordem digitada. Exemplo: 5, 1-3, 8-fim.",
            3 => "Selecione um único PDF. Cada intervalo separado por vírgula gera um arquivo. Exemplo: 1-3, 4-6, 7-fim. Páginas não incluídas nos intervalos não serão copiadas.",
            4 => mixed ? "E90 = 90° à esquerda; D90 = 90° à direita; 180 = inverter. Exemplo: 1:E90, 2:180, 3-5:D90. Também aceita 1;E;90, 2;180. As páginas não citadas ficam como estão."
                : "Selecione um único PDF e o sentido da rotação. Campo vazio gira todas. Escolha 'Giros por página' para aplicar orientações diferentes.",
            5 => "Selecione um único PDF. Comprime os dados internos sem reduzir a resolução das imagens. A redução depende do documento; se não houver ganho, será salva uma cópia do original.",
            6 => "Recomprime imagens e reduz sua resolução quando necessário. Documentos com pouco conteúdo de imagem ou já compactados podem não diminuir. Se o resultado ficar maior, salvamos o original. Confira a legibilidade e os formulários.",
            7 => "Selecione um PDF, a qualidade e o limite por parte. Primeiro comprimimos, depois dividimos mantendo a ordem. Cada parte é medida. Se uma página ainda exceder o limite, nenhuma parte será salva.",
            8 => "Aceita JPG, PNG, BMP e TIFF. Cada imagem vira uma página; TIFF com várias páginas é expandido. A4 automático acompanha a orientação da imagem, sem cortar ou distorcer. Transparência vira fundo branco.",
            9 => "Aplicada sobre o conteúdo de todas as páginas, em cinza com a opacidade escolhida. Aceita até 80 caracteres latinos, incluindo acentos. Confira o resultado para evitar cobrir informações importantes.",
            10 => "Selecione um PDF. Use primeira página 2 para deixar a capa sem número. A sequência começa no número inicial escolhido; no formato 'Página X de Y', Y é o último número da sequência. Confira se a numeração cobre algum conteúdo ou fica fora da área visível em páginas recortadas.",
            _ => "Selecione um PDF. A janela mostra miniaturas e uma página ampliada. Arraste para reordenar, exclua as páginas dispensáveis e salve um novo PDF. As primeiras 20 miniaturas são carregadas; use 'Carregar mais' para as demais. Requer Ghostscript para visualizar."
        };
        EngineButton.Content = operation is 6 or 7 or 11 ? "Localizar Ghostscript" : "Localizar PDFtk";
        EngineStatus.Text = operation is 6 or 7 or 11
            ? (ghostscript is null ? "Ghostscript não encontrado" : "Ghostscript disponível") + (executable is null ? " • PDFtk necessário" : " • PDFtk disponível")
            : executable is null ? "Selecione pdftk.exe para começar." : "PDFtk disponível";
    }
    private void AddFiles(object sender, RoutedEventArgs e)
    {
        bool images = Operation.SelectedIndex == 8;
        var dialog = new OpenFileDialog { Filter = images ? "Imagens (*.jpg;*.jpeg;*.png;*.bmp;*.tif;*.tiff)|*.jpg;*.jpeg;*.png;*.bmp;*.tif;*.tiff" : "Documentos PDF (*.pdf)|*.pdf", Multiselect = true, Title = images ? "Adicionar imagens" : "Adicionar documentos PDF" };
        if (dialog.ShowDialog(this) == true) AddPaths(dialog.FileNames);
    }
    private void FilesDropped(object sender, DragEventArgs e)
    {
        if (active is null && e.Data.GetData(DataFormats.FileDrop) is string[] paths) AddPaths(paths);
    }
    private void AddPaths(IEnumerable<string> paths)
    {
        int skipped = 0;
        foreach (var path in paths)
        {
            string extension = System.IO.Path.GetExtension(path).ToLowerInvariant();
            bool accepted = Operation.SelectedIndex == 8 ? ImageExtensions.Contains(extension) : extension == ".pdf";
            if (!File.Exists(path) || !accepted) { skipped++; continue; }
            if (!files.Any(f => string.Equals(f.Path, path, StringComparison.OrdinalIgnoreCase))) files.Add(new(path));
        }
        Status.Text = $"{files.Count} arquivo(s) na lista." + (skipped > 0 ? $" {skipped} item(ns) ignorado(s): selecione arquivos compatíveis com a operação." : "");
    }
    private void MoveUp(object sender, RoutedEventArgs e) => Move(-1);
    private void MoveDown(object sender, RoutedEventArgs e) => Move(1);
    private void Move(int delta)
    {
        int index = FileList.SelectedIndex, target = index + delta;
        if (index < 0 || target < 0 || target >= files.Count) return;
        files.Move(index, target); FileList.SelectedIndex = target;
    }
    private void RemoveFile(object sender, RoutedEventArgs e) { if (FileList.SelectedItem is PdfItem item) files.Remove(item); }
    private void ClearFiles(object sender, RoutedEventArgs e) => files.Clear();
    private void LocateEngine(object sender, RoutedEventArgs e)
    {
        bool compression = Operation.SelectedIndex is 6 or 7 or 11;
        var dialog = new OpenFileDialog { Filter = compression ? "Ghostscript (gswin64c.exe;gswin32c.exe)|gswin64c.exe;gswin32c.exe" : "PDFtk (pdftk.exe)|pdftk.exe", Title = compression ? "Localizar o Ghostscript" : "Localizar pdftk.exe" };
        if (dialog.ShowDialog(this) == true)
        {
            if (compression) ghostscript = dialog.FileName;
            else executable = dialog.FileName;
            RefreshOperation();
        }
    }
    private async void RunOperation(object sender, RoutedEventArgs e)
    {
        if (active is not null) return;
        executable ??= PdfTools.FindExecutable();
        if (executable is null) { ShowDependencies(this, e); return; }
        int operation = Operation.SelectedIndex;
        bool split = operation is 1 or 3 or 7;
        if ((operation == 0 && files.Count < 2) || (operation == 8 && files.Count == 0) || (operation is not (0 or 8) && files.Count != 1))
        { MessageBox.Show(this, operation == 0 ? "Adicione pelo menos dois PDFs para juntar." : operation == 8 ? "Adicione pelo menos uma imagem para criar o PDF." : "Deixe apenas um PDF na lista para esta operação.", "Selecione os arquivos"); return; }
        if (files.Any(item => operation == 8 ? !ImageExtensions.Contains(System.IO.Path.GetExtension(item.Path).ToLowerInvariant()) : !item.Path.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)))
        { MessageBox.Show(this, "A lista contém arquivos de outro tipo. Limpe a lista e adicione os arquivos compatíveis com esta operação.", "Verifique a lista"); return; }
        if (operation is 6 or 7 or 11)
        {
            ghostscript ??= PdfCompression.FindExecutable();
            if (ghostscript is null) { ShowDependencies(this, e); return; }
        }
        if (operation == 11)
        {
            var organizer = new PageOrganizer(files[0].Path, executable, ghostscript!) { Owner = this };
            organizer.ShowDialog();
            if (organizer.SavedPath is string saved) { outputFolder = System.IO.Path.GetDirectoryName(saved); OpenOutputButton.Visibility = Visibility.Visible; Status.Text = "Novo PDF organizado salvo. O original foi preservado."; }
            return;
        }
        var selection = PageExpression.Text.Trim();
        if (operation is 2 or 3 && string.IsNullOrWhiteSpace(selection))
        { MessageBox.Show(this, "Informe as páginas ou intervalos. Exemplo: 1-3, 5, 8-fim.", "Informe as páginas"); return; }
        int degrees = Rotation.SelectedIndex switch { 0 => 90, 1 => -90, _ => 180 };
        var quality = (CompressionQuality)(operation == 7 ? CombinedQuality.SelectedIndex : Quality.SelectedIndex);
        var imageLayout = (ImagePageLayout)ImageLayout.SelectedIndex;
        var watermark = WatermarkText.Text.Trim();
        int opacity = WatermarkOpacity.SelectedIndex switch { 0 => 15, 1 => 25, _ => 40 };
        bool diagonal = WatermarkAngle.SelectedIndex == 0;
        if (operation == 9 && string.IsNullOrWhiteSpace(watermark)) { MessageBox.Show(this, "Informe o texto da marca-d'água.", "Texto necessário"); return; }
        int firstNumberedPage = 1, initialNumber = 1;
        var numberFormat = (PageNumberFormat)NumberFormat.SelectedIndex;
        var numberPosition = (PageNumberPosition)NumberPosition.SelectedIndex;
        if (operation == 10 && (!int.TryParse(NumberFirstPage.Text.Trim(), out firstNumberedPage) || firstNumberedPage < 1 || !int.TryParse(NumberStart.Text.Trim(), out initialNumber) || initialNumber is < 1 or > 1_000_000))
        { MessageBox.Show(this, "Informe uma primeira página válida e um número inicial entre 1 e 1.000.000.", "Verifique a numeração"); return; }
        long limitBytes = 0;
        string destination;
        if (split)
        {
            if (operation is 1 or 7)
            {
                var normalized = Limit.Text.Trim().Replace(',', '.');
                if (!decimal.TryParse(normalized, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var mb) || mb < 0.001m || mb > 100_000m)
                { MessageBox.Show(this, "Informe um limite entre 0,001 e 100.000 MB. Exemplo: 50 ou 2,5.", "Limite inválido"); return; }
                limitBytes = (long)decimal.Floor(mb * 1_000_000m);
            }
            var dialog = new OpenFolderDialog { Title = "Escolha onde criar a pasta com as partes" };
            if (dialog.ShowDialog(this) != true) return;
            destination = dialog.FolderName;
        }
        else
        {
            var suffix = operation switch { 0 => "unidos", 2 => "paginas", 4 => "girado", 5 => "otimizado", 8 => "imagens", 9 => "com_marca", 10 => "numerado", _ => "comprimido" };
            var stem = operation is 0 or 8 ? "documentos" : System.IO.Path.GetFileNameWithoutExtension(files[0].Path);
            var dialog = new SaveFileDialog { Filter = "Documento PDF (*.pdf)|*.pdf", FileName = $"{stem}_{suffix}.pdf", Title = "Salvar resultado com um novo nome", DefaultExt = ".pdf" };
            if (dialog.ShowDialog(this) != true) return;
            destination = dialog.FileName;
            if (File.Exists(destination)) { MessageBox.Show(this, "Escolha um nome de arquivo que ainda não exista. O aplicativo preserva os arquivos existentes.", "Escolha outro nome"); return; }
        }
        var inputs = files.Select(f => f.Path).ToArray();
        using var cancellation = new CancellationTokenSource();
        active = cancellation;
        SetBusy(true);
        OpenOutputButton.Visibility = Visibility.Collapsed;
        try
        {
            var tools = new PdfTools(executable);
            var progress = new Progress<string>(message => Status.Text = message);
            if (operation != 8)
            {
                var inspections = new List<PdfInspection>();
                foreach (string input in inputs) inspections.Add(await tools.InspectAsync(input, cancellation.Token));
                SignatureInfo.Text = inspections.Any(i => i.Signatures == SignatureDetection.SignatureFields)
                    ? "Há campos de assinatura nos arquivos selecionados. As alterações podem invalidar assinaturas; certificados não foram validados."
                    : inspections.Any(i => i.Signatures == SignatureDetection.InspectionFailed) ? "A verificação de assinaturas ficou incompleta. Alterações podem invalidar assinaturas."
                    : "Nenhum campo de assinatura identificado nesta verificação; isso não comprova a ausência de assinaturas.";
            }
            if (split)
            {
                var result = operation switch
                {
                    1 => await tools.SplitBySizeAsync(inputs[0], destination, limitBytes, progress, cancellation.Token),
                    7 => await new PdfCompression(ghostscript!, tools).CompressAndSplitAsync(inputs[0], destination, limitBytes, quality, progress, cancellation.Token),
                    _ => await tools.SplitByRangesAsync(inputs[0], destination, selection, progress, cancellation.Token)
                };
                outputFolder = result.Directory;
                Status.Text = $"Concluído: {result.Parts.Count} parte(s)" + (operation is 1 or 7 ? ", todas dentro do limite." : ", com os intervalos escolhidos.") + " O original foi preservado.";
            }
            else
            {
                OptimizationResult? reduction = null;
                switch (operation)
                {
                    case 0: await tools.MergeAsync(inputs, destination, progress, cancellation.Token); break;
                    case 2: await tools.ExtractAsync(inputs[0], destination, selection, progress, cancellation.Token); break;
                    case 4:
                        if (Rotation.SelectedIndex == 3) await tools.RotateMixedAsync(inputs[0], destination, selection, progress, cancellation.Token);
                        else await tools.RotateAsync(inputs[0], destination, selection, degrees, progress, cancellation.Token);
                        break;
                    case 5: reduction = await tools.OptimizeAsync(inputs[0], destination, progress, cancellation.Token); break;
                    case 6: reduction = await new PdfCompression(ghostscript!, tools).CompressAsync(inputs[0], destination, quality, progress, cancellation.Token); break;
                    case 8: await ImagesToPdf.ConvertAsync(inputs, destination, imageLayout, tools, progress, cancellation.Token); break;
                    case 9: await tools.AddWatermarkAsync(inputs[0], destination, watermark, opacity, diagonal, progress, cancellation.Token); break;
                    case 10: await tools.AddPageNumbersAsync(inputs[0], destination, firstNumberedPage, initialNumber, numberFormat, numberPosition, progress, cancellation.Token); break;
                }
                outputFolder = System.IO.Path.GetDirectoryName(destination);
                Status.Text = reduction is null ? "Concluído: resultado salvo e número de páginas verificado. Os originais foram preservados."
                    : reduction.OutputBytes < reduction.OriginalBytes
                        ? $"Concluído: {reduction.OriginalBytes / 1_000_000m:0.###} MB → {reduction.OutputBytes / 1_000_000m:0.###} MB (redução de {reduction.ReductionPercent:0.#}%). Original preservado."
                        : reduction.ProcessedBytes is long attempted
                            ? $"Sem redução: original {reduction.OriginalBytes:N0} bytes; tentativa {attempted:N0} bytes. Foi salva uma cópia do original."
                            : "O processamento não reduziu o tamanho. Foi salva uma cópia do original.";
            }
            OpenOutputButton.Visibility = Visibility.Visible;
        }
        catch (OperationCanceledException) { Status.Text = "Operação cancelada. Nenhum resultado foi salvo."; }
        catch (Exception error) { Status.Text = "Não foi possível concluir a operação."; MessageBox.Show(this, error.Message, "Verifique o documento", MessageBoxButton.OK, MessageBoxImage.Warning); }
        finally { active = null; SetBusy(false); }
    }
    private void SetBusy(bool busy)
    {
        OperationPanel.IsEnabled = ListActions.IsEnabled = FileList.IsEnabled = AddButton.IsEnabled = EngineButton.IsEnabled = RunButton.IsEnabled = !busy;
        ExtraOptions.IsEnabled = !busy;
        InspectButton.IsEnabled = DependenciesButton.IsEnabled = !busy;
        Progress.Visibility = CancelButton.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
    }
    private async void InspectSignatures(object sender, RoutedEventArgs e)
    {
        executable ??= PdfTools.FindExecutable();
        if (executable is null) { ShowDependencies(sender, e); return; }
        if (files.Count != 1 || !files[0].Path.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)) { Status.Text = "Deixe um único PDF na lista para verificar os campos de assinatura."; return; }
        using var cancellation = new CancellationTokenSource(); active = cancellation; SetBusy(true);
        try { var info = await new PdfTools(executable).InspectAsync(files[0].Path, cancellation.Token); SignatureInfo.Text = info.SignatureMessage; Status.Text = $"Verificação concluída: {info.Pages} página(s). O arquivo não foi alterado."; }
        catch (OperationCanceledException) { Status.Text = "Verificação cancelada."; }
        catch (Exception error) { Status.Text = error.Message; SignatureInfo.Text = "Não foi possível verificar as assinaturas deste documento."; }
        finally { active = null; SetBusy(false); }
    }
    private void ShowDependencies(object sender, RoutedEventArgs e)
    {
        new DependenciesWindow { Owner = this }.ShowDialog();
        executable = PdfTools.FindExecutable() ?? executable; ghostscript = PdfCompression.FindExecutable() ?? ghostscript; RefreshOperation();
    }
    private void CancelOperation(object sender, RoutedEventArgs e) { active?.Cancel(); Status.Text = "Cancelando…"; }
    private void OpenOutput(object sender, RoutedEventArgs e)
    {
        if (outputFolder is null || !Directory.Exists(outputFolder)) return;
        try { Process.Start(new ProcessStartInfo(outputFolder) { UseShellExecute = true }); }
        catch (Exception error) { MessageBox.Show(this, error.Message, "Não foi possível abrir a pasta"); }
    }
    private void WindowClosing(object? sender, CancelEventArgs e)
    {
        if (active is null) return;
        e.Cancel = true;
        active.Cancel();
        Status.Text = "Cancelando a operação. Aguarde e feche novamente.";
    }
}
