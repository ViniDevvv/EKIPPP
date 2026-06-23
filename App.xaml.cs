using System.Threading;
using System.Windows;
using System.Windows.Threading;
using EKIPPP.Services;
using EKIPPP.Windows;

namespace EKIPPP;

public partial class App : Application
{
    private static Mutex? _instanceMutex;

    private void App_Startup(object sender, StartupEventArgs e)
    {
        DispatcherUnhandledException += (_, ex) =>
        {
            MessageBox.Show(ex.Exception.ToString(), "EKIPPP – Erreur au démarrage",
                MessageBoxButton.OK, MessageBoxImage.Error);
            ex.Handled = true;
            Shutdown(1);
        };
        AppDomain.CurrentDomain.UnhandledException += (_, ex) =>
        {
            MessageBox.Show(ex.ExceptionObject?.ToString(), "EKIPPP – Erreur critique",
                MessageBoxButton.OK, MessageBoxImage.Error);
        };

        // Instance unique
        _instanceMutex = new Mutex(true, "EKIPPP_SingleInstance_v1", out bool isFirstInstance);
        if (!isFirstInstance)
        {
            MessageBox.Show("EKIPPP est déjà en cours d'exécution.\nVérifiez dans la barre des tâches (icône en bas à droite).",
                "EKIPPP", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        // Vérification de licence
        var license = new LicenseService();
        if (!license.HasLocalLicense())
        {
            var activation = new ActivationWindow();
            if (activation.ShowDialog() != true)
            {
                Shutdown();
                return;
            }
        }

        // Read FirstRun before ViewModel instantiates and resets it
        var preSettings = new SettingsService();
        preSettings.Load();
        bool isFirstRun = preSettings.Current.FirstRun;

        var splash = new SplashWindow();
        splash.Show();

        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();

            var main = new MainWindow();
            MainWindow = main;
            main.Show();   // Show first so there's always a window open

            splash.Close(); // Close splash after main is visible

            if (isFirstRun)
            {
                var welcome = new WelcomeWindow { Owner = main };
                welcome.ShowDialog();
            }
        };
        timer.Start();
    }
}
