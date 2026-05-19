using System;
using System.IO.Ports;
using System.Text.RegularExpressions;
using ScrewingHub.Core.Interfaces;
using Serilog;

namespace ScrewingHub.Communication.Common;

/// <summary>
/// A serial client designed to connect to external USB Torque Meters (like Hios HP-10 or Quick HM-10C).
/// These meters typically output an ASCII string representing the torque value.
/// </summary>
public class TorqueMeterClient : ITorqueMeterClient
{
    private static readonly ILogger Logger = Log.ForContext<TorqueMeterClient>();
    private SerialPort? _serialPort;

    public event EventHandler<double>? DataReceived;
    public event EventHandler<string>? ErrorOccurred;
    public event EventHandler<bool>? ConnectionStatusChanged;

    public bool IsConnected => _serialPort?.IsOpen ?? false;

    public void Connect(string portName, int baudRate)
    {
        try
        {
            if (IsConnected)
                Disconnect();

            _serialPort = new SerialPort(portName, baudRate, Parity.None, 8, StopBits.One);
            _serialPort.Handshake = Handshake.None;
            _serialPort.DataReceived += SerialPort_DataReceived;
            _serialPort.ErrorReceived += SerialPort_ErrorReceived;
            
            _serialPort.Open();
            Logger.Information("Connected to Torque Meter on {PortName} at {BaudRate} baud", portName, baudRate);
            ConnectionStatusChanged?.Invoke(this, true);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to connect to Torque Meter on {PortName}", portName);
            ErrorOccurred?.Invoke(this, $"Connection failed: {ex.Message}");
            ConnectionStatusChanged?.Invoke(this, false);
            throw;
        }
    }

    public void Disconnect()
    {
        try
        {
            if (_serialPort != null)
            {
                _serialPort.DataReceived -= SerialPort_DataReceived;
                _serialPort.ErrorReceived -= SerialPort_ErrorReceived;
                if (_serialPort.IsOpen)
                {
                    _serialPort.Close();
                }
                _serialPort.Dispose();
                _serialPort = null;
                
                Logger.Information("Disconnected from Torque Meter");
                ConnectionStatusChanged?.Invoke(this, false);
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error disconnecting from Torque Meter");
            ErrorOccurred?.Invoke(this, $"Disconnect error: {ex.Message}");
        }
    }

    private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
    {
        try
        {
            if (_serialPort == null || !_serialPort.IsOpen) return;

            string data = _serialPort.ReadExisting();
            if (string.IsNullOrWhiteSpace(data)) return;

            Logger.Debug("Raw data received from Torque Meter: '{Data}'", data.Trim());

            // Simple regex to extract the first continuous decimal/number found in the string
            // This handles inputs like "  015.35 \r\n" or "015.35"
            var match = Regex.Match(data, @"[-+]?[0-9]*\.?[0-9]+");
            if (match.Success && double.TryParse(match.Value, out double torqueValue))
            {
                Logger.Information("Parsed Actual Torque from Meter: {Torque}", torqueValue);
                DataReceived?.Invoke(this, torqueValue);
            }
            else
            {
                Logger.Warning("Could not parse numeric torque value from received string: '{Data}'", data);
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error processing data from Torque Meter");
            ErrorOccurred?.Invoke(this, $"Data read error: {ex.Message}");
        }
    }

    private void SerialPort_ErrorReceived(object sender, SerialErrorReceivedEventArgs e)
    {
        Logger.Error("Serial error received from Torque Meter: {Error}", e.EventType.ToString());
        ErrorOccurred?.Invoke(this, $"Serial Error: {e.EventType}");
    }

    public void Dispose()
    {
        Disconnect();
        GC.SuppressFinalize(this);
    }
}
