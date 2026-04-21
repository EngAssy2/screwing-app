using System.IO;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using ScrewingHub.App.ViewModels;
using ScrewingHub.Communication.Dtm10;
using ScrewingHub.Communication.OmronPlc;
using ScrewingHub.Core.Interfaces;
using ScrewingHub.Core.Services;
using Serilog;

namespace ScrewingHub.App;

public partial class App : Application
{
    private ServiceProvider? _serviceProvider;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Setup Serilog
        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ScrewingHub");
        Directory.CreateDirectory(appDataPath);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File(
                Path.Combine(appDataPath, "logs", "app-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        Log.Information("=== ScrewingHub Application Starting ===");

        try
        {
            // Load settings
            var settingsPath = Path.Combine(appDataPath, "appsettings.json");
            var configService = new ModelConfigService(settingsPath);
            configService.Load();

            // Create services
            var services = new ServiceCollection();

            // Singletons
            services.AddSingleton(configService);
            services.AddSingleton(configService.Settings);
            services.AddSingleton<IJudgmentService, JudgmentService>();
            services.AddSingleton<ICsvLogService>(sp =>
                new CsvLogService(
                    configService.Settings,
                    configService.Settings.CsvLogFolder));
            services.AddSingleton<IUnitLogService, UnitLogService>();

            // Communication clients
            services.AddSingleton<IDtm10Client>(sp =>
                new Dtm10Client(
                    configService.Settings.Dtm10Serial.PortName,
                    configService.Settings.ReconnectIntervalMs));

            services.AddSingleton<IPlcClient>(sp =>
            {
                var plcSettings = configService.Settings.PlcSerial;
                return new HostLinkClient(
                    plcSettings.PortName,
                    plcSettings.BaudRate,
                    plcSettings.NodeAddress,
                    plcSettings.DmStartAddress,
                    plcSettings.CommandTimeoutMs,
                    plcSettings.HeartbeatIntervalMs,
                    plcSettings.PollingIntervalMs,
                    configService.Settings.ReconnectIntervalMs);
            });

            // ViewModels
            services.AddSingleton<DashboardViewModel>();
            services.AddSingleton<SettingsViewModel>();
            services.AddSingleton<HistoryViewModel>();

            // Main Window
            services.AddSingleton<MainWindow>();

            _serviceProvider = services.BuildServiceProvider();

            // Wire up DTM10 and PLC to Dashboard
            var dashboardVm = _serviceProvider.GetRequiredService<DashboardViewModel>();
            var dtm10 = _serviceProvider.GetRequiredService<IDtm10Client>();
            var plc = _serviceProvider.GetRequiredService<IPlcClient>();
            dashboardVm.SetDevices(dtm10, plc);
            dashboardVm.OperatorId = configService.Settings.DefaultOperatorId;

            // Show main window
            var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
            mainWindow.Show();

            Log.Information("Application started successfully");
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Application failed to start");
            MessageBox.Show($"Failed to start application:\n{ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Log.Information("=== ScrewingHub Application Shutting Down ===");

        // Dispose communication clients
        if (_serviceProvider != null)
        {
            try
            {
                _serviceProvider.GetService<IDtm10Client>()?.Dispose();
                _serviceProvider.GetService<IPlcClient>()?.Dispose();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error during shutdown");
            }

            _serviceProvider.Dispose();
        }

        Log.CloseAndFlush();
        base.OnExit(e);
    }
}
