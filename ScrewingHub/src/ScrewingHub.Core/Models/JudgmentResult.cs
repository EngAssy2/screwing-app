using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ScrewingHub.Core.Models;

/// <summary>
/// Result of the OK/NG judgment for a single screw fastening.
/// </summary>
public class JudgmentResult : INotifyPropertyChanged
{
    private string _remarks = string.Empty;
    private PlcSendStatus _plcStatus = PlcSendStatus.Pending;

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public int RecordNumber { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.Now;

    // Source data from DTM10
    public int Channel { get; set; }
    public int RawCurrentValue { get; set; }
    public int ScrewTimeMs { get; set; }

    // Model & screw info
    public string ModelName { get; set; } = string.Empty;
    public string StationName { get; set; } = string.Empty;
    public int ModelId { get; set; }
    public string ScrewName { get; set; } = string.Empty;
    public int ScrewNumber { get; set; }
    public int TotalScrews { get; set; }

    // Thresholds used for judgment
    public int CurrentUpperLimit { get; set; }
    public int CurrentLowerLimit { get; set; }
    public int TimeUpperLimitMs { get; set; }
    public int TimeLowerLimitMs { get; set; }

    // Calculated values
    public double TorqueConversionFactor { get; set; }
    public double ConvertedTorque => RawCurrentValue * TorqueConversionFactor;
    public double TorqueUpperLimit => CurrentUpperLimit * TorqueConversionFactor;
    public double TorqueLowerLimit => CurrentLowerLimit * TorqueConversionFactor;

    // Judgment
    public JudgmentStatus Judgment { get; set; } = JudgmentStatus.NG;
    public string JudgmentDetail { get; set; } = string.Empty;

    // PLC communication
    public PlcSendStatus PlcStatus
    {
        get => _plcStatus;
        set { if (_plcStatus != value) { _plcStatus = value; OnPropertyChanged(); } }
    }

    // Operator info
    public string OperatorId { get; set; } = string.Empty;
    public string WorkOrderNo { get; set; } = string.Empty;

    public string Remarks
    {
        get => _remarks;
        set { if (_remarks != value) { _remarks = value; OnPropertyChanged(); } }
    }

    /// <summary>Display string for screw progress, e.g., "2/4".</summary>
    public string ScrewProgress => $"{ScrewNumber}/{TotalScrews}";
}

public enum JudgmentStatus
{
    OK = 1,
    NG = 2,
    Error = 3
}

public enum PlcSendStatus
{
    Pending,
    Sent,
    Failed
}
