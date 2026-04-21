using ScrewingHub.Core.Models;

namespace ScrewingHub.Core.Interfaces;

/// <summary>
/// DTM10 screw fastening monitor client.
/// </summary>
public interface IDtm10Client : ISerialDevice
{
    /// <summary>Fired when a complete screw fastening data frame is received.</summary>
    event EventHandler<ScrewData>? DataReceived;

    /// <summary>Fired when raw data is received (for diagnostics).</summary>
    event EventHandler<byte[]>? RawDataReceived;
}
