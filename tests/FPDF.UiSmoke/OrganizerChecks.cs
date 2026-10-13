using System.Collections.ObjectModel;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using FPDF.App;
using FPDF.Core;

internal static class OrganizerChecks
{
    public static void Run(string directory)
    {
        var root = Path.Combine(directory, "organizer-fixtures"); Directory.CreateDirectory(root);
        var input = Path.Combine(root, "visualizacao.pdf");
        // Use existing PDF with meaningful page content when available.
        string source = Directory.GetFiles(Path.GetFullPath("artifacts/smoke"), "documento ação.pdf", SearchOption.AllDirectories).First();
        File.Copy(source, input, true);
        var window = new PageOrganizer(input, PdfTools.FindExecutable()!, PdfCompression.FindExecutable()!);
        ((TextBlock)window.FindName("SignatureStatus")).Text = new PdfTools(PdfTools.FindExecutable()!).InspectAsync(input).GetAwaiter().GetResult().SignatureMessage;
        var type = typeof(PageOrganizer);
        var pages = (ObservableCollection<PageTile>)type.GetField("pages", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window)!;
        var originals = (List<PageTile>)type.GetField("originals", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window)!;
        string work = (string)type.GetField("work", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window)!;
        for (int i = 1; i <= 6; i++)
        {
            string png = Path.Combine(root, $"pagina-{i}.png");
            PdfPreview.RenderPageAsync(PdfCompression.FindExecutable()!, input, i, png).GetAwaiter().GetResult();
            using var file = File.OpenRead(png);
            var image = BitmapDecoder.Create(file, BitmapCreateOptions.None, BitmapCacheOption.OnLoad).Frames[0]; image.Freeze();
            var tile = new PageTile(i) { Thumbnail = image }; originals.Add(tile); pages.Add(tile);
            File.Copy(png, Path.Combine(work, $"large-{i}.png"), true);
        }
        ((FrameworkElement)window.FindName("Actions")).IsEnabled = true;
        ((FrameworkElement)window.FindName("SaveButton")).IsEnabled = true;
        var list = (ListBox)window.FindName("Pages");
        list.SelectedIndex = 5;
        Invoke("MovePrevious"); Assert(pages.Select(p => p.OriginalPage).SequenceEqual([1,2,3,4,6,5]), "Botão muda a ordem visual");
        Invoke("DeletePage"); Assert(pages.Select(p => p.OriginalPage).SequenceEqual([1,2,3,4,5]), "Exclusão remove a página selecionada");
        Invoke("Undo"); Assert(pages.Select(p => p.OriginalPage).SequenceEqual([1,2,3,4,6,5]), "Desfazer recupera página e posição");
        Invoke("Reset"); Assert(pages.Select(p => p.OriginalPage).SequenceEqual([1,2,3,4,5,6]), "Restaurar recupera a sequência original");
        Invoke("Undo"); Assert(pages.Select(p => p.OriginalPage).SequenceEqual([1,2,3,4,6,5]), "Restauração também pode ser desfeita");
        list.SelectedIndex = 0;
        list.SelectedItems.Add(pages[1]);
        Invoke("RotateSelected"); Assert(pages[0].Degrees==90 && pages[1].Degrees==90 && pages[2].Degrees==0,"Giro aplica-se às páginas marcadas");
        Invoke("Undo"); Assert(pages.All(p=>p.Degrees==0),"Desfazer recupera orientação das páginas");
        list.SelectedItems.Clear(); list.SelectedItems.Add(pages[0]); list.SelectedItems.Add(pages[1]);
        Invoke("MoveNext"); Assert(pages.Select(p=>p.OriginalPage).SequenceEqual([3,1,2,4,6,5]),"Duas páginas movem-se juntas mantendo ordem");
        Invoke("DeletePage"); Assert(pages.Select(p=>p.OriginalPage).SequenceEqual([3,4,6,5]),"Exclusão em grupo remove apenas páginas marcadas");
        Invoke("Undo"); Invoke("Undo");
        list.SelectedItems.Clear(); foreach(var page in pages) list.SelectedItems.Add(page);
        Invoke("DeletePage"); Assert(pages.Count==6,"Exclusão de todas as páginas é bloqueada");
        list.SelectedItems.Clear(); list.SelectedIndex=0;
        type.GetMethod("MoveGroup",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(window,[new PageTile[]{pages[0],pages[1]},pages[2]]);
        Assert(pages.Select(p=>p.OriginalPage).SequenceEqual([3,1,2,4,6,5]),"Arraste avança grupo para a posição escolhida");
        Invoke("Undo"); list.SelectedItems.Clear(); list.SelectedIndex=0;
        Program.Render(window, 1060, 780, Path.Combine(directory, "organizador-paginas.png"));
        Program.Render(window, 810, 590, Path.Combine(directory, "organizador-minimo.png"));
        window.Close(); Assert(!Directory.Exists(work), "Fechar organizador remove cópias temporárias");
        var dependencies = new DependenciesWindow(); Program.Render(dependencies, 620, 440, Path.Combine(directory, "dependencias.png")); dependencies.Close();
        var batchWindow = new BatchResultsWindow(new BatchCompressionResult(root,[new("documento A.pdf","a.pdf",2_000_000,1_200_000,"Comprimido"),new("documento B.pdf","b.pdf",300_000,300_000,"Sem redução; cópia do original"),new("documento C.pdf",null,500_000,null,"Falha: PDF protegido ou danificado")],false));
        Program.Render(batchWindow,810,480,Path.Combine(directory,"resultado-lote.png")); batchWindow.Close();
        CheckLoading();
        void Invoke(string name) => type.GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, [window, new RoutedEventArgs()]);
        void CheckLoading()
        {
            var oldContext = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
            var actual = new PageOrganizer(input, PdfTools.FindExecutable()!, PdfCompression.FindExecutable()!);
            var frame = new DispatcherFrame();
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
            var deadline = DateTime.UtcNow.AddSeconds(45);
            bool finished = false;
            timer.Tick += (_, _) =>
            {
                bool ready = ((Button)actual.FindName("SaveButton")).IsEnabled;
                var thumbs = (Task)type.GetField("thumbnails", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(actual)!;
                var enlarged = (Task)type.GetField("preview", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(actual)!;
                if (ready && thumbs.IsCompleted && enlarged.IsCompleted) { finished = true; frame.Continue = false; }
                else if (DateTime.UtcNow >= deadline) frame.Continue = false;
            };
            try
            {
                actual.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent)); timer.Start(); Dispatcher.PushFrame(frame);
                Assert(finished, "Carregamento assíncrono real do organizador termina: " + ((TextBlock)actual.FindName("Status")).Text);
                var actualPages = (ObservableCollection<PageTile>)type.GetField("pages", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(actual)!;
                Assert(actualPages.Count == 6 && actualPages.All(p => p.Thumbnail is not null), "Miniaturas reais são carregadas localmente");
                Assert(((System.Windows.Controls.Image)actual.FindName("LargePreview")).Source is BitmapSource bitmap && bitmap.PixelWidth == 1000, "Página ampliada usa imagem de maior resolução");
                ((ComboBox)actual.FindName("Zoom")).SelectedIndex = 2;
                Assert(((System.Windows.Controls.Image)actual.FindName("LargePreview")).Width == 1500, "Zoom de 150% amplia a página");
                frame = new DispatcherFrame(); finished=false; deadline=DateTime.UtcNow.AddSeconds(45); Dispatcher.PushFrame(frame);
                Assert(finished && ((System.Windows.Controls.Image)actual.FindName("LargePreview")).Source is BitmapSource sharp && sharp.PixelWidth==2000 && sharp.PixelHeight==2600,"Zoom carrega nova renderização com mais detalhes");
                Assert(((System.Windows.Controls.Image)actual.FindName("LargePreview")).Width==1500,"Melhorar nitidez mantém o tamanho do zoom");
                ((ComboBox)actual.FindName("Zoom")).SelectedIndex=3;
                Assert(((System.Windows.Controls.Image)actual.FindName("LargePreview")).Width==2000,"Zoom de 200% reutiliza a imagem nítida");
                ((ComboBox)actual.FindName("Zoom")).SelectedIndex = 0;
                Program.Render(actual, 1060, 780, Path.Combine(directory, "organizador-carregado.png"));
                Assert(((System.Windows.Controls.Image)actual.FindName("LargePreview")).Width > 300, "Ajustar à janela aproveita a área de visualização");
            }
            finally { timer.Stop(); actual.Close(); SynchronizationContext.SetSynchronizationContext(oldContext); }
        }
    }
    private static void Assert(bool result, string message)
    {
        if (!result) throw new Exception("FAIL: " + message); Console.WriteLine("PASS: " + message);
    }
}
