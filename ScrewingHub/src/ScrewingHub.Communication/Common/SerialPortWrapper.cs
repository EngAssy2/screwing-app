using System.IO.Ports;
using Serilog;

namespace ScrewingHub.Communication.Common;

/// <summary>
/// Wrapper around System.IO.Ports.SerialPort with auto-reconnect, 
/// thread-safe operations, and event-driven data reception.
/// </summary>
public class SerialPortWrapper : IDisposable
{
    private static readonly ILogger Logger = Log.ForContext<SerialPortWrapper>();

    private SerialPort? _port;
    private CancellationTokenSource? _reconnectCts;
    private bool _disposed;

    public string PortName { get; set; }
    public int BaudRate { get; set; }
    public int DataBits { get; set; } = 8;
    public Parity Parity { get; set; } = Parity.None;
    public StopBits StopBits { get; set; } = StopBits.One;
    public Handshake Handshake { get; set; } = Handshake.None;
    public int ReadTimeoutMs { get; set; } = 1000;
    public int WriteTimeoutMs { get; set; } = 1000;
    public int ReconnectIntervalMs { get; set; } = 3000;
    public bool AutoReconnect { get; set; } = true;

    public bool IsConnected => _port?.IsOpen ?? false;

    public event EventHandler<byte[]>? DataReceived;
    public event EventHandler<bool>? ConnectionChanged;
    public event EventHandler<Exception>? ErrorOccurred;

    public SerialPortWrapper(string portName, int baudRate)
    {
        PortName = portName;
        BaudRate = baudRate;
    }

    public void Connect()
    {
        if (IsConnected) return;

        try
        {
            _port = new SerialPort
            {
                PortName = PortName,
                BaudRate = BaudRate,
                DataBits = DataBits,
                Parity = Parity,
                StopBits = StopBits,
                Handshake = Handshake,
                ReadTimeout = ReadTimeoutMs,
                WriteTimeout = WriteTimeoutMs,
                ReadBufferSize = 4096,
                WriteBufferSize = 2048
            };

            _port.DataReceived += OnPortDataReceived;
            _port.ErrorReceived += OnPortErrorReceived;
            _port.Open();

            Logger.Information("Serial port {Port} opened ({Baud},{DataBits},{Parity},{StopBits})",
                PortName, BaudRate, DataBits, Parity, StopBits);

            ConnectionChanged?.Invoke(this, true);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to open serial port {Port}", PortName);
            ConnectionChanged?.Invoke(this, false);

            if (AutoReconnect)
                StartReconnect();

            throw;
        }
    }

    public void Disconnect()
    {
        StopReconnect();

        if (_port?.IsOpen == true)
        {
            try
            {
                _port.DataReceived -= OnPortDataReceived;
                _port.ErrorReceived -= OnPortErrorReceived;
                _port.Close();
                _port.Dispose();
                _port = null;
                Logger.Information("Serial port {Port} closed", PortName);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error closing serial port {Port}", PortName);
            }
        }

        ConnectionChanged?.Invoke(this, false);
    }

    public void Write(byte[] data)
    {
        if (_port?.IsOpen != true)
            throw new InvalidOperationException($"Serial port {PortName} is not open");

        _port.Write(data, 0, data.Length);
    }

    public void Write(string text)
    {
        if (_port?.IsOpen != true)
            throw new InvalidOperationException($"Serial port {PortName} is not open");

        _port.Write(text);
    }

    public string ReadLine()
    {
        if (_port?.IsOpen != true)
            throw new InvalidOperationException($"Serial port {PortName} is not open");

        return _port.ReadLine();
    }

    public int ReadByte()
    {
        if (_port?.IsOpen != true)
            throw new InvalidOperationException($"Serial port {PortName} is not open");

        return _port.ReadByte();
    }

    public byte[] ReadExisting()
    {
        if (_port?.IsOpen != true)
            return Array.Empty<byte>();

        int bytesToRead = _port.BytesToRead;
        if (bytesToRead == 0) return Array.Empty<byte>();

        var buffer = new byte[bytesToRead];
        _port.Read(buffer, 0, bytesToRead);
        return buffer;
    }

    /// <summary>List all available COM ports on the system.</summary>
    public static string[] GetAvailablePorts()
    {
        return SerialPort.GetPortNames();
    }

    private void OnPortDataReceived(object sender, SerialDataReceivedEventArgs e)
    {
        try
        {
            if (_port?.IsOpen != true) return;

            var data = ReadExisting();
            if (data.Length > 0)
            {
                DataReceived?.Invoke(this, data);
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error reading data from {Port}", PortName);
            ErrorOccurred?.Invoke(this, ex);

            if (AutoReconnect)
                HandleConnectionLost();
        }
    }

    private void OnPortErrorReceived(object sender, SerialErrorReceivedEventArgs e)
    {
        Logger.Error("Serial port error on {Port}: {Error}", PortName, e.EventType);
        ErrorOccurred?.Invoke(this, new IOException($"Serial error: {e.EventType}"));
    }

    private void HandleConnectionLost()
    {
        Logger.Warning("Connection lost on {Port}, initiating reconnect...", PortName);
        ConnectionChanged?.Invoke(this, false);

        try { _port?.Close(); } catch { }
        _port?.Dispose();
        _port = null;

        if (AutoReconnect)
            StartReconnect();
    }

    private void StartReconnect()
    {
        StopReconnect();
        _reconnectCts = new CancellationTokenSource();
        var token = _reconnectCts.Token;

        Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                await Task.Delay(ReconnectIntervalMs, token);

                try
                {
                    Logger.Information("Attempting reconnect to {Port}...", PortName);
                    Connect();
                    Logger.Information("Reconnected to {Port}", PortName);
                    return;
                }
                catch
                {
                    Logger.Debug("Reconnect to {Port} failed, retrying in {Interval}ms", PortName, ReconnectIntervalMs);
                }
            }
        }, token);
    }

    private void StopReconnect()
    {
        _reconnectCts?.Cancel();
        _reconnectCts?.Dispose();
        _reconnectCts = null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Disconnect();
        GC.SuppressFinalize(this);
    }
}
