using ScrewingHub.Communication.Dtm10;

namespace ScrewingHub.Communication.Tests;

public class Dtm10SumValidatorTests
{
    [Fact]
    public void CalculateSum_ManualExample1_Returns0x74()
    {
        // CH1:100-400, -> SUM should be 0x74 (= 't')
        string data = "CH1:100-400,";
        var sum = Dtm10SumValidator.CalculateSum(data);
        Assert.Equal(0x74, sum);
    }

    [Fact]
    public void CalculateSum_ManualExample2_Returns0xA8()
    {
        // CH3:310-3000, -> SUM should be 0xA8
        string data = "CH3:310-3000,";
        var sum = Dtm10SumValidator.CalculateSum(data);
        Assert.Equal(0xA8, sum);
    }

    [Fact]
    public void Validate_ValidFrame_ReturnsTrue()
    {
        byte[] frame = { 0x43, 0x48, 0x31, 0x3A, 0x31, 0x30, 0x30, 0x2D, 0x34, 0x30, 0x30, 0x2C, 0x74, 0x0D, 0x0A };
        Assert.True(Dtm10SumValidator.Validate(frame));
    }

    [Fact]
    public void Validate_InvalidFrame_ReturnsFalse()
    {
        byte[] frame = { 0x43, 0x48, 0x31, 0x3A, 0x31, 0x30, 0x30, 0x2D, 0x34, 0x30, 0x30, 0x2C, 0xFF, 0x0D, 0x0A };
        Assert.False(Dtm10SumValidator.Validate(frame));
    }

    [Fact]
    public void CalculateSum_VerifyManualCalculation()
    {
        // Manual says: 0x43 + 0x48 + 0x31 + 0x3a + 0x31 + 0x30 + 0x30 + 0x2d + 0x34 + 0x30 + 0x30 + 0x2c = 0x274
        // 0x274 & 0xFF = 0x74
        int manualSum = 0x43 + 0x48 + 0x31 + 0x3A + 0x31 + 0x30 + 0x30 + 0x2D + 0x34 + 0x30 + 0x30 + 0x2C;
        Assert.Equal(0x274, manualSum);
        Assert.Equal(0x74, (byte)(manualSum & 0xFF));

        // Verify our function matches
        var calculated = Dtm10SumValidator.CalculateSum("CH1:100-400,");
        Assert.Equal((byte)(manualSum & 0xFF), calculated);
    }
}
