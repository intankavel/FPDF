using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FPDF.Core;
using Microsoft.Win32;

namespace FPDF.App;

public sealed class PageTile(int originalPage) : INotifyPropertyChanged
{
    public int OriginalPage { get; } = originalPage;
    public string Label => $"Página original {OriginalPage}" + (Degrees == 0 ? "" : $" · {Degrees}°");
    private int degrees;
    public int Degrees { get => degrees; set { degrees=value; PropertyChanged?.Invoke(this,new(nameof(Degrees))); PropertyChanged?.Invoke(this,new(nameof(Label))); } }
    private BitmapSource? thumbnail;
    public BitmapSource? Thumbnail { get => thumbnail; set { thumbnail = value; PropertyChanged?.Invoke(this, new(nameof(Thumbnail))); } }
    public event PropertyChangedEventHandler? PropertyChanged;
}

public partial class PageOrganizer : Window
{
    private readonly string input, ghostscript;
    private readonly PdfTools tools;
    private readonly ObservableCollection<PageTile> pages = [];
    private readonly List<PageTile> originals = [];
    private readonly Stack<(int Page, int Degrees)[]> history = [];
    private PageTile? activePage;
    private readonly CancellationTokenSource lifetime = new();
    private CancellationTokenSource? previewCancellation;
    private readonly string work = Path.Combine(Path.GetTempPath(), "FPDF-preview-" + Guid.NewGuid().ToString("N"));
    private Task thumbnails = Task.CompletedTask, preview = Task.CompletedTask;
    private Point dragStart;
    private PageTile? dragPage;
    private bool saving, closed, initialized;
    public string? SavedPath { get; private set; }

    public PageOrganizer(string input, string pdftk, string ghostscript)
    {
        this.input = input; this.ghostscript = ghostscript; tools = new(pdftk);
        InitializeComponent(); DocumentName.Text = Path.GetFileName(input); Pages.ItemsSource = pages;
        Directory.CreateDirectory(work);
    }
    private async void WindowLoaded(object sender, RoutedEventArgs e)
    {
        if (initialized || closed) return;
        initialized = true;
        try
        {
            var inspection = await tools.InspectAsync(input, lifetime.Token);
            if (inspection.Pages > 5000) throw new InvalidOperationException("Esta versão permite organizar visualmente até 5.000 páginas por documento.");
            SignatureStatus.Text = inspection.SignatureMessage;
            for (int i = 1; i <= inspection.Pages; i++) { var tile = new PageTile(i); originals.Add(tile); pages.Add(tile); }
            Actions.IsEnabled = SaveButton.IsEnabled = true;
            UpdateStatus(); Pages.SelectedIndex = 0; BeginThumbnails();
        }
        catch (OperationCanceledException) { }
        catch (Exception error) { Status.Text = error.Message; }
    }
    private void BeginThumbnails()
    {
        if (!thumbnails.IsCompleted || closed) return;
        thumbnails = LoadThumbnails();
    }
    private async Task LoadThumbnails()
    {
        MoreButton.IsEnabled = false;
        try
        {
            foreach (var tile in pages.Where(p => p.Thumbnail is null).Take(20).ToArray())
            {
                string path = Path.Combine(work, $"thumb-{tile.OriginalPage}.png");
                if (!File.Exists(path)) await PdfPreview.RenderPageAsync(ghostscript, input, tile.OriginalPage, path, cancellation: lifetime.Token);
                lifetime.Token.ThrowIfCancellationRequested(); tile.Thumbnail = ReadImage(path);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception error) { if (!closed) Status.Text = "Miniatura indisponível: " + error.Message; }
        finally { if (!closed) MoreButton.IsEnabled = pages.Any(p => p.Thumbnail is null); }
    }
    private void LoadMore(object sender, RoutedEventArgs e) => BeginThumbnails();
    private void PageSelected(object sender, SelectionChangedEventArgs e)
    {
        if ((e.AddedItems.OfType<PageTile>().LastOrDefault() ?? Pages.SelectedItems.OfType<PageTile>().LastOrDefault()) is not PageTile tile || closed) return;
        activePage = tile;
        SetPreview(tile.Thumbnail);
        QueuePreview(tile);
    }
    private void QueuePreview(PageTile tile)
    {
        previewCancellation?.Cancel();
        // Serialize rendering to avoid collecting processes on rapid selection changes.
        var previous = preview;
        var cancellation = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
        previewCancellation = cancellation;
        PreviewCaption.Text = $"Página original {tile.OriginalPage} — posição {pages.IndexOf(tile) + 1}";
        int scale = Zoom.SelectedIndex >= 2 ? 2 : 1;
        preview = ShowPage(previous, tile, cancellation, scale);
    }
    private async Task ShowPage(Task previous, PageTile tile, CancellationTokenSource cancellation, int scale)
    {
        try
        {
            await previous; cancellation.Token.ThrowIfCancellationRequested();
            string sharp = Path.Combine(work, $"sharp-{tile.OriginalPage}.png");
            string path = scale == 2 || File.Exists(sharp) ? sharp : Path.Combine(work, $"large-{tile.OriginalPage}.png");
            if (!File.Exists(path))
            {
                if (scale == 2) PreviewCaption.Text += " — melhorando a nitidez…";
                await PdfPreview.RenderPageAsync(ghostscript, input, tile.OriginalPage, path, large: true, cancellation: cancellation.Token, previewScale: scale);
            }
            cancellation.Token.ThrowIfCancellationRequested();
            SetPreview(ReadImage(path));
            PreviewCaption.Text = $"Página original {tile.OriginalPage} — posição {pages.IndexOf(tile) + 1}";
        }
        catch (OperationCanceledException) { }
        catch (Exception error) { if (!closed) Status.Text = "Pré-visualização indisponível: " + error.Message; }
        finally { if (ReferenceEquals(previewCancellation, cancellation)) previewCancellation = null; cancellation.Dispose(); }
    }
    private void SetPreview(BitmapSource? image) { LargePreview.Source = image; LargePreview.LayoutTransform = new RotateTransform(activePage?.Degrees ?? 0); UpdateZoom(); }
    private void ZoomChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateZoom();
        if (!closed && activePage is not null && Zoom.SelectedIndex >= 2 && LargePreview.Source is BitmapSource image && image.PixelWidth < 2000) QueuePreview(activePage);
    }
    private void PreviewResized(object sender, SizeChangedEventArgs e) => UpdateZoom();
    private void UpdateZoom()
    {
        if (LargePreview?.Source is not BitmapSource image || PreviewScroll is null || Zoom is null) return;
        double width = Math.Max(100, PreviewScroll.ActualWidth - 16), height = Math.Max(100, PreviewScroll.ActualHeight - 16);
        LargePreview.Width = Zoom.SelectedIndex switch
        {
            1 => 1000,
            2 => 1500,
            3 => 2000,
            _ => image.PixelWidth * Math.Min(width / ((activePage?.Degrees ?? 0) % 180 == 0 ? image.PixelWidth : image.PixelHeight), height / ((activePage?.Degrees ?? 0) % 180 == 0 ? image.PixelHeight : image.PixelWidth))
        };
    }
    private static BitmapSource ReadImage(string path)
    {
        using var file = File.OpenRead(path);
        var image = BitmapDecoder.Create(file, BitmapCreateOptions.None, BitmapCacheOption.OnLoad).Frames[0]; image.Freeze(); return image;
    }
    private void Remember() => history.Push(pages.Select(p => (p.OriginalPage,p.Degrees)).ToArray());
    private PageTile[] Selected() => pages.Where(p => Pages.SelectedItems.Contains(p)).ToArray();
    private void Select(IEnumerable<PageTile> selected) { Pages.SelectedItems.Clear(); foreach (var page in selected) Pages.SelectedItems.Add(page); }
    private void UpdateStatus() { Status.Text = $"{pages.Count} de {originals.Count} páginas no resultado. O original será preservado."; }
    private void MovePrevious(object sender, RoutedEventArgs e) => Move(-1);
    private void MoveNext(object sender, RoutedEventArgs e) => Move(1);
    private void Move(int offset)
    {
        var selected = Selected();
        if (selected.Length == 0) return;
        Remember();
        if (offset < 0) { for (int i=1;i<pages.Count;i++) if (selected.Contains(pages[i]) && !selected.Contains(pages[i-1])) pages.Move(i,i-1); }
        else { for (int i=pages.Count-2;i>=0;i--) if (selected.Contains(pages[i]) && !selected.Contains(pages[i+1])) pages.Move(i,i+1); }
        Select(selected); UpdateStatus();
    }
    private void DeletePage(object sender, RoutedEventArgs e)
    {
        var selected = Selected();
        if (selected.Length == 0) return;
        if (selected.Length == pages.Count) { Status.Text = "Mantenha pelo menos uma página no resultado."; return; }
        int index = pages.IndexOf(selected[0]);
        Remember(); foreach (var page in selected) pages.Remove(page);
        Select([pages[Math.Min(index,pages.Count-1)]]); UpdateStatus();
    }
    private void Undo(object sender, RoutedEventArgs e)
    {
        if (history.Count == 0) return;
        Restore(history.Pop());
    }
    private void Restore(IEnumerable<(int Page, int Degrees)> order)
    {
        int? selected = (Pages.SelectedItem as PageTile)?.OriginalPage;
        pages.Clear(); foreach (var state in order) { var page = originals[state.Page-1]; page.Degrees=state.Degrees; pages.Add(page); }
        Pages.SelectedItem = pages.FirstOrDefault(p => p.OriginalPage == selected) ?? pages[0]; UpdateStatus();
        MoreButton.IsEnabled = thumbnails.IsCompleted && pages.Any(p => p.Thumbnail is null);
    }
    private void Reset(object sender, RoutedEventArgs e) { Remember(); Restore(originals.Select(p => (p.OriginalPage,0))); }
    private void RotateSelected(object sender, RoutedEventArgs e)
    {
        var selected = Selected(); if (selected.Length == 0) return;
        Remember(); foreach (var page in selected) page.Degrees=(page.Degrees+90)%360;
        SetPreview(LargePreview.Source as BitmapSource); UpdateStatus();
    }
    private static PageTile? TileAt(DependencyObject? source)
    {
        while (source is not null) { if (source is ListBoxItem item) return item.DataContext as PageTile; source = ParentOf(source); }
        return null;
    }
    private static DependencyObject? ParentOf(DependencyObject source) => source is FrameworkContentElement content ? content.Parent : VisualTreeHelper.GetParent(source);
    private void DragStarted(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject source)
            for (var node=source; node is not null; node=ParentOf(node)) if (node is CheckBox) { dragPage=null; return; }
        dragStart = e.GetPosition(Pages); dragPage = TileAt(e.OriginalSource as DependencyObject);
        if (dragPage is not null && Pages.SelectedItems.Contains(dragPage) && Pages.SelectedItems.Count>1) e.Handled=true;
    }
    private void DragMoved(object sender, MouseEventArgs e)
    {
        if (dragPage is null || e.LeftButton != MouseButtonState.Pressed || saving) return;
        var point = e.GetPosition(Pages);
        if (Math.Abs(point.X - dragStart.X) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(point.Y - dragStart.Y) < SystemParameters.MinimumVerticalDragDistance) return;
        var tile = dragPage; dragPage = null;
        var group = Pages.SelectedItems.Contains(tile) ? Selected() : [tile];
        DragDrop.DoDragDrop(Pages, new DataObject(typeof(PageTile[]), group), DragDropEffects.Move);
    }
    private void PageDropped(object sender, DragEventArgs e)
    {
        if (saving || e.Data.GetData(typeof(PageTile[])) is not PageTile[] group || TileAt(e.OriginalSource as DependencyObject) is not PageTile target) return;
        e.Handled = MoveGroup(group,target);
    }
    private bool MoveGroup(PageTile[] group, PageTile target)
    {
        if (group.Length==0 || group.Contains(target) || !pages.Contains(target) || group.Any(p=>!pages.Contains(p))) return false;
        bool after = pages.IndexOf(group[0]) < pages.IndexOf(target);
        Remember(); foreach (var page in group) pages.Remove(page);
        int to=pages.IndexOf(target) + (after ? 1 : 0); foreach (var page in group) pages.Insert(to++,page);
        Select(group); UpdateStatus(); return true;
    }
    private async void Save(object sender, RoutedEventArgs e)
    {
        if (saving || pages.Count == 0) return;
        var dialog = new SaveFileDialog { Filter = "Documento PDF (*.pdf)|*.pdf", FileName = Path.GetFileNameWithoutExtension(input) + "_organizado.pdf", Title = "Salvar a organização em um novo PDF" };
        if (dialog.ShowDialog(this) != true) return;
        if (File.Exists(dialog.FileName)) { Status.Text = "Escolha um novo nome. Arquivos existentes são preservados."; return; }
        saving = true; Actions.IsEnabled = SaveButton.IsEnabled = Pages.IsEnabled = false;
        try
        {
            await tools.OrganizePagesAsync(input, dialog.FileName, pages.Select(p => p.OriginalPage).ToArray(), pages.Select(p=>p.Degrees).ToArray(), new Progress<string>(s => Status.Text = s), lifetime.Token);
            SavedPath = dialog.FileName; Status.Text = "Novo PDF salvo e quantidade de páginas verificada. Original preservado.";
        }
        catch (OperationCanceledException) { Status.Text = "Salvamento cancelado. Nenhum resultado foi publicado."; }
        catch (Exception error) { Status.Text = error.Message; }
        finally { saving = false; Actions.IsEnabled = SaveButton.IsEnabled = Pages.IsEnabled = true; }
    }
    private void CloseWindow(object sender, RoutedEventArgs e) => Close();
    private async void WindowClosing(object? sender, CancelEventArgs e)
    {
        if (saving) { e.Cancel = true; lifetime.Cancel(); Status.Text = "Cancelando o salvamento. Aguarde e feche novamente."; return; }
        if (closed) return;
        closed = true; lifetime.Cancel();
        await Task.WhenAll(thumbnails, preview);
        try { Directory.Delete(work, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        lifetime.Dispose();
    }
}
