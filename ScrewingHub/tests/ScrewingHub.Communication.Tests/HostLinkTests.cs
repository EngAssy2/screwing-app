using ScrewingHub.Communication.OmronPlc;

namespace ScrewingHub.Communication.Tests;

public class FcsCalculatorTests
{
    [Fact]
    public void Calculate_SimpleFrame_ReturnsCorrectFcs()
    {
        // @00RD00000010
        string frame = "@00RD00000010";
        var fcs = FcsCalculator.Calculate(frame);

        // Verify by manual XOR
        byte expected = 0;
        foreach (char c in frame)
            expected ^= (byte)c;

        Assert.Equal(expected.ToString("X2"), fcs);
    }

    [Fact]
    public void Calculate_WriteFrame_ReturnsCorrectFcs()
    {
        string frame = "@00WD01000001";
        var fcs = FcsCalculator.Calculate(frame);

        byte expected = 0;
        foreach (char c in frame)
            expected ^= (byte)c;

        Assert.Equal(expected.ToString("X2"), fcs);
    }

    [Fact]
    public void BuildCompleteFrame_IncludesFcsAndTerminator()
    {
        string frame = "@00MS";
        var complete = FcsCalculator.BuildCompleteFrame(frame);

        Assert.StartsWith("@00MS", complete);
        Assert.EndsWith("*\r", complete);
        Assert.Equal(frame.Length + 4, complete.Length); // frame + 2 FCS + * + CR
    }
}

public class HostLinkCommandBuilderTests
{
    [Fact]
    public void BuildWriteDm_SingleWord_CorrectFormat()
    {
        var builder = new HostLinkCommandBuilder(0);
        var cmd = builder.BuildWriteDm(100, 1);

        // Should start with @00WD0100 and contain hex value 0001
        Assert.Contains("@00WD", cmd);
        Assert.Contains("0100", cmd);
        Assert.Contains("0001", cmd);
        Assert.EndsWith("*\r", cmd);
    }

    [Fact]
    public void BuildWriteDm_MultipleWords_CorrectFormat()
    {
        var builder = new HostLinkCommandBuilder(0);
        var cmd = builder.BuildWriteDm(100, 1, 2048, 410, 1);

        Assert.Contains("@00WD0100", cmd);
        Assert.Contains("0001", cmd); // Judgment = 1
        Assert.Contains("0800", cmd); // 2048 = 0x800
        Assert.Contains("019A", cmd); // 410 = 0x19A
    }

    [Fact]
    public void BuildReadDm_CorrectFormat()
    {
        var builder = new HostLinkCommandBuilder(0);
        var cmd = builder.BuildReadDm(100, 1);

        Assert.Contains("@00RD", cmd);
        Assert.Contains("0100", cmd);
        Assert.Contains("0001", cmd);
    }

    [Fact]
    public void BuildStatusRead_CorrectFormat()
    {
        var builder = new HostLinkCommandBuilder(0);
        var cmd = builder.BuildStatusRead();

        Assert.Contains("@00MS", cmd);
        Assert.EndsWith("*\r", cmd);
    }

    [Fact]
    public void BuildWriteDm_DifferentNode_CorrectNodeAddress()
    {
        var builder = new HostLinkCommandBuilder(5);
        var cmd = builder.BuildWriteDm(100, 1);

        Assert.Contains("@05WD", cmd);
    }
}

public class HostLinkResponseParserTests
{
    private readonly HostLinkResponseParser _parser = new();

    [Fact]
    public void Parse_SuccessResponse_IsValid()
    {
        // Build a valid response: @00WD00{FCS}*
        string frameContent = "@00WD00";
        string fcs = FcsCalculator.Calculate(frameContent);
        string response = $"{frameContent}{fcs}*\r";

        var result = _parser.Parse(response);

        Assert.NotNull(result);
        Assert.True(result.IsValid);
        Assert.True(result.IsSuccess);
        Assert.Equal("WD", result.Command);
        Assert.Equal("00", result.EndCode);
    }

    [Fact]
    public void Parse_ErrorResponse_HasErrorMessage()
    {
        // Error code 04 = Address over
        string frameContent = "@00WD04";
        string fcs = FcsCalculator.Calculate(frameContent);
        string response = $"{frameContent}{fcs}*\r";

        var result = _parser.Parse(response);

        Assert.NotNull(result);
        Assert.True(result.IsValid);
        Assert.False(result.IsSuccess);
        Assert.Equal("04", result.EndCode);
        Assert.Contains("Address over", result.ErrorMessage);
    }

    [Fact]
    public void ParseDmValues_SingleWord_Correct()
    {
        var values = _parser.ParseDmValues("0800");
        Assert.Single(values);
        Assert.Equal(0x800, values[0]); // 2048
    }

    [Fact]
    public void ParseDmValues_MultipleWords_Correct()
    {
        var values = _parser.ParseDmValues("000108000001");
        Assert.Equal(3, values.Length);
        Assert.Equal(1, values[0]);
        Assert.Equal(2048, values[1]);
        Assert.Equal(1, values[2]);
    }
}
