using Ladeco.Diag.Application.Abstractions;
using Ladeco.Diag.Infrastructure.Diagnostics;
using Ladeco.Diag.Infrastructure.Persistence;
using Ladeco.Diag.Infrastructure.System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace Ladeco.Diag.Infrastructure.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string sqliteConnectionString)
    {
        services.AddDbContext<LadecoDiagDbContext>(options => 
        {
            options.UseSqlite(sqliteConnectionString);
            options.ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.PendingModelChangesWarning));
        });

        services.AddSingleton<ISystemSnapshotProvider, SystemSnapshotProvider>();
        services.AddSingleton<IHardwareInventoryProvider, HardwareInventoryProvider>();
        services.AddSingleton<IHardwareTelemetryProvider, LibreHardwareTelemetryProvider>();
        services.AddScoped<IScanHistoryRepository, ScanHistoryRepository>();

        services.AddScoped<IDiagnosticModule, SystemHealthModule>();
        services.AddScoped<IDiagnosticModule, InternetDiagnosticsModule>();
        services.AddScoped<IDiagnosticModule, SecurityDiagnosticsModule>();
        services.AddScoped<IDiagnosticModule, HardwareInventoryModule>();
        services.AddScoped<IDiagnosticModule, PrinterDiagnosticsModule>();
        services.AddScoped<IDiagnosticModule, OfficeDiagnosticsModule>();
        services.AddScoped<IDiagnosticModule, BrowserDiagnosticsModule>();
        services.AddScoped<IDiagnosticModule, SoftwareInventoryModule>();
        services.AddScoped<IDiagnosticModule, DriverDiagnosticsModule>();
        services.AddScoped<IDiagnosticModule, WindowsIntegrityModule>();
        services.AddScoped<IDiagnosticModule, SmartDiskDiagnosticsModule>();
        services.AddScoped<IDiagnosticModule, TemperatureDiagnosticsModule>();
        services.AddScoped<IDiagnosticModule, PerformanceAndReliabilityModule>();

        return services;
    }
}
