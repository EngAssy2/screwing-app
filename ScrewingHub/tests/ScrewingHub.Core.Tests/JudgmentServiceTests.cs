using ScrewingHub.Core.Models;
using ScrewingHub.Core.Services;

namespace ScrewingHub.Core.Tests;

public class JudgmentServiceTests
{
    private readonly JudgmentService _service = new();

    private static ProductModel CreateModel(int upperLimit = 2500, int lowerLimit = 1500,
        int timeUpper = 1500, int timeLower = 300)
    {
        return new ProductModel
        {
            ModelId = 1,
            ModelName = "Test Model",
            TotalScrewCount = 4,
            Channels = new List<ChannelConfig>
            {
                new()
                {
                    ChannelNumber = 1,
                    ScrewName = "Test Screw",
                    CurrentValueUpperLimit = upperLimit,
                    CurrentValueLowerLimit = lowerLimit,
                    TimeUpperLimitMs = timeUpper,
                    TimeLowerLimitMs = timeLower,
                    TorqueConversionFactor = 0.001,
                    IsEnabled = true
                }
            }
        };
    }

    [Fact]
    public void Evaluate_WithinLimits_ReturnsOK()
    {
        var model = CreateModel();
        var data = new ScrewData { Channel = 1, ConvertedCurrentValue = 2000, ScrewTimeMs = 500, IsChecksumValid = true };

        var result = _service.Evaluate(data, model, 1);

        Assert.Equal(JudgmentStatus.OK, result.Judgment);
    }

    [Fact]
    public void Evaluate_AtLowerBoundary_ReturnsOK()
    {
        var model = CreateModel();
        var data = new ScrewData { Channel = 1, ConvertedCurrentValue = 1500, ScrewTimeMs = 300, IsChecksumValid = true };

        var result = _service.Evaluate(data, model, 1);

        Assert.Equal(JudgmentStatus.OK, result.Judgment);
    }

    [Fact]
    public void Evaluate_AtUpperBoundary_ReturnsOK()
    {
        var model = CreateModel();
        var data = new ScrewData { Channel = 1, ConvertedCurrentValue = 2500, ScrewTimeMs = 1500, IsChecksumValid = true };

        var result = _service.Evaluate(data, model, 1);

        Assert.Equal(JudgmentStatus.OK, result.Judgment);
    }

    [Fact]
    public void Evaluate_CurrentAboveUpper_ReturnsNG()
    {
        var model = CreateModel();
        var data = new ScrewData { Channel = 1, ConvertedCurrentValue = 3000, ScrewTimeMs = 500, IsChecksumValid = true };

        var result = _service.Evaluate(data, model, 1);

        Assert.Equal(JudgmentStatus.NG, result.Judgment);
        Assert.Contains("exceeds upper limit", result.JudgmentDetail);
    }

    [Fact]
    public void Evaluate_CurrentBelowLower_ReturnsNG()
    {
        var model = CreateModel();
        var data = new ScrewData { Channel = 1, ConvertedCurrentValue = 1000, ScrewTimeMs = 500, IsChecksumValid = true };

        var result = _service.Evaluate(data, model, 1);

        Assert.Equal(JudgmentStatus.NG, result.Judgment);
        Assert.Contains("below lower limit", result.JudgmentDetail);
    }

    [Fact]
    public void Evaluate_TimeAboveUpper_ReturnsNG()
    {
        var model = CreateModel();
        var data = new ScrewData { Channel = 1, ConvertedCurrentValue = 2000, ScrewTimeMs = 2000, IsChecksumValid = true };

        var result = _service.Evaluate(data, model, 1);

        Assert.Equal(JudgmentStatus.NG, result.Judgment);
        Assert.Contains("Time", result.JudgmentDetail);
        Assert.Contains("exceeds upper limit", result.JudgmentDetail);
    }

    [Fact]
    public void Evaluate_TimeBelowLower_ReturnsNG()
    {
        var model = CreateModel();
        var data = new ScrewData { Channel = 1, ConvertedCurrentValue = 2000, ScrewTimeMs = 100, IsChecksumValid = true };

        var result = _service.Evaluate(data, model, 1);

        Assert.Equal(JudgmentStatus.NG, result.Judgment);
        Assert.Contains("Time", result.JudgmentDetail);
        Assert.Contains("below lower limit", result.JudgmentDetail);
    }

    [Fact]
    public void Evaluate_BothOutOfRange_ReturnsNGWithBothReasons()
    {
        var model = CreateModel();
        var data = new ScrewData { Channel = 1, ConvertedCurrentValue = 3000, ScrewTimeMs = 100, IsChecksumValid = true };

        var result = _service.Evaluate(data, model, 1);

        Assert.Equal(JudgmentStatus.NG, result.Judgment);
        Assert.Contains("Current value", result.JudgmentDetail);
        Assert.Contains("Time", result.JudgmentDetail);
    }

    [Fact]
    public void Evaluate_UnknownChannel_ReturnsError()
    {
        var model = CreateModel();
        var data = new ScrewData { Channel = 5, ConvertedCurrentValue = 2000, ScrewTimeMs = 500, IsChecksumValid = true };

        var result = _service.Evaluate(data, model, 1);

        Assert.Equal(JudgmentStatus.Error, result.Judgment);
    }

    [Fact]
    public void Evaluate_SetsModelAndScrewInfo()
    {
        var model = CreateModel();
        var data = new ScrewData { Channel = 1, ConvertedCurrentValue = 2000, ScrewTimeMs = 500, IsChecksumValid = true };

        var result = _service.Evaluate(data, model, 3);

        Assert.Equal("Test Model", result.ModelName);
        Assert.Equal(1, result.ModelId);
        Assert.Equal("Test Screw", result.ScrewName);
        Assert.Equal(3, result.ScrewNumber);
        Assert.Equal(4, result.TotalScrews);
    }

    [Fact]
    public void Evaluate_CalculatesConvertedTorque()
    {
        var model = CreateModel();
        var data = new ScrewData { Channel = 1, ConvertedCurrentValue = 2000, ScrewTimeMs = 500, IsChecksumValid = true };

        var result = _service.Evaluate(data, model, 1);

        Assert.Equal(2.0, result.ConvertedTorque, 3); // 2000 * 0.001 = 2.0
    }
}
