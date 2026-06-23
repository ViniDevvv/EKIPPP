using System.Windows;
using System.Windows.Input;
using EKIPPP.Services;

namespace EKIPPP.Windows;

public partial class ActivationWindow : Window
{
    private readonly LicenseService _license = new();
    private bool _isActivating = false;

    public ActivationWindow()
    {
        InitializeComponent();
        KeyInput.Focus();
    }

    private void KeyInput_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        // Auto-format XXXX-XXXX-XXXX-XXXX
        var raw = KeyInput.Text.Replace("-", "").ToUpper();
        if (raw.Length > 16) raw = raw[..16];

        var formatted = string.Join("-", Enumerable.Range(0, (raw.Length + 3) / 4)
            .Select(i => raw.Substring(i * 4, Math.Min(4, raw.Length - i * 4))));

        if (KeyInput.Text != formatted)
        {
            KeyInput.TextChanged -= KeyInput_TextChanged;
            KeyInput.Text = formatted;
            KeyInput.CaretIndex = formatted.Length;
            KeyInput.TextChanged += KeyInput_TextChanged;
        }

        ErrorText.Visibility = Visibility.Collapsed;
        ActivateBtn.IsEnabled = raw.Length == 16;
    }

    private async void ActivateClick(object sender, RoutedEventArgs e)
    {
        if (_isActivating) return;
        _isActivating = true;
        ActivateBtn.IsEnabled = false;
        BtnLabel.Text = "Vérification…";
        ErrorText.Visibility = Visibility.Collapsed;

        var key    = KeyInput.Text.Trim();
        var result = await _license.ValidateAsync(key);

        switch (result)
        {
            case LicenseResult.Activated:
            case LicenseResult.Valid:
                _license.SaveLicense(key);
                DialogResult = true;
                Close();
                break;

            case LicenseResult.HwidMismatch:
                ShowError("Cette clé est déjà utilisée sur un autre PC.\nContacte le support sur Discord.");
                break;

            case LicenseResult.Invalid:
                ShowError("Clé invalide. Vérifie que tu l'as bien copiée.");
                break;

            case LicenseResult.NetworkError:
                ShowError("Impossible de se connecter au serveur.\nVérifie ta connexion internet.");
                break;
        }

        BtnLabel.Text = "ACTIVER LA LICENCE";
        ActivateBtn.IsEnabled = true;
        _isActivating = false;
    }

    private void ShowError(string msg)
    {
        ErrorText.Text = msg;
        ErrorText.Visibility = Visibility.Visible;
    }

    private void CloseClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void DiscordLink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
    {
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }
}
