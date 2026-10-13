using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        try { Run(args); }
        catch (Exception error) { Console.Error.WriteLine(error); Environment.ExitCode = 1; }
    }
    private static void Run(string[] args)
    {
        var app = new FPDF.App.App();
        app.InitializeComponent();
        app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        var window = new FPDF.App.MainWindow();
        var directory = Path.GetFullPath("artifacts/preview");
        Directory.CreateDirectory(directory);
        var splash = (Window)Activator.CreateInstance(typeof(FPDF.App.App).Assembly.GetType("FPDF.App.StartupSplash")!)!;
        if (splash.ShowInTaskbar || splash.WindowStyle != WindowStyle.None) throw new Exception("Tela de abertura deve ser simples e sem botão na barra de tarefas.");
        Render(splash, 420, 420, Path.Combine(directory, "abertura.png"));
        splash.Close();
        Console.WriteLine("PASS: FPDF.jpg carrega como tela de abertura incorporada ao aplicativo.");
        var operations = (ComboBox)window.FindName("Operation");
        if (operations.SelectedValue is not int initial || initial != 0) throw new Exception("A ferramenta inicial deve continuar sendo Juntar PDFs.");
        var view = (System.Windows.Data.CollectionView)operations.ItemsSource;
        if (view.Groups?.Count != 4 || operations.Items.Count != 14) throw new Exception("O menu deve ter quatro categorias e quatorze ferramentas.");
        if (operations.Items.Cast<FPDF.App.OperationOption>().Select(o => o.Id).Distinct().Count() != 14) throw new Exception("Há ferramentas duplicadas no menu.");
        Console.WriteLine("PASS: menu agrupado preserva as quatorze ferramentas e seleção inicial.");
        if (window.SizeToContent != SizeToContent.Height) throw new Exception("A altura inicial não acompanha o conteúdo.");
        var startup = (ScrollViewer)window.Content;
        startup.Measure(new Size(984, double.PositiveInfinity));
        int initialHeight = (int)Math.Ceiling(startup.DesiredSize.Height);
        Render(window, 984, initialHeight, Path.Combine(directory, "janela-inicial.png"));
        if (startup.ScrollableHeight > 1) throw new Exception("A janela inicial apresenta rolagem vertical desnecessária.");
        Console.WriteLine($"PASS: altura inicial de conteúdo {initialHeight}, sem rolagem vertical.");
        if (args.Length > 0)
        {
            var inputs = Directory.GetFiles(args[0], "documento*.pdf");
            typeof(FPDF.App.MainWindow).GetMethod("AddPaths", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, [inputs]);
        }
        Render(window, 884, 684, Path.Combine(directory, "juntar.png"));
        RenderOperationMenu(operations, directory);
        ((ComboBox)window.FindName("Operation")).SelectedValue = 1;
        if (((FrameworkElement)window.FindName("LimitPanel")).Visibility != Visibility.Visible)
            throw new Exception("Campo de limite não ficou visível.");
        if (((TextBox)window.FindName("Limit")).Text != "50") throw new Exception("Limite padrão incorreto.");
        Render(window, 884, 684, Path.Combine(directory, "dividir.png"));
        Render(window, 724, 664, Path.Combine(directory, "dividir-minimo.png"));
        foreach (var item in new[] { (Index: 2, Name: "extrair"), (Index: 3, Name: "intervalos"), (Index: 4, Name: "girar"), (Index: 5, Name: "otimizar"), (Index: 6, Name: "comprimir"), (Index: 7, Name: "comprimir-dividir"), (Index: 8, Name: "imagens"), (Index: 9, Name: "marca-dagua"), (Index: 10, Name: "numerar"), (Index: 11, Name: "organizar"), (Index: 12, Name: "comprimir-limite"), (Index: 13, Name: "ocr") })
        {
            ((ComboBox)window.FindName("Operation")).SelectedValue = item.Index;
            if (item.Index is 2 or 3 or 4 && ((FrameworkElement)window.FindName("SelectionPanel")).Visibility != Visibility.Visible) throw new Exception("Seleção de páginas não apareceu.");
            if (item.Index == 4 && ((FrameworkElement)window.FindName("RotationPanel")).Visibility != Visibility.Visible) throw new Exception("Opções de rotação não apareceram.");
            if (item.Index == 6 && ((FrameworkElement)window.FindName("QualityPanel")).Visibility != Visibility.Visible) throw new Exception("Qualidade não apareceu.");
            if (item.Index == 7 && ((FrameworkElement)window.FindName("CombinedQualityPanel")).Visibility != Visibility.Visible) throw new Exception("Qualidade da operação combinada não apareceu.");
            if (item.Index == 8 && ((FrameworkElement)window.FindName("ImageLayoutPanel")).Visibility != Visibility.Visible) throw new Exception("Formato das imagens não apareceu.");
            if (item.Index == 9 && ((FrameworkElement)window.FindName("WatermarkOptionsPanel")).Visibility != Visibility.Visible) throw new Exception("Opções de marca-d'água não apareceram.");
            if (item.Index == 10 && ((FrameworkElement)window.FindName("NumberOptionsPanel")).Visibility != Visibility.Visible) throw new Exception("Opções de numeração não apareceram.");
            if (item.Index == 12 && (((FrameworkElement)window.FindName("LimitPanel")).Visibility != Visibility.Visible || ((FrameworkElement)window.FindName("QualityPanel")).Visibility != Visibility.Collapsed)) throw new Exception("Compressão por limite deve exibir apenas o tamanho máximo.");
            if (item.Index == 13 && (((FrameworkElement)window.FindName("OcrPanel")).Visibility != Visibility.Visible || ((ComboBox)window.FindName("OcrLanguage")).SelectedIndex != 0)) throw new Exception("OCR deve mostrar português como padrão.");
            if (item.Index >= 2)
            {
                var illustration = (FPDF.App.OperationIllustration)window.FindName("Illustration");
                if (illustration.Visibility != Visibility.Visible || illustration.Operation != item.Index) throw new Exception("Infográfico incorreto.");
            }
            Render(window, 884, 930, Path.Combine(directory, item.Name + ".png"));
            Render(window, 724, 664, Path.Combine(directory, item.Name + "-minimo.png"));
        }
        ((ComboBox)window.FindName("Operation")).SelectedValue = 4;
        ((ComboBox)window.FindName("Rotation")).SelectedIndex = 3;
        ((TextBox)window.FindName("PageExpression")).Text = "1:E90, 2:180, 3:D90";
        if (!((TextBlock)window.FindName("Hint")).Text.Contains("E90")) throw new Exception("Explicação dos giros não apareceu.");
        Render(window, 884, 930, Path.Combine(directory, "giros-por-pagina.png"));
        window.Close();
        RenderIllustrations(directory);
        OrganizerChecks.Run(directory);
        if (!args.Contains("--preview-only")) { ImageChecks.Run(); NumberingChecks.Run(); }
        Console.WriteLine("PASS: interface inicializada, troca de operação e limite de 50 MB. Pré-visualizações em " + directory);
    }
    private static void RenderIllustrations(string directory)
    {
        string[] names = ["Juntar PDFs", "Dividir por tamanho", "Extrair páginas", "Dividir por intervalos", "Girar páginas", "Otimizar sem perda", "Comprimir PDF", "Comprimir e dividir", "Imagens para PDF", "Marca-d’água", "Numerar páginas", "Organizar páginas", "Comprimir até o limite", "Reconhecer texto (OCR)"];
        var surface = new DrawingVisual();
        using (var context = surface.RenderOpen())
        {
            context.DrawRectangle(Brushes.White, null, new Rect(0, 0, 1160, 1020));
            for (int i = 0; i < names.Length; i++)
            {
                double x = 10 + (i % 2) * 580, y = 10 + (i / 2) * 145;
                context.DrawText(new FormattedText(names[i], System.Globalization.CultureInfo.GetCultureInfo("pt-BR"), FlowDirection.LeftToRight, new Typeface("Segoe UI Semibold"), 14, Brushes.Black, 1), new Point(x, y));
                var diagram = new FPDF.App.OperationIllustration { Operation = i };
                diagram.Measure(new Size(560, 108)); diagram.Arrange(new Rect(0, 0, 560, 108));
                var image = new RenderTargetBitmap(560, 108, 96, 96, PixelFormats.Pbgra32);
                image.Render(diagram);
                context.DrawImage(image, new Rect(x, y + 25, 560, 108));
            }
        }
        var bitmap = new RenderTargetBitmap(1160, 1020, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(surface);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(Path.Combine(directory, "todos-infograficos.png")); encoder.Save(file);
    }
    private static void RenderOperationMenu(ComboBox operations, string directory)
    {
        operations.IsDropDownOpen = true;
        operations.UpdateLayout();
        var popup = (System.Windows.Controls.Primitives.Popup)operations.Template.FindName("PART_Popup", operations);
        var menu = (FrameworkElement)popup.Child;
        menu.Measure(new Size(550, 650));
        menu.Arrange(new Rect(new Point(), menu.DesiredSize));
        menu.UpdateLayout();
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(menu.ActualWidth), (int)Math.Ceiling(menu.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(menu);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(Path.Combine(directory, "menu-categorias.png")); encoder.Save(file);
        operations.IsDropDownOpen = false;
    }
    internal static void Render(Window window, int width, int height, string path)
    {
        var content = (FrameworkElement)window.Content;
        content.Measure(new Size(width, height));
        content.Arrange(new Rect(0, 0, width, height));
        content.UpdateLayout();
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        var surface = new DrawingVisual();
        using (var context = surface.RenderOpen())
        {
            context.DrawRectangle(window.Background, null, new Rect(0, 0, width, height));
            var contentBitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            contentBitmap.Render(content);
            context.DrawImage(contentBitmap, new Rect(0, 0, width, height));
        }
        bitmap.Render(surface);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(path);
        encoder.Save(file);
    }
}
