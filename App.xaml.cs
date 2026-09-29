using BatchBaker.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Configuration;
using System.Data;
using System.Windows;

namespace BatchBaker
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private readonly IHost _host;
        public App()
        {
            _host = Host.CreateDefaultBuilder()
                .ConfigureServices((context, services) =>
                {
                    services.Configure<ADDepartments>(context.Configuration.GetSection("ActiveDirectory:Departments"));
                    services.AddSingleton<MainWindow>();
                    services.AddSingleton<ImportPreviewWindow>();
                    services.AddSingleton<ADService>();
                })
                .Build();
        }

        protected override async void OnStartup(StartupEventArgs e)
        {
            await _host.StartAsync();
            var mainWindow = _host.Services.GetRequiredService<MainWindow>();
            mainWindow.Show();
            base.OnStartup(e);
        }
        protected override async void OnExit(ExitEventArgs e)
        {
            using(_host)
            {
                await _host.StopAsync();
            }
            base.OnExit(e);
        }
    }

}
