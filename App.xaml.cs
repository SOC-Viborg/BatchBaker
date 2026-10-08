using BatchBaker.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace BatchBaker
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private IHost? _host;

        public App()
        {
            DispatcherUnhandledException += OnDispatcherUnhandledException;
        }

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            try
            {
                // QuestPDF refuses to generate anything until a license type is set.
                QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

                // Content root is the exe's folder, so appsettings.json is found
                // no matter which directory the app is launched from.
                _host = Host.CreateDefaultBuilder()
                    .UseContentRoot(AppContext.BaseDirectory)
                    .ConfigureServices((context, services) =>
                    {
                        services.Configure<ActiveDirectory>(context.Configuration.GetSection("ActiveDirectory"));
                        services.AddSingleton<MainWindow>();
                        services.AddSingleton<ImportPreviewWindow>();
                        services.AddSingleton<ADService>();
                    })
                    .Build();

                await _host.StartAsync();
                var mainWindow = _host.Services.GetRequiredService<MainWindow>();
                mainWindow.Show();
            }
            catch (Exception ex)
            {
                ReportError("BatchBaker kunne ikke starte", ex);
                Shutdown(1);
            }
        }

        protected override async void OnExit(ExitEventArgs e)
        {
            if (_host != null)
            {
                using (_host)
                {
                    await _host.StopAsync();
                }
            }
            base.OnExit(e);
        }

        // Catches errors thrown later while the app is running (e.g. in a button
        // handler) so they're shown and recorded instead of closing the app silently.
        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            ReportError("Der opstod en uventet fejl", e.Exception);
            e.Handled = true;
        }

        // Writes the full error to crash.log next to the exe (falling back to the
        // temp folder if that isn't writable) and shows it in a message box.
        private static void ReportError(string title, Exception ex)
        {
            string details = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {title}{Environment.NewLine}{ex}{Environment.NewLine}{Environment.NewLine}";
            string logPath = Path.Combine(AppContext.BaseDirectory, "crash.log");
            try
            {
                File.AppendAllText(logPath, details);
            }
            catch
            {
                logPath = Path.Combine(Path.GetTempPath(), "BatchBaker-crash.log");
                try { File.AppendAllText(logPath, details); } catch { logPath = "(kunne ikke skrives)"; }
            }

            MessageBox.Show($"{ex.GetType().Name}: {ex.Message}{Environment.NewLine}{Environment.NewLine}Alle detaljer: {logPath}",
                title, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
