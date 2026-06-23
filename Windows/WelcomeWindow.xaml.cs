using System.Windows;
using System.Windows.Media;
using EKIPPP.Services;

namespace EKIPPP.Windows;

public partial class WelcomeWindow : Window
{
    private int _step = 0;
    private readonly FiveMService _fiveM = new FiveMService();

    public WelcomeWindow()
    {
        InitializeComponent();
        UpdateStep();
    }

    private void UpdateStep()
    {
        Step0Panel.Visibility = _step == 0 ? Visibility.Visible : Visibility.Collapsed;
        Step1Panel.Visibility = _step == 1 ? Visibility.Visible : Visibility.Collapsed;
        Step2Panel.Visibility = _step == 2 ? Visibility.Visible : Visibility.Collapsed;
        Step3Panel.Visibility = _step == 3 ? Visibility.Visible : Visibility.Collapsed;

        var purple = new LinearGradientBrush();
        purple.StartPoint = new System.Windows.Point(0, 0);
        purple.EndPoint   = new System.Windows.Point(1, 0);
        purple.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#A855F7"), 0));
        purple.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#7C3AED"), 1));

        var active   = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#A855F7"));
        var inactive = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2A2A45"));

        Dot0.Fill = _step >= 0 ? purple   : inactive;
        Dot1.Fill = _step >= 1 ? active   : inactive;
        Dot2.Fill = _step >= 2 ? active   : inactive;
        Dot3.Fill = _step >= 3 ? active   : inactive;

        PrevButton.Visibility = _step > 0 ? Visibility.Visible : Visibility.Collapsed;

        NextButton.Content = _step == 3 ? "Commencer" : "Suivant →";

        // Bloquer Suivant sur l'étape licence si non accepté
        NextButton.IsEnabled = _step != 0 || (AcceptCheckBox.IsChecked == true);

        if (_step == 2)
        {
            bool installed = _fiveM.IsFiveMInstalled();
            FiveMFoundPanel.Visibility    = installed ? Visibility.Visible  : Visibility.Collapsed;
            FiveMNotFoundPanel.Visibility = installed ? Visibility.Collapsed : Visibility.Visible;
            if (installed) FiveMPathText.Text = _fiveM.FiveMAppPath;
        }
    }

    private void AcceptCheckBox_Changed(object sender, RoutedEventArgs e)
        => NextButton.IsEnabled = AcceptCheckBox.IsChecked == true;

    private void NextClick(object sender, RoutedEventArgs e)
    {
        if (_step < 3) { _step++; UpdateStep(); }
        else           { DialogResult = true; Close(); }
    }

    private void PrevClick(object sender, RoutedEventArgs e)
    {
        if (_step > 0) { _step--; UpdateStep(); }
    }
}
