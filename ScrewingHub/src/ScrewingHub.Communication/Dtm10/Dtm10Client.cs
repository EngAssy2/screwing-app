using System.IO.Ports;
using ScrewingHub.Communication.Common;
using ScrewingHub.Core.Interfaces;
using ScrewingHub.Core.Models;
using Serilog;

namespace ScrewingHub.Communication.Dtm10;

/// <summary>
/// DTM10 screw fastening monitor RS232 client.
/// Listens for data frames and emits parsed ScrewData events.
/// 
/// DTM10 Protocol: 38400 baud, 8N1, no handshake.
/// Frame: CH{n}:{currentValue}-{time},{SUM}\r\n
/// </summary>
public class Dtm10Client : IDtm10Client
{
    private static readonly ILogger Logger = Log.ForContext<Dtm10Client>();

    private readonly SerialPortWrapper _serial;
    private readonly Dtm10DataParser _parser = new();
    private byte[] _buffer = Array.Empty<byte>();
    private readonly object _bufferLock = new();

    public string PortName => _serial.PortName;
    public bool IsConnected => _serial.IsConnected;

    public event EventHandler<ScrewData>? DataReceived;
    public event EventHandler<byte[]>? RawDataReceived;
    public event EventHandler<bool>? ConnectionChanged;

    public Dtm10Client(string portName, int reconnectIntervalMs = 3000)
    {
        _serial = new SerialPortWrapper(portName, 38400)
        {
            DataBits = 8,
            Parity = Parity.None,
            StopBits = StopBits.One,
            Handshake = Handshake.None,
            AutoReconnect = true,
            ReconnectIntervalMs = reconnectIntervalMs
        };

        _serial.DataReceived += OnSerialDataReceived;
        _serial.ConnectionChanged += (_, connected) => ConnectionChanged?.Invoke(this, connected);
    }

    public Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        _serial.Connect();
        Logger.Information("DTM10 client connected on {Port}", PortName);
        return Task.CompletedTask;
    }

    public Task DisconnectAsync()
    {
        _serial.Disconnect();
        Logger.Information("DTM10 client disconnected from {Port}", PortName);
        return Task.CompletedTask;
    }

    private void OnSerialDataReceived(object? sender, byte[] data)
    {
        // Emit raw data for diagnostics
        RawDataReceived?.Invoke(this, data);

        lock (_bufferLock)
        {
            // Append new data to buffer
            var newBuffer = new byte[_buffer.Length + data.Length];
            Array.Copy(_buffer, 0, newBuffer, 0, _buffer.Length);
            Array.Copy(data, 0, newBuffer, _buffer.Length, data.Length);
            _buffer = newBuffer;

            // Try to extract complete frames
            while (_parser.TryExtractFrame(_buffer, out var frame, out var remaining))
            {
                _buffer = remaining;

                var screwData = _parser.Parse(frame);
                if (screwData != null)
                {
                    DataReceived?.Invoke(this, screwData);
                }
            }

            // Prevent buffer overflow (discard if buffer grows too large)
            if (_buffer.Length > 1024)
            {
                Logger.Warning("DTM10 buffer overflow, discarding {Length} bytes", _buffer.Length);
                _buffer = Array.Empty<byte>();
            }
        }
    }

    public void Dispose()
    {
        _serial.DataReceived -= OnSerialDataReceived;
        _serial.Dispose();
    }
}
