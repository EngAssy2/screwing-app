namespace ScrewingHub.Core.Models;

/// <summary>
/// Threshold configuration for a single DTM10 channel within a product model.
/// </summary>
public class ChannelConfig
{
    /// <summary>DTM10 channel number (1–30).</summary>
    public int ChannelNumber { get; set; }

    /// <summary>Descriptive name, e.g., "M3x8 Top Cover Screw".</summary>
    public string ScrewName { get; set; } = string.Empty;

    // Current value thresholds (0–4095)
    public int CurrentValueUpperLimit { get; set; } = 4095;
    public int CurrentValueLowerLimit { get; set; } = 0;

    // Time thresholds in milliseconds (0–9990)
    public int TimeUpperLimitMs { get; set; } = 9990;
    public int TimeLowerLimitMs { get; set; } = 0;

    /// <summary>
    /// Factor to convert DTM10 current value to torque.
    /// OutputTorque = ConvertedCurrentValue × TorqueConversionFactor
    /// </summary>
    public double TorqueConversionFactor { get; set; }

    /// <summary>Unit for torque display, e.g., "Nm", "kgf·cm".</summary>
    public string TorqueUnit { get; set; } = "Nm";

    public bool IsEnabled { get; set; } = true;
}
