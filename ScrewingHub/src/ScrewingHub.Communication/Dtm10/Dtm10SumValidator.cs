namespace ScrewingHub.Communication.Dtm10;

/// <summary>
/// Validates and calculates the SUM checksum used by DTM10 protocol.
/// SUM = lowest byte of the sum of all character byte values from first byte to comma (inclusive).
/// </summary>
public static class Dtm10SumValidator
{
    /// <summary>
    /// Calculate the expected SUM byte for a DTM10 data frame.
    /// The SUM covers all bytes from 'C' (start) through ',' (comma, inclusive).
    /// </summary>
    /// <param name="dataBeforeSum">The raw bytes from 'C' through ',' (inclusive).</param>
    /// <returns>Expected SUM byte value.</returns>
    public static byte CalculateSum(byte[] dataBeforeSum)
    {
        int sum = 0;
        foreach (var b in dataBeforeSum)
        {
            sum += b;
        }
        return (byte)(sum & 0xFF);
    }

    /// <summary>
    /// Calculate the expected SUM byte from the ASCII string portion before the SUM.
    /// </summary>
    /// <param name="frameTextBeforeSum">e.g., "CH1:100-400,"</param>
    public static byte CalculateSum(string frameTextBeforeSum)
    {
        int sum = 0;
        foreach (var c in frameTextBeforeSum)
        {
            sum += (byte)c;
        }
        return (byte)(sum & 0xFF);
    }

    /// <summary>
    /// Validate a complete DTM10 frame's SUM checksum.
    /// </summary>
    /// <param name="rawFrame">Complete frame bytes including SUM, CR, LF.</param>
    /// <returns>True if SUM is valid.</returns>
    public static bool Validate(byte[] rawFrame)
    {
        // Frame: [data...],[SUM][CR][LF]
        // Find the comma position (last comma before SUM)
        int commaPos = -1;
        for (int i = rawFrame.Length - 1; i >= 0; i--)
        {
            if (rawFrame[i] == 0x2C) // ','
            {
                commaPos = i;
                break;
            }
        }

        if (commaPos < 0 || commaPos + 1 >= rawFrame.Length)
            return false;

        // Data to checksum: from index 0 through comma (inclusive)
        var dataBytes = new byte[commaPos + 1];
        Array.Copy(rawFrame, 0, dataBytes, 0, commaPos + 1);

        var expectedSum = CalculateSum(dataBytes);
        var actualSum = rawFrame[commaPos + 1]; // SUM byte right after comma

        return expectedSum == actualSum;
    }
}
