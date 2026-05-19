using System;
using System.Collections.ObjectModel;
using System.IO.Ports;
using System.Linq;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ScrewingHub.Communication.Common;
using ScrewingHub.Core.Interfaces;
using ScrewingHub.Core.Services;
using Serilog;

namespace ScrewingHub.App.ViewModels;

public partial class CalibrationPoint : ObservableObject
{
    [ObservableProperty] private int _id;
    [ObservableProperty] private double _actualTorque;
    [ObservableProperty] private int _rawCurrentValue;
    
    public double Factor => RawCurrentValue > 0 ? ActualTorque / RawCurrentValue : 0;
}

public partial class CalibrationViewModel : ObservableObject, IDisposable
{
    private static readonly ILogger Logger = Log.ForContext<CalibrationViewModel>();
    
    private readonly ModelConfigService _configService;
    private readonly DashboardViewModel _dashboardVm;
    private readonly SettingsViewModel _settingsVm;
    private readonly ITorqueMeterClient _torqueMeter;
    private readonly IDtm10Client _dtm10Client;

    // --- Torque Meter Connection ---
    [ObservableProperty] private ObservableCollection<string> _availablePorts = new();
    [ObservableProperty] private string _selectedPort = "COM5";
    [ObservableProperty] private bool _isConnected;
    [ObservableProperty] private string _connectionStatus = "Disconnected";
    [ObservableProperty] private string _statusMessage = string.Empty;

    // --- Calibration Data ---
    [ObservableProperty] private double? _pendingActualTorque;
    [ObservableProperty] private int? _pendingRawCurrentValue;
    
    public ObservableCollection<CalibrationPoint> CalibrationPoints { get; } = new();
    [ObservableProperty] private CalibrationPoint? _selectedPoint;
    
    [ObservableProperty] private double? _calculatedFactor;

    public CalibrationViewModel(
        ModelConfigService configService,
        DashboardViewModel dashboardVm,
        SettingsViewModel settingsVm,
        ITorqueMeterClient torqueMeter,
        IDtm10Client dtm10Client)
    {
        _configService = configService;
        _dashboardVm = dashboardVm;
        _settingsVm = settingsVm;
        _torqueMeter = torqueMeter;
        _dtm10Client = dtm10Client;

        // Initialize from settings
        SelectedPort = _configService.Settings.TorqueMeterSerial.PortName;
        
        RefreshPorts();

        // Subscribe to events
        _torqueMeter.ConnectionStatusChanged += OnTorqueMeterConnectionStatusChanged;
        _torqueMeter.DataReceived += OnTorqueMeterDataReceived;
        _torqueMeter.ErrorOccurred += OnTorqueMeterError;

        _dtm10Client.DataReceived += OnDtm10DataReceived;
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

    [RelayCommand]
    private void ToggleConnection()
    {
        if (IsConnected)
        {
            _torqueMeter.Disconnect();
        }
        else
        {
            try
            {
                int baudRate = _configService.Settings.TorqueMeterSerial.BaudRate;
                _torqueMeter.Connect(SelectedPort, baudRate);
                
                // Save port selection to settings
                _configService.Settings.TorqueMeterSerial.PortName = SelectedPort;
                _configService.Save();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Connection failed: {ex.Message}";
            }
        }
    }

    private void OnTorqueMeterConnectionStatusChanged(object? sender, bool isConnected)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            IsConnected = isConnected;
            ConnectionStatus = isConnected ? "Connected" : "Disconnected";
            StatusMessage = isConnected ? "Connected to Torque Meter" : "Disconnected";
        });
    }

    private void OnTorqueMeterDataReceived(object? sender, double torque)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            PendingActualTorque = torque;
            StatusMessage = "Received Actual Torque from Meter.";
        });
    }

    private void OnTorqueMeterError(object? sender, string message)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            StatusMessage = $"Meter Error: {message}";
        });
    }

    private void OnDtm10DataReceived(object? sender, Core.Models.ScrewData e)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            PendingRawCurrentValue = e.ConvertedCurrentValue;
            StatusMessage = "Captured Raw Value from DTM10.";
        });
    }

    [RelayCommand]
    private void AddPoint()
    {
        if (PendingActualTorque.HasValue && PendingActualTorque.Value > 0 && 
            PendingRawCurrentValue.HasValue && PendingRawCurrentValue.Value > 0)
        {
            CalibrationPoints.Add(new CalibrationPoint
            {
                Id = CalibrationPoints.Count + 1,
                ActualTorque = PendingActualTorque.Value,
                RawCurrentValue = PendingRawCurrentValue.Value
            });
            
            PendingActualTorque = null;
            PendingRawCurrentValue = null;
            StatusMessage = $"Added data point {CalibrationPoints.Count}. Need at least 5.";
        }
        else
        {
            StatusMessage = "Error: Please provide valid Actual Torque and Raw Value.";
        }
    }
    
    [RelayCommand]
    private void RemoveSelectedPoint()
    {
        if (SelectedPoint != null)
        {
            CalibrationPoints.Remove(SelectedPoint);
            // Re-index
            for (int i = 0; i < CalibrationPoints.Count; i++)
            {
                CalibrationPoints[i].Id = i + 1;
            }
            StatusMessage = "Point removed.";
            CalculatedFactor = null;
        }
    }

    [RelayCommand]
    private void CalculateFactor()
    {
        if (CalibrationPoints.Count < 5)
        {
            StatusMessage = $"Error: Need at least 5 data points to calculate. Currently have {CalibrationPoints.Count}.";
            MessageBox.Show($"Need at least 5 data points to calculate. Currently have {CalibrationPoints.Count}.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Calculate average factor across all points
        double avgFactor = CalibrationPoints.Average(p => p.Factor);
        CalculatedFactor = avgFactor;
        StatusMessage = $"Factor calculated successfully based on {CalibrationPoints.Count} points.";
    }

    [RelayCommand]
    private void ClearData()
    {
        PendingActualTorque = null;
        PendingRawCurrentValue = null;
        CalibrationPoints.Clear();
        CalculatedFactor = null;
        StatusMessage = "All data cleared. Ready for new readings.";
    }

    [RelayCommand]
    private void ApplyToModel(string model)
    {
        if (!CalculatedFactor.HasValue || CalculatedFactor.Value <= 0)
        {
            MessageBox.Show("Please calculate a valid factor first.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            double factor = Math.Round(CalculatedFactor.Value, 6);

            if (model == "A")
            {
                _settingsVm.ModelATorqueFactor = factor;
                _settingsVm.SaveCommand.Execute(null);
                StatusMessage = $"Applied Factor {factor:F6} to Model A and saved settings.";
            }
            else if (model == "B")
            {
                _settingsVm.ModelBTorqueFactor = factor;
                _settingsVm.SaveCommand.Execute(null);
                StatusMessage = $"Applied Factor {factor:F6} to Model B and saved settings.";
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to apply factor to Model {Model}", model);
            StatusMessage = $"Failed to apply: {ex.Message}";
        }
    }

    public void Dispose()
    {
        _torqueMeter.ConnectionStatusChanged -= OnTorqueMeterConnectionStatusChanged;
        _torqueMeter.DataReceived -= OnTorqueMeterDataReceived;
        _torqueMeter.ErrorOccurred -= OnTorqueMeterError;
        _dtm10Client.DataReceived -= OnDtm10DataReceived;
    }
}
