using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

namespace EKIPPP.Windows;

public partial class SplashWindow : Window
{
    public SplashWindow()
    {
        InitializeComponent();
        TryLoadLogo();
        Loaded += OnLoaded;
    }

    private void TryLoadLogo()
    {
        try
        {
            var si = System.Windows.Application.GetResourceStream(
                new Uri("pack://application:,,,/Assets/logo.png"));
            if (si == null) throw new Exception();
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.StreamSource = si.Stream;
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.EndInit();
            bmp.Freeze();
            LogoImage.Source = bmp;
        }
        catch
        {
            LogoImage.Visibility = Visibility.Collapsed;
            FallbackText.Visibility = Visibility.Visible;
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var anim = new DoubleAnimation(0, 300, TimeSpan.FromSeconds(1.8))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
        };
        ProgressBar.BeginAnimation(WidthProperty, anim);

        var statuses = new[] { "Détection de FiveM…", "Chargement des stats…", "Prêt !" };
        var timer = new System.Windows.Threading.DispatcherTimer
            { Interval = TimeSpan.FromMilliseconds(600) };
        int i = 0;
        timer.Tick += (_, _) =>
        {
            if (i < statuses.Length) StatusText.Text = statuses[i++];
            else timer.Stop();
        };
        timer.Start();
    }
}
