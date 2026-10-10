using System.Windows;
using System.Windows.Threading;

namespace MediaGlass;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        base.OnStartup(e);

        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000))
        {
            MessageBox.Show(
                "Media Glass requires Windows 11.",
                AppStrings.ProductName,
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            Shutdown();
            return;
        }

        var window = new MainWindow();
        MainWindow = window;
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        DispatcherUnhandledException -= OnDispatcherUnhandledException;
        base.OnExit(e);
    }

    private static void OnDispatcherUnhandledException(
        object sender,
        DispatcherUnhandledExceptionEventArgs eventArgs)
    {
        MessageBox.Show(
            eventArgs.Exception.Message,
            AppStrings.ProductName,
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        eventArgs.Handled = true;
    }
}
