using System.Collections.ObjectModel;
using System.IO.Ports;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ScrewingHub.Communication.Common;
using ScrewingHub.Core.Interfaces;
using ScrewingHub.Core.Models;
using ScrewingHub.Core.Services;
using Serilog;

namespace ScrewingHub.App.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private static readonly ILogger Logger = Log.ForContext<SettingsViewModel>();
    private readonly ModelConfigService _configService;
    private readonly IUnitLogService _unitLogService;
    private readonly ICsvLogService _csvLogService;
    private readonly DashboardViewModel _dashboardVm;

    // ===== COM PORTS =====
    [ObservableProperty] private ObservableCollection<string> _availablePorts = new();

    // ===== DTM10 SETTINGS =====
    [ObservableProperty] private string _dtm10Port = "COM3";
    [ObservableProperty] private int _dtm10BaudRate = 38400;

    // ===== PLC SETTINGS =====
    [ObservableProperty] private string _plcPort = "COM4";
    [ObservableProperty] private int _plcBaudRate = 9600;
    [ObservableProperty] private int _plcNodeAddress = 0;
    [ObservableProperty] private int _plcDmStartAddress = 100;
    [ObservableProperty] private int _plcHeartbeatInterval = 200;
    [ObservableProperty] private int _plcPollingInterval = 500;

    // ===== LOGGING =====
    [ObservableProperty] private string _csvLogFolder = @"D:\ScrewingHub\Logs";
    [ObservableProperty] private string _csvLogPattern = "[Status]_[ModelNumber]_[Date]_[StationName].csv";

    // ===== GENERAL =====
    [ObservableProperty] private string _operatorId = string.Empty;
    [ObservableProperty] private string _ngSoundFile = string.Empty;

    // ===== MODEL A =====
    [ObservableProperty] private string _modelAName = "Model A";
    [ObservableProperty] private string _modelAStationName = string.Empty;
    [ObservableProperty] private int _modelAScrewCount = 4;
    [ObservableProperty] private int _modelAChannel = 1;
    [ObservableProperty] private double _modelATorqueUpper = 2.5;
    [ObservableProperty] private double _modelATorqueLower = 1.5;
    [ObservableProperty] private int _modelATimeUpper = 1500;
    [ObservableProperty] private int _modelATimeLower = 300;
    [ObservableProperty] private double _modelATorqueFactor = 0.001098;
    [ObservableProperty] private string _modelAScrewName = "Screw A-1";

    // ===== MODEL B =====
    [ObservableProperty] private string _modelBName = "Model B";
    [ObservableProperty] private string _modelBStationName = string.Empty;
    [ObservableProperty] private int _modelBScrewCount = 6;
    [ObservableProperty] private int _modelBChannel = 1;
    [ObservableProperty] private double _modelBTorqueUpper = 3.2;
    [ObservableProperty] private double _modelBTorqueLower = 2.0;
    [ObservableProperty] private int _modelBTimeUpper = 2000;
    [ObservableProperty] private int _modelBTimeLower = 500;
    [ObservableProperty] private double _modelBTorqueFactor = 0.002475;
    [ObservableProperty] private string _modelBScrewName = "Screw B-1";

    [ObservableProperty] private string _statusMessage = string.Empty;

    public SettingsViewModel(
        ModelConfigService configService,
        IUnitLogService unitLogService,
        ICsvLogService csvLogService,
        DashboardViewModel dashboardVm)
    {
        _configService = configService;
        _unitLogService = unitLogService;
        _csvLogService = csvLogService;
        _dashboardVm = dashboardVm;
        RefreshPorts();
        LoadFromSettings();
    }

    [RelayCommand]
    private void RefreshPorts()
    {
        AvailablePorts.Clear();
        foreach (var port in SerialPortWrapper.GetAvailablePorts().OrderBy(p => p))
        {
            AvailablePorts.Add(port);
        }
    }

    private void LoadFromSettings()
    {
        var s = _configService.Settings;

        Dtm10Port = s.Dtm10Serial.PortName;
        Dtm10BaudRate = s.Dtm10Serial.BaudRate;

        PlcPort = s.PlcSerial.PortName;
        PlcBaudRate = s.PlcSerial.BaudRate;
        PlcNodeAddress = s.PlcSerial.NodeAddress;
        PlcDmStartAddress = s.PlcSerial.DmStartAddress;
        PlcHeartbeatInterval = s.PlcSerial.HeartbeatIntervalMs;
        PlcPollingInterval = s.PlcSerial.PollingIntervalMs;

        CsvLogFolder = s.CsvLogFolder;
        CsvLogPattern = s.CsvFileNamePattern;
        OperatorId = s.DefaultOperatorId;
        NgSoundFile = s.NgSoundFilePath;

        if (s.Models.Count >= 1)
        {
            var ma = s.Models[0];
            ModelAName = ma.ModelName;
            ModelAStationName = ma.StationName;
            ModelAScrewCount = ma.TotalScrewCount;
            if (ma.Channels.Count > 0)
            {
                var ch = ma.Channels[0];
                ModelAChannel = ch.ChannelNumber;
                ModelATorqueFactor = ch.TorqueConversionFactor > 0 ? ch.TorqueConversionFactor : 0.001;
                ModelATorqueUpper = Math.Round(ch.CurrentValueUpperLimit * ModelATorqueFactor, 3);
                ModelATorqueLower = Math.Round(ch.CurrentValueLowerLimit * ModelATorqueFactor, 3);
                ModelATimeUpper = ch.TimeUpperLimitMs;
                ModelATimeLower = ch.TimeLowerLimitMs;
                ModelAScrewName = ch.ScrewName;
            }
        }

        if (s.Models.Count >= 2)
        {
            var mb = s.Models[1];
            ModelBName = mb.ModelName;
            ModelBStationName = mb.StationName;
            ModelBScrewCount = mb.TotalScrewCount;
            if (mb.Channels.Count > 0)
            {
                var ch = mb.Channels[0];
                ModelBChannel = ch.ChannelNumber;
                ModelBTorqueFactor = ch.TorqueConversionFactor > 0 ? ch.TorqueConversionFactor : 0.001;
                ModelBTorqueUpper = Math.Round(ch.CurrentValueUpperLimit * ModelBTorqueFactor, 3);
                ModelBTorqueLower = Math.Round(ch.CurrentValueLowerLimit * ModelBTorqueFactor, 3);
                ModelBTimeUpper = ch.TimeUpperLimitMs;
                ModelBTimeLower = ch.TimeLowerLimitMs;
                ModelBScrewName = ch.ScrewName;
            }
        }
    }

    [RelayCommand]
    private void Save()
    {
        if (!ScrewingHub.App.Views.ConfirmationDialog.Show("Are you sure you want to save the settings?", "Confirm Save"))
        {
            return;
        }

        try
        {
            var s = _configService.Settings;

            s.Dtm10Serial.PortName = Dtm10Port;
            s.Dtm10Serial.BaudRate = Dtm10BaudRate;

            s.PlcSerial.PortName = PlcPort;
            s.PlcSerial.BaudRate = PlcBaudRate;
            s.PlcSerial.NodeAddress = PlcNodeAddress;
            s.PlcSerial.DmStartAddress = PlcDmStartAddress;
            s.PlcSerial.HeartbeatIntervalMs = PlcHeartbeatInterval;
            s.PlcSerial.PollingIntervalMs = PlcPollingInterval;

            s.CsvLogFolder = CsvLogFolder;
            s.CsvFileNamePattern = CsvLogPattern;
            s.DefaultOperatorId = OperatorId;
            s.NgSoundFilePath = NgSoundFile;

            // Model A
            while (s.Models.Count < 2)
                s.Models.Add(new ProductModel());

            s.Models[0].ModelId = 1;
            s.Models[0].ModelName = ModelAName;
            s.Models[0].StationName = ModelAStationName;
            s.Models[0].TotalScrewCount = ModelAScrewCount;
            s.Models[0].IsEnabled = true;
            s.Models[0].Channels = new List<ChannelConfig>
            {
                new()
                {
                    ChannelNumber = ModelAChannel,
                    ScrewName = ModelAScrewName,
                    CurrentValueUpperLimit = ModelATorqueFactor > 0 ? Math.Max(0, Math.Min(4095, (int)Math.Round(ModelATorqueUpper / ModelATorqueFactor))) : 4095,
                    CurrentValueLowerLimit = ModelATorqueFactor > 0 ? Math.Max(0, Math.Min(4095, (int)Math.Round(ModelATorqueLower / ModelATorqueFactor))) : 0,
                    TimeUpperLimitMs = ModelATimeUpper,
                    TimeLowerLimitMs = ModelATimeLower,
                    TorqueConversionFactor = ModelATorqueFactor,
                    IsEnabled = true
                }
            };

            // Model B
            s.Models[1].ModelId = 2;
            s.Models[1].ModelName = ModelBName;
            s.Models[1].StationName = ModelBStationName;
            s.Models[1].TotalScrewCount = ModelBScrewCount;
            s.Models[1].IsEnabled = true;
            s.Models[1].Channels = new List<ChannelConfig>
            {
                new()
                {
                    ChannelNumber = ModelBChannel,
                    ScrewName = ModelBScrewName,
                    CurrentValueUpperLimit = ModelBTorqueFactor > 0 ? Math.Max(0, Math.Min(4095, (int)Math.Round(ModelBTorqueUpper / ModelBTorqueFactor))) : 4095,
                    CurrentValueLowerLimit = ModelBTorqueFactor > 0 ? Math.Max(0, Math.Min(4095, (int)Math.Round(ModelBTorqueLower / ModelBTorqueFactor))) : 0,
                    TimeUpperLimitMs = ModelBTimeUpper,
                    TimeLowerLimitMs = ModelBTimeLower,
                    TorqueConversionFactor = ModelBTorqueFactor,
                    IsEnabled = true
                }
            };

            _configService.Save();
            _dashboardVm.RefreshSettings();
            StatusMessage = "✅ Settings saved successfully!";
            Logger.Information("Settings saved and Dashboard refreshed");
        }
        catch (Exception ex)
        {
            StatusMessage = $"❌ Save failed: {ex.Message}";
            Logger.Error(ex, "Failed to save settings");
        }
    }

    [RelayCommand]
    private async Task TestLoggingAsync(string parameter)
    {
        try
        {
            var parts = parameter.Split(':');
            if (parts.Length != 2) return;

            string modelId = parts[0];
            string type = parts[1]; // Accept, Reject, Raw

            ProductModel testModel = new ProductModel();
            int channelNum = 1;
            double factor = 0.001;

            if (modelId == "1")
            {
                testModel.ModelId = 1;
                testModel.ModelName = ModelAName;
                testModel.StationName = ModelAStationName;
                testModel.TotalScrewCount = ModelAScrewCount;
                channelNum = ModelAChannel;
                factor = ModelATorqueFactor;
            }
            else
            {
                testModel.ModelId = 2;
                testModel.ModelName = ModelBName;
                testModel.StationName = ModelBStationName;
                testModel.TotalScrewCount = ModelBScrewCount;
                channelNum = ModelBChannel;
                factor = ModelBTorqueFactor;
            }

            // Find configured channel details from UI properties
            string screwName = modelId == "1" ? ModelAScrewName : ModelBScrewName;
            double guiTorqueUpper = modelId == "1" ? ModelATorqueUpper : ModelBTorqueUpper;
            double guiTorqueLower = modelId == "1" ? ModelATorqueLower : ModelBTorqueLower;
            int timeUpper = modelId == "1" ? ModelATimeUpper : ModelBTimeUpper;
            int timeLower = modelId == "1" ? ModelATimeLower : ModelBTimeLower;

            // Convert GUI torque back to raw limits for the internal result model
            int upperLimit = factor > 0 ? (int)Math.Round(guiTorqueUpper / factor) : 4095;
            int lowerLimit = factor > 0 ? (int)Math.Round(guiTorqueLower / factor) : 0;

            var rand = new Random();
            if (type == "Raw")
            {
                var result = new JudgmentResult
                {
                    ModelId = testModel.ModelId,
                    ModelName = testModel.ModelName,
                    StationName = testModel.StationName,
                    Channel = channelNum,
                    ScrewName = screwName,
                    RawCurrentValue = rand.Next(lowerLimit, upperLimit),
                    ScrewTimeMs = rand.Next(timeLower, timeUpper),
                    Judgment = JudgmentStatus.OK,
                    OperatorId = OperatorId,
                    WorkOrderNo = $"TEST-RAW-{DateTime.Now:HHmm}",
                    ScrewNumber = 1,
                    TotalScrews = testModel.TotalScrewCount,
                    TorqueConversionFactor = factor,
                    CurrentUpperLimit = upperLimit,
                    CurrentLowerLimit = lowerLimit,
                    TimeUpperLimitMs = timeUpper,
                    TimeLowerLimitMs = timeLower
                };
                await _csvLogService.LogResultAsync(result);
                StatusMessage = $"✅ Raw test log generated for {testModel.ModelName}";
                MessageBox.Show($"Raw test log generated successfully.\nFile: {testModel.ModelName}_Raw...", "Test Completed", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                var batch = new List<JudgmentResult>();
                for (int i = 1; i <= testModel.TotalScrewCount; i++)
                {
                    bool isNg = (type == "Reject" && i == testModel.TotalScrewCount);
                    var result = new JudgmentResult
                    {
                        ModelId = testModel.ModelId,
                        ModelName = testModel.ModelName,
                        StationName = testModel.StationName,
                        Channel = channelNum,
                        ScrewName = screwName,
                        RawCurrentValue = isNg ? (upperLimit + 1000) : rand.Next(lowerLimit, upperLimit),
                        ScrewTimeMs = isNg ? (timeUpper + 1000) : rand.Next(timeLower, timeUpper),
                        Judgment = isNg ? JudgmentStatus.NG : JudgmentStatus.OK,
                        OperatorId = OperatorId,
                        WorkOrderNo = $"TEST-{type.ToUpper()}-{DateTime.Now:HHmm}",
                        ScrewNumber = i,
                        TotalScrews = testModel.TotalScrewCount,
                        TorqueConversionFactor = factor,
                        CurrentUpperLimit = upperLimit,
                        CurrentLowerLimit = lowerLimit,
                        TimeUpperLimitMs = timeUpper,
                        TimeLowerLimitMs = timeLower
                    };
                    batch.Add(result);
                }
                await _unitLogService.LogUnitAsync(type, batch);
                StatusMessage = $"✅ {type} unit log generated for {testModel.ModelName}";
                MessageBox.Show($"{type} unit log generated successfully for {testModel.ModelName}.", "Test Completed", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"❌ Test failed: {ex.Message}";
            Logger.Error(ex, "Test logging failed");
            MessageBox.Show($"Test failed: {ex.Message}", "Test Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private void BrowseCsvFolder()
    {
        var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "Select CSV Log Folder",
            SelectedPath = CsvLogFolder
        };
        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            CsvLogFolder = dialog.SelectedPath;
        }
    }

    [RelayCommand]
    private void BrowseNgSound()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Select NG Alert Sound",
            Filter = "WAV files (*.wav)|*.wav|All files (*.*)|*.*",
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
        };
        if (dialog.ShowDialog() == true)
        {
            NgSoundFile = dialog.FileName;
        }
    }
}
