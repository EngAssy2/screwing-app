using ScrewingHub.Communication.Dtm10;
using System.Text;

namespace ScrewingHub.Communication.Tests;

public class Dtm10DataParserTests
{
    private readonly Dtm10DataParser _parser = new();

    [Fact]
    public void Parse_ManualExample1_CH1_100_400()
    {
        // From manual: CH1:100-400,t\r\n
        // SUM = 0x74 = 't'
        byte[] frame = { 0x43, 0x48, 0x31, 0x3A, 0x31, 0x30, 0x30, 0x2D, 0x34, 0x30, 0x30, 0x2C, 0x74, 0x0D, 0x0A };

        var result = _parser.Parse(frame);

        Assert.NotNull(result);
        Assert.Equal(1, result.Channel);
        Assert.Equal(100, result.ConvertedCurrentValue);
        Assert.Equal(400, result.ScrewTimeMs);
        Assert.True(result.IsChecksumValid);
    }

    [Fact]
    public void Parse_ManualExample2_CH3_310_3000()
    {
        // From manual: CH3:310-3000,\xa8\r\n
        byte[] frame = { 0x43, 0x48, 0x33, 0x3A, 0x33, 0x31, 0x30, 0x2D, 0x33, 0x30, 0x30, 0x30, 0x2C, 0xA8, 0x0D, 0x0A };

        var result = _parser.Parse(frame);

        Assert.NotNull(result);
        Assert.Equal(3, result.Channel);
        Assert.Equal(310, result.ConvertedCurrentValue);
        Assert.Equal(3000, result.ScrewTimeMs);
        Assert.True(result.IsChecksumValid);
    }

    [Fact]
    public void Parse_MaxValues_CH30_4095_9990()
    {
        // CH30:4095-9990,{SUM}\r\n
        var text = "CH30:4095-9990,";
        byte sum = Dtm10SumValidator.CalculateSum(text);
        var bytes = Encoding.ASCII.GetBytes(text);
        byte[] frame = new byte[bytes.Length + 3];
        Array.Copy(bytes, frame, bytes.Length);
        frame[bytes.Length] = sum;
        frame[bytes.Length + 1] = 0x0D;
        frame[bytes.Length + 2] = 0x0A;

        var result = _parser.Parse(frame);

        Assert.NotNull(result);
        Assert.Equal(30, result.Channel);
        Assert.Equal(4095, result.ConvertedCurrentValue);
        Assert.Equal(9990, result.ScrewTimeMs);
        Assert.True(result.IsChecksumValid);
    }

    [Fact]
    public void Parse_MinValues_CH1_0_0()
    {
        var text = "CH1:0-0,";
        byte sum = Dtm10SumValidator.CalculateSum(text);
        var bytes = Encoding.ASCII.GetBytes(text);
        byte[] frame = new byte[bytes.Length + 3];
        Array.Copy(bytes, frame, bytes.Length);
        frame[bytes.Length] = sum;
        frame[bytes.Length + 1] = 0x0D;
        frame[bytes.Length + 2] = 0x0A;

        var result = _parser.Parse(frame);

        Assert.NotNull(result);
        Assert.Equal(1, result.Channel);
        Assert.Equal(0, result.ConvertedCurrentValue);
        Assert.Equal(0, result.ScrewTimeMs);
    }

    [Fact]
    public void Parse_InvalidChecksum_ReturnsFalseChecksum()
    {
        // Same as example 1 but with bad SUM byte
        byte[] frame = { 0x43, 0x48, 0x31, 0x3A, 0x31, 0x30, 0x30, 0x2D, 0x34, 0x30, 0x30, 0x2C, 0xFF, 0x0D, 0x0A };

        var result = _parser.Parse(frame);

        Assert.NotNull(result);
        Assert.False(result.IsChecksumValid);
    }

    [Fact]
    public void Parse_TooShort_ReturnsNull()
    {
        byte[] frame = { 0x43, 0x48, 0x0D, 0x0A };
        var result = _parser.Parse(frame);
        Assert.Null(result);
    }

    [Fact]
    public void TryExtractFrame_CompleteFrame_ExtractsCorrectly()
    {
        byte[] data = { 0x43, 0x48, 0x31, 0x3A, 0x31, 0x30, 0x30, 0x2D, 0x34, 0x30, 0x30, 0x2C, 0x74, 0x0D, 0x0A };

        bool found = _parser.TryExtractFrame(data, out var frame, out var remaining);

        Assert.True(found);
        Assert.Equal(15, frame.Length);
        Assert.Empty(remaining);
    }

    [Fact]
    public void TryExtractFrame_PartialData_ReturnsFalse()
    {
        byte[] partial = { 0x43, 0x48, 0x31, 0x3A, 0x31 };

        bool found = _parser.TryExtractFrame(partial, out var frame, out var remaining);

        Assert.False(found);
    }

    [Fact]
    public void TryExtractFrame_TwoFrames_ExtractsFirstAndKeepsRemaining()
    {
        byte[] frame1 = { 0x43, 0x48, 0x31, 0x3A, 0x31, 0x30, 0x30, 0x2D, 0x34, 0x30, 0x30, 0x2C, 0x74, 0x0D, 0x0A };
        byte[] frame2 = { 0x43, 0x48, 0x32, 0x3A };
        byte[] combined = new byte[frame1.Length + frame2.Length];
        Array.Copy(frame1, combined, frame1.Length);
        Array.Copy(frame2, 0, combined, frame1.Length, frame2.Length);

        bool found = _parser.TryExtractFrame(combined, out var frame, out var remaining);

        Assert.True(found);
        Assert.Equal(frame1.Length, frame.Length);
        Assert.Equal(frame2.Length, remaining.Length);
    }
}
