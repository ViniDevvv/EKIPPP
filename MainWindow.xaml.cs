using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using EKIPPP.ViewModels;
using WinForms = System.Windows.Forms;

namespace EKIPPP;

public partial class MainWindow : Window
{
    private bool   _isMaximized;
    private double _restoreLeft, _restoreTop, _restoreWidth, _restoreHeight;
    private WinForms.NotifyIcon _trayIcon = null!;
    private ScrollViewer? _activeTabPanel;

    public MainWindow()
    {
        InitializeComponent();
        TryLoadTitleLogo();
        InitTray();
        _activeTabPanel = Tab0Panel;

        if (DataContext is MainViewModel vm)
        {
            vm.ShowToast = (title, msg) =>
                _trayIcon.ShowBalloonTip(3000, title, msg, WinForms.ToolTipIcon.Info);
            vm.PropertyChanged += OnVmPropertyChanged;
        }
    }

    // ── Tab fade animation ─────────────────────────────────────────────
    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(MainViewModel.SelectedTab)) return;
        if (sender is not MainViewModel vm) return;

        ScrollViewer? newPanel = vm.SelectedTab switch
        {
            0 => Tab0Panel,
            1 => Tab1Panel,
            2 => Tab2Panel,
            3 => Tab3Panel,
            4 => Tab4Panel,
            _ => null
        };

        if (newPanel == null || newPanel == _activeTabPanel) return;

        var oldPanel = _activeTabPanel;
        _activeTabPanel = newPanel;

        if (oldPanel != null)
        {
            var fadeOut = new DoubleAnimation(1, 0, new Duration(TimeSpan.FromMilliseconds(110)));
            fadeOut.Completed += (_, _) => oldPanel.Visibility = Visibility.Collapsed;
            oldPanel.BeginAnimation(UIElement.OpacityProperty, fadeOut);
        }

        newPanel.Opacity = 0;
        newPanel.Visibility = Visibility.Visible;
        var fadeIn = new DoubleAnimation(0, 1, new Duration(TimeSpan.FromMilliseconds(180)));
        fadeIn.BeginTime = TimeSpan.FromMilliseconds(60);
        newPanel.BeginAnimation(UIElement.OpacityProperty, fadeIn);
    }

    // ── Drag & Drop (installation cards) ──────────────────────────────
    private void InstallCard_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private async void InstallCard_Drop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
        var paths = (string[])e.Data.GetData(DataFormats.FileDrop);
        if (paths.Length == 0) return;
        if (DataContext is not MainViewModel vm) return;

        var tag = GetInstallCardTag(sender);
        await vm.InstallFromDropAsync(paths, tag);
    }

    private static string GetInstallCardTag(object sender)
    {
        var element = sender as System.Windows.DependencyObject;
        while (element != null)
        {
            if (element is FrameworkElement fe && fe.Tag is string t && !string.IsNullOrEmpty(t))
                return t;
            element = System.Windows.Media.VisualTreeHelper.GetParent(element);
        }
        return "";
    }

    private void TryLoadTitleLogo()
    {
        try
        {
            var si = System.Windows.Application.GetResourceStream(
                new Uri("pack://application:,,,/Assets/logo.png"));
            if (si == null) return;
            var bmp = new System.Windows.Media.Imaging.BitmapImage();
            bmp.BeginInit();
            bmp.StreamSource = si.Stream;
            bmp.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
            bmp.EndInit();
            bmp.Freeze();
            TitleLogoImage.Source = bmp;
            TitleLogoImage.Visibility = Visibility.Visible;
            TitleLogoFallback.Visibility = Visibility.Collapsed;
            Icon = bmp;
        }
        catch { }
    }

    private void InitTray()
    {
        var bmp = new System.Drawing.Bitmap(16, 16);
        using (var g = System.Drawing.Graphics.FromImage(bmp))
        {
            g.FillRectangle(
                new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(124, 58, 237)),
                0, 0, 16, 16);
            g.DrawString("E",
                new System.Drawing.Font("Arial", 8f, System.Drawing.FontStyle.Bold),
                System.Drawing.Brushes.White, 1f, 1f);
        }

        _trayIcon = new WinForms.NotifyIcon
        {
            Icon    = System.Drawing.Icon.FromHandle(bmp.GetHicon()),
            Text    = "EKIPPP – Optimiseur FiveM",
            Visible = true
        };

        var menu = new WinForms.ContextMenuStrip();
        menu.Items.Add("Ouvrir EKIPPP", null, (_, _) => RestoreWindow());
        menu.Items.Add(new WinForms.ToolStripSeparator());
        menu.Items.Add("Quitter", null, (_, _) =>
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
            Application.Current.Shutdown();
        });

        _trayIcon.ContextMenuStrip = menu;
        _trayIcon.DoubleClick     += (_, _) => RestoreWindow();
    }

    private void RestoreWindow()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
    }

    private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) return;
        try
        {
            var element = e.OriginalSource as DependencyObject;
            while (element != null)
            {
                if (element is System.Windows.Controls.Button) return;
                if (element is not System.Windows.Media.Visual) break;
                element = System.Windows.Media.VisualTreeHelper.GetParent(element);
            }

            if (_isMaximized)
            {
                var screen = WinForms.Cursor.Position;
                double ratio = e.GetPosition(this).X / ActualWidth;
                _isMaximized = false;
                RootBorder.CornerRadius = new CornerRadius(16);
                Left   = screen.X - _restoreWidth  * ratio;
                Top    = screen.Y - 20;
                Width  = _restoreWidth;
                Height = _restoreHeight;
                UpdateMaximizeIcon();
            }

            DragMove();
        }
        catch { }
    }

    private void TitleBar_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
            ToggleMaximize();
    }

    private void MaximizeClick(object sender, RoutedEventArgs e) => ToggleMaximize();

    private void UpdateMaximizeIcon()
    {
        var tb = MaximizeButton.Template.FindName("MaxIco", MaximizeButton) as System.Windows.Controls.TextBlock;
        if (tb != null)
            tb.Text = _isMaximized ? "" : ""; // ChromeRestore vs ChromeMaximize
    }
    private void DiscordLink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
    {
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }

    private void MinimizeClick(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void CloseClick(object sender, RoutedEventArgs e)
    {
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        Application.Current.Shutdown();
    }

    private void ToggleMaximize()
    {
        if (_isMaximized)
        {
            Left   = _restoreLeft;
            Top    = _restoreTop;
            Width  = _restoreWidth;
            Height = _restoreHeight;
            RootBorder.CornerRadius = new CornerRadius(16);
            _isMaximized = false;
        }
        else
        {
            _restoreLeft   = Left;
            _restoreTop    = Top;
            _restoreWidth  = Width;
            _restoreHeight = Height;

            var area = SystemParameters.WorkArea;
            Left   = area.Left;
            Top    = area.Top;
            Width  = area.Width;
            Height = area.Height;
            RootBorder.CornerRadius = new CornerRadius(0);
            _isMaximized = true;
        }
        UpdateMaximizeIcon();
    }
}
