using System.Collections.ObjectModel;
using System.IO.Ports;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ScrewingHub.Communication.Dtm10;
using ScrewingHub.Communication.OmronPlc;
using ScrewingHub.Core.Interfaces;
using ScrewingHub.Core.Models;
using ScrewingHub.Core.Services;
using Serilog;

namespace ScrewingHub.App.ViewModels;

public partial class DashboardViewModel : ObservableObject
{
    private static readonly ILogger Logger = Log.ForContext<DashboardViewModel>();

    private readonly IJudgmentService _judgmentService;
    private readonly ICsvLogService _csvLogService;
    private readonly IUnitLogService _unitLogService;
    private readonly ModelConfigService _configService;
    private readonly ScrewSequenceTracker _tracker = new();
    private readonly List<JudgmentResult> _currentAssemblyBatch = new();

    private IDtm10Client? _dtm10Client;
    private IPlcClient? _plcClient;
    private System.Media.SoundPlayer? _ngSoundPlayer;
    private DispatcherTimer? _flashTimer;

    // ===== CONNECTION STATUS =====
    [ObservableProperty] private bool _isDtm10Connected;
    [ObservableProperty] private string _dtm10PortInfo = "Not connected";
    [ObservableProperty] private bool _isPlcConnected;
    [ObservableProperty] private string _plcPortInfo = "Not connected";

    // ===== CURRENT RESULT =====
    [ObservableProperty] private string _judgmentDisplay = "---";
    [ObservableProperty] private string _judgmentColor = "#6B7280";
    [ObservableProperty] private bool _showOkFlash;
    [ObservableProperty] private bool _showNgFlash;
    [ObservableProperty] private bool _isPlcHeartbeatActive;
    [ObservableProperty] private int _currentValue;
    [ObservableProperty] private int _screwTimeMs;
    [ObservableProperty] private int _channelNumber;
    [ObservableProperty] private double _convertedTorque;
    [ObservableProperty] private string _torqueDisplay = "---";
    [ObservableProperty] private int _currentUpperLimit;
    [ObservableProperty] private int _currentLowerLimit;
    [ObservableProperty] private double _torqueUpperLimit;
    [ObservableProperty] private double _torqueLowerLimit;
    [ObservableProperty] private double _gaugePercentage;
    [ObservableProperty] private System.Windows.Media.Brush _gaugeColor = System.Windows.Media.Brushes.DodgerBlue;

    // ===== MODEL & SCREW PROGRESS =====
    [ObservableProperty] private ObservableCollection<ProductModel> _availableModels = new();
    [ObservableProperty] private ProductModel? _selectedModel;
    [ObservableProperty] private int _currentScrewNumber;
    [ObservableProperty] private int _totalScrews;
    [ObservableProperty] private string _screwProgressText = "0/0";
    [ObservableProperty] private ObservableCollection<ScrewProgressItem> _screwProgressItems = new();

    // ===== STATISTICS (Unit Qty) =====
    [ObservableProperty] private int _todayUnitTotal;
    [ObservableProperty] private int _todayUnitOk;
    [ObservableProperty] private int _todayUnitNg;
    [ObservableProperty] private string _unitPassRate = "0.0%";

    // ===== STATISTICS (Screw Points) =====
    [ObservableProperty] private int _todayScrewTotal;
    [ObservableProperty] private int _todayScrewOk;
    [ObservableProperty] private int _todayScrewNg;
    [ObservableProperty] private string _screwPassRate = "0.0%";

    // ===== RECENT LOG =====
    [ObservableProperty] private ObservableCollection<JudgmentResult> _recentResults = new();

    // ===== OPERATOR =====
    [ObservableProperty] private string _operatorId = string.Empty;
    [ObservableProperty] private string _workOrderNo = string.Empty;
    [ObservableProperty] private string _warningText = string.Empty;

    public DashboardViewModel(
        IJudgmentService judgmentService,
        ICsvLogService csvLogService,
        IUnitLogService unitLogService,
        ModelConfigService configService)
    {
        _judgmentService = judgmentService;
        _csvLogService = csvLogService;
        _unitLogService = unitLogService;
        _configService = configService;

        LoadModels();
        LoadTodayStats();
    }

    private void LoadModels()
    {
        AvailableModels.Clear();
        foreach (var model in _configService.Settings.Models.Where(m => m.IsEnabled))
        {
            AvailableModels.Add(model);
        }
        if (AvailableModels.Count > 0)
            SelectedModel = AvailableModels[0];
    }

    partial void OnSelectedModelChanged(ProductModel? value)
    {
        if (value != null)
        {
            _tracker.StartNewUnit(value);
            _currentAssemblyBatch.Clear();
            UpdateScrewProgress();
            
            // Initialization: Show limits for the first channel immediately
            var firstChannel = value.Channels.FirstOrDefault();
            if (firstChannel != null)
            {
                ChannelNumber = firstChannel.ChannelNumber;
                TorqueLowerLimit = firstChannel.CurrentValueLowerLimit * firstChannel.TorqueConversionFactor;
                TorqueUpperLimit = firstChannel.CurrentValueUpperLimit * firstChannel.TorqueConversionFactor;
                TorqueDisplay = "Ready";
                GaugePercentage = 0;
                GaugeColor = System.Windows.Media.Brushes.DodgerBlue;
                WarningText = string.Empty;
            }
            
            Logger.Information("Model changed to: {Model}", value.ModelName);
        }
    }

    public void SetDevices(IDtm10Client? dtm10, IPlcClient? plc)
    {
        // Unhook old events
        if (_dtm10Client != null)
        {
            _dtm10Client.DataReceived -= OnDtm10DataReceived;
            _dtm10Client.ConnectionChanged -= OnDtm10ConnectionChanged;
        }
        if (_plcClient != null)
        {
            _plcClient.ConnectionChanged -= OnPlcConnectionChanged;
            _plcClient.ResetRequestReceived -= OnPlcResetRequest;
            _plcClient.HeartbeatSent -= OnPlcHeartbeatSent;
        }

        _dtm10Client = dtm10;
        _plcClient = plc;

        // Hook new events
        if (_dtm10Client != null)
        {
            _dtm10Client.DataReceived += OnDtm10DataReceived;
            _dtm10Client.ConnectionChanged += OnDtm10ConnectionChanged;
        }
        if (_plcClient != null)
        {
            _plcClient.ConnectionChanged += OnPlcConnectionChanged;
            _plcClient.ResetRequestReceived += OnPlcResetRequest;
            _plcClient.HeartbeatSent += OnPlcHeartbeatSent;
        }
    }

    private void OnPlcHeartbeatSent(object? sender, EventArgs e)
    {
        Application.Current?.Dispatcher.Invoke(async () =>
        {
            IsPlcHeartbeatActive = true;
            await Task.Delay(200);
            IsPlcHeartbeatActive = false;
        });
    }

    public void RefreshSettings()
    {
        Application.Current?.Dispatcher.Invoke(() =>
        {
            LoadModels();
            Logger.Information("Dashboard settings refreshed from config");
        });
    }

    private void OnDtm10ConnectionChanged(object? sender, bool connected)
    {
        Application.Current?.Dispatcher.Invoke(() =>
        {
            IsDtm10Connected = connected;
            var settings = _configService.Settings.Dtm10Serial;
            Dtm10PortInfo = connected
                ? $"{settings.PortName}:{settings.BaudRate}"
                : "Disconnected";
        });
    }

    private void OnPlcConnectionChanged(object? sender, bool connected)
    {
        Application.Current?.Dispatcher.Invoke(() =>
        {
            IsPlcConnected = connected;
            var settings = _configService.Settings.PlcSerial;
            PlcPortInfo = connected
                ? $"{settings.PortName}:{settings.BaudRate}"
                : "Disconnected";
        });
    }

    private async void OnPlcResetRequest(object? sender, EventArgs e)
    {
        await Application.Current?.Dispatcher.InvokeAsync(async () =>
        {
            await PerformResetAsync("PLC Signal (Loose)");
        });
    }

    private async void OnDtm10DataReceived(object? sender, ScrewData data)
    {
        try
        {
            if (SelectedModel == null)
            {
                Logger.Warning("No model selected, ignoring DTM10 data");
                return;
            }

            // Unknown channel warning
            var channelConfig = SelectedModel.GetChannel(data.Channel);
            if (channelConfig == null)
            {
                Application.Current?.Dispatcher.Invoke(() => 
                {
                    WarningText = $"⚠️ Unknown Channel: CH{data.Channel}";
                });
                Logger.Warning("Data received for unconfigured Channel: CH{Channel}", data.Channel);
                return;
            }
            else
            {
                Application.Current?.Dispatcher.Invoke(() => WarningText = string.Empty);
            }

            // Evaluate judgment
            int screwNum = _tracker.CurrentScrewNumber;
            var result = _judgmentService.Evaluate(data, SelectedModel, screwNum);
            result.OperatorId = OperatorId;
            result.WorkOrderNo = WorkOrderNo;

            // Record in tracker
            _tracker.RecordScrew(result);

            // Send to PLC
            if (_plcClient != null)
            {
                await _plcClient.WriteJudgmentAsync(result);
            }

            // Log raw to CSV
            await _csvLogService.LogResultAsync(result);

            // Add to batch tracking
            _currentAssemblyBatch.Add(result);

            // Check if we reached ACCEPT or REJECT conditions
            if (result.Judgment == JudgmentStatus.NG)
            {
                // Immediate REJECT - Stop sequence and reset
                await _unitLogService.LogUnitAsync("Reject", new List<JudgmentResult>(_currentAssemblyBatch));
                
                // Update stats
                Application.Current?.Dispatcher.Invoke(() => UpdateUnitStatistics("Reject"));
                
                _tracker.StartNewUnit(SelectedModel);
                _currentAssemblyBatch.Clear();
                
                Logger.Warning("NG detected — sequence stopped and reset to start");
            }
            else if (result.Judgment == JudgmentStatus.OK && _tracker.AllScrewsComplete)
            {
                // Sequence completed successfully -> ACCEPT
                await _unitLogService.LogUnitAsync("Accept", new List<JudgmentResult>(_currentAssemblyBatch));
                
                // Update stats
                Application.Current?.Dispatcher.Invoke(() => UpdateUnitStatistics("Accept"));
                
                _tracker.StartNewUnit(SelectedModel);
                _currentAssemblyBatch.Clear();
            }

            // Update UI on dispatcher thread
            Application.Current?.Dispatcher.Invoke(() =>
            {
                UpdateCurrentResult(result);
                UpdateScrewProgress();
                UpdateStatistics(result);
                AddToRecentLog(result);
                TriggerFlash(result.Judgment);

                // Auto-reset if all screws complete
                if (_tracker.AllScrewsComplete)
                {
                    Logger.Information("All screws complete for {Model}!", SelectedModel.ModelName);
                    // Short delay then reset for next unit
                    Task.Delay(2000).ContinueWith(_ =>
                    {
                        Application.Current?.Dispatcher.Invoke(() =>
                        {
                            _tracker.ResetForNextUnit();
                            UpdateScrewProgress();
                        });
                    });
                }
            });
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error processing DTM10 data");
        }
    }

    private void UpdateCurrentResult(JudgmentResult result)
    {
        JudgmentDisplay = result.Judgment.ToString();
        JudgmentColor = result.Judgment == JudgmentStatus.OK ? "#22C55E"
                       : result.Judgment == JudgmentStatus.NG ? "#EF4444"
                       : "#F59E0B";
        CurrentValue = result.RawCurrentValue;
        ScrewTimeMs = result.ScrewTimeMs;
        ChannelNumber = result.Channel;
        ConvertedTorque = result.ConvertedTorque;
        TorqueDisplay = result.TorqueConversionFactor > 0
            ? $"{result.ConvertedTorque:F3} kgf·cm"
            : "N/A";
        CurrentUpperLimit = result.CurrentUpperLimit;
        CurrentLowerLimit = result.CurrentLowerLimit;
        TorqueUpperLimit = result.TorqueUpperLimit;
        TorqueLowerLimit = result.TorqueLowerLimit;

        // Calculate gauge percentage (0–100)
        int range = result.CurrentUpperLimit - result.CurrentLowerLimit;
        if (range > 0)
        {
            double normalized = (double)(result.RawCurrentValue - result.CurrentLowerLimit) / range;
            GaugePercentage = Math.Max(0, Math.Min(100, normalized * 100));

            // Set color based on judgment and proximity to limits
            if (result.Judgment == JudgmentStatus.OK)
            {
                // OK, but check for warning zone (within 10% of either limit)
                double warningMargin = range * 0.1;
                if (result.RawCurrentValue < (result.CurrentLowerLimit + warningMargin) ||
                    result.RawCurrentValue > (result.CurrentUpperLimit - warningMargin))
                {
                    GaugeColor = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#F59E0B")); // Amber (Warning)
                }
                else
                {
                    GaugeColor = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#22C55E")); // Green (Safe OK)
                }
            }
            else
            {
                GaugeColor = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#EF4444")); // Red (NG)
            }
        }
        else
        {
            GaugePercentage = 0;
            GaugeColor = System.Windows.Media.Brushes.Gray; // Gray (Idle)
        }
    }

    private void UpdateScrewProgress()
    {
        CurrentScrewNumber = _tracker.CurrentScrewNumber;
        TotalScrews = _tracker.TotalScrews;
        ScrewProgressText = $"{Math.Min(CurrentScrewNumber, TotalScrews)}/{TotalScrews}";

        ScrewProgressItems.Clear();
        for (int i = 1; i <= TotalScrews; i++)
        {
            var completed = _tracker.CompletedScrews
                .Where(s => s.ScrewNumber == i && s.Judgment == JudgmentStatus.OK)
                .LastOrDefault();

            ScrewProgressItems.Add(new ScrewProgressItem
            {
                ScrewNumber = i,
                IsCompleted = completed != null,
                IsCurrent = i == _tracker.CurrentScrewNumber,
                Value = completed?.RawCurrentValue
            });
        }
    }

    private void UpdateStatistics(JudgmentResult result)
    {
        TodayScrewTotal++;
        if (result.Judgment == JudgmentStatus.OK) TodayScrewOk++;
        else TodayScrewNg++;
        
        ScrewPassRate = TodayScrewTotal > 0
            ? $"{(double)TodayScrewOk / TodayScrewTotal * 100:F1}%"
            : "0.0%";
    }

    private void UpdateUnitStatistics(string status)
    {
        TodayUnitTotal++;
        if (status == "Accept") TodayUnitOk++;
        else TodayUnitNg++;

        UnitPassRate = TodayUnitTotal > 0
            ? $"{(double)TodayUnitOk / TodayUnitTotal * 100:F1}%"
            : "0.0%";
        
        Logger.Information("Unit statistics updated: {Status} (Total: {Total}, OK: {Ok}, NG: {Ng})", 
            status, TodayUnitTotal, TodayUnitOk, TodayUnitNg);
    }

    private void AddToRecentLog(JudgmentResult result)
    {
        RecentResults.Insert(0, result);
        while (RecentResults.Count > 50)
            RecentResults.RemoveAt(RecentResults.Count - 1);
    }

    private void TriggerFlash(JudgmentStatus judgment)
    {
        ShowOkFlash = judgment == JudgmentStatus.OK;
        ShowNgFlash = judgment == JudgmentStatus.NG;

        // Play NG sound
        if (judgment == JudgmentStatus.NG)
        {
            try
            {
                var soundPath = _configService.Settings.NgSoundFilePath;
                if (!string.IsNullOrEmpty(soundPath) && File.Exists(soundPath))
                {
                    _ngSoundPlayer ??= new System.Media.SoundPlayer(soundPath);
                    _ngSoundPlayer.Play();
                }
                else
                {
                    System.Media.SystemSounds.Exclamation.Play();
                }
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "Failed to play NG sound");
            }
        }

        // Auto-clear flash after 2 seconds
        _flashTimer?.Stop();
        _flashTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _flashTimer.Tick += (_, _) =>
        {
            ShowOkFlash = false;
            ShowNgFlash = false;
            _flashTimer.Stop();
        };
        _flashTimer.Start();
    }

    [RelayCommand]
    private async Task ResetUnit()
    {
        await PerformResetAsync("Manual Button");
    }

    private async Task PerformResetAsync(string reason)
    {
        if (_currentAssemblyBatch.Count > 0)
        {
            Logger.Information("Unit reset requested via {Reason} with {Count} screws completed. Saving as Reject.", reason, _currentAssemblyBatch.Count);
            // Log as reject since work was in progress
            await _unitLogService.LogUnitAsync("Reject", new List<JudgmentResult>(_currentAssemblyBatch));
            
            // Update stats
            Application.Current?.Dispatcher.Invoke(() => UpdateUnitStatistics("Reject"));
        }

        if (SelectedModel != null)
        {
            _tracker.StartNewUnit(SelectedModel);
        }
        _currentAssemblyBatch.Clear();
        UpdateScrewProgress();
        JudgmentDisplay = "---";
        JudgmentColor = "#6B7280";
        CurrentValue = 0;
        ScrewTimeMs = 0;
        GaugePercentage = 0;
        GaugeColor = System.Windows.Media.Brushes.DodgerBlue;
        Logger.Information("Unit reset processed: {Reason}", reason);
    }

    private void LoadTodayStats()
    {
        // Load detailed stats from logs
        Task.Run(async () =>
        {
            var records = await _csvLogService.ReadLogAsync(DateTime.Today);
            int unitOk = _csvLogService.GetTodayUnitCount("Accept");
            int unitNg = _csvLogService.GetTodayUnitCount("Reject");

            Application.Current?.Dispatcher.Invoke(() =>
            {
                // Screw stats
                TodayScrewTotal = records.Count;
                TodayScrewOk = records.Count(r => r.Judgment == JudgmentStatus.OK);
                TodayScrewNg = records.Count(r => r.Judgment == JudgmentStatus.NG);
                ScrewPassRate = TodayScrewTotal > 0
                    ? $"{(double)TodayScrewOk / TodayScrewTotal * 100:F1}%"
                    : "0.0%";

                // Unit stats
                TodayUnitOk = unitOk;
                TodayUnitNg = unitNg;
                TodayUnitTotal = unitOk + unitNg;
                UnitPassRate = TodayUnitTotal > 0
                    ? $"{(double)TodayUnitOk / TodayUnitTotal * 100:F1}%"
                    : "0.0%";
                
                Logger.Information("Initial stats loaded: Units={UTotal}({UOk}/{UNg}), Screws={STotal}({SOk}/{SNg})",
                    TodayUnitTotal, TodayUnitOk, TodayUnitNg, TodayScrewTotal, TodayScrewOk, TodayScrewNg);
            });
        });
    }

    [RelayCommand]
    private async Task ConnectDevices()
    {
        try
        {
            if (_dtm10Client != null && !IsDtm10Connected)
                await _dtm10Client.ConnectAsync();
        }
        catch (Exception ex) { Logger.Error(ex, "Failed to connect DTM10"); }

        try
        {
            if (_plcClient != null && !IsPlcConnected)
                await _plcClient.ConnectAsync();
        }
        catch (Exception ex) { Logger.Error(ex, "Failed to connect PLC"); }
    }

    [RelayCommand]
    private async Task DisconnectDevices()
    {
        if (_dtm10Client != null) await _dtm10Client.DisconnectAsync();
        if (_plcClient != null) await _plcClient.DisconnectAsync();
    }
}

public class ScrewProgressItem
{
    public int ScrewNumber { get; set; }
    public bool IsCompleted { get; set; }
    public bool IsCurrent { get; set; }
    public int? Value { get; set; }
    public string Display => IsCompleted ? $"✅ {ScrewNumber}" : IsCurrent ? $"▶ {ScrewNumber}" : $"⬜ {ScrewNumber}";
}
