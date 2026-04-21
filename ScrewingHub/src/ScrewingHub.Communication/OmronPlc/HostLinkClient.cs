using System.IO.Ports;
using System.Text;
using ScrewingHub.Communication.Common;
using ScrewingHub.Core.Interfaces;
using ScrewingHub.Core.Models;
using Serilog;

namespace ScrewingHub.Communication.OmronPlc;

/// <summary>
/// Omron CJ2M PLC Host Link (C-mode) client via RS232.
/// Handles writing judgment results, heartbeat, and DM read/write.
/// 
/// CJ2M typical settings: 9600 baud, 7E2, no handshake.
/// </summary>
public class HostLinkClient : IPlcClient
{
    private static readonly ILogger Logger = Log.ForContext<HostLinkClient>();

    private readonly SerialPortWrapper _serial;
    private readonly HostLinkCommandBuilder _commandBuilder;
    private readonly HostLinkResponseParser _responseParser = new();
    private readonly SemaphoreSlim _commandLock = new(1, 1);
    private readonly int _dmStartAddress;
    private readonly int _commandTimeoutMs;

    private CancellationTokenSource? _heartbeatCts;
    private CancellationTokenSource? _pollingCts;
    private readonly int _heartbeatIntervalMs;
    private readonly int _pollingIntervalMs;
    private readonly StringBuilder _responseBuffer = new();
    private readonly SemaphoreSlim _responseSignal = new(0, 1);

    public event EventHandler? ResetRequestReceived;
    public event EventHandler? HeartbeatSent;

    public string PortName => _serial.PortName;
    public bool IsConnected => _serial.IsConnected;
    public event EventHandler<bool>? ConnectionChanged;

    public HostLinkClient(
        string portName,
        int baudRate = 9600,
        int nodeAddress = 0,
        int dmStartAddress = 100,
        int commandTimeoutMs = 1000,
        int heartbeatIntervalMs = 1000,
        int pollingIntervalMs = 500,
        int reconnectIntervalMs = 3000)
    {
        _dmStartAddress = dmStartAddress;
        _commandTimeoutMs = commandTimeoutMs;
        _heartbeatIntervalMs = heartbeatIntervalMs;
        _pollingIntervalMs = pollingIntervalMs;

        _serial = new SerialPortWrapper(portName, baudRate)
        {
            DataBits = 7,
            Parity = Parity.Even,
            StopBits = StopBits.Two,
            Handshake = Handshake.None,
            AutoReconnect = true,
            ReconnectIntervalMs = reconnectIntervalMs,
            ReadTimeoutMs = commandTimeoutMs,
            WriteTimeoutMs = commandTimeoutMs
        };

        _commandBuilder = new HostLinkCommandBuilder(nodeAddress);

        _serial.DataReceived += OnSerialDataReceived;
        _serial.ConnectionChanged += (_, connected) =>
        {
            ConnectionChanged?.Invoke(this, connected);
            if (connected)
            {
                StartHeartbeat();
                StartPolling();
            }
            else
            {
                StopHeartbeat();
                StopPolling();
            }
        };
    }

    public Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        _serial.Connect();
        StartHeartbeat();
        StartPolling();
        Logger.Information("PLC Host Link client connected on {Port}", PortName);
        return Task.CompletedTask;
    }

    public Task DisconnectAsync()
    {
        StopHeartbeat();
        StopPolling();
        _serial.Disconnect();
        Logger.Information("PLC Host Link client disconnected from {Port}", PortName);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Write a complete judgment result to the PLC DM area.
    /// Writes: Judgment(DM+0), CurrentValue(DM+1), Time(DM+2), Channel(DM+3),
    ///         ModelId(DM+4), ScrewNo(DM+5), TotalScrews(DM+6), AllComplete(DM+7), SeqCounter(DM+8)
    /// </summary>
    public async Task<bool> WriteJudgmentAsync(JudgmentResult result)
    {
        try
        {
            int allComplete = (result.Judgment == JudgmentStatus.OK && result.ScrewNumber >= result.TotalScrews) ? 1 : 0;

            var values = new int[]
            {
                (int)result.Judgment,           // DM+0: Judgment (1=OK, 2=NG, 3=Error)
                result.RawCurrentValue,         // DM+1: Current value (0–4095)
                result.ScrewTimeMs,             // DM+2: Screw time (ms)
                result.Channel,                 // DM+3: Channel number
                result.ModelId,                 // DM+4: Model ID
                result.ScrewNumber,             // DM+5: Current screw number
                result.TotalScrews,             // DM+6: Total screws for model
                allComplete,                    // DM+7: All screws complete flag
            };

            var command = _commandBuilder.BuildWriteDm(_dmStartAddress, values);
            var response = await SendCommandAsync(command);

            // Production Trigger signaling
            int triggerAddr = result.Judgment switch
            {
                JudgmentStatus.OK => 1010,
                JudgmentStatus.NG => 1012,
                _ => 1014
            };
            await WriteDmAsync(triggerAddr, 1);

            if (response?.IsSuccess == true)
            {
                result.PlcStatus = PlcSendStatus.Sent;
                Logger.Information("Judgment written to PLC: {Judgment} (DM{Addr})", result.Judgment, _dmStartAddress);
                return true;
            }
            else
            {
                result.PlcStatus = PlcSendStatus.Failed;
                Logger.Warning("PLC write failed: {Error}", response?.ErrorMessage ?? "No response");
                return false;
            }
        }
        catch (Exception ex)
        {
            result.PlcStatus = PlcSendStatus.Failed;
            Logger.Error(ex, "Exception writing judgment to PLC");
            return false;
        }
    }

    /// <summary>Write heartbeat toggle to DM 1016 with read handshake.</summary>
    public async Task<bool> WriteHeartbeatAsync()
    {
        try
        {
            // Optimization: Remove the "Read" handshake to reduce serial traffic by 50%.
            // We just force Write '1' to DM 1016 every interval.
            // This is the most reliable way to prevent timeouts at 9600 baud.
            bool success = await WriteDmAsync(1016, 1);
            if (success)
                HeartbeatSent?.Invoke(this, EventArgs.Empty);
            return success;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Exception writing heartbeat to PLC");
            return false;
        }
    }

    /// <summary>Read a single DM word from the PLC.</summary>
    public async Task<int?> ReadDmAsync(int address)
    {
        try
        {
            var command = _commandBuilder.BuildReadDm(address, 1);
            var response = await SendCommandAsync(command);

            if (response?.IsSuccess == true && !string.IsNullOrEmpty(response.Data))
            {
                var values = _responseParser.ParseDmValues(response.Data);
                return values.Length > 0 ? values[0] : null;
            }
            return null;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to read DM{Address}", address);
            return null;
        }
    }

    /// <summary>Write a single value to a DM address.</summary>
    public async Task<bool> WriteDmAsync(int address, int value)
    {
        try
        {
            var command = _commandBuilder.BuildWriteDm(address, value);
            var response = await SendCommandAsync(command);
            return response?.IsSuccess == true;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to write DM{Address}={Value}", address, value);
            return false;
        }
    }

    private async Task<HostLinkResponse?> SendCommandAsync(string command)
    {
        await _commandLock.WaitAsync();
        try
        {
            if (!_serial.IsConnected)
            {
                Logger.Warning("Cannot send command: PLC not connected");
                return null;
            }

            _responseBuffer.Clear();

            // Clear any stale signal
            while (_responseSignal.CurrentCount > 0) _responseSignal.Wait(0);

            Logger.Debug("PLC TX: {Command}", command.TrimEnd('\r'));
            _serial.Write(command);

            // Wait for signal from OnSerialDataReceived or timeout
            using var cts = new CancellationTokenSource(_commandTimeoutMs);
            try
            {
                await _responseSignal.WaitAsync(cts.Token);
                
                string currentResponse;
                lock (_responseBuffer)
                {
                    currentResponse = _responseBuffer.ToString();
                }

                if (currentResponse.Contains("*"))
                {
                    Logger.Debug("PLC RX: {Response}", currentResponse.TrimEnd('\r', '\n'));
                    return _responseParser.Parse(currentResponse);
                }
            }
            catch (OperationCanceledException)
            {
                Logger.Warning("PLC command timeout after {Timeout}ms", _commandTimeoutMs);
                return null;
            }

            return null;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error during SendCommandAsync");
            return null;
        }
        finally
        {
            _commandLock.Release();
        }
    }

    private void OnSerialDataReceived(object? sender, byte[] data)
    {
        var text = Encoding.ASCII.GetString(data);
        lock (_responseBuffer)
        {
            _responseBuffer.Append(text);
            if (_responseBuffer.ToString().Contains("*"))
            {
                if (_responseSignal.CurrentCount == 0)
                {
                    _responseSignal.Release();
                }
            }
        }
    }

    private void StartHeartbeat()
    {
        StopHeartbeat();
        _heartbeatCts = new CancellationTokenSource();
        var token = _heartbeatCts.Token;

        Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                await Task.Delay(_heartbeatIntervalMs, token);
                await WriteHeartbeatAsync();
            }
        }, token);

        Logger.Information("PLC heartbeat started (DM{Addr}, interval {Interval}ms)",
            _dmStartAddress + 9, _heartbeatIntervalMs);
    }

    private void StartPolling()
    {
        StopPolling();
        _pollingCts = new CancellationTokenSource();
        var token = _pollingCts.Token;

        Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(_pollingIntervalMs, token);
                    if (!IsConnected) continue;

                    // Read DM 1000 (Loose/Reset Request)
                    var val = await ReadDmAsync(1000);
                    if (val == 1)
                    {
                        Logger.Information("PLC Reset Request (Loose) received at DM 1000");
                        ResetRequestReceived?.Invoke(this, EventArgs.Empty);

                        // Reset the trigger back to 0
                        await WriteDmAsync(1000, 0);
                    }
                }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    Logger.Debug(ex, "PLC polling loop error");
                }
            }
        }, token);
    }

    private void StopHeartbeat()
    {
        _heartbeatCts?.Cancel();
        _heartbeatCts?.Dispose();
        _heartbeatCts = null;
    }

    private void StopPolling()
    {
        _pollingCts?.Cancel();
        _pollingCts?.Dispose();
        _pollingCts = null;
    }

    public void Dispose()
    {
        StopHeartbeat();
        StopPolling();
        _serial.DataReceived -= OnSerialDataReceived;
        _serial.Dispose();
        _commandLock.Dispose();
        _responseSignal.Dispose();
    }
}
