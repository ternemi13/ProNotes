using System.Windows;
using System.IO;
using System.Windows.Threading;

namespace CuadernoDigital.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        LogException(e.Exception);
        MessageBox.Show(
            $"ProNotes encontro un error y no pudo completar la accion.\n\n{e.Exception.Message}",
            "Error de ProNotes",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true;
    }

    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception exception)
        {
            LogException(exception);
        }
    }

    private static void LogException(Exception exception)
    {
        try
        {
            var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "ProNotes");
            Directory.CreateDirectory(directory);
            var logPath = Path.Combine(directory, "pronotes-errors.log");
            File.AppendAllText(logPath, $"[{DateTimeOffset.Now:u}]\n{exception}\n\n");
        }
        catch
        {
            // Avoid crashing while reporting a crash.
        }
    }
}
