using System.Text;
using System.Text.RegularExpressions;
using ScrewingHub.Core.Models;
using Serilog;

namespace ScrewingHub.Communication.Dtm10;

/// <summary>
/// Parses DTM10 serial data frames.
/// Format: CH{p}:{CurrentValue}-{Time},{SUM}\r\n
/// Example: CH1:100-400,t\r\n
/// </summary>
public class Dtm10DataParser
{
    private static readonly ILogger Logger = Log.ForContext<Dtm10DataParser>();

    // Pattern: CH followed by 1-2 digits, colon, 1-4 digits, dash, 1-4 digits
    private static readonly Regex FramePattern = new(
        @"^CH(\d{1,2}):(\d{1,4})-(\d{1,4}),$",
        RegexOptions.Compiled);

    /// <summary>
    /// Parse a complete DTM10 frame (raw bytes including SUM, CR, LF).
    /// </summary>
    /// <param name="rawFrame">Complete raw frame bytes.</param>
    /// <returns>Parsed ScrewData or null if parsing fails.</returns>
    public ScrewData? Parse(byte[] rawFrame)
    {
        try
        {
            if (rawFrame.Length < 11) // Minimum frame size
            {
                Logger.Warning("Frame too short: {Length} bytes", rawFrame.Length);
                return null;
            }

            // Validate checksum
            bool checksumValid = Dtm10SumValidator.Validate(rawFrame);

            // Find comma position to extract text portion
            int commaPos = -1;
            for (int i = rawFrame.Length - 1; i >= 0; i--)
            {
                if (rawFrame[i] == 0x2C)
                {
                    commaPos = i;
                    break;
                }
            }

            if (commaPos < 0)
            {
                Logger.Warning("No comma found in frame");
                return null;
            }

            // Extract the ASCII text portion (before SUM)
            var textPart = Encoding.ASCII.GetString(rawFrame, 0, commaPos + 1);

            // Parse using regex
            var match = FramePattern.Match(textPart);
            if (!match.Success)
            {
                Logger.Warning("Frame text does not match pattern: '{Text}'", textPart);
                return null;
            }

            var data = new ScrewData
            {
                Channel = int.Parse(match.Groups[1].Value),
                ConvertedCurrentValue = int.Parse(match.Groups[2].Value),
                ScrewTimeMs = int.Parse(match.Groups[3].Value),
                RawData = rawFrame,
                ReceivedAt = DateTime.Now,
                IsChecksumValid = checksumValid
            };

            // Validate ranges
            if (data.Channel < 1 || data.Channel > 30)
            {
                Logger.Warning("Channel out of range: {Channel}", data.Channel);
                return null;
            }

            if (data.ConvertedCurrentValue < 0 || data.ConvertedCurrentValue > 4095)
            {
                Logger.Warning("Current value out of range: {Value}", data.ConvertedCurrentValue);
                return null;
            }

            if (data.ScrewTimeMs < 0 || data.ScrewTimeMs > 9990)
            {
                Logger.Warning("Time out of range: {Time}", data.ScrewTimeMs);
                return null;
            }

            Logger.Debug("Parsed: {Data} (Checksum: {Valid})", data, checksumValid ? "OK" : "INVALID");
            return data;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to parse DTM10 frame");
            return null;
        }
    }

    /// <summary>
    /// Try to extract a complete frame from a byte buffer.
    /// Looks for data ending with \r\n.
    /// </summary>
    /// <param name="buffer">Input buffer (may contain partial data).</param>
    /// <param name="frame">Extracted complete frame if found.</param>
    /// <param name="remainingBuffer">Remaining bytes after the frame.</param>
    /// <returns>True if a complete frame was found.</returns>
    public bool TryExtractFrame(byte[] buffer, out byte[] frame, out byte[] remainingBuffer)
    {
        frame = Array.Empty<byte>();
        remainingBuffer = buffer;

        // Look for CR LF sequence
        for (int i = 0; i < buffer.Length - 1; i++)
        {
            if (buffer[i] == 0x0D && buffer[i + 1] == 0x0A) // \r\n
            {
                // Frame goes from start to after \r\n
                int frameLength = i + 2;
                frame = new byte[frameLength];
                Array.Copy(buffer, 0, frame, 0, frameLength);

                // Remaining data
                int remaining = buffer.Length - frameLength;
                if (remaining > 0)
                {
                    remainingBuffer = new byte[remaining];
                    Array.Copy(buffer, frameLength, remainingBuffer, 0, remaining);
                }
                else
                {
                    remainingBuffer = Array.Empty<byte>();
                }

                return true;
            }
        }

        return false;
    }
}
