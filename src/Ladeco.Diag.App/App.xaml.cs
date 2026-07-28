using System.Windows;
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

public partial class App : System.Windows.Application
{
    private IHost? _host;

    protected override async void OnStartup(StartupEventArgs e)
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

                services.AddSingleton(appOptions);
                services.AddSingleton(storageOptions);

                services.AddSingleton<ILocalizationService>(InMemoryLocalizationService.DutchDefaults());
                services.AddSingleton<IThemeService, ThemeService>();
                services.AddSingleton<IConfirmationDialogService, ConfirmationDialogService>();
                services.AddSingleton<IRemediationService, RemediationService>();
                services.AddSingleton<IFindingExplanationService, FindingExplanationService>();
                services.AddSingleton<IReportExporter, ReportExporter>();

                services.AddScoped<IDiagnosticsOrchestrator, DiagnosticsOrchestrator>();
                services.AddInfrastructure(storageOptions.SqliteConnectionString);

                services.AddSingleton<MainViewModel>();
                services.AddSingleton<MainWindow>();
            })
            .Build();

        await _host.StartAsync();

        using (var scope = _host.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LadecoDiagDbContext>();
            await db.Database.MigrateAsync();
        }

        var window = _host.Services.GetRequiredService<MainWindow>();
        window.Show();

        base.OnStartup(e);
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }

        Log.CloseAndFlush();
        base.OnExit(e);
    }
}
