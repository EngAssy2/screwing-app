using ScrewingHub.Core.Models;

namespace ScrewingHub.Core.Interfaces;

/// <summary>
/// Omron PLC Host Link client for sending judgment results.
/// </summary>
public interface IPlcClient : ISerialDevice
{
    /// <summary>Write judgment result to PLC DM area.</summary>
    Task<bool> WriteJudgmentAsync(JudgmentResult result);

    /// <summary>Write heartbeat signal to PLC.</summary>
    Task<bool> WriteHeartbeatAsync();

    /// <summary>Read a DM word from the PLC.</summary>
    Task<int?> ReadDmAsync(int address);

    /// <summary>Write a value to a DM address.</summary>
    Task<bool> WriteDmAsync(int address, int value);

    /// <summary>Raised when the PLC requests a cycle reset (e.g. DM 1000 == 1).</summary>
    event EventHandler? ResetRequestReceived;

    /// <summary>Raised when the PLC detects a screw floating status (e.g. DM 1002 == 1).</summary>
    event EventHandler? ScrewFloatingDetected;

    /// <summary>Raised when a heartbeat signal is successfully sent to the PLC.</summary>
    event EventHandler? HeartbeatSent;
}
