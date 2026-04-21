using ScrewingHub.Core.Models;

namespace ScrewingHub.Core.Interfaces;

/// <summary>
/// Abstraction for serial device communication (DTM10 or PLC).
/// </summary>
public interface ISerialDevice : IDisposable
{
    string PortName { get; }
    bool IsConnected { get; }

    event EventHandler<bool>? ConnectionChanged;

    Task ConnectAsync(CancellationToken cancellationToken = default);
    Task DisconnectAsync();
}
