using Microsoft.UI.Xaml;
using Ladeco.Diag.App.Models;
using Ladeco.Diag.App.Services;
using Ladeco.Diag.App.ViewModels;
using Ladeco.Diag.Application.Abstractions;
using Ladeco.Diag.Application.Diagnostics;
using Ladeco.Diag.Application.Localization;
using Ladeco.Diag.Infrastructure.Extensions;
using Ladeco.Diag.Infrastructure.Persistence;
using Ladeco.Diag.Reporting.Abstractions;
using Ladeco.Diag.Reporting.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace Ladeco.Diag.App;

public partial class App : Microsoft.UI.Xaml.Application
{
    private IHost? _host;
    public static MainWindow? MainWindow { get; private set; }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(configuration)
            .CreateLogger();

        _host = Host.CreateDefaultBuilder()
            .UseSerilog()
            .ConfigureServices((_, services) =>
            {
                var appOptions = configuration.GetSection("App").Get<AppOptions>() ?? new AppOptions();
                var storageOptions = configuration.GetSection("Storage").Get<StorageOptions>() ?? new StorageOptions();

                var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                var appFolder = System.IO.Path.Combine(appData, "Ladeco", "Diag");
                
                // Ensure an writable absolute path for SQLite (e.g., LocalAppData) to prevent "unable to open database file" when installed in Program Files
                var dbPath = storageOptions.SqliteConnectionString.Replace("Data Source=", "");
                if (!System.IO.Path.IsPathRooted(dbPath))
                {
                    System.IO.Directory.CreateDirectory(appFolder);
                    dbPath = System.IO.Path.Combine(appFolder, dbPath);
                }
                var connectionString = $"Data Source={dbPath}";

                // Also ensure a writable path for PDF exports
                var reportPath = storageOptions.ReportOutputDirectory;
                if (!System.IO.Path.IsPathRooted(reportPath))
                {
                    reportPath = System.IO.Path.Combine(appFolder, reportPath);
                }
                var activeStorageOptions = new StorageOptions
                {
                    SqliteConnectionString = storageOptions.SqliteConnectionString,
                    ReportOutputDirectory = reportPath
                };

                services.AddSingleton(appOptions);
                services.AddSingleton(activeStorageOptions);

                services.AddSingleton<ILocalizationService>(InMemoryLocalizationService.DutchDefaults());
                services.AddSingleton<IThemeService, ThemeService>();
                services.AddSingleton<IConfirmationDialogService, ConfirmationDialogService>();
                services.AddSingleton<ILocalUserAccountService, LocalUserAccountService>();
                services.AddSingleton<IRemediationService, RemediationService>();
                services.AddSingleton<IFindingExplanationService, FindingExplanationService>();
                services.AddSingleton<IReportExporter, ReportExporter>();

                services.AddScoped<IDiagnosticsOrchestrator, DiagnosticsOrchestrator>();
                services.AddInfrastructure(connectionString);

                services.AddSingleton<MainViewModel>();
                services.AddSingleton<MainWindow>();
            })
            .Build();

        await _host.StartAsync();

        using (var scope = _host.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LadecoDiagDbContext>();
            // Use EnsureCreatedAsync since there are no migrations in the project
            await db.Database.EnsureCreatedAsync();
        }

        MainWindow = _host.Services.GetRequiredService<MainWindow>();
        MainWindow.Closed += (_, _) => _ = StopHostAsync();
        MainWindow.Activate();
    }

    private async Task StopHostAsync()
    {
        if (_host is not null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }

        Log.CloseAndFlush();
    }
}
