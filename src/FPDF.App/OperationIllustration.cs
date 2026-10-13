using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace FPDF.App;

public sealed class OperationIllustration : FrameworkElement
{
    public static readonly DependencyProperty OperationProperty = DependencyProperty.Register(nameof(Operation), typeof(int), typeof(OperationIllustration), new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.AffectsRender));
    public int Operation { get => (int)GetValue(OperationProperty); set => SetValue(OperationProperty, value); }
    public bool IsSplit => Operation == 3;
    protected override void OnRender(DrawingContext drawing)
    {
        base.OnRender(drawing);
        double scale = Math.Min(1, ActualWidth / 560);
        drawing.PushTransform(new ScaleTransform(scale, scale));
        var ink = new SolidColorBrush(Color.FromRgb(24, 53, 74));
        var teal = new SolidColorBrush(Color.FromRgb(8, 126, 130));
        var line = new Pen(new SolidColorBrush(Color.FromRgb(192, 211, 220)), 1);
        void Text(string text, double x, double y, double size = 12) => drawing.DrawText(new FormattedText(text, CultureInfo.GetCultureInfo("pt-BR"), FlowDirection.LeftToRight, new Typeface("Segoe UI"), size, ink, VisualTreeHelper.GetDpi(this).PixelsPerDip), new Point(x, y));
        void Page(int number, double x, double y, bool selected)
        {
            drawing.DrawRoundedRectangle(selected ? new SolidColorBrush(Color.FromRgb(220, 242, 240)) : Brushes.White, line, new Rect(x, y, 23, 32), 3, 3);
            Text(number.ToString(), x + 8, y + 7);
        }
        void Arrow(double x) => drawing.DrawText(new FormattedText("→", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 28, teal, VisualTreeHelper.GetDpi(this).PixelsPerDip), new Point(x, 20));
        void Group(double x, params int[] pages)
        {
            drawing.DrawRoundedRectangle(Brushes.White, line, new Rect(x, 18, pages.Length * 27 + 13, 42), 5, 5);
            for (int i = 0; i < pages.Length; i++) Page(pages[i], x + 7 + i * 27, 23, true);
        }
        void Photo(double x, double y, double width = 42, double height = 32)
        {
            drawing.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(220, 242, 240)), line, new Rect(x, y, width, height), 2, 2);
            drawing.DrawEllipse(teal, null, new Point(x + width * .75, y + height * .27), height * .1, height * .1);
            var mountain = new StreamGeometry();
            using (var geometry = mountain.Open())
            {
                geometry.BeginFigure(new Point(x + width * .1, y + height * .8), true, true);
                geometry.PolyLineTo([new Point(x + width * .4, y + height * .32), new Point(x + width * .65, y + height * .8)], true, false);
            }
            drawing.DrawGeometry(teal, null, mountain);
        }
        void Document(double x, bool photo = false, bool watermark = false)
        {
            drawing.DrawRoundedRectangle(Brushes.White, line, new Rect(x, 20, 48, 43), 3, 3);
            for (int i = 0; i < 3; i++) drawing.DrawLine(line, new Point(x + 7, 28 + i * 5), new Point(x + 40, 28 + i * 5));
            if (photo) Photo(x + 8, 43, 32, 14);
            else drawing.DrawRectangle(new SolidColorBrush(Color.FromRgb(220, 242, 240)), null, new Rect(x + 7, 46, 33, 10));
            if (watermark)
            {
                drawing.PushTransform(new RotateTransform(-28, x + 24, 42));
                drawing.PushOpacity(.45);
                Text("MINUTA", x + 2, 35, 10);
                drawing.Pop(); drawing.Pop();
            }
        }
        if (Operation is not (2 or 3))
        {
            switch (Operation)
            {
                case 0:
                    Text("PDF A + PDF B", 0, 0); Group(0, 1, 2); Text("+", 78, 28, 18); Group(102, 3, 4);
                    Arrow(190); Text("Um único PDF", 237, 0); Group(230, 1, 2, 3, 4);
                    Text("A", 27, 65); Text("B", 129, 65); Text("Páginas na ordem dos arquivos", 230, 65);
                    Text("Exemplo: juntar dois documentos, mantendo a ordem escolhida.", 0, 86);
                    break;
                case 1:
                    Text("PDF original", 0, 0); Group(0, 1, 2, 3, 4); Arrow(139);
                    Text("Partes dentro do limite", 191, 0); Group(185, 1, 2); Group(280, 3, 4);
                    Text("PDF 1", 199, 65); Text("PDF 2", 294, 65);
                    Text("Exemplo: limite de 50 MB, alterável. A divisão respeita páginas inteiras.", 0, 86);
                    break;
                case 4:
                    Text("Antes", 0, 0); Page(1, 0, 25, true);
                    drawing.PushTransform(new RotateTransform(90, 56.5, 41)); Page(2, 45, 25, true); drawing.Pop();
                    Page(3, 90, 25, true); Arrow(142); Text("Depois do giro", 198, 0); Group(192, 1, 2, 3);
                    Text("Página 2 deitada", 0, 65); Text("Página 2 corrigida", 192, 65);
                    Text("Exemplo: girar a página 2. Você pode definir um giro diferente por página.", 0, 86);
                    break;
                case 5:
                    Text("PDF original", 0, 0); Document(14); Text("Dados internos", 0, 65);
                    Arrow(134); Text("PDF otimizado", 192, 0); Document(210); Text("Dados compactados", 192, 65);
                    Text("Mesmas páginas e resolução. A redução depende do PDF original.", 0, 86);
                    break;
                case 6:
                    Text("PDF com imagens", 0, 0); Document(22, photo: true); Text("Mais detalhes", 0, 65);
                    Arrow(151); Text("PDF comprimido", 209, 0); Document(235, photo: true); Text("Qualidade escolhida", 209, 65);
                    Text("Imagens recomprimidas. Pode perder detalhes; o tamanho pode não diminuir.", 0, 86);
                    break;
                case 13:
                    Text("Digitalização", 0, 0); Document(22, photo: true); Arrow(151);
                    Text("PDF pesquisável", 209, 0); Document(235, photo: true);
                    Text("Texto como imagem", 0, 65); Text("Pesquisar e copiar texto", 209, 65);
                    Text("Reconhecer texto, mantendo a aparência. Revise palavras e números.", 0, 86);
                    break;
                case 12:
                    Text("PDF original", 0, 0); Document(22, photo: true); Arrow(151);
                    Text("Tentar chegar ao limite", 209, 0); Document(235, photo: true);
                    Text("Limite escolhido em MB", 0, 65); Text("Mesmo número de páginas", 209, 65);
                    Text("Maior qualidade → equilíbrio → menor tamanho. Confira o limite no resultado.", 0, 86);
                    break;
                case 7:
                    Text("PDF original", 0, 0); Document(18, photo: true); Arrow(107);
                    Text("Comprimir", 159, 0); Document(169, photo: true); Arrow(260);
                    Text("Dividir dentro do limite", 311, 0); Group(307, 1, 2); Group(397, 3, 4);
                    Text("Qualidade escolhida", 145, 65); Text("PDF 1", 324, 65); Text("PDF 2", 414, 65);
                    Text("Exemplo: recomprimir imagens e separar por páginas inteiras, até o limite.", 0, 86);
                    break;
                case 8:
                    Text("Imagens na ordem escolhida", 0, 0); Photo(0, 23); Photo(54, 23); Photo(108, 23);
                    Arrow(173); Text("Um único PDF", 227, 0); Group(221, 1, 2, 3);
                    Text("JPG, PNG, BMP ou TIFF", 0, 65); Text("Imagens ajustadas ao A4", 221, 65);
                    Text("Exemplo: reunir fotos e digitalizações, sem cortar as imagens.", 0, 86);
                    break;
                case 11:
                    Text("PDF original", 0, 0); Group(0, 1, 2, 3, 4); Arrow(142);
                    Text("PDF organizado", 198, 0); Group(192, 4, 1, 3);
                    Text("Conferir pelas miniaturas", 0, 65); Text("Nova ordem; página 2 excluída", 192, 65);
                    Text("Exemplo: reordenar e excluir páginas. As alterações podem ser desfeitas.", 0, 86);
                    break;
                case 10:
                    Text("PDF original", 0, 0); Group(0, 1, 2, 3); Arrow(118);
                    Text("PDF com numeração", 171, 0); Group(165, 1, 2, 3);
                    Text("1", 183, 49, 8); Text("2", 210, 49, 8); Text("3", 237, 49, 8);
                    Text("Sem números acrescentados", 0, 65); Text("Sequência no rodapé", 200, 65);
                    Text("Exemplo: numerar a partir de 1. Você pode deixar a capa sem número.", 0, 86);
                    break;
                case 9:
                    Text("PDF original", 0, 0); Document(22); Arrow(151);
                    Text("PDF com marca-d’água", 209, 0); Document(245, watermark: true);
                    Text("Sem marca", 0, 65); Text("Marca em todas as páginas", 209, 65);
                    Text("Exemplo: adicionar MINUTA. Texto, posição e opacidade são configuráveis.", 0, 86);
                    break;
            }
            drawing.Pop();
            return;
        }
        Text("PDF original", 0, 0);
        for (int i = 1; i <= 6; i++) Page(i, (i - 1) * 27, 22, i is 1 or 2 or 4 or 5);
        drawing.DrawText(new FormattedText("→", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 28, teal, VisualTreeHelper.GetDpi(this).PixelsPerDip), new Point(178, 20));
        Text(IsSplit ? "Dois arquivos separados" : "Um único arquivo", 229, 0);
        if (IsSplit)
        {
            drawing.DrawRoundedRectangle(Brushes.White, line, new Rect(223, 18, 67, 42), 5, 5);
            drawing.DrawRoundedRectangle(Brushes.White, line, new Rect(310, 18, 67, 42), 5, 5);
            Page(1, 230, 23, true); Page(2, 257, 23, true); Page(4, 317, 23, true); Page(5, 344, 23, true);
            Text("PDF 1", 239, 65); Text("PDF 2", 326, 65);
        }
        else
        {
            drawing.DrawRoundedRectangle(Brushes.White, line, new Rect(223, 18, 121, 42), 5, 5);
            int[] pages = [1, 2, 4, 5];
            for (int i = 0; i < pages.Length; i++) Page(pages[i], 230 + i * 27, 23, true);
            Text("Páginas reunidas no mesmo PDF", 225, 65);
        }
        Text("Exemplo de seleção: 1-2, 4-5", 0, 86);
        drawing.Pop();
    }
}
