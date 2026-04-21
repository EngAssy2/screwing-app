using ScrewingHub.Core.Interfaces;
using ScrewingHub.Core.Models;
using Serilog;

namespace ScrewingHub.Core.Services;

/// <summary>
/// Evaluates OK/NG judgment for screw fastening data
/// based on per-channel threshold configuration.
/// </summary>
public class JudgmentService : IJudgmentService
{
    private static readonly ILogger Logger = Log.ForContext<JudgmentService>();

    /// <summary>
    /// Evaluate a screw fastening against the model's channel thresholds.
    /// Both current value AND time must be within limits for OK.
    /// </summary>
    public JudgmentResult Evaluate(ScrewData data, ProductModel model, int screwNumber)
    {
        var channelConfig = model.GetChannel(data.Channel);

        var result = new JudgmentResult
        {
            Timestamp = DateTime.Now,
            Channel = data.Channel,
            RawCurrentValue = data.ConvertedCurrentValue,
            ScrewTimeMs = data.ScrewTimeMs,
            ModelName = model.ModelName,
            StationName = model.StationName,
            ModelId = model.ModelId,
            ScrewNumber = screwNumber,
            TotalScrews = model.TotalScrewCount
        };

        if (channelConfig == null)
        {
            result.Judgment = JudgmentStatus.Error;
            result.JudgmentDetail = $"No channel config found for CH{data.Channel} in model '{model.ModelName}'";
            Logger.Warning(result.JudgmentDetail);
            return result;
        }

        result.ScrewName = channelConfig.ScrewName;
        result.CurrentUpperLimit = channelConfig.CurrentValueUpperLimit;
        result.CurrentLowerLimit = channelConfig.CurrentValueLowerLimit;
        result.TimeUpperLimitMs = channelConfig.TimeUpperLimitMs;
        result.TimeLowerLimitMs = channelConfig.TimeLowerLimitMs;
        result.TorqueConversionFactor = channelConfig.TorqueConversionFactor;

        // Check current value
        bool currentOk = data.ConvertedCurrentValue >= channelConfig.CurrentValueLowerLimit
                      && data.ConvertedCurrentValue <= channelConfig.CurrentValueUpperLimit;

        // Check time
        bool timeOk = data.ScrewTimeMs >= channelConfig.TimeLowerLimitMs
                   && data.ScrewTimeMs <= channelConfig.TimeUpperLimitMs;

        if (currentOk && timeOk)
        {
            result.Judgment = JudgmentStatus.OK;
            result.JudgmentDetail = "Within limits";
        }
        else
        {
            result.Judgment = JudgmentStatus.NG;
            var reasons = new List<string>();

            if (data.ConvertedCurrentValue > channelConfig.CurrentValueUpperLimit)
                reasons.Add($"Current value {data.ConvertedCurrentValue} exceeds upper limit {channelConfig.CurrentValueUpperLimit}");
            else if (data.ConvertedCurrentValue < channelConfig.CurrentValueLowerLimit)
                reasons.Add($"Current value {data.ConvertedCurrentValue} below lower limit {channelConfig.CurrentValueLowerLimit}");

            if (data.ScrewTimeMs > channelConfig.TimeUpperLimitMs)
                reasons.Add($"Time {data.ScrewTimeMs}ms exceeds upper limit {channelConfig.TimeUpperLimitMs}ms");
            else if (data.ScrewTimeMs < channelConfig.TimeLowerLimitMs)
                reasons.Add($"Time {data.ScrewTimeMs}ms below lower limit {channelConfig.TimeLowerLimitMs}ms");

            result.JudgmentDetail = string.Join("; ", reasons);
        }

        // Checksum validation
        if (!data.IsChecksumValid)
        {
            result.Remarks = "Warning: DTM10 checksum invalid";
            Logger.Warning("Checksum invalid for CH{Channel} data", data.Channel);
        }

        Logger.Information("Judgment: {Model} CH{Channel} Screw {ScrewNo}/{Total} = {Judgment} (Current={Current}, Time={Time}ms)",
            model.ModelName, data.Channel, screwNumber, model.TotalScrewCount,
            result.Judgment, data.ConvertedCurrentValue, data.ScrewTimeMs);

        return result;
    }
}
