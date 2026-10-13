using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FPDF.App;

internal sealed class StartupSplash : Window
{
    public StartupSplash()
    {
        double side = Math.Min(420, Math.Min(SystemParameters.WorkArea.Width, SystemParameters.WorkArea.Height) * .75);
        Width = Height = side;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        ShowActivated = false;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Title = "FPDF — iniciando";
        Background = Brushes.White;
        Content = new Image
        {
            Source = new BitmapImage(new Uri("pack://application:,,,/FPDF;component/Assets/FPDF.jpg")),
            Stretch = Stretch.Uniform
        };
    }
}
