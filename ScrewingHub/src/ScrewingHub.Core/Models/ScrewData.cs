namespace ScrewingHub.Core.Models;

/// <summary>
/// Parsed data from a single DTM10 screw fastening transmission.
/// </summary>
public class ScrewData
{
    /// <summary>Operation channel number (1–30).</summary>
    public int Channel { get; set; }

    /// <summary>Motor current value at torque-up, converted to 0–4095.</summary>
    public int ConvertedCurrentValue { get; set; }

    /// <summary>Screw fastening time in milliseconds (0–9990).</summary>
    public int ScrewTimeMs { get; set; }

    /// <summary>Raw bytes received from DTM10 (for diagnostics).</summary>
    public byte[]? RawData { get; set; }

    /// <summary>Timestamp when data was received by the application.</summary>
    public DateTime ReceivedAt { get; set; } = DateTime.Now;

    /// <summary>Whether the SUM checksum was valid.</summary>
    public bool IsChecksumValid { get; set; }

    public override string ToString()
        => $"CH{Channel}:{ConvertedCurrentValue}-{ScrewTimeMs}ms [{(IsChecksumValid ? "OK" : "BAD SUM")}]";
}
